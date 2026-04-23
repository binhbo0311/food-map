using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

public partial class RegisterPage : ContentPage
{
    private readonly RegisterViewModel _viewModel;

    public RegisterPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _viewModel = services.GetRequiredService<RegisterViewModel>();
        _viewModel.RegisterCompleted += OnRegisterCompleted;
        BindingContext = _viewModel;
    }

    private async void OnRegisterCompleted(object? sender, EventArgs e)
    {
        try
        {
            // Quay về trang đăng nhập ngay sau khi tạo tài khoản thành công.
            await DisplayAlertAsync("Register", "Tạo tài khoản thành công.", "OK");
            await Navigation.PopAsync();
        }
        catch
        {
            await DisplayAlertAsync("Register", "Không thể quay lại màn đăng nhập.", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.RegisterCompleted -= OnRegisterCompleted;
    }
}
