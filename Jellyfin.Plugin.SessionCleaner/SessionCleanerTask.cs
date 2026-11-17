using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Queries;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.EnhancedSessionCleaner;

/// <summary>
/// Device cleaner task.
/// </summary>
public class SessionCleanerTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly IDeviceManager _deviceManager;
    private readonly ISessionManager _sessionManager;
    private readonly ILocalizationManager _localizationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCleanerTask"/> class.
    /// </summary>
    /// <param name="sessionManager">Instance of the <see cref="ISessionManager"/> interface.</param>
    /// <param name="localizationManager">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="deviceManager">Instance of the <see cref="IDeviceManager"/> interface.</param>
    public SessionCleanerTask(
        ISessionManager sessionManager,
        ILocalizationManager localizationManager,
        IDeviceManager deviceManager)
    {
        _sessionManager = sessionManager;
        _localizationManager = localizationManager;
        _deviceManager = deviceManager;
    }

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public string Name => "Clean Old Sessions";

    /// <inheritdoc />
    public string Key => "CleanOldSessions";

    /// <inheritdoc />
    public string Description => "Removes sessions older then the configured age.";

    /// <inheritdoc />
    public string Category => _localizationManager.GetLocalizedString("TasksMaintenanceCategory");

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(SessionCleanerPlugin.Instance?.Configuration);
        var config = SessionCleanerPlugin.Instance.Configuration;
        var now = DateTime.UtcNow;
        var deviceResult = _deviceManager.GetDevices(new DeviceQuery());
        var devices = deviceResult?.Items;

        if (devices is null || devices.Count == 0)
        {
            return;
        }

        for (var i = 0; i < devices.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var device = devices[i];
            var category = ClientClassifier.Classify(device.AppName, device.DeviceName, config);
            var expireDays = ClientClassifier.GetRetentionDays(category, config);

            // 0 or less disables cleaning for this kind of client.
            if (expireDays > 0 && device.DateLastActivity < now.AddDays(-expireDays))
            {
                await _sessionManager.Logout(device).ConfigureAwait(false);
            }

            progress.Report(100d * (i + 1) / devices.Count);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return Enumerable.Empty<TaskTriggerInfo>();
    }
}
