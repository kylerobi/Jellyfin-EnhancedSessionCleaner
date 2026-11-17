using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.EnhancedSessionCleaner.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the amount of days a device should be kept.
    /// Used for clients that are neither web nor TV clients, and as the fallback
    /// for web / TV clients when no specific value is configured.
    /// A value of 0 or less disables cleaning for these clients.
    /// </summary>
    public int Days { get; set; } = 30;

    /// <summary>
    /// Gets or sets the amount of days a web client device should be kept.
    /// <c>null</c> means "use <see cref="Days"/>". A value of 0 or less disables cleaning of web clients.
    /// </summary>
    public int? WebDays { get; set; }

    /// <summary>
    /// Gets or sets the amount of days a TV client device should be kept.
    /// <c>null</c> means "use <see cref="Days"/>". A value of 0 or less disables cleaning of TV clients.
    /// </summary>
    public int? TvDays { get; set; }

    /// <summary>
    /// Gets or sets a comma separated list of keywords identifying web clients.
    /// Matched case-insensitively against the client (app) name and the device name.
    /// </summary>
    public string WebClientKeywords { get; set; } = ClientClassifier.DefaultWebKeywords;

    /// <summary>
    /// Gets or sets a comma separated list of keywords identifying TV clients.
    /// Matched case-insensitively against the client (app) name and the device name.
    /// TV keywords are checked before web keywords.
    /// </summary>
    public string TvClientKeywords { get; set; } = ClientClassifier.DefaultTvKeywords;

    /// <summary>
    /// Gets or sets a value indicating whether playback that stays paused for too long is stopped.
    /// The client stays signed in; only the paused video is stopped.
    /// </summary>
    public bool StopPausedPlayback { get; set; }

    /// <summary>
    /// Gets or sets how many minutes playback may stay paused before it is stopped.
    /// Used for clients that are neither web nor TV clients, and as the fallback for web / TV clients.
    /// A value of 0 or less never stops paused playback for these clients.
    /// </summary>
    public int PausedMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets how many minutes playback on a web client may stay paused.
    /// <c>null</c> means "use <see cref="PausedMinutes"/>". 0 or less never stops paused web playback.
    /// </summary>
    public int? PausedWebMinutes { get; set; }

    /// <summary>
    /// Gets or sets how many minutes playback on a TV client may stay paused.
    /// <c>null</c> means "use <see cref="PausedMinutes"/>". 0 or less never stops paused TV playback.
    /// </summary>
    public int? PausedTvMinutes { get; set; }

    /// <summary>
    /// Gets or sets the message shown on the client when its paused playback is stopped.
    /// Empty means no message is shown.
    /// </summary>
    public string PausedStopMessage { get; set; } = DefaultPausedStopMessage;

    /// <summary>
    /// Gets the default message shown when paused playback is stopped.
    /// </summary>
    public static string DefaultPausedStopMessage => "Playback was stopped because it was paused for too long.";
}
