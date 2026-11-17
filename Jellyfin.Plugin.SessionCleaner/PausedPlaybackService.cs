using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EnhancedSessionCleaner;

/// <summary>
/// Runs the paused playback check in the background every 30 seconds while the server is running.
/// Does nothing unless "Stop Paused Playback" is enabled in the plugin settings.
/// </summary>
public sealed class PausedPlaybackService : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    private readonly PausedPlaybackMonitor _monitor;
    private readonly ILogger<PausedPlaybackService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PausedPlaybackService"/> class.
    /// </summary>
    /// <param name="monitor">The paused playback monitor.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{PausedPlaybackService}"/> interface.</param>
    public PausedPlaybackService(PausedPlaybackMonitor monitor, ILogger<PausedPlaybackService> logger)
    {
        _monitor = monitor;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Paused playback check started (every {Seconds} seconds)", _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await _monitor.CheckAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Paused playback check failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Server is shutting down.
        }
    }
}
