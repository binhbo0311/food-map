using FOOD_MAP.Services;
using FOOD_MAP.Shared.Configuration;
using FOOD_MAP.Shared.Services;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace FOOD_MAP
{
    public static class MauiProgram
    {
        private const string AndroidPhysicalFallbackApiHost = "192.168.1.8";
        private const string AndroidPhysicalFallbackApiPort = "5226";
        // Tên meta-data key trong AndroidManifest, được inject từ .env lúc build
        private const string AndroidManifestApiUrlMetaKey = "food_map.MOBILE_API_BASE_URL";

        public static MauiApp CreateMauiApp()
        {
            // Nạp biến môi trường từ file .env (hien tai uu tien USB deploy mode qua adb reverse).
            PostgresEnvironmentConfiguration.LoadDotEnvIfExists();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiMaps()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Đăng ký các service phụ thuộc thiết bị được dùng bởi dự án shared.
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<IDataService, ApiDataService>();
            builder.Services.AddSingleton<IPoiWorkflowRepository, ApiPoiWorkflowRepository>();

            var mobileApiBaseUrl = BuildMobileApiBaseUrl();
            builder.Services.AddSingleton(_ => new HttpClient
            {
                BaseAddress = new Uri(mobileApiBaseUrl),
                Timeout = TimeSpan.FromSeconds(20)
            });

            builder.Services.AddSingleton<IPoiRepository, ApiPoiRepository>();
            builder.Services.AddSingleton<MobilePoiCatalogService>();
            builder.Services.AddSingleton<INarrationService, NarrationService>();
            builder.Services.AddSingleton<IUserSessionService, UserSessionService>();
            builder.Services.AddSingleton<IAuthService, ApiAuthService>();
            builder.Services.AddSingleton<IUserProfileService, ApiUserProfileService>();
            builder.Services.AddSingleton<IUserActivityRepository, ApiUserActivityRepository>();
            builder.Services.AddSingleton<ISyncService, ApiSyncService>();
            builder.Services.AddSingleton<IActiveUserTrackerService, ApiActiveUserTrackerService>();
            builder.Services.AddSingleton<IActiveMobileHeartbeatAgent, ActiveMobileHeartbeatAgent>();
            builder.Services.AddSingleton<ViewModels.LoginViewModel>();
            builder.Services.AddTransient<ViewModels.RegisterViewModel>();
            builder.Services.AddTransient<ViewModels.SettingsViewModel>();

#if ANDROID
            builder.Services.AddSingleton<ILocationService, AndroidLocationService>();
#else
            builder.Services.AddSingleton<ILocationService, UnsupportedLocationService>();
#endif

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        private static string BuildMobileApiBaseUrl()
        {
            var explicitBaseUrl = Environment.GetEnvironmentVariable("MOBILE_API_BASE_URL");
            if (!string.IsNullOrWhiteSpace(explicitBaseUrl))
            {
                return EnsureValidMobileApiBaseUrl(explicitBaseUrl);
            }

#if ANDROID
            // Emulator luôn ưu tiên host 10.0.2.2 thay vì URL baked để tránh timeout do trỏ sai LAN IP.
            if (!IsAndroidEmulator())
            {
                // Khi APK deploy lên máy thật: không có file .env nên đọc URL đã được bake
                // vào AndroidManifest meta-data lúc build (inject từ MOBILE_API_BASE_URL trong .env).
                var bakedUrl = ReadAndroidManifestMetaString(AndroidManifestApiUrlMetaKey);
                if (!string.IsNullOrWhiteSpace(bakedUrl))
                {
                    return EnsureValidMobileApiBaseUrl(bakedUrl.Trim());
                }
            }
#endif
            var host = Environment.GetEnvironmentVariable("MOBILE_API_HOST");
#if ANDROID
            if (string.IsNullOrWhiteSpace(host) && IsAndroidEmulator())
            {
                host = "10.0.2.2";
            }
#endif
            if (string.IsNullOrWhiteSpace(host) && IsDevelopmentEnvironment())
            {
                host = "10.0.2.2";
            }

            if (string.IsNullOrWhiteSpace(host) && IsAndroidPlatform())
            {
                // Khi deploy lên máy thật, dùng sẵn IP LAN của máy chạy Web API để app vẫn khởi động được.
                host = Environment.GetEnvironmentVariable("MOBILE_API_HOST_ANDROID");
                if (string.IsNullOrWhiteSpace(host))
                {
                    host = AndroidPhysicalFallbackApiHost;
                }
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException("Missing MOBILE_API_BASE_URL or MOBILE_API_HOST. Configure a reachable API endpoint for physical deployment.");
            }

            var scheme = Environment.GetEnvironmentVariable("MOBILE_API_SCHEME");
            if (string.IsNullOrWhiteSpace(scheme))
            {
                scheme = IsDevelopmentEnvironment() || IsAndroidPlatform() ? "http" : "https";
            }

            var port = Environment.GetEnvironmentVariable("MOBILE_API_PORT");
            if (string.IsNullOrWhiteSpace(port) && IsAndroidPlatform())
            {
                port = AndroidPhysicalFallbackApiPort;
            }

            var baseUrl = string.IsNullOrWhiteSpace(port)
                ? $"{scheme}://{host}/"
                : $"{scheme}://{host}:{port}/";

            return EnsureValidMobileApiBaseUrl(baseUrl);
        }

        private static string EnsureValidMobileApiBaseUrl(string rawUrl)
        {
            var normalizedUrl = rawUrl.Trim().TrimEnd('/') + "/";
            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException("MOBILE_API_BASE_URL is not a valid absolute URL.");
            }

            var allowLoopback = string.Equals(
                Environment.GetEnvironmentVariable("ALLOW_LOCAL_LOOPBACK_ENDPOINTS"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (!allowLoopback && IsLoopbackHost(uri.Host))
            {
                throw new InvalidOperationException("Loopback API endpoints are blocked. Use a remote API host or set ALLOW_LOCAL_LOOPBACK_ENDPOINTS=true for local debugging.");
            }

            return normalizedUrl;
        }

        private static bool IsDevelopmentEnvironment()
        {
            var aspnetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            return string.Equals(aspnetEnvironment, "Development", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dotnetEnvironment, "Development", StringComparison.OrdinalIgnoreCase);
        }

            private static bool IsAndroidPlatform()
            {
        #if ANDROID
                return true;
        #else
                return false;
        #endif
            }

        private static bool IsLoopbackHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        }

#if ANDROID
        // Đọc giá trị string từ AndroidManifest <meta-data> qua ApplicationInfo.MetaData
        private static string? ReadAndroidManifestMetaString(string key)
        {
            try
            {
                var context = Android.App.Application.Context;
                var appInfo = context.PackageManager?.GetApplicationInfo(
                    context.PackageName!,
                    Android.Content.PM.PackageInfoFlags.MetaData);
                var value = appInfo?.MetaData?.GetString(key);
                // Placeholder chưa được inject sẽ có giá trị "${MOBILE_API_BASE_URL}" — bỏ qua
                if (value != null && value.StartsWith("${", StringComparison.Ordinal))
                {
                    return null;
                }
                return value;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAndroidEmulator()
        {
            try
            {
                var fingerprint = Android.OS.Build.Fingerprint ?? string.Empty;
                var model = Android.OS.Build.Model ?? string.Empty;
                var product = Android.OS.Build.Product ?? string.Empty;
                var manufacturer = Android.OS.Build.Manufacturer ?? string.Empty;
                var brand = Android.OS.Build.Brand ?? string.Empty;
                var device = Android.OS.Build.Device ?? string.Empty;

                return fingerprint.Contains("generic", StringComparison.OrdinalIgnoreCase)
                    || fingerprint.Contains("emulator", StringComparison.OrdinalIgnoreCase)
                    || model.Contains("Emulator", StringComparison.OrdinalIgnoreCase)
                    || model.Contains("Android SDK built for", StringComparison.OrdinalIgnoreCase)
                    || manufacturer.Contains("Genymotion", StringComparison.OrdinalIgnoreCase)
                    || brand.StartsWith("generic", StringComparison.OrdinalIgnoreCase)
                    || device.StartsWith("generic", StringComparison.OrdinalIgnoreCase)
                    || product.Contains("sdk", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
#endif

        private sealed class UnsupportedLocationService : ILocationService
        {
            public Task<bool> RequestLocationPermissionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

            public Task StartTrackingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task StopTrackingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<Location?> GetLastKnownLocationAsync(CancellationToken cancellationToken = default) => Task.FromResult<Location?>(null);
        }
    }
}
