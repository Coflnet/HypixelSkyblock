using System;
using System.Threading.Tasks;
using Coflnet.Core.Security;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Coflnet.Sky.Core;

/// <summary>
/// Opt-in MariaDB client certificate support. Active only when the <c>DB:TLS</c> section exists
/// (<c>CA_CERT_PATH</c>, <c>CERT_PATH</c>, <c>KEY_PATH</c>); otherwise nothing changes.
/// <see cref="HypixelContext.SetConfiguration"/> installs it only when <c>DB:TLS:SHARED_CONTEXT</c> is <c>true</c>:
/// several services already set <c>DB:TLS</c> for their own DbContext while their HypixelContext connection
/// string points at a host the strict server check would reject, so the shared context needs its own opt-in.
/// </summary>
public sealed class DatabaseTlsFactory
{
    private const string SectionName = "DB:TLS";
    private const string SharedContextKey = SectionName + ":SHARED_CONTEXT";
    private static readonly string[] RequiredKeys = { "CA_CERT_PATH", "CERT_PATH", "KEY_PATH" };
    private readonly PemTlsOptions credentials;

    /// <summary>Throws <see cref="InvalidOperationException"/> naming the missing key when the section is incomplete.</summary>
    public DatabaseTlsFactory(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        if (!section.Exists())
            return;
        foreach (var key in RequiredKeys)
            if (string.IsNullOrWhiteSpace(section[key]))
                throw new InvalidOperationException($"{SectionName}:{key} must be configured.");
        credentials = PemTlsOptions.FromConfiguration(section);
    }

    /// <summary>True when <c>DB:TLS</c> is configured.</summary>
    public bool Enabled => credentials != null;

    /// <summary>
    /// Installs the TLS factory on <see cref="HypixelContext.ConnectionFactory"/> if <c>DB:TLS:SHARED_CONTEXT</c> is
    /// <c>true</c> and no factory was installed before. Returns true when it installed one.
    /// </summary>
    public static bool InstallIfConfigured(IConfiguration configuration)
    {
        if (!SharedContextRequested(configuration))
            return false;
        if (HypixelContext.ConnectionFactory != null)
            return false;
        var tls = new DatabaseTlsFactory(configuration);
        HypixelContext.ConnectionFactory = tls.CreateConnection;
        return true;
    }

    private static bool SharedContextRequested(IConfiguration configuration) =>
        configuration != null && string.Equals(configuration[SharedContextKey], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Creates a closed connection; without credentials the connection string is used verbatim.</summary>
    public MySqlConnection CreateConnection(string connectionString)
    {
        if (credentials == null)
            return new MySqlConnection(connectionString);
        var options = new MySqlConnectionStringBuilder(connectionString);
        options.ConnectionTimeout = options.ConnectionTimeout == 0 ? 10 : Math.Min(options.ConnectionTimeout, 10);
        options.ConnectionLifeTime = options.ConnectionLifeTime == 0 ? 300 : Math.Min(options.ConnectionLifeTime, 300);
        // Required + unconditional strict callback also rejects password-bound TLS validation failures.
        options.SslMode = MySqlSslMode.Required;
        options.SslCa = options.SslCert = options.SslKey = "";
        options.CertificateFile = options.CertificatePassword = options.CertificateThumbprint = "";
        options.CertificateStoreLocation = MySqlCertificateStoreLocation.None;
        var connection = new MySqlConnection(options.ConnectionString);
        connection.RemoteCertificateValidationCallback = credentials.ValidateServerCertificate;
        connection.ProvideClientCertificatesCallback = AddClientCertificate;
        return connection;
    }

    private ValueTask AddClientCertificate(System.Security.Cryptography.X509Certificates.X509CertificateCollection certificates)
    {
        credentials.RegisterClientIntermediates();
        certificates.Add(credentials.GetCertificate());
        return ValueTask.CompletedTask;
    }
}
