using System;
using System.Linq;
using Jellyfin.Plugin.EnhancedSessionCleaner.Configuration;

namespace Jellyfin.Plugin.EnhancedSessionCleaner;

/// <summary>
/// The kind of client a device belongs to.
/// </summary>
public enum ClientCategory
{
    /// <summary>
    /// Any client that is neither a web nor a TV client.
    /// </summary>
    Other,

    /// <summary>
    /// A browser based client (Jellyfin Web).
    /// </summary>
    Web,

    /// <summary>
    /// A TV client (Android TV, Roku, tvOS, webOS, Tizen, Kodi, ...).
    /// </summary>
    Tv
}

/// <summary>
/// Classifies devices into client categories and resolves their retention period.
/// </summary>
public static class ClientClassifier
{
    /// <summary>
    /// Default keywords identifying web clients.
    /// </summary>
    public const string DefaultWebKeywords = "Jellyfin Web";

    /// <summary>
    /// Default keywords identifying TV clients.
    /// </summary>
    public const string DefaultTvKeywords = "Android TV, AndroidTV, Fire TV, FireTV, Roku, tvOS, Apple TV, webOS, Tizen, Smart TV, Kodi, Xbox";

    /// <summary>
    /// Determines the category of a client.
    /// </summary>
    /// <param name="appName">The client (app) name.</param>
    /// <param name="deviceName">The device name.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>The client category.</returns>
    public static ClientCategory Classify(string? appName, string? deviceName, PluginConfiguration config)
    {
        // TV keywords win: some TV apps (e.g. webOS / Tizen) wrap Jellyfin Web.
        if (Matches(config.TvClientKeywords, appName, deviceName))
        {
            return ClientCategory.Tv;
        }

        if (Matches(config.WebClientKeywords, appName, deviceName))
        {
            return ClientCategory.Web;
        }

        return ClientCategory.Other;
    }

    /// <summary>
    /// Gets the number of days devices of a category are kept. 0 or less means never clean.
    /// </summary>
    /// <param name="category">The client category.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>The retention in days.</returns>
    public static int GetRetentionDays(ClientCategory category, PluginConfiguration config)
    {
        return category switch
        {
            ClientCategory.Web => config.WebDays ?? config.Days,
            ClientCategory.Tv => config.TvDays ?? config.Days,
            _ => config.Days
        };
    }

    /// <summary>
    /// Gets the number of minutes playback of a category may stay paused. 0 or less means never stop.
    /// </summary>
    /// <param name="category">The client category.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>The allowed pause time in minutes.</returns>
    public static int GetPausedMinutes(ClientCategory category, PluginConfiguration config)
    {
        return category switch
        {
            ClientCategory.Web => config.PausedWebMinutes ?? config.PausedMinutes,
            ClientCategory.Tv => config.PausedTvMinutes ?? config.PausedMinutes,
            _ => config.PausedMinutes
        };
    }

    private static bool Matches(string? keywords, string? appName, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return false;
        }

        return keywords
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(k => Contains(appName, k) || Contains(deviceName, k));
    }

    private static bool Contains(string? value, string keyword)
        => value is not null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
