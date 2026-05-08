using System.Collections.ObjectModel;
using System.Windows.Input;
using FOOD_MAP.Services;
using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

public partial class PoiPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;
    private bool _isLoading;

    public PoiPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _viewModel = new MainPageViewModel(
            services.GetRequiredService<IPoiRepository>(),
            services.GetRequiredService<INarrationService>(),
            services.GetRequiredService<IDataService>(),
            services.GetRequiredService<IUserSessionService>(),
            services.GetRequiredService<IUserActivityRepository>());

        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadAsync(true));
    }

    public MainPageViewModel ViewModel => _viewModel;

    public ICommand RefreshCommand { get; }

    public bool IsRefreshing { get; private set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync(false);
    }

    private async Task LoadAsync(bool isRefresh)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        if (isRefresh)
        {
            IsRefreshing = true;
            OnPropertyChanged(nameof(IsRefreshing));
        }

        try
        {
            _viewModel.InvalidateData();
            await _viewModel.LoadPoisAsync();
        }
        catch
        {
        }
        finally
        {
            if (isRefresh)
            {
                IsRefreshing = false;
                OnPropertyChanged(nameof(IsRefreshing));
            }

            _isLoading = false;
        }
    }

    private async void OnPoiSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PoiListItemViewModel poiItem)
        {
            return;
        }

        await _viewModel.OnPoiSelectedAsync(poiItem);
        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }
    }
}