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
        // Sau khi xác thực (hoặc vào guest), chuyển sang trang chính để bắt đầu tour.
        await Navigation.PushAsync(new MainPage());

        // Xóa trang login khỏi stack để tránh quay ngược về màn đăng nhập.
        if (Navigation.NavigationStack.FirstOrDefault() is Page firstPage && firstPage == this)
        {
            Navigation.RemovePage(this);
        }
    }

    private async void OnCreateAccountClicked(object? sender, EventArgs e)
    {
        // Điều hướng sang màn hình đăng ký để tạo tài khoản mới.
        await Navigation.PushAsync(new RegisterPage());
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.LoginCompleted -= OnLoginCompleted;
    }
}
