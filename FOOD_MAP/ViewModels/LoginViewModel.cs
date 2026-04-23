using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FOOD_MAP.Services;

namespace FOOD_MAP.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;
    private readonly string _apiBaseUrl;

    private string _userName = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage;
    private bool _isBusy;

    public LoginViewModel(IAuthService authService, HttpClient httpClient)
    {
        _authService = authService;
        // Hiện rõ URL đang dùng để debug kết nối trên thiết bị thật
        _apiBaseUrl = httpClient.BaseAddress?.ToString() ?? "(chưa cấu hình)";
        _statusMessage = $"Server: {_apiBaseUrl}";
        LoginCommand = new Command(async () => await LoginAsync(), () => !IsBusy);
        ContinueAsGuestCommand = new Command(async () => await ContinueAsGuestAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LoginCompleted;

    public ICommand LoginCommand { get; }

    public ICommand ContinueAsGuestCommand { get; }

    public string UserName
    {
        get => _userName;
        set
        {
            if (_userName == value)
            {
                return;
            }

            _userName = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password == value)
            {
                return;
            }

            _password = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            (LoginCommand as Command)?.ChangeCanExecute();
            (ContinueAsGuestCommand as Command)?.ChangeCanExecute();
        }
    }

    private async Task LoginAsync()
    {
        IsBusy = true;
        StatusMessage = "Đang đăng nhập...";
        try
        {
            var (isSuccess, message) = await _authService.LoginAsync(UserName, Password);
            StatusMessage = message;

            if (isSuccess)
            {
                LoginCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (HttpRequestException ex)
        {
            // Lỗi kết nối mạng: server chưa chạy hoặc sai IP/port
            StatusMessage = $"Không kết nối được server. Kiểm tra WiFi và server đang chạy.\n({ex.Message})";
        }
        catch (TaskCanceledException)
        {
            // Timeout sau 20 giây
            StatusMessage = "Kết nối tới server bị timeout (20s). Kiểm tra IP server trong .env.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi: {ex.GetType().Name} - {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ContinueAsGuestAsync()
    {
        IsBusy = true;
        StatusMessage = "Đang vào chế độ Guest...";
        try
        {
            await _authService.ContinueAsGuestAsync();
            StatusMessage = "Đang sử dụng ở chế độ Guest.";
            LoginCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Không kết nối được server.\n({ex.Message})";
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Kết nối tới server bị timeout. Kiểm tra IP server trong .env.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi: {ex.GetType().Name} - {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
