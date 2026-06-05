using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

#nullable enable

namespace Coflnet.Security.OpenBao;

public static class OpenBaoConfigurationExtensions
{
    public static IConfigurationBuilder AddOpenBaoFromEnvironment(this IConfigurationBuilder builder)
    {
        var options = OpenBaoOptions.FromEnvironment();
        if (!options.Enabled && string.IsNullOrWhiteSpace(options.Address))
            return builder;
        return builder.Add(new OpenBaoConfigurationSource(options));
    }
}

internal sealed class OpenBaoConfigurationSource : IConfigurationSource
{
    private readonly OpenBaoOptions options;
    public OpenBaoConfigurationSource(OpenBaoOptions options) => this.options = options;
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new OpenBaoConfigurationProvider(options);
}

internal sealed class OpenBaoConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly OpenBaoOptions options;
    private readonly Timer? timer;

    public OpenBaoConfigurationProvider(OpenBaoOptions options)
    {
        this.options = options;
        if (options.ReloadInterval > TimeSpan.Zero)
        {
            timer = new Timer(_ => ReloadSafe(), null, Timeout.InfiniteTimeSpan, options.ReloadInterval);
        }
    }

    public override void Load()
    {
        LogInfo("Load() called");
        Reload();
        if (timer != null)
            timer.Change(options.ReloadInterval, options.ReloadInterval);
        LogInfo("Load() completed");
    }

    public void Dispose() => timer?.Dispose();

    private void Reload()
    {
        try
        {
            Data = LoadAsync().GetAwaiter().GetResult();
            LogInfo($"Reload succeeded ({Data.Count} keys)");
            OnReload();
        }
        catch (Exception ex)
        {
            LogError("load", ex);
            if (!options.Optional)
                throw;
        }
    }

    /// <summary>Timer-safe reload that never throws (avoids crashing the process on background reload failures).</summary>
    private void ReloadSafe()
    {
        try
        {
            Reload();
        }
        catch (Exception ex)
        {
            // Reload already logged the error; this catch prevents the exception from
            // escaping the timer callback and crashing the process or stopping the timer.
            LogError("background-reload", ex);
        }
    }

    private static void LogError(string operation, Exception ex)
    {
        Console.Error.WriteLine($"[OpenBao] {operation} failed: {ex}");
    }

    private static void LogInfo(string message)
    {
        Console.Error.WriteLine($"[OpenBao] {message}");
    }

    private async Task<IDictionary<string, string?>> LoadAsync()
    {
        LogInfo($"Starting: addr={options.Address} role={options.Role} path={options.Path} mount={options.Mount} optional={options.Optional}");

        // ── validation ──────────────────────────────────────────────
        try { options.Validate(); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"OpenBao configuration validation failed. " +
                $"Required: OPENBAO__ADDR, OPENBAO__ROLE, OPENBAO__PATH, and a valid OPENBAO__TOKEN_PATH. " +
                $"Check your environment variables.", ex);
        }
        LogInfo($"Validation passed. Reading JWT from {options.TokenPath}");

        var jwt = await File.ReadAllTextAsync(options.TokenPath).ConfigureAwait(false);
        LogInfo($"JWT read ({jwt.Length} chars)");

        var handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(options.CACertPath))
        {
            if (File.Exists(options.CACertPath))
            {
                LogInfo($"Loading CA cert from {options.CACertPath}");
                var caCert = X509CertificateLoader.LoadCertificateFromFile(options.CACertPath);
                LogInfo($"CA cert loaded: subject={caCert.Subject} thumbprint={caCert.Thumbprint}");
                handler.ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                        return true;
                    if (cert is null || chain is null)
                        return false;
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(caCert);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreWrongUsage;
                    return chain.Build(new X509Certificate2(cert));
                };
            }
            else
            {
                LogInfo($"WARNING: OPENBAO__CACERT is set but file not found: {options.CACertPath}");
            }
        }
        else
        {
            LogInfo("No CACertPath configured, using system trust store only");
        }
        using var client = new HttpClient(handler) { BaseAddress = new Uri(options.Address.TrimEnd('/') + "/") };
        client.Timeout = TimeSpan.FromSeconds(30);
        LogInfo($"HttpClient created with base={client.BaseAddress} timeout={client.Timeout.TotalSeconds}s");

        // ── login ───────────────────────────────────────────────────
        LogInfo($"Logging in to OpenBao at v1/auth/{options.AuthPath.Trim('/')}/login with role={options.Role}");
        HttpResponseMessage loginResponse;
        try
        {
            var loginPayload = JsonSerializer.Serialize(new { role = options.Role, jwt });
            using var loginRequest = new HttpRequestMessage(HttpMethod.Post, $"v1/auth/{options.AuthPath.Trim('/')}/login")
            {
                Content = new StringContent(loginPayload, Encoding.UTF8, "application/json")
            };
            loginResponse = await client.SendAsync(loginRequest).ConfigureAwait(false);
            loginResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"OpenBao login failed at {options.Address}/v1/auth/{options.AuthPath.Trim('/')}/login " +
                $"(role={options.Role}). Verify the OpenBao address, auth path, role, and service-account token.", ex);
        }
        LogInfo("Login succeeded");

        string token;
        using (var loginDocument = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync().ConfigureAwait(false)))
        {
            token = loginDocument.RootElement.GetProperty("auth").GetProperty("client_token").GetString()!;
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("OpenBao login response did not contain a client_token.");
        }
        LogInfo($"Client token obtained ({token.Length} chars)");

        // ── fetch secret ────────────────────────────────────────────
        LogInfo($"Fetching secret from v1/{options.Mount.Trim('/')}/data/{options.Path.Trim('/')}");
        HttpResponseMessage secretResponse;
        try
        {
            using var secretRequest = new HttpRequestMessage(HttpMethod.Get, $"v1/{options.Mount.Trim('/')}/data/{options.Path.Trim('/')}");
            secretRequest.Headers.Add("X-Vault-Token", token);
            secretRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            secretResponse = await client.SendAsync(secretRequest).ConfigureAwait(false);
            secretResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"OpenBao secret read failed at {options.Address}/v1/{options.Mount.Trim('/')}/data/{options.Path.Trim('/')}. " +
                $"Verify the mount point and secret path.", ex);
        }

        // ── parse secret ────────────────────────────────────────────
        try
        {
            using var secretDocument = JsonDocument.Parse(await secretResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
            var data = secretDocument.RootElement.GetProperty("data").GetProperty("data");
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in data.EnumerateObject())
            {
                var key = property.Name.Replace("__", ":", StringComparison.Ordinal);
                result[key] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.GetRawText();
            }
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"OpenBao secret response was malformed for path {options.Mount}/{options.Path}. " +
                "Expected KV v2 response with .data.data.", ex);
        }
    }
}

