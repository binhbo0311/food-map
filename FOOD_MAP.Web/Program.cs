using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Services;
using FOOD_MAP.Web.Components;
using FOOD_MAP.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Nạp biến môi trường từ file .env để dùng chung cấu hình PostgreSQL với ứng dụng MAUI.
LoadDotEnvIfExists();

// Đăng ký dịch vụ Blazor Server cho giao diện quản trị.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Đăng ký dịch vụ phụ thuộc thiết bị được dùng bởi dự án dùng chung.
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<IPoiWorkflowRepository, PoiWorkflowRepository>();

// Liên kết chung cơ sở dữ liệu PostgreSQL để Web và App dùng cùng nguồn dữ liệu.
var postgresConnectionString = BuildPostgresConnectionString();
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

var app = builder.Build();

// Cấu hình pipeline HTTP cho môi trường production.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // Mặc định HSTS bật 30 ngày; có thể thay đổi theo chính sách triển khai thực tế.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(FOOD_MAP.Shared._Imports).Assembly);

app.Run();

static string BuildPostgresConnectionString()
{
    var explicitConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    if (!string.IsNullOrWhiteSpace(explicitConnectionString))
    {
        return explicitConnectionString;
    }

    var host = GetEnvOrDefault("POSTGRES_HOST", "localhost");
    var port = GetEnvOrDefault("POSTGRES_PORT", "5432");
    var database = GetEnvOrDefault("POSTGRES_DATABASE", "food_map");
    var username = GetEnvOrDefault("POSTGRES_USER", "postgres");
    var password = GetEnvOrDefault("POSTGRES_PASSWORD", "postgres");

    // Thiết lập timeout ngắn để tránh treo request khi database không khả dụng.
    return $"Host={host};Port={port};Database={database};Username={username};Password={password};Timeout=4;Command Timeout=6;Pooling=true";
}

static string GetEnvOrDefault(string key, string defaultValue)
{
    var value = Environment.GetEnvironmentVariable(key);
    return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
}

static void LoadDotEnvIfExists()
{
    // Dò tìm .env từ thư mục chạy hiện tại đi ngược lên để phù hợp nhiều cấu trúc chạy khác nhau.
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

static void ApplyDotEnv(string filePath)
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

        // Không ghi đè biến môi trường đã có để giữ cấu hình riêng của máy chạy.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
