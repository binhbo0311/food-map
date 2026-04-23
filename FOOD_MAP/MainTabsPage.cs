namespace FOOD_MAP;

public sealed class MainTabsPage : TabbedPage
{
    public MainTabsPage()
    {
        Title = "FOOD_MAP";
        BarBackgroundColor = Color.FromArgb("#066D72");
        BarTextColor = Colors.White;
        SelectedTabColor = Colors.White;
        UnselectedTabColor = Color.FromArgb("#BFE6E7");

        // Tách các luồng chính thành tab dưới để người dùng chuyển nhanh giữa bản đồ, Home, POI, Camera và Settings.
        Children.Add(new NavigationPage(new HomePage())
        {
            Title = "Home"
        });

        Children.Add(new NavigationPage(new MainPage())
        {
            Title = "Map"
        });

        Children.Add(new NavigationPage(new PoiPage())
        {
            Title = "POI"
        });

        Children.Add(new NavigationPage(new CameraPage())
        {
            Title = "Camera"
        });

        Children.Add(new NavigationPage(new SettingsPage())
        {
            Title = "Settings"
        });
    }
}