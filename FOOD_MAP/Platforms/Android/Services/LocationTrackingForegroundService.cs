#if ANDROID
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Location;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using FOOD_MAP.Shared.Data;
using FOOD_MAP.Services.Geofencing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui;
using Microsoft.Maui.Storage;
using System.Runtime.Versioning;

namespace FOOD_MAP.Services;

[Service(Enabled = true, Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class LocationTrackingForegroundService : Service
{
    private const string NotificationChannelId = "food_map_tracking_channel";
    private const string TriggerNotificationChannelId = "food_map_trigger_channel";
    private const int TrackingNotificationId = 22031;
    private const int TriggerNotificationBaseId = 23000;
    private const string NotificationChannelName = "FOOD_MAP Tracking";
    private const string TriggerNotificationChannelName = "FOOD_MAP POI Alerts";

    private static readonly TimeSpan TriggerCooldown = TimeSpan.FromSeconds(15);
    private const int DebounceHitsRequired = 1;

    private readonly Dictionary<string, DateTimeOffset> _lastTriggeredUtcByPoiId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _isInsidePoiById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _insideDebounceHitsByPoiId = new(StringComparer.Ordinal);

    private IFusedLocationProviderClient? _fusedClient;
    private LocationRequest? _locationRequest;
    private LocationCallback? _locationCallback;

    private readonly SemaphoreSlim _geoSemaphore = new(1, 1);

    public override void OnCreate()
    {
        base.OnCreate();

        _fusedClient = LocationServices.GetFusedLocationProviderClient(this);

        _locationRequest = new LocationRequest.Builder(Priority.PriorityHighAccuracy, 5000)
            .SetMinUpdateIntervalMillis(3000)
            .Build();

        _locationCallback = new TrackingLocationCallback(this);
        EnsureNotificationChannel();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var notification = BuildTrackingNotification("Tracking location in background");
        StartForeground(TrackingNotificationId, notification);

        _ = StartLocationUpdatesAsync();
        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        _ = StopLocationUpdatesAsync();
    }

    private Task StartLocationUpdatesAsync()
    {
        if (_fusedClient is null || _locationRequest is null || _locationCallback is null)
        {
            return Task.CompletedTask;
        }

        if (!HasLocationPermission())
        {
            StopSelf();
            return Task.CompletedTask;
        }

        try
        {
            _fusedClient.RequestLocationUpdates(_locationRequest, _locationCallback, Looper.MainLooper);
        }
        catch (Exception)
        {
            StopSelf();
        }

        return Task.CompletedTask;
    }

    private Task StopLocationUpdatesAsync()
    {
        if (_fusedClient is null || _locationCallback is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _fusedClient.RemoveLocationUpdates(_locationCallback);
        }
        catch
        {
            // Không cần ném lỗi khi app đang tắt service.
        }

        return Task.CompletedTask;
    }

    private bool HasLocationPermission()
    {
        var fineGranted = CheckSelfPermission(Manifest.Permission.AccessFineLocation) == Permission.Granted;
        var coarseGranted = CheckSelfPermission(Manifest.Permission.AccessCoarseLocation) == Permission.Granted;
        return fineGranted || coarseGranted;
    }

    private void EnsureNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        CreateNotificationChannelForApi26AndAbove();
    }

    [SupportedOSPlatform("android26.0")]
    private void CreateNotificationChannelForApi26AndAbove()
    {

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        if (notificationManager is null)
        {
            return;
        }

        var channel = new NotificationChannel(
            NotificationChannelId,
            NotificationChannelName,
            NotificationImportance.Low)
        {
            Description = "Background location tracking for POI geofence."
        };

        notificationManager.CreateNotificationChannel(channel);

        var triggerChannel = new NotificationChannel(
            TriggerNotificationChannelId,
            TriggerNotificationChannelName,
            NotificationImportance.High)
        {
            Description = "Thông báo khi người dùng vào vùng kích hoạt POI."
        };

        notificationManager.CreateNotificationChannel(triggerChannel);
    }

    private Notification BuildTrackingNotification(string contentText)
    {
        var builder = new NotificationCompat.Builder(this, NotificationChannelId);
        builder.SetContentTitle("FOOD_MAP");
        builder.SetContentText(contentText);
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetOngoing(true);
        builder.SetSilent(true);
        builder.SetPriority((int)NotificationPriority.Low);

        return builder.Build()!;
    }

    private Notification BuildTriggerNotification(string contentText)
    {
        var builder = new NotificationCompat.Builder(this, TriggerNotificationChannelId);
        builder.SetContentTitle("FOOD_MAP - POI Nearby");
        builder.SetContentText(contentText);
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetAutoCancel(true);
        builder.SetPriority((int)NotificationPriority.High);

        return builder.Build()!;
    }

    private async Task HandleLocationUpdateAsync(double latitude, double longitude)
    {
        var serviceProvider = IPlatformApplication.Current?.Services;
        if (serviceProvider is null)
        {
            return;
        }

        await _geoSemaphore.WaitAsync();
        try
        {
            var dbContextFactory = serviceProvider.GetService(typeof(IDbContextFactory<AppDbContext>)) as IDbContextFactory<AppDbContext>;
            if (dbContextFactory is null)
            {
                return;
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var pois = await dbContext.Pois
                .Select(x => new { x.Id, x.Latitude, x.Longitude, x.ActivationRadius, x.Priority })
                .ToListAsync();

            if (pois.Count == 0)
            {
                return;
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var nearestPoiText = "No nearby POI";
            double nearestDistance = double.MaxValue;

            foreach (var poi in pois)
            {
                var distanceMeters = GeofenceMath.CalculateDistanceMeters(
                    latitude,
                    longitude,
                    poi.Latitude,
                    poi.Longitude);

                if (distanceMeters < nearestDistance)
                {
                    nearestDistance = distanceMeters;
                    nearestPoiText = $"Nearest POI #{poi.Id} - {(int)distanceMeters}m";
                }

                var isInside = distanceMeters <= poi.ActivationRadius;
                var previousInside = _isInsidePoiById.TryGetValue(poi.Id, out var cachedInside) && cachedInside;

                if (!isInside)
                {
                    _isInsidePoiById[poi.Id] = false;
                    _insideDebounceHitsByPoiId[poi.Id] = 0;
                    continue;
                }

                var previousHits = _insideDebounceHitsByPoiId.TryGetValue(poi.Id, out var hits) ? hits : 0;
                var currentHits = previousHits + 1;
                _insideDebounceHitsByPoiId[poi.Id] = currentHits;

                var isDebouncedReady = currentHits >= DebounceHitsRequired;
                var hasCooldown = _lastTriggeredUtcByPoiId.TryGetValue(poi.Id, out var lastTriggeredUtc) &&
                                  nowUtc - lastTriggeredUtc < TriggerCooldown;

                if (!previousInside && isDebouncedReady && !hasCooldown)
                {
                    _isInsidePoiById[poi.Id] = true;
                    _lastTriggeredUtcByPoiId[poi.Id] = nowUtc;

                    // Luồng proximity: phát intro ngắn gọn khi vào geofence.
                    _ = TriggerProximityNarrationAsync(serviceProvider, poi.Id);

                    var triggerText = $"Triggered POI #{poi.Id} at {(int)distanceMeters}m";
                    var notificationManager = NotificationManagerCompat.From(this);
                    if (notificationManager is not null)
                    {
                        // Dùng ID riêng để thông báo geofence không bị ghi đè ngay bởi tracking notification.
                        var triggerNotificationId = TriggerNotificationBaseId + Math.Abs(poi.Id.GetHashCode(StringComparison.Ordinal) % 1000);
                        notificationManager.Notify(triggerNotificationId, BuildTriggerNotification(triggerText));
                    }
                }
                else
                {
                    _isInsidePoiById[poi.Id] = true;
                }
            }

            var trackingManager = NotificationManagerCompat.From(this);
            if (trackingManager is not null)
            {
                trackingManager.Notify(TrackingNotificationId, BuildTrackingNotification(nearestPoiText));
            }
        }
        finally
        {
            _geoSemaphore.Release();
        }
    }

    private static async Task TriggerProximityNarrationAsync(IServiceProvider serviceProvider, string poiId)
    {
        try
        {
            var selectedLanguage = Preferences.Default.Get("selected_language", "vi");
            var narrationService = serviceProvider.GetService(typeof(INarrationService)) as INarrationService;
            var dbContextFactory = serviceProvider.GetService(typeof(IDbContextFactory<AppDbContext>)) as IDbContextFactory<AppDbContext>;

            if (narrationService is null || dbContextFactory is null)
            {
                return;
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var translation = await dbContext.PoiTranslations
                .AsNoTracking()
                .Include(x => x.Language)
                .Where(x => x.PoiId == poiId)
                .FirstOrDefaultAsync(x => x.Language != null && x.Language.LanguageCode == selectedLanguage)
                ?? await dbContext.PoiTranslations
                    .AsNoTracking()
                    .Where(x => x.PoiId == poiId)
                    .FirstOrDefaultAsync();

            if (translation is null)
            {
                return;
            }

            // Proximity chỉ đọc intro ngắn, ưu tiên LocationName.
            var shortIntro = string.IsNullOrWhiteSpace(translation.LocationName)
                ? translation.TtsScript
                : $"Bạn đang đến gần {translation.LocationName}";

            await narrationService.PlayProximityNarrationAsync(shortIntro, selectedLanguage);
        }
        catch
        {
            // Nếu proximity narration lỗi thì bỏ qua để không ảnh hưởng luồng tracking.
        }
    }

    private sealed class TrackingLocationCallback : LocationCallback
    {
        private readonly WeakReference<LocationTrackingForegroundService> _serviceReference;

        public TrackingLocationCallback(LocationTrackingForegroundService service)
        {
            _serviceReference = new WeakReference<LocationTrackingForegroundService>(service);
        }

        public override void OnLocationResult(LocationResult result)
        {
            base.OnLocationResult(result);

            if (!_serviceReference.TryGetTarget(out var service))
            {
                return;
            }

            var latestLocation = result.LastLocation;
            if (latestLocation is null)
            {
                return;
            }

            _ = service.HandleLocationUpdateAsync(latestLocation.Latitude, latestLocation.Longitude);
        }
    }
}
#endif
