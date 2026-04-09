#if ANDROID
using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System.Runtime.Versioning;

namespace FOOD_MAP.Services;

public sealed class AndroidLocationService : ILocationService
{
    public async Task<bool> RequestLocationPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var whenInUseStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (whenInUseStatus != PermissionStatus.Granted)
        {
            whenInUseStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (whenInUseStatus != PermissionStatus.Granted)
        {
            return false;
        }

        // Trên Android 13+, xin thêm quyền thông báo để hiển thị cảnh báo geofence.
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            try
            {
                var notificationStatus = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (notificationStatus != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.PostNotifications>();
                }
            }
            catch
            {
                // Không chặn luồng theo dõi vị trí nếu xin quyền thông báo lỗi.
            }
        }

        // Xin quyền Always theo kiểu best-effort để hỗ trợ nền, nhưng không chặn tracking khi user chỉ cấp WhileInUse.
        _ = TryRequestBackgroundLocationPermissionAsync();
        return true;
    }

    public Task StartTrackingAsync(CancellationToken cancellationToken = default)
    {
        var context = Android.App.Application.Context;
        var serviceIntent = new Intent(context, typeof(LocationTrackingForegroundService));

        // Android 8+ bat buoc dung ForegroundService, ban thap hon dung StartService.
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            StartForegroundServiceForApi26AndAbove(context, serviceIntent);
        }
        else
        {
            context.StartService(serviceIntent);
        }

        return Task.CompletedTask;
    }

    public Task StopTrackingAsync(CancellationToken cancellationToken = default)
    {
        var context = Android.App.Application.Context;
        var serviceIntent = new Intent(context, typeof(LocationTrackingForegroundService));
        context.StopService(serviceIntent);
        return Task.CompletedTask;
    }

    public async Task<Location?> GetLastKnownLocationAsync(CancellationToken cancellationToken = default)
    {
        return await Geolocation.GetLastKnownLocationAsync();
    }

    [SupportedOSPlatform("android26.0")]
    private static void StartForegroundServiceForApi26AndAbove(Context context, Intent serviceIntent)
    {
        context.StartForegroundService(serviceIntent);
    }

    private static async Task TryRequestBackgroundLocationPermissionAsync()
    {
        try
        {
            var alwaysStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (alwaysStatus == PermissionStatus.Granted || alwaysStatus == PermissionStatus.Restricted)
            {
                return;
            }

            await Permissions.RequestAsync<Permissions.LocationAlways>();
        }
        catch
        {
            // Bỏ qua lỗi để tránh gián đoạn luồng tracking foreground.
        }
    }
}
#endif
