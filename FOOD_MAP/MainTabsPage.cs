using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace FOOD_MAP;

public sealed class MainTabsPage : Shell
{
    private readonly CameraPage _cameraPage;
    private readonly Services.DeepLinkRoutingService _deepLinkRoutingService;
    private bool _isHandlingDeepLink;

    public MainTabsPage()
    {
        Title = "FOOD_MAP";
        FlyoutBehavior = FlyoutBehavior.Disabled;
        BackgroundColor = Color.FromArgb("#EAF8F8");
        _deepLinkRoutingService = IPlatformApplication.Current?.Services.GetRequiredService<Services.DeepLinkRoutingService>()
            ?? throw new InvalidOperationException("Service provider is not available.");
        _cameraPage = new CameraPage();

        Routing.RegisterRoute(nameof(TourPage), typeof(TourPage));
        Routing.RegisterRoute(nameof(TourBuilderPage), typeof(TourBuilderPage));

        Items.Add(new TabBar
        {
            Items =
            {
                CreateTab("Home", "home.png", "home", () => new HomePage()),
                CreateTab("Map", "mapicon.png", "map", () => new MainPage()),
                CreateTab("POI", "pinicon.png", "poi", () => new PoiPage()),
                CreateTab("Camera", "qrscannericon.png", "camera", () => _cameraPage),
                CreateTab("Settings", "settingicon.png", "settings", () => new SettingsPage())
            }
        });

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _deepLinkRoutingService.DeepLinkPayloadReceived += OnDeepLinkPayloadReceived;
    }

    private static ShellContent CreateTab(string title, string icon, string route, Func<Page> pageFactory)
    {
        return new ShellContent
        {
            Title = title,
            Icon = icon,
            Route = route,
            ContentTemplate = new DataTemplate(() =>
            {
                var page = pageFactory();
                Shell.SetNavBarIsVisible(page, false);
                return page;
            })
        };
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await ConsumePendingDeepLinkAsync();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _deepLinkRoutingService.DeepLinkPayloadReceived -= OnDeepLinkPayloadReceived;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private async void OnDeepLinkPayloadReceived(object? sender, Services.DeepLinkPayloadReceivedEventArgs e)
    {
        await ConsumePendingDeepLinkAsync(e.Payload);
    }

    private async Task ConsumePendingDeepLinkAsync(string? externalPayload = null)
    {
        if (_isHandlingDeepLink)
        {
            return;
        }

        var pendingPayload = string.IsNullOrWhiteSpace(externalPayload)
            ? _deepLinkRoutingService.TryDequeuePendingPayload()
            : externalPayload;

        if (string.IsNullOrWhiteSpace(pendingPayload))
        {
            return;
        }

        _isHandlingDeepLink = true;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await GoToAsync("//camera");
                await _cameraPage.HandleExternalPayloadAsync(pendingPayload);
            });
        }
        finally
        {
            _isHandlingDeepLink = false;
        }
    }
}
