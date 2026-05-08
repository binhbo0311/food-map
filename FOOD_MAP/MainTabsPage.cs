namespace FOOD_MAP;

public sealed class MainTabsPage : Shell
{
    public MainTabsPage()
    {
        Title = "FOOD_MAP";
        FlyoutBehavior = FlyoutBehavior.Disabled;
        BackgroundColor = Color.FromArgb("#EAF8F8");

        Routing.RegisterRoute(nameof(TourPage), typeof(TourPage));
        Routing.RegisterRoute(nameof(TourBuilderPage), typeof(TourBuilderPage));

        Items.Add(new TabBar
        {
            Items =
            {
                CreateTab("Home", "home.png", () => new HomePage()),
                CreateTab("Map", "mapicon.png", () => new MainPage()),
                CreateTab("POI", "pinicon.png", () => new PoiPage()),
                CreateTab("Camera", "qrscannericon.png", () => new CameraPage()),
                CreateTab("Settings", "settingicon.png", () => new SettingsPage())
            }
        });
    }

    private static ShellContent CreateTab(string title, string icon, Func<Page> pageFactory)
    {
        return new ShellContent
        {
            Title = title,
            Icon = icon,
            ContentTemplate = new DataTemplate(() =>
            {
                var page = pageFactory();
                Shell.SetNavBarIsVisible(page, false);
                return page;
            })
        };
    }
}
