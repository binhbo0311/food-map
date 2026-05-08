#if ANDROID
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Location;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using FOOD_MAP.Services.Geofencing;
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
        var semaphoreAcquired = false;

        try
        {
            var serviceProvider = IPlatformApplication.Current?.Services;
            if (serviceProvider is null)
            {
                return;
            }

            await _geoSemaphore.WaitAsync();
            semaphoreAcquired = true;

            var poiRepository = serviceProvider.GetService(typeof(IPoiRepository)) as IPoiRepository;
            if (poiRepository is null)
            {
                return;
            }

            var selectedLanguage = NormalizeLanguageCode(Preferences.Default.Get("selected_language", "vi"));
            var pois = await poiRepository.GetPoiItemsAsync(selectedLanguage);

            if (pois.Count == 0)
            {
                return;
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var nearestPoiText = "No nearby POI";
            double nearestDistance = double.MaxValue;

            // Collect tất cả POI vừa được trigger trong lần update này.
            // Sau vòng lặp sẽ enqueue một lần duy nhất theo priority order.
            var triggeredPois = new List<(string PoiId, int Priority)>();

            foreach (var poi in pois)
            {
                var activationRadius = poi.ActivationRadius > 0
                    ? poi.ActivationRadius
                    : ResolveActivationRadiusMeters(poi.DistanceText);

                var distanceMeters = GeofenceMath.CalculateDistanceMeters(
                    latitude,
                    longitude,
                    poi.Latitude,
                    poi.Longitude);

                if (distanceMeters < nearestDistance)
                {
                    nearestDistance = distanceMeters;
                    nearestPoiText = $"Nearest POI #{poi.PoiId} - {(int)distanceMeters}m";
                }

                var isInside = distanceMeters <= activationRadius;
                var previousInside = _isInsidePoiById.TryGetValue(poi.PoiId, out var cachedInside) && cachedInside;

                if (!isInside)
                {
                    _isInsidePoiById[poi.PoiId] = false;
                    _insideDebounceHitsByPoiId[poi.PoiId] = 0;
                    continue;
                }

                var previousHits = _insideDebounceHitsByPoiId.TryGetValue(poi.PoiId, out var hits) ? hits : 0;
                var currentHits = previousHits + 1;
                _insideDebounceHitsByPoiId[poi.PoiId] = currentHits;

                var isDebouncedReady = currentHits >= DebounceHitsRequired;
                var hasCooldown = _lastTriggeredUtcByPoiId.TryGetValue(poi.PoiId, out var lastTriggeredUtc) &&
                                  nowUtc - lastTriggeredUtc < TriggerCooldown;

                if (!previousInside && isDebouncedReady && !hasCooldown)
                {
                    _isInsidePoiById[poi.PoiId] = true;
                    _lastTriggeredUtcByPoiId[poi.PoiId] = nowUtc;

                    // Thêm vào batch để enqueue cùng lúc sau vòng lặp (có priority sort).
                    triggeredPois.Add((poi.PoiId, poi.Priority));

                    var triggerText = $"Triggered POI #{poi.PoiId} (P{poi.Priority}) at {(int)distanceMeters}m";
                    var notificationManager = NotificationManagerCompat.From(this);
                    if (notificationManager is not null)
                    {
                        // Dùng ID riêng để thông báo geofence không bị ghi đè ngay bởi tracking notification.
                        var triggerNotificationId = TriggerNotificationBaseId + Math.Abs(poi.PoiId.GetHashCode(StringComparison.Ordinal) % 1000);
                        notificationManager.Notify(triggerNotificationId, BuildTriggerNotification(triggerText));
                    }
                }
                else
                {
                    _isInsidePoiById[poi.PoiId] = true;
                }
            }

            // Enqueue một lần duy nhất: NarrationService sẽ sort theo priority và phát tuần tự.
            if (triggeredPois.Count > 0)
            {
                _ = TriggerProximityBatchAsync(serviceProvider, triggeredPois);
            }

            var trackingManager = NotificationManagerCompat.From(this);
            if (trackingManager is not null)
            {
                trackingManager.Notify(TrackingNotificationId, BuildTrackingNotification(nearestPoiText));
            }
        }
        catch (Exception)
        {
            // Bắt mọi lỗi của callback nền để không đẩy exception ra Java proxy trên Android.
        }
        finally
        {
            if (semaphoreAcquired)
            {
                _geoSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Resolve TtsScript cho nhiều POI cùng lúc rồi enqueue vào NarrationService theo priority.
    /// POI có priority cao hơn sẽ được phát trước.
    /// </summary>
    private static async Task TriggerProximityBatchAsync(
        IServiceProvider serviceProvider,
        IReadOnlyList<(string PoiId, int Priority)> triggeredPois)
    {
        try
        {
            var selectedLanguage = NormalizeLanguageCode(Preferences.Default.Get("selected_language", "vi"));
            var narrationService = serviceProvider.GetService(typeof(INarrationService)) as INarrationService;
            var poiRepository = serviceProvider.GetService(typeof(IPoiRepository)) as IPoiRepository;

            if (narrationService is null || poiRepository is null)
            {
                return;
            }

            // Fetch TtsScript cho từng POI, giữ nguyên priority để sort sau.
            var pendingItems = new List<PendingProximityItem>();

            foreach (var (poiId, priority) in triggeredPois)
            {
                try
                {
                    var scanResult = await poiRepository.GetPoiScanResultAsync(poiId, selectedLanguage);
                    if (scanResult is null)
                    {
                        continue;
                    }

                    // Proximity ưu tiên script đã biên tập để giữ đúng dấu tiếng Việt và ngữ điệu.
                    var narrationText = !string.IsNullOrWhiteSpace(scanResult.TtsScript)
                        ? scanResult.TtsScript
                        : BuildFallbackIntro(scanResult.LocationName, selectedLanguage);

                    pendingItems.Add(new PendingProximityItem(poiId, priority, narrationText, selectedLanguage));
                }
                catch
                {
                    // Bỏ qua POI lỗi, tiếp tục với POI tiếp theo.
                }
            }

            if (pendingItems.Count == 0)
            {
                return;
            }

            // NarrationService sẽ sort lại theo priority trước khi phát.
            await narrationService.EnqueueProximityBatchAsync(pendingItems);
        }
        catch
        {
            // Nếu batch narration lỗi thì bỏ qua để không ảnh hưởng luồng tracking.
        }
    }

    private static string BuildFallbackIntro(string? locationName, string languageCode)
    {
        if (string.IsNullOrWhiteSpace(locationName))
        {
            return string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase)
                ? "You are near a point of interest."
                : "Bạn đang đến gần một địa điểm tham quan.";
        }

        return string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase)
            ? $"You are near {locationName}."
            : $"Bạn đang đến gần {locationName}.";
    }

    private static int ResolveActivationRadiusMeters(string distanceText)
    {
        if (string.IsNullOrWhiteSpace(distanceText))
        {
            return 100;
        }

        var digits = new string(distanceText.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return 100;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var normalized = languageCode.Trim().ToLowerInvariant();

        if (normalized.StartsWith("vi", StringComparison.Ordinal) || normalized.Contains("viet", StringComparison.Ordinal))
        {
            return "vi";
        }

        if (normalized.StartsWith("en", StringComparison.Ordinal) || normalized.Contains("english", StringComparison.Ordinal))
        {
            return "en";
        }

        return normalized;
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

            try
            {
                _ = service.HandleLocationUpdateAsync(latestLocation.Latitude, latestLocation.Longitude);
            }
            catch
            {
                // Nuốt lỗi sync rất sớm từ callback Java để tránh crash proxy.
            }
        }
    }
}
#endif
