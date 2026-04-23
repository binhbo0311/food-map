using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP
{
    public partial class App : Application
    {
        private Services.IActiveMobileHeartbeatAgent? _activeMobileHeartbeatAgent;

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Khoi dong tu LoginPage de ho tro che do Guest/User ngay tu dau.
            var window = new Window(new NavigationPage(new LoginPage())) { Title = "FOOD_MAP" };

            // Theo doi heartbeat nguoi dung theo vong doi cua cua so app.
            _activeMobileHeartbeatAgent = IPlatformApplication.Current?.Services.GetService<Services.IActiveMobileHeartbeatAgent>();
            _activeMobileHeartbeatAgent?.Start();

            window.Stopped += (_, _) => _activeMobileHeartbeatAgent?.Stop();
            window.Resumed += (_, _) => _activeMobileHeartbeatAgent?.Start();
            window.Destroying += (_, _) => _activeMobileHeartbeatAgent?.Stop();

            return window;
        }
    }
}
