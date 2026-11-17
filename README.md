# Jellyfin Enhanced Session Cleaner Plugin

![Plugin Version](https://img.shields.io/badge/version-2.0.0.0-blue)
![Jellyfin Compatible](https://img.shields.io/badge/jellyfin-12.0--12.4-purple)

**Enhanced Session Cleaner** is a new plugin based on the original Jellyfin Session Cleaner. It not only automates the removal of stale devices but puts the control back in your hands with a new dashboard interface.

![Active Session Dashboard](https://i.imgur.com/GxW4749.png)

##  Key Features

*   **Active Session Dashboard**: View a real-time list of all active sessions directly from the plugin configuration page.
*   **Kill Switch**: Manually disconnect (logout) any specific session or device with a single click.
*   **Auto-Cleanup**: Automatically removes sessions/devices that have been inactive for a configurable number of days.
*   **Per-Client Cleanup Rules**: Set separate retention periods for **web** (browser) sessions, **TV** clients (Android TV, Roku, Apple TV, webOS, Tizen, Kodi, ...) and **all other** clients. Leave a web/TV value empty to use the default, or set it to `0` to never clean that client type. The keywords used to detect web and TV clients are editable, and each active session in the dashboard shows the type it was detected as.

##  Build & Installation

### Requirements
*   .NET 10.0 SDK
*   Jellyfin 12.0 – 12.4 (version 2.x). For Jellyfin 10.11, use plugin version 1.x.

```bash
dotnet build -c Release
```

### Repository Installation

1.  Open your Jellyfin Dashboard.
2.  Go to **Plugins** > **Repositories**.
3.  Click (+) Add Repository.
4.  Name: `Enhanced Session Cleaner`
5.  Repository URL: `https://raw.githubusercontent.com/Trewqa/jellyfin-plugin-enhancedsessioncleaner/master/manifest.json`
6.  Save and go to the Catalog to install.

## Credits & License

This project is a **hard fork** of the original [Jellyfin Session Cleaner Plugin](https://github.com/jellyfin/jellyfin-plugin-sessioncleaner).
Original code licensed under the generic MIT/GPL license of the original repository.

Modifications and enhancements (UI, Kill Switch) developed by Trewqa.