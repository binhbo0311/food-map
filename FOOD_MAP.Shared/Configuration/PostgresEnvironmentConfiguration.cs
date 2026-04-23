using Npgsql;

namespace FOOD_MAP.Shared.Configuration;

public static class PostgresEnvironmentConfiguration
{
    public static void LoadDotEnvIfExists()
    {
        // Tìm file .env từ nhiều điểm vào để dùng chung cho MAUI và Web.
        foreach (var directory in EnumerateCandidateDirectories())
        {
            var directDotEnvPath = Path.Combine(directory, ".env");
            if (File.Exists(directDotEnvPath))
            {
                ApplyDotEnv(directDotEnvPath);
                return;
            }

            var nestedAppDotEnvPath = Path.Combine(directory, "FOOD_MAP", ".env");
            if (File.Exists(nestedAppDotEnvPath))
            {
                ApplyDotEnv(nestedAppDotEnvPath);
                return;
            }
        }
    }

    public static string BuildPostgresConnectionString(bool normalizeAndroidLoopback = false)
    {
        var explicitConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return NormalizeConnectionString(explicitConnectionString, normalizeAndroidLoopback);
        }

        var host = ResolvePostgresHost(normalizeAndroidLoopback);
        var port = GetEnvOrDefault("POSTGRES_PORT", "5432");
        var database = ResolvePostgresDatabaseName();
        var username = GetEnvOrDefault("POSTGRES_USER", "postgres");
        var password = ResolvePostgresPassword(normalizeAndroidLoopback);

        var sslMode = ResolvePostgresSslMode();
        return $"Host={host};Port={port};Database={database};Username={username};Password={password};Timeout=4;Command Timeout=6;Pooling=true;SSL Mode={sslMode}";
    }

    public static string GetEnvOrDefault(string key, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string ResolvePostgresDatabaseName()
    {
        var preferredName = Environment.GetEnvironmentVariable("POSTGRES_DATABASE");
        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            return preferredName;
        }

        // Hỗ trợ thêm khóa POSTGRES_DB để tương thích cấu hình cũ.
        return GetEnvOrDefault("POSTGRES_DB", "food_map");
    }

    private static string ResolvePostgresHost(bool normalizeAndroidLoopback)
    {
        var configuredHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
        if (string.IsNullOrWhiteSpace(configuredHost))
        {
            if (IsDevelopmentEnvironment())
            {
                return "localhost";
            }

            throw new InvalidOperationException("POSTGRES_HOST is required outside Development. Configure a reachable remote PostgreSQL host.");
        }

        var normalizedHost = configuredHost.Trim();

        if (!normalizeAndroidLoopback && !IsDevelopmentEnvironment() && IsLoopbackHost(normalizedHost))
        {
            throw new InvalidOperationException("Loopback PostgreSQL hosts are not allowed outside Development.");
        }

        if (!normalizeAndroidLoopback || !IsLoopbackHost(normalizedHost))
        {
            return normalizedHost;
        }

        var androidHost = Environment.GetEnvironmentVariable("POSTGRES_HOST_ANDROID");
        return string.IsNullOrWhiteSpace(androidHost) ? "10.0.2.2" : androidHost.Trim();
    }

    private static string ResolvePostgresPassword(bool normalizeAndroidLoopback)
    {
        var sharedPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        if (!string.IsNullOrWhiteSpace(sharedPassword))
        {
            return sharedPassword;
        }

        if (normalizeAndroidLoopback)
        {
            var androidPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD_ANDROID");
            if (!string.IsNullOrWhiteSpace(androidPassword))
            {
                return androidPassword;
            }

            return "123456";
        }

        return "postgres";
    }

    private static string NormalizeConnectionString(string rawConnectionString, bool normalizeAndroidLoopback)
    {
        var builder = new NpgsqlConnectionStringBuilder(rawConnectionString)
        {
            Timeout = 4,
            CommandTimeout = 6,
            Pooling = true
        };

        builder.SslMode = ResolvePostgresSslMode();

        if (normalizeAndroidLoopback && IsLoopbackHost(builder.Host))
        {
            // Chuyển localhost về host phù hợp khi chạy trên Android emulator.
            builder.Host = GetEnvOrDefault("POSTGRES_HOST_ANDROID", "10.0.2.2");
        }

        return builder.ConnectionString;
    }

    private static SslMode ResolvePostgresSslMode()
    {
        // Moi truong local/dev thuong dung PostgreSQL khong bat SSL.
        // Ep Disable trong Development de tranh loi "SSL connection requested" do bien moi truong he thong gay ra.
        if (IsDevelopmentEnvironment())
        {
            return SslMode.Disable;
        }

        var rawSslMode = GetEnvOrDefault("POSTGRES_SSL_MODE", "Disable");
        return Enum.TryParse<SslMode>(rawSslMode, ignoreCase: true, out var parsedSslMode)
            ? parsedSslMode
            : SslMode.Disable;
    }


    private static bool IsLoopbackHost(string? host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDevelopmentEnvironment()
    {
        var aspnetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.Equals(aspnetEnvironment, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dotnetEnvironment, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateCandidateDirectories()
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rootPath in GetRootPaths())
        {
            var directory = new DirectoryInfo(rootPath);
            while (directory is not null)
            {
                if (visited.Add(directory.FullName))
                {
                    yield return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
    }

    private static IEnumerable<string> GetRootPaths()
    {
        var appBaseDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(appBaseDirectory))
        {
            yield return appBaseDirectory;
        }

        var currentDirectory = Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            yield return currentDirectory;
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

            // Không ghi đè biến môi trường đã được cấu hình từ bên ngoài.
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
