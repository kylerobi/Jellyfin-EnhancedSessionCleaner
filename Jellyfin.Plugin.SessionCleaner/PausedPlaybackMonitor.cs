using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EnhancedSessionCleaner.Configuration;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EnhancedSessionCleaner;

/// <summary>
/// Stops playback that has been paused for longer than the configured time, without signing the client out.
/// Used by <see cref="PausedPlaybackService"/> (automatic checks) and <see cref="StopPausedPlaybackTask"/> (run now).
/// </summary>
public sealed class PausedPlaybackMonitor
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<PausedPlaybackMonitor> _logger;
    private readonly ConcurrentDictionary<string, SessionState> _states = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="PausedPlaybackMonitor"/> class.
    /// </summary>
    /// <param name="sessionManager">Instance of the <see cref="ISessionManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{PausedPlaybackMonitor}"/> interface.</param>
    public PausedPlaybackMonitor(ISessionManager sessionManager, ILogger<PausedPlaybackMonitor> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Checks all sessions once and stops playback that has been paused for too long.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of sessions whose playback was stopped.</returns>
    public async Task<int> CheckAsync(CancellationToken cancellationToken)
    {
        var config = SessionCleanerPlugin.Instance?.Configuration;
        if (config is null || !config.StopPausedPlayback)
        {
            _states.Clear();
            return 0;
        }

        // The background service and a manual task run must not stop the same session twice.
        await _checkLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await CheckSessionsAsync(config, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private async Task<int> CheckSessionsAsync(PluginConfiguration config, DateTime now, CancellationToken cancellationToken)
    {
        var stoppedCount = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var session in _sessionManager.Sessions.ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = session.NowPlayingItem;
            if (item is null)
            {
                continue;
            }

            seen.Add(session.Id);
            var state = _states.GetOrAdd(session.Id, _ => new SessionState());

            // A different item started: anything paused before now belongs to the previous item.
            if (state.ItemId != item.Id)
            {
                if (state.ItemId is not null)
                {
                    state.NotPausedAt = now;
                }

                state.ItemId = item.Id;
                state.FirstSeenPaused = null;
                state.Reported = false;
            }

            if (session.PlayState?.IsPaused != true)
            {
                state.NotPausedAt = now;
                state.FirstSeenPaused = null;
                state.Reported = false;
                continue;
            }

            state.FirstSeenPaused ??= now;

            // Jellyfin records when the current pause began (LastPausedDate). If it doesn't, fall back to
            // when this plugin first saw the pause. Never count from before we last saw the video playing.
            var pausedSince = session.LastPausedDate ?? state.FirstSeenPaused.Value;
            if (state.NotPausedAt is DateTime notPausedAt && notPausedAt > pausedSince)
            {
                pausedSince = notPausedAt;
            }

            var category = ClientClassifier.Classify(session.Client, session.DeviceName, config);
            var allowedMinutes = ClientClassifier.GetPausedMinutes(category, config);
            var pausedFor = now - pausedSince;

            _logger.LogDebug(
                "{DeviceName} ({Client}, {Category}) paused for {PausedMinutes:F1} of {AllowedMinutes} minutes",
                session.DeviceName,
                session.Client,
                category,
                pausedFor.TotalMinutes,
                allowedMinutes);

            if (allowedMinutes <= 0 || pausedFor < TimeSpan.FromMinutes(allowedMinutes))
            {
                continue;
            }

            if (await StopAsync(session, item.Name, config.PausedStopMessage, state, cancellationToken).ConfigureAwait(false))
            {
                stoppedCount++;
                _states.TryRemove(session.Id, out _);
            }
        }

        // Forget sessions that stopped playing or disconnected.
        foreach (var sessionId in _states.Keys)
        {
            if (!seen.Contains(sessionId))
            {
                _states.TryRemove(sessionId, out _);
            }
        }

        return stoppedCount;
    }

    private async Task<bool> StopAsync(SessionInfo session, string? itemName, string? message, SessionState state, CancellationToken cancellationToken)
    {
        if (!session.SupportsMediaControl)
        {
            if (!state.Reported)
            {
                state.Reported = true;
                _logger.LogInformation(
                    "Can't stop paused playback on {DeviceName} ({Client}): the client doesn't accept remote control",
                    session.DeviceName,
                    session.Client);
            }

            return false;
        }

        try
        {
            // Sent the same way as Jellyfin's own inactive-session check: as the session itself, on behalf
            // of its user. Some clients ignore playstate commands that have no controlling user.
            await _sessionManager.SendPlaystateCommand(
                session.Id,
                session.Id,
                new PlaystateRequest
                {
                    Command = PlaystateCommand.Stop,
                    ControllingUserId = session.UserId.ToString("N")
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!state.Reported)
            {
                state.Reported = true;
                _logger.LogWarning(ex, "Failed to stop paused playback on {DeviceName} ({Client})", session.DeviceName, session.Client);
            }

            return false;
        }

        _logger.LogInformation(
            "Stopped paused playback of {ItemName} on {DeviceName} ({Client}) for user {UserName}",
            itemName,
            session.DeviceName,
            session.Client,
            session.UserName);

        // The message is a courtesy: a client that can't show it must not keep the video from stopping.
        if (!string.IsNullOrWhiteSpace(message) && session.SupportsRemoteControl)
        {
            try
            {
                await _sessionManager.SendMessageCommand(
                    session.Id,
                    session.Id,
                    new MessageCommand { Header = "Playback stopped", Text = message, TimeoutMs = 10000 },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Couldn't show the stop message on {DeviceName} ({Client})", session.DeviceName, session.Client);
            }
        }

        return true;
    }

    private sealed class SessionState
    {
        public Guid? ItemId { get; set; }

        public DateTime? FirstSeenPaused { get; set; }

        public DateTime? NotPausedAt { get; set; }

        public bool Reported { get; set; }
    }
}
