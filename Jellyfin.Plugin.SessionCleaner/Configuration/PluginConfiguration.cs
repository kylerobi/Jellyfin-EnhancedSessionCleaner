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
}