internal sealed record OpenBaoOptions
{
    public bool Enabled { get; init; }
    public bool Optional { get; init; }
    public string Address { get; init; } = "";
    public string AuthPath { get; init; } = "kubernetes";
    public string Mount { get; init; } = "kv";
    public string Path { get; init; } = "";
    public string Role { get; init; } = "";
    public string TokenPath { get; init; } = "/var/run/secrets/kubernetes.io/serviceaccount/token";
    public string CACertPath { get; init; } = "";
    public TimeSpan ReloadInterval { get; init; } = TimeSpan.FromMinutes(5);

    public static OpenBaoOptions FromEnvironment()
    {
        return new OpenBaoOptions
        {
            Enabled = Bool("OPENBAO__ENABLED", false),
            Optional = Bool("OPENBAO__OPTIONAL", true),
            Address = Env("OPENBAO__ADDR"),
            AuthPath = Env("OPENBAO__AUTH_PATH", "kubernetes"),
            Mount = Env("OPENBAO__MOUNT", "kv"),
            Path = Env("OPENBAO__PATH"),
            Role = Env("OPENBAO__ROLE"),
            TokenPath = Env("OPENBAO__TOKEN_PATH", "/var/run/secrets/kubernetes.io/serviceaccount/token"),
            CACertPath = Env("OPENBAO__CACERT"),
            ReloadInterval = TimeSpan.FromSeconds(Int("OPENBAO__RELOAD_SECONDS", 300))
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Address)) throw new InvalidOperationException("OPENBAO__ADDR is required.");
        if (string.IsNullOrWhiteSpace(Role)) throw new InvalidOperationException("OPENBAO__ROLE is required.");
        if (string.IsNullOrWhiteSpace(Path)) throw new InvalidOperationException("OPENBAO__PATH is required.");
        if (!File.Exists(TokenPath)) throw new FileNotFoundException("Kubernetes service account token not found.", TokenPath);
    }

    private static string Env(string key, string fallback = "") => Environment.GetEnvironmentVariable(key) ?? fallback;
    private static bool Bool(string key, bool fallback) => bool.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : fallback;
    private static int Int(string key, int fallback) => int.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : fallback;
}
