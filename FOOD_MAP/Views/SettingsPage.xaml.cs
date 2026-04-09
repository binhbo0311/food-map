using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _viewModel = services.GetRequiredService<SettingsViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Tải dữ liệu hồ sơ mỗi khi mở màn hình để đồng bộ trạng thái mới nhất.
        await _viewModel.LoadAsync();
    }
}
