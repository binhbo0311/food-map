using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FOOD_MAP.Services;

namespace FOOD_MAP.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IUserProfileService _userProfileService;
    private readonly IUserSessionService _userSessionService;

    private string _userName = string.Empty;
    private string _displayName = string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmNewPassword = string.Empty;
    private string _statusMessage = "Load your profile to start editing.";
    private bool _isBusy;
    private bool _canEditSettings;

    public SettingsViewModel(IUserProfileService userProfileService, IUserSessionService userSessionService)
    {
        _userProfileService = userProfileService;
        _userSessionService = userSessionService;

        SaveProfileCommand = new Command(async () => await SaveProfileAsync(), () => CanEditSettings && !IsBusy);
        ChangePasswordCommand = new Command(async () => await ChangePasswordAsync(), () => CanEditSettings && !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SaveProfileCommand { get; }

    public ICommand ChangePasswordCommand { get; }

    public string UserName
    {
        get => _userName;
        private set
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

    public string CurrentPassword
    {
        get => _currentPassword;
        set
        {
            if (_currentPassword == value)
            {
                return;
            }

            _currentPassword = value;
            OnPropertyChanged();
        }
    }

    public string NewPassword
    {
        get => _newPassword;
        set
        {
            if (_newPassword == value)
            {
                return;
            }

            _newPassword = value;
            OnPropertyChanged();
        }
    }

    public string ConfirmNewPassword
    {
        get => _confirmNewPassword;
        set
        {
            if (_confirmNewPassword == value)
            {
                return;
            }

            _confirmNewPassword = value;
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
            RefreshCommandStates();
        }
    }

    public bool CanEditSettings
    {
        get => _canEditSettings;
        private set
        {
            if (_canEditSettings == value)
            {
                return;
            }

            _canEditSettings = value;
            OnPropertyChanged();
            RefreshCommandStates();
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _userSessionService.CurrentUserId;
        if (_userSessionService.IsGuest || !currentUserId.HasValue)
        {
            CanEditSettings = false;
            StatusMessage = "Guest mode cannot edit profile settings.";
            return;
        }

        IsBusy = true;
        try
        {
            var profile = await _userProfileService.GetProfileAsync(currentUserId.Value, cancellationToken);
            if (profile is null)
            {
                CanEditSettings = false;
                StatusMessage = "Unable to load profile information.";
                return;
            }

            UserName = profile.UserName;
            DisplayName = profile.DisplayName;
            CanEditSettings = true;
            StatusMessage = "Profile loaded. You can update your information.";
        }
        catch
        {
            CanEditSettings = false;
            StatusMessage = "An unexpected error occurred while loading profile.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveProfileAsync()
    {
        var currentUserId = _userSessionService.CurrentUserId;
        if (!CanEditSettings || !currentUserId.HasValue)
        {
            StatusMessage = "Profile updates are not available in guest mode.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _userProfileService.UpdateDisplayNameAsync(currentUserId.Value, DisplayName);
            StatusMessage = result.Message;

            if (result.IsSuccess)
            {
                // Cập nhật lại session để các màn hình khác hiển thị đúng tên mới.
                _userSessionService.SignIn(currentUserId.Value, DisplayName);
            }
        }
        catch
        {
            StatusMessage = "An unexpected error occurred while saving profile.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ChangePasswordAsync()
    {
        var currentUserId = _userSessionService.CurrentUserId;
        if (!CanEditSettings || !currentUserId.HasValue)
        {
            StatusMessage = "Password change is not available in guest mode.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
        {
            StatusMessage = "New password and confirmation do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _userProfileService.ChangePasswordAsync(currentUserId.Value, CurrentPassword, NewPassword);
            StatusMessage = result.Message;

            if (result.IsSuccess)
            {
                // Xóa dữ liệu nhạy cảm khỏi ô nhập sau khi đổi mật khẩu thành công.
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmNewPassword = string.Empty;
            }
        }
        catch
        {
            StatusMessage = "An unexpected error occurred while changing password.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCommandStates()
    {
        (SaveProfileCommand as Command)?.ChangeCanExecute();
        (ChangePasswordCommand as Command)?.ChangeCanExecute();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
