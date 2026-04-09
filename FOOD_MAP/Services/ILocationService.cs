using Microsoft.Maui.Devices.Sensors;

namespace FOOD_MAP.Services;

public interface ILocationService
{
    // Kiem tra va xin quyen vi tri can thiet tren Android.
    Task<bool> RequestLocationPermissionsAsync(CancellationToken cancellationToken = default);

    // Bat dau theo doi vi tri (foreground/background se duoc xu ly o buoc implement).
    Task StartTrackingAsync(CancellationToken cancellationToken = default);

    // Dung theo doi vi tri.
    Task StopTrackingAsync(CancellationToken cancellationToken = default);

    // Lay vi tri gan nhat da biet de su dung nhanh tren UI.
    Task<Location?> GetLastKnownLocationAsync(CancellationToken cancellationToken = default);
}
