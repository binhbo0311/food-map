namespace FOOD_MAP
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Khoi dong tu LoginPage de ho tro che do Guest/User ngay tu dau.
            return new Window(new NavigationPage(new LoginPage())) { Title = "FOOD_MAP" };
        }
    }
}
