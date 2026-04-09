using FOOD_MAP.Services;
using FOOD_MAP.Shared.Services;
using FOOD_MAP.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FOOD_MAP
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Nạp biến môi trường từ file .env nếu có để dễ cấu hình kết nối PostgreSQL.
            LoadDotEnvIfExists();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiMaps()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Đăng ký các service phụ thuộc thiết bị được dùng bởi dự án shared.
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<IDataService, DataService>();

            builder.Services.AddSingleton<IPoiRepository, PoiRepository>();
            builder.Services.AddSingleton<INarrationService, NarrationService>();
            builder.Services.AddSingleton<IUserSessionService, UserSessionService>();
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<IUserProfileService, UserProfileService>();
            builder.Services.AddSingleton<IUserActivityRepository, UserActivityRepository>();
            builder.Services.AddSingleton<ViewModels.LoginViewModel>();
            builder.Services.AddTransient<ViewModels.RegisterViewModel>();
            builder.Services.AddTransient<ViewModels.SettingsViewModel>();

#if ANDROID
            builder.Services.AddSingleton<ILocationService, AndroidLocationService>();
#else
            builder.Services.AddSingleton<ILocationService, UnsupportedLocationService>();
#endif

            // Ưu tiên đọc connection string tổng, nếu không có thì ghép từ các biến POSTGRES_*.
            var postgresConnectionString = BuildPostgresConnectionString();
            builder.Services.AddDbContextFactory<AppDbContext>(options =>
                options.UseNpgsql(postgresConnectionString));

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        private static string BuildPostgresConnectionString()
        {
            var explicitConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (!string.IsNullOrWhiteSpace(explicitConnectionString))
            {
                // Chuan hoa host theo nen tang de tranh loi localhost khi chay tren Android emulator.
                return NormalizeConnectionStringForPlatform(explicitConnectionString);
            }

            var host = ResolvePostgresHost();
            var port = GetEnvOrDefault("POSTGRES_PORT", "5432");
            var database = GetEnvOrDefault("POSTGRES_DATABASE", "food_map");
            var username = GetEnvOrDefault("POSTGRES_USER", "postgres");
            var password = ResolvePostgresPassword();

            // Dat timeout ngan de app khong bi treo lau neu khong ket noi duoc PostgreSQL.
            return $"Host={host};Port={port};Database={database};Username={username};Password={password};Timeout=4;Command Timeout=6;Pooling=true";
        }

        private static string ResolvePostgresHost()
        {
            var configuredHost = GetEnvOrDefault("POSTGRES_HOST", "localhost");

#if ANDROID
            // Android emulator: localhost tro den chinh emulator, khong phai may dev.
            if (IsLoopbackHost(configuredHost))
            {
                return GetEnvOrDefault("POSTGRES_HOST_ANDROID", "10.0.2.2");
            }
#endif

            return configuredHost;
        }

        private static string NormalizeConnectionStringForPlatform(string connectionString)
        {
#if ANDROID
            var builder = new NpgsqlConnectionStringBuilder(connectionString);

            if (IsLoopbackHost(builder.Host))
            {
                // Tu dong doi localhost sang host hop le cho Android emulator.
                builder.Host = GetEnvOrDefault("POSTGRES_HOST_ANDROID", "10.0.2.2");
            }

            // Dam bao timeout ngan de tranh ANR khi emulator khong den duoc DB.
            builder.Timeout = 4;
            builder.CommandTimeout = 6;
            builder.Pooling = true;

            return builder.ConnectionString;
#else
            return connectionString;
#endif
        }

        private static string ResolvePostgresPassword()
        {
            var commonPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
            if (!string.IsNullOrWhiteSpace(commonPassword))
            {
                return commonPassword;
            }

#if ANDROID
            var androidPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD_ANDROID");
            if (!string.IsNullOrWhiteSpace(androidPassword))
            {
                return androidPassword;
            }

            // Fallback dev tren emulator de dong bo voi setup hien tai cua du an.
            return "123456";
#else
            return "postgres";
#endif
        }

        private static bool IsLoopbackHost(string? host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetEnvOrDefault(string key, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static void LoadDotEnvIfExists()
        {
            // Thử tìm .env từ thư mục hiện tại đi ngược lên để phù hợp khi chạy từ nhiều cwd khác nhau.
            var currentDirectory = AppContext.BaseDirectory;
            var directory = new DirectoryInfo(currentDirectory);

            while (directory is not null)
            {
                var dotEnvPath = Path.Combine(directory.FullName, ".env");
                if (File.Exists(dotEnvPath))
                {
                    ApplyDotEnv(dotEnvPath);
                    return;
                }

                directory = directory.Parent;
            }
        }

        private static void ApplyDotEnv(string filePath)
        {
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var equalIndex = line.IndexOf('=');
                if (equalIndex <= 0)
                {
                    continue;
                }

                var key = line[..equalIndex].Trim();
                var value = line[(equalIndex + 1)..].Trim().Trim('\'', '"');

                if (key.Length == 0)
                {
                    continue;
                }

                // Không ghi đè biến đã tồn tại để tránh làm mất cấu hình trên máy CI/CD.
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }

        private sealed class UnsupportedLocationService : ILocationService
        {
            public Task<bool> RequestLocationPermissionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

            public Task StartTrackingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task StopTrackingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<Location?> GetLastKnownLocationAsync(CancellationToken cancellationToken = default) => Task.FromResult<Location?>(null);
        }
    }
}
