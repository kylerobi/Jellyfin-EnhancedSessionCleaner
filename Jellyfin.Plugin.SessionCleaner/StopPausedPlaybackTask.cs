using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EnhancedSessionCleaner;

/// <summary>
/// Stops playback that has been paused for longer than the configured time, without signing the client out.
/// </summary>
public class StopPausedPlaybackTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ISessionManager _sessionManager;
    private readonly ILocalizationManager _localizationManager;
    private readonly ILogger<StopPausedPlaybackTask> _logger;

    // Session id -> (item id, first time this task saw the item paused).
    private readonly ConcurrentDictionary<string, (Guid ItemId, DateTime FirstSeenPaused)> _pausedSince = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StopPausedPlaybackTask"/> class.
    /// </summary>
    /// <param name="sessionManager">Instance of the <see cref="ISessionManager"/> interface.</param>
    /// <param name="localizationManager">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{StopPausedPlaybackTask}"/> interface.</param>
    public StopPausedPlaybackTask(
        ISessionManager sessionManager,
        ILocalizationManager localizationManager,
        ILogger<StopPausedPlaybackTask> logger)
    {
        _sessionManager = sessionManager;
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
    public string Description => "Stops videos that have been paused longer than the configured time. Clients stay signed in.";

    /// <inheritdoc />
    public string Category => _localizationManager.GetLocalizedString("TasksMaintenanceCategory");

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(SessionCleanerPlugin.Instance?.Configuration);
        var config = SessionCleanerPlugin.Instance.Configuration;

        if (!config.StopPausedPlayback)
        {
            _pausedSince.Clear();
            return;
        }

        var now = DateTime.UtcNow;
        var sessions = _sessionManager.Sessions.ToList();
        var stillPaused = new HashSet<string>(StringComparer.Ordinal);

        foreach (var session in sessions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = session.NowPlayingItem;
            if (item is null || session.PlayState?.IsPaused != true)
            {
                continue;
            }

            stillPaused.Add(session.Id);

            // Track when we first saw this item paused. Jellyfin's LastPausedDate is also used,
            // and the later of the two wins so a stale value can never stop playback early.
            var tracked = _pausedSince.AddOrUpdate(
                session.Id,
                _ => (item.Id, now),
                (_, existing) => existing.ItemId == item.Id ? existing : (item.Id, now));
            var pausedSince = session.LastPausedDate is DateTime lastPaused && lastPaused > tracked.FirstSeenPaused
                ? lastPaused
                : tracked.FirstSeenPaused;

            var category = ClientClassifier.Classify(session.Client, session.DeviceName, config);
            var allowedMinutes = ClientClassifier.GetPausedMinutes(category, config);
            if (allowedMinutes <= 0 || now - pausedSince < TimeSpan.FromMinutes(allowedMinutes))
            {
                continue;
            }

            if (!session.SupportsMediaControl)
            {
                _logger.LogDebug(
                    "Paused playback on {DeviceName} ({Client}) can't be stopped: the client doesn't accept remote control",
                    session.DeviceName,
                    session.Client);
                continue;
            }

            await StopAsync(session, item.Name, config.PausedStopMessage, cancellationToken).ConfigureAwait(false);
            _pausedSince.TryRemove(session.Id, out _);
        }

        // Forget sessions that resumed, stopped or disconnected.
        foreach (var sessionId in _pausedSince.Keys)
        {
            if (!stillPaused.Contains(sessionId))
            {
                _pausedSince.TryRemove(sessionId, out _);
            }
        }

        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromMinutes(1).Ticks
        };
    }

    private async Task StopAsync(SessionInfo session, string? itemName, string? message, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(message) && session.SupportsRemoteControl)
            {
                await _sessionManager.SendMessageCommand(
                    null,
                    session.Id,
                    new MessageCommand { Header = "Playback stopped", Text = message, TimeoutMs = 10000 },
                    cancellationToken).ConfigureAwait(false);
            }

            await _sessionManager.SendPlaystateCommand(
                null,
                session.Id,
                new PlaystateRequest { Command = PlaystateCommand.Stop },
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Stopped paused playback of {ItemName} on {DeviceName} ({Client}) for user {UserName}",
                itemName,
                session.DeviceName,
                session.Client,
                session.UserName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to stop paused playback on {DeviceName} ({Client})", session.DeviceName, session.Client);
        }
    }
}
