using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FOOD_MAP.Services;

namespace FOOD_MAP.ViewModels;

public sealed class RegisterViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;

    private string _userName = string.Empty;
    private string _displayName = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = "Tạo tài khoản mới để đồng bộ dữ liệu cá nhân.";
    private bool _isBusy;

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
        RegisterCommand = new Command(async () => await RegisterAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RegisterCompleted;

    public ICommand RegisterCommand { get; }

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

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName == value)
            {
                return;
            }

            _displayName = value;
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

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (_confirmPassword == value)
            {
                return;
            }

            _confirmPassword = value;
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
            (RegisterCommand as Command)?.ChangeCanExecute();
        }
    }

    private async Task RegisterAsync()
    {
        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            StatusMessage = "Xác nhận mật khẩu chưa khớp.";
            return;
        }

        IsBusy = true;
        try
        {
            var (isSuccess, message) = await _authService.RegisterAsync(UserName, DisplayName, Password);
            StatusMessage = message;

            if (!isSuccess)
            {
                return;
            }

            // Xóa dữ liệu nhạy cảm khi đăng ký thành công.
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            RegisterCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            StatusMessage = "Đã xảy ra lỗi khi đăng ký tài khoản. Vui lòng thử lại.";
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
