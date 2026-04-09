using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FOOD_MAP.Services;

namespace FOOD_MAP.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;

    private string _userName = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = "Đăng nhập để đồng bộ favorites và tour history.";
    private bool _isBusy;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
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
        try
        {
            var (isSuccess, message) = await _authService.LoginAsync(UserName, Password);
            StatusMessage = message;

            if (isSuccess)
            {
                LoginCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            StatusMessage = "Đã xảy ra lỗi khi đăng nhập. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ContinueAsGuestAsync()
    {
        IsBusy = true;
        try
        {
            await _authService.ContinueAsGuestAsync();
            StatusMessage = "Đang sử dụng ở chế độ Guest.";
            LoginCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            StatusMessage = "Đã xảy ra lỗi khi tiếp tục dưới chế độ Guest. Vui lòng thử lại.";
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
