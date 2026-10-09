using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.Core;

/// <summary>
/// Loads <see cref="PlayerOptOut"/> before the host finishes starting (fails closed) and reloads it every hour.
/// When not required the host starts without the list and loading is retried until it succeeds.
/// </summary>
public class PlayerOptOutRefresher : BackgroundService
{
    private const int StartupAttempts = 4;
    private readonly Func<CancellationToken, Task> load;
    private readonly ILogger<PlayerOptOutRefresher> logger;
    private readonly TimeSpan interval;
    private readonly TimeSpan retryDelay;
    private readonly bool required;
    private readonly Func<CancellationToken, Task> onLoaded;
    private bool loaded;

    /// <param name="fromIndexer">true: load over HTTP from the indexer (INDEXER_BASE_URL) instead of the database</param>
    public PlayerOptOutRefresher(IConfiguration config, ILogger<PlayerOptOutRefresher> logger, bool required = true, Func<CancellationToken, Task> onLoaded = null, bool fromIndexer = false)
        : this(CreateLoader(config, fromIndexer), logger, TimeSpan.FromHours(1), TimeSpan.FromSeconds(3), required, onLoaded)
    {
    }

    private static Func<CancellationToken, Task> CreateLoader(IConfiguration config, bool fromIndexer)
    {
        if (!fromIndexer)
            return token => PlayerOptOut.LoadAsync(PlayerOptOut.ResolveConnectionString(config), token);
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        return token => PlayerOptOut.LoadFromIndexerAsync(client, PlayerOptOut.ResolveIndexerBaseUrl(config), token);
    }

    /// <param name="required">false: don't block startup on the first load, retry it in the background</param>
    /// <param name="onLoaded">invoked after every successful load, eg. to remove data of newly opted out players</param>
    public PlayerOptOutRefresher(Func<CancellationToken, Task> load, ILogger<PlayerOptOutRefresher> logger, TimeSpan interval, TimeSpan retryDelay, bool required = true, Func<CancellationToken, Task> onLoaded = null)
    {
        this.load = load;
        this.logger = logger;
        this.interval = interval;
        this.retryDelay = retryDelay;
        this.required = required;
        this.onLoaded = onLoaded;
    }

    /// <summary>Performs the first load; throws after a few failed attempts so the service doesn't process data without the list</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; required; attempt++)
        {
            try
            {
                await LoadAndNotify(cancellationToken);
                break;
            }
            catch (Exception e) when (attempt < StartupAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(e, "Loading player opt-out list failed (attempt {Attempt}/{Max})", attempt, StartupAttempts);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (!loaded)
            {
                await RefreshOnce(stoppingToken);
                if (!loaded)
                    await Task.Delay(retryDelay * 20, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RefreshOnce(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshOnce(CancellationToken token)
    {
        try
        {
            await LoadAndNotify(token);
        }
        catch (Exception e) when (!token.IsCancellationRequested)
        {
            logger.LogError(e, "Refreshing player opt-out list failed, keeping the previous list");
        }
    }

    private async Task LoadAndNotify(CancellationToken token)
    {
        await load(token);
        loaded = true;
        if (onLoaded == null)
            return;
        try
        {
            await onLoaded(token);
        }
        catch (Exception e) when (!token.IsCancellationRequested)
        {
            logger.LogError(e, "Handling the loaded player opt-out list failed");
        }
    }
}

public static class PlayerOptOutServiceExtention
{
    /// <summary>Registers the hosted service that loads the opt-out list at startup and refreshes it hourly</summary>
    /// <param name="required">false: the service starts without the list and loading is retried in the background</param>
    /// <param name="onLoaded">invoked after every successful load</param>
    /// <param name="fromIndexer">true: load from the indexer's GET Player/optout (INDEXER_BASE_URL) instead of the database</param>
    public static IServiceCollection AddPlayerOptOut(this IServiceCollection services, bool required = true, Func<IServiceProvider, CancellationToken, Task> onLoaded = null, bool fromIndexer = false)
    {
        services.AddHostedService(sp => new PlayerOptOutRefresher(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<PlayerOptOutRefresher>>(),
            required, onLoaded == null ? null : token => onLoaded(sp, token), fromIndexer));
        return services;
    }
}
