using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Configuration;
using FOOD_MAP.Shared.Services;
using FOOD_MAP.Web.Components;
using FOOD_MAP.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Nạp biến môi trường từ file .env để dùng chung cấu hình PostgreSQL với ứng dụng MAUI.
PostgresEnvironmentConfiguration.LoadDotEnvIfExists();

// Đăng ký dịch vụ Blazor Server cho giao diện quản trị.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<PortalUserSessionState>();
builder.Services.AddScoped<PortalSessionService>();
builder.Services.AddSingleton<IActiveUserTrackerService, ActiveUserTrackerService>();

// Đăng ký dịch vụ phụ thuộc thiết bị được dùng bởi dự án dùng chung.
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<IPoiWorkflowRepository, PoiWorkflowRepository>();
builder.Services.AddSingleton<ISubscriptionService, SubscriptionService>();
builder.Services.AddSingleton<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri)
    };
});

// Liên kết chung cơ sở dữ liệu PostgreSQL để Web và App dùng cùng nguồn dữ liệu.
var postgresConnectionString = PostgresEnvironmentConfiguration.BuildPostgresConnectionString();
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(
        postgresConnectionString,
        npgsqlOptions =>
        {
            // Bật cơ chế retry để tự phục hồi các lỗi kết nối tạm thời từ PostgreSQL.
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        }));

var app = builder.Build();

// Cấu hình pipeline HTTP cho môi trường production.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // Mặc định HSTS bật 30 ngày; có thể thay đổi theo chính sách triển khai thực tế.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapStaticAssets();

app.MapMobileApiEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(FOOD_MAP.Shared._Imports).Assembly);

app.Run();
