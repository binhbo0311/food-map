using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _viewModel = services.GetRequiredService<LoginViewModel>();
        _viewModel.LoginCompleted += OnLoginCompleted;
        BindingContext = _viewModel;
    }

    private async void OnLoginCompleted(object? sender, EventArgs e)
    {
        try
        {
            // Sau khi xác thực (hoặc vào guest), chuyển root sang tab dưới để điều hướng nhanh.
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page = new MainTabsPage();
                return;
            }

            // Fallback khi runtime không có window khả dụng.
            await Navigation.PushAsync(new MainTabsPage());
        }
        catch
        {
            await DisplayAlertAsync("Login", "Không thể chuyển sang màn hình chính lúc này.", "OK");
        }
    }

    private async void OnCreateAccountClicked(object? sender, EventArgs e)
    {
        try
        {
            // Điều hướng sang màn hình đăng ký để tạo tài khoản mới.
            await Navigation.PushAsync(new RegisterPage());
        }
        catch
        {
            await DisplayAlertAsync("Login", "Không thể mở trang đăng ký lúc này.", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.LoginCompleted -= OnLoginCompleted;
    }
}
