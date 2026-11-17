using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EnhancedSessionCleaner;

/// <summary>
/// Runs the paused playback check on demand. The automatic check runs in <see cref="PausedPlaybackService"/>
/// every 30 seconds, so this task has no triggers by default.
/// </summary>
public class StopPausedPlaybackTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly PausedPlaybackMonitor _monitor;
    private readonly ILocalizationManager _localizationManager;
    private readonly ILogger<StopPausedPlaybackTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StopPausedPlaybackTask"/> class.
    /// </summary>
    /// <param name="monitor">The paused playback monitor.</param>
    /// <param name="localizationManager">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{StopPausedPlaybackTask}"/> interface.</param>
    public StopPausedPlaybackTask(
        PausedPlaybackMonitor monitor,
        ILocalizationManager localizationManager,
        ILogger<StopPausedPlaybackTask> logger)
    {
        _monitor = monitor;
        _localizationManager = localizationManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => false;

    /// <inheritdoc />
    public string Name => "Stop Paused Playback";

    /// <inheritdoc />
    public string Key => "StopPausedPlayback";

    /// <inheritdoc />
    public string Description => "Checks now for videos paused longer than the configured time and stops them. Clients stay signed in. This check also runs automatically every 30 seconds when enabled in the plugin settings.";

    /// <inheritdoc />
    public string Category => _localizationManager.GetLocalizedString("TasksMaintenanceCategory");

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (SessionCleanerPlugin.Instance?.Configuration.StopPausedPlayback != true)
        {
            _logger.LogInformation("Stop Paused Playback is turned off in the Enhanced Session Cleaner settings");
            return;
        }

        var stopped = await _monitor.CheckAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Paused playback check stopped {Count} session(s)", stopped);
        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();
}
