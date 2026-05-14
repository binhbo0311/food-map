using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using FOOD_MAP.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static string? _startupDeepLinkPayload;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RouteIncomingDeepLink(Intent);
            ForwardStartupDeepLinkIfAvailable();
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            RouteIncomingDeepLink(intent);
            ForwardStartupDeepLinkIfAvailable();
        }

        private static void RouteIncomingDeepLink(Intent? intent)
        {
            if (!string.Equals(intent?.Action, Intent.ActionView, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var deepLinkPayload = intent.DataString;
            if (string.IsNullOrWhiteSpace(deepLinkPayload))
            {
                return;
            }

            var serviceProvider = IPlatformApplication.Current?.Services;
            var deepLinkRoutingService = serviceProvider?.GetService<DeepLinkRoutingService>();
            if (deepLinkRoutingService is null)
            {
                _startupDeepLinkPayload = deepLinkPayload;
                return;
            }

            deepLinkRoutingService.RegisterIncomingUri(deepLinkPayload);
            _startupDeepLinkPayload = null;
        }

        private static void ForwardStartupDeepLinkIfAvailable()
        {
            if (string.IsNullOrWhiteSpace(_startupDeepLinkPayload))
            {
                return;
            }

            var serviceProvider = IPlatformApplication.Current?.Services;
            var deepLinkRoutingService = serviceProvider?.GetService<DeepLinkRoutingService>();
            if (deepLinkRoutingService is null)
            {
                return;
            }

            deepLinkRoutingService.RegisterIncomingUri(_startupDeepLinkPayload);
            _startupDeepLinkPayload = null;
        }
    }
}
