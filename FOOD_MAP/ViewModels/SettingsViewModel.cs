using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FOOD_MAP.Services;
using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IUserProfileService _userProfileService;
    private readonly IUserSessionService _userSessionService;
    private readonly FOOD_MAP.Shared.Services.IPoiWorkflowRepository _poiWorkflowRepository;

    private string _userName = string.Empty;
    private string _displayName = string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmNewPassword = string.Empty;
    private string _businessName = string.Empty;
    private string _businessAddress = string.Empty;
    private string _contactPhone = string.Empty;
    private string _ownerRegistrationNote = string.Empty;
    private string _ownerRegistrationActionText = "Submit Owner Registration";
    private string _statusMessage = "Load your profile to start editing.";
    private bool _isBusy;
    private bool _canEditSettings;
    private bool _hasPendingOwnerRequest;
    private OwnerRegistrationStatus? _latestOwnerRequestStatus;
    private UserRole _currentUserRole = UserRole.User;
    private bool _isOwnerUser;

    private string? _editingOwnerPoiId;
    private PoiType _ownerPoiType = PoiType.Food;
    private string _ownerPoiLatitudeText = string.Empty;
    private string _ownerPoiLongitudeText = string.Empty;
    private string _ownerPoiActivationRadiusText = "100";
    private string _ownerPoiPriorityText = "1";
    private string _ownerPoiQrCodeId = string.Empty;
    private string _ownerPoiBaseLanguageCode = "vi";
    private string _ownerPoiName = string.Empty;
    private string _ownerPoiDescription = string.Empty;
    private string _ownerPoiImageUrl = string.Empty;
    private string _ownerPoiAudioFileUrl = string.Empty;
    private string _ownerPoiTtsScript = string.Empty;
    private string _ownerPoiActionText = "Submit Owner POI";

    private string _selectedOwnerPoiIdForFood = string.Empty;
    private int? _editingFoodItemId;
    private string _foodItemName = string.Empty;
    private string _foodItemDescription = string.Empty;
    private string _foodItemPriceText = "0";
    private string _foodItemCurrency = "VND";
    private bool _foodItemIsAvailable = true;
    private string _foodItemDisplayOrderText = "1";
    private string _foodItemActionText = "Add Food Item";

    public SettingsViewModel(
        IUserProfileService userProfileService,
        IUserSessionService userSessionService,
        FOOD_MAP.Shared.Services.IPoiWorkflowRepository poiWorkflowRepository)
    {
        _userProfileService = userProfileService;
        _userSessionService = userSessionService;
        _poiWorkflowRepository = poiWorkflowRepository;

        OwnerPoiItems = new ObservableCollection<OwnerPoiManagementItemViewModel>();
        OwnerFoodItems = new ObservableCollection<OwnerFoodItemEditorRowViewModel>();
        OwnerPoiTypes = new ObservableCollection<PoiType>
        {
            PoiType.Food,
            PoiType.Visit,
            PoiType.StayIn
        };

        SaveProfileCommand = new Command(async () => await SaveProfileAsync(), () => CanEditSettings && !IsBusy);
        ChangePasswordCommand = new Command(async () => await ChangePasswordAsync(), () => CanEditSettings && !IsBusy);
        RegisterOwnerCommand = new Command(async () => await RegisterOwnerAsync(), () => CanEditSettings && !IsBusy);
        CancelOwnerRequestCommand = new Command(async () => await CancelOwnerRequestAsync(), () => CanEditSettings && HasPendingOwnerRequest && !IsBusy);
        RefreshOwnerWorkspaceCommand = new Command(async () => await RefreshOwnerWorkspaceAsync(), () => CanEditSettings && IsOwnerUser && !IsBusy);
        SubmitOwnerPoiCommand = new Command(async () => await SubmitOrUpdateOwnerPoiAsync(), () => CanEditSettings && IsOwnerUser && !IsBusy);
        ResetOwnerPoiEditorCommand = new Command(ResetOwnerPoiEditor, () => CanEditSettings && IsOwnerUser && !IsBusy);
        SelectOwnerPoiForEditCommand = new Command<OwnerPoiManagementItemViewModel>(async item => await SelectOwnerPoiForEditAsync(item), _ => CanEditSettings && IsOwnerUser && !IsBusy);
        SelectOwnerPoiForFoodCommand = new Command<OwnerPoiManagementItemViewModel>(async item => await SelectOwnerPoiForFoodAsync(item), _ => CanEditSettings && IsOwnerUser && !IsBusy);
        SaveOwnerFoodItemCommand = new Command(async () => await SaveOwnerFoodItemAsync(), () => CanEditSettings && IsOwnerUser && !IsBusy && !string.IsNullOrWhiteSpace(SelectedOwnerPoiIdForFood));
        EditOwnerFoodItemCommand = new Command<OwnerFoodItemEditorRowViewModel>(EditOwnerFoodItem, _ => CanEditSettings && IsOwnerUser && !IsBusy);
        DeleteOwnerFoodItemCommand = new Command<OwnerFoodItemEditorRowViewModel>(async item => await DeleteOwnerFoodItemAsync(item), _ => CanEditSettings && IsOwnerUser && !IsBusy);
        ResetOwnerFoodItemEditorCommand = new Command(ResetOwnerFoodItemEditor, () => CanEditSettings && IsOwnerUser && !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SaveProfileCommand { get; }

    public ICommand ChangePasswordCommand { get; }

    public ICommand RegisterOwnerCommand { get; }

    public ICommand CancelOwnerRequestCommand { get; }

    public ICommand RefreshOwnerWorkspaceCommand { get; }

    public ICommand SubmitOwnerPoiCommand { get; }

    public ICommand ResetOwnerPoiEditorCommand { get; }

    public ICommand SelectOwnerPoiForEditCommand { get; }

    public ICommand SelectOwnerPoiForFoodCommand { get; }

    public ICommand SaveOwnerFoodItemCommand { get; }

    public ICommand EditOwnerFoodItemCommand { get; }

    public ICommand DeleteOwnerFoodItemCommand { get; }

    public ICommand ResetOwnerFoodItemEditorCommand { get; }

    public ObservableCollection<PoiType> OwnerPoiTypes { get; }

    public ObservableCollection<OwnerPoiManagementItemViewModel> OwnerPoiItems { get; }

    public ObservableCollection<OwnerFoodItemEditorRowViewModel> OwnerFoodItems { get; }

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

    public string BusinessName
    {
        get => _businessName;
        set
        {
            if (_businessName == value)
            {
                return;
            }

            _businessName = value;
            OnPropertyChanged();
        }
    }

    public string BusinessAddress
    {
        get => _businessAddress;
        set
        {
            if (_businessAddress == value)
            {
                return;
            }

            _businessAddress = value;
            OnPropertyChanged();
        }
    }

    public string ContactPhone
    {
        get => _contactPhone;
        set
        {
            if (_contactPhone == value)
            {
                return;
            }

            _contactPhone = value;
            OnPropertyChanged();
        }
    }

    public string OwnerRegistrationNote
    {
        get => _ownerRegistrationNote;
        set
        {
            if (_ownerRegistrationNote == value)
            {
                return;
            }

            _ownerRegistrationNote = value;
            OnPropertyChanged();
        }
    }

    public string OwnerRegistrationActionText
    {
        get => _ownerRegistrationActionText;
        private set
        {
            if (_ownerRegistrationActionText == value)
            {
                return;
            }

            _ownerRegistrationActionText = value;
            OnPropertyChanged();
        }
    }

    public bool HasPendingOwnerRequest
    {
        get => _hasPendingOwnerRequest;
        private set
        {
            if (_hasPendingOwnerRequest == value)
            {
                return;
            }

            _hasPendingOwnerRequest = value;
            OnPropertyChanged();
        }
    }

    public bool IsOwnerUser
    {
        get => _isOwnerUser;
        private set
        {
            if (_isOwnerUser == value)
            {
                return;
            }

            _isOwnerUser = value;
            OnPropertyChanged();
            RefreshCommandStates();
        }
    }

    public PoiType OwnerPoiType
    {
        get => _ownerPoiType;
        set
        {
            if (_ownerPoiType == value)
            {
                return;
            }

            _ownerPoiType = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiLatitudeText
    {
        get => _ownerPoiLatitudeText;
        set
        {
            if (_ownerPoiLatitudeText == value)
            {
                return;
            }

            _ownerPoiLatitudeText = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiLongitudeText
    {
        get => _ownerPoiLongitudeText;
        set
        {
            if (_ownerPoiLongitudeText == value)
            {
                return;
            }

            _ownerPoiLongitudeText = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiActivationRadiusText
    {
        get => _ownerPoiActivationRadiusText;
        set
        {
            if (_ownerPoiActivationRadiusText == value)
            {
                return;
            }

            _ownerPoiActivationRadiusText = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiPriorityText
    {
        get => _ownerPoiPriorityText;
        set
        {
            if (_ownerPoiPriorityText == value)
            {
                return;
            }

            _ownerPoiPriorityText = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiQrCodeId
    {
        get => _ownerPoiQrCodeId;
        set
        {
            if (_ownerPoiQrCodeId == value)
            {
                return;
            }

            _ownerPoiQrCodeId = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiBaseLanguageCode
    {
        get => _ownerPoiBaseLanguageCode;
        set
        {
            if (_ownerPoiBaseLanguageCode == value)
            {
                return;
            }

            _ownerPoiBaseLanguageCode = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiName
    {
        get => _ownerPoiName;
        set
        {
            if (_ownerPoiName == value)
            {
                return;
            }

            _ownerPoiName = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiDescription
    {
        get => _ownerPoiDescription;
        set
        {
            if (_ownerPoiDescription == value)
            {
                return;
            }

            _ownerPoiDescription = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiImageUrl
    {
        get => _ownerPoiImageUrl;
        set
        {
            if (_ownerPoiImageUrl == value)
            {
                return;
            }

            _ownerPoiImageUrl = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiAudioFileUrl
    {
        get => _ownerPoiAudioFileUrl;
        set
        {
            if (_ownerPoiAudioFileUrl == value)
            {
                return;
            }

            _ownerPoiAudioFileUrl = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiTtsScript
    {
        get => _ownerPoiTtsScript;
        set
        {
            if (_ownerPoiTtsScript == value)
            {
                return;
            }

            _ownerPoiTtsScript = value;
            OnPropertyChanged();
        }
    }

    public string OwnerPoiActionText
    {
        get => _ownerPoiActionText;
        private set
        {
            if (_ownerPoiActionText == value)
            {
                return;
            }

            _ownerPoiActionText = value;
            OnPropertyChanged();
        }
    }

    public string SelectedOwnerPoiIdForFood
    {
        get => _selectedOwnerPoiIdForFood;
        private set
        {
            if (_selectedOwnerPoiIdForFood == value)
            {
                return;
            }

            _selectedOwnerPoiIdForFood = value;
            OnPropertyChanged();
            RefreshCommandStates();
        }
    }

    public string FoodItemName
    {
        get => _foodItemName;
        set
        {
            if (_foodItemName == value)
            {
                return;
            }

            _foodItemName = value;
            OnPropertyChanged();
        }
    }

    public string FoodItemDescription
    {
        get => _foodItemDescription;
        set
        {
            if (_foodItemDescription == value)
            {
                return;
            }

            _foodItemDescription = value;
            OnPropertyChanged();
        }
    }

    public string FoodItemPriceText
    {
        get => _foodItemPriceText;
        set
        {
            if (_foodItemPriceText == value)
            {
                return;
            }

            _foodItemPriceText = value;
            OnPropertyChanged();
        }
    }

    public string FoodItemCurrency
    {
        get => _foodItemCurrency;
        set
        {
            if (_foodItemCurrency == value)
            {
                return;
            }

            _foodItemCurrency = value;
            OnPropertyChanged();
        }
    }

    public bool FoodItemIsAvailable
    {
        get => _foodItemIsAvailable;
        set
        {
            if (_foodItemIsAvailable == value)
            {
                return;
            }

            _foodItemIsAvailable = value;
            OnPropertyChanged();
        }
    }

    public string FoodItemDisplayOrderText
    {
        get => _foodItemDisplayOrderText;
        set
        {
            if (_foodItemDisplayOrderText == value)
            {
                return;
            }

            _foodItemDisplayOrderText = value;
            OnPropertyChanged();
        }
    }

    public string FoodItemActionText
    {
        get => _foodItemActionText;
        private set
        {
            if (_foodItemActionText == value)
            {
                return;
            }

            _foodItemActionText = value;
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
            IsOwnerUser = false;
            OwnerPoiItems.Clear();
            OwnerFoodItems.Clear();
            SelectedOwnerPoiIdForFood = string.Empty;
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
            _currentUserRole = profile.Role;
            IsOwnerUser = profile.Role == UserRole.Owner;
            _userSessionService.SignIn(currentUserId.Value, profile.DisplayName, profile.Role);

            if (string.IsNullOrWhiteSpace(BusinessName))
            {
                BusinessName = profile.DisplayName;
            }

            var latestOwnerRequest = await _poiWorkflowRepository.GetLatestOwnerRegistrationRequestForUserAsync(
                currentUserId.Value,
                cancellationToken);

            if (latestOwnerRequest is not null)
            {
                BusinessName = latestOwnerRequest.BusinessName;
                BusinessAddress = latestOwnerRequest.BusinessAddress;
                ContactPhone = latestOwnerRequest.ContactPhone;
                OwnerRegistrationNote = latestOwnerRequest.Notes ?? string.Empty;
                UpdateOwnerRegistrationState(latestOwnerRequest.Status);
            }
            else
            {
                UpdateOwnerRegistrationState(null);
            }

            CanEditSettings = true;

            if (latestOwnerRequest?.Status == OwnerRegistrationStatus.Pending)
            {
                StatusMessage = "You already have a pending owner request. Edit the form and submit to update it.";
            }
            else if (latestOwnerRequest?.Status == OwnerRegistrationStatus.Approved)
            {
                StatusMessage = "Your owner request was approved. Any new edits will create a new pending review request.";
            }
            else
            {
                StatusMessage = "Profile loaded. You can update your information.";
            }

            if (IsOwnerUser)
            {
                await RefreshOwnerWorkspaceAsync(cancellationToken);
            }
            else
            {
                OwnerPoiItems.Clear();
                OwnerFoodItems.Clear();
                SelectedOwnerPoiIdForFood = string.Empty;
                ResetOwnerPoiEditor();
                ResetOwnerFoodItemEditor();
            }
        }
        catch
        {
            IsOwnerUser = false;
            OwnerPoiItems.Clear();
            OwnerFoodItems.Clear();
            SelectedOwnerPoiIdForFood = string.Empty;
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
                _userSessionService.SignIn(currentUserId.Value, DisplayName, _currentUserRole);
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

    private async Task RegisterOwnerAsync()
    {
        var currentUserId = _userSessionService.CurrentUserId;
        if (!CanEditSettings || !currentUserId.HasValue)
        {
            StatusMessage = "Owner registration is not available in guest mode.";
            return;
        }

        if (string.IsNullOrWhiteSpace(BusinessName)
            || string.IsNullOrWhiteSpace(BusinessAddress)
            || string.IsNullOrWhiteSpace(ContactPhone))
        {
            StatusMessage = "Please fill Business Name, Address, and Contact Phone.";
            return;
        }

        IsBusy = true;
        try
        {
            var previousStatus = _latestOwnerRequestStatus;
            var requestId = await _poiWorkflowRepository.SubmitOwnerRegistrationRequestAsync(
                currentUserId.Value,
                BusinessName,
                BusinessAddress,
                ContactPhone,
                OwnerRegistrationNote);

            UpdateOwnerRegistrationState(OwnerRegistrationStatus.Pending);

            if (previousStatus == OwnerRegistrationStatus.Pending)
            {
                StatusMessage = $"Pending request #{requestId} was updated. Admin will review your latest information.";
            }
            else if (previousStatus == OwnerRegistrationStatus.Approved)
            {
                StatusMessage = $"Owner profile update submitted as pending request #{requestId}. Please wait for admin review.";
            }
            else
            {
                StatusMessage = $"Owner registration submitted successfully. Request #{requestId} is pending admin approval.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to submit owner registration: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelOwnerRequestAsync()
    {
        var currentUserId = _userSessionService.CurrentUserId;
        if (!CanEditSettings || !currentUserId.HasValue)
        {
            StatusMessage = "Owner request cancellation is not available in guest mode.";
            return;
        }

        if (!HasPendingOwnerRequest)
        {
            StatusMessage = "There is no pending owner request to cancel.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _poiWorkflowRepository.CancelPendingOwnerRegistrationRequestAsync(currentUserId.Value);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                return;
            }

            UpdateOwnerRegistrationState(OwnerRegistrationStatus.Rejected);
            StatusMessage = "Pending owner request cancelled. You can edit and submit again anytime.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to cancel owner request: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshOwnerWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _userSessionService.CurrentUserId;
        if (!CanEditSettings || !currentUserId.HasValue || !IsOwnerUser)
        {
            OwnerPoiItems.Clear();
            OwnerFoodItems.Clear();
            SelectedOwnerPoiIdForFood = string.Empty;
            return;
        }

        var ownerPois = await _poiWorkflowRepository.GetOwnerPoisAsync(currentUserId.Value, cancellationToken);

        OwnerPoiItems.Clear();
        foreach (var poi in ownerPois)
        {
            var baseTranslation = poi.PoiTranslations.FirstOrDefault();

            OwnerPoiItems.Add(new OwnerPoiManagementItemViewModel(
                poi.Id,
                poi.Type,
                poi.ApprovalStatus,
                poi.Latitude,
                poi.Longitude,
                poi.ActivationRadius,
                poi.Priority,
                poi.QRCodeId,
                baseTranslation?.LocationName ?? poi.Id,
                baseTranslation?.Description ?? string.Empty,
                baseTranslation?.ImageUrl ?? string.Empty,
                baseTranslation?.AudioFileUrl ?? string.Empty,
                baseTranslation?.TtsScript ?? string.Empty,
                poi.SubmittedUtc));
        }

        if (!string.IsNullOrWhiteSpace(_editingOwnerPoiId)
            && !OwnerPoiItems.Any(x => string.Equals(x.Id, _editingOwnerPoiId, StringComparison.OrdinalIgnoreCase)))
        {
            ResetOwnerPoiEditor();
        }

        if (string.IsNullOrWhiteSpace(SelectedOwnerPoiIdForFood)
            || !OwnerPoiItems.Any(x => x.Type == PoiType.Food && string.Equals(x.Id, SelectedOwnerPoiIdForFood, StringComparison.OrdinalIgnoreCase)))
        {
            var firstFoodPoi = OwnerPoiItems.FirstOrDefault(x => x.Type == PoiType.Food);
            SelectedOwnerPoiIdForFood = firstFoodPoi?.Id ?? string.Empty;
        }

        await LoadOwnerFoodItemsAsync(cancellationToken);
    }

    private async Task SubmitOrUpdateOwnerPoiAsync()
    {
        var currentUserId = _userSessionService.CurrentUserId;
        if (!CanEditSettings || !currentUserId.HasValue || !IsOwnerUser)
        {
            StatusMessage = "Owner POI workspace is not available in guest or non-owner mode.";
            return;
        }

        if (!TryParseDoubleValue(OwnerPoiLatitudeText, out var latitude)
            || !TryParseDoubleValue(OwnerPoiLongitudeText, out var longitude))
        {
            StatusMessage = "Please enter valid latitude and longitude values.";
            return;
        }

        if (!TryParseIntValue(OwnerPoiActivationRadiusText, out var activationRadius) || activationRadius <= 0)
        {
            StatusMessage = "Activation radius must be a positive integer.";
            return;
        }

        if (!TryParseIntValue(OwnerPoiPriorityText, out var priority) || priority <= 0)
        {
            StatusMessage = "Priority must be a positive integer.";
            return;
        }

        IsBusy = true;
        try
        {
            if (string.IsNullOrWhiteSpace(_editingOwnerPoiId))
            {
                if (string.IsNullOrWhiteSpace(OwnerPoiName)
                    || string.IsNullOrWhiteSpace(OwnerPoiDescription)
                    || string.IsNullOrWhiteSpace(OwnerPoiImageUrl)
                    || string.IsNullOrWhiteSpace(OwnerPoiAudioFileUrl)
                    || string.IsNullOrWhiteSpace(OwnerPoiTtsScript))
                {
                    StatusMessage = "Please fill all base translation fields before submitting owner POI.";
                    return;
                }

                var poiId = await _poiWorkflowRepository.SubmitOwnerPoiAsync(
                    currentUserId.Value,
                    OwnerPoiType,
                    latitude,
                    longitude,
                    activationRadius,
                    priority,
                    OwnerPoiQrCodeId,
                    OwnerPoiBaseLanguageCode,
                    OwnerPoiName,
                    OwnerPoiDescription,
                    OwnerPoiImageUrl,
                    OwnerPoiAudioFileUrl,
                    OwnerPoiTtsScript);

                StatusMessage = $"Owner POI {poiId} submitted and is pending admin approval.";
                ResetOwnerPoiEditor();
                await RefreshOwnerWorkspaceAsync();
            }
            else
            {
                var result = await _poiWorkflowRepository.UpdateOwnerPoiBasicInfoAsync(
                    currentUserId.Value,
                    _editingOwnerPoiId,
                    OwnerPoiType,
                    latitude,
                    longitude,
                    activationRadius,
                    priority,
                    OwnerPoiQrCodeId);

                StatusMessage = result.Message;
                if (!result.IsSuccess)
                {
                    return;
                }

                ResetOwnerPoiEditor();
                await RefreshOwnerWorkspaceAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to save owner POI: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SelectOwnerPoiForEditAsync(OwnerPoiManagementItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _editingOwnerPoiId = item.Id;
        OwnerPoiType = item.Type;
        OwnerPoiLatitudeText = item.Latitude.ToString(CultureInfo.InvariantCulture);
        OwnerPoiLongitudeText = item.Longitude.ToString(CultureInfo.InvariantCulture);
        OwnerPoiActivationRadiusText = item.ActivationRadius.ToString(CultureInfo.InvariantCulture);
        OwnerPoiPriorityText = item.Priority.ToString(CultureInfo.InvariantCulture);
        OwnerPoiQrCodeId = item.QrCodeId ?? string.Empty;
        OwnerPoiName = item.BaseLocationName;
        OwnerPoiDescription = item.BaseDescription;
        OwnerPoiImageUrl = item.BaseImageUrl;
        OwnerPoiAudioFileUrl = item.BaseAudioFileUrl;
        OwnerPoiTtsScript = item.BaseTtsScript;
        OwnerPoiActionText = $"Update POI {item.Id}";

        if (item.Type == PoiType.Food)
        {
            await SelectOwnerPoiForFoodAsync(item);
        }

        StatusMessage = $"Editing owner POI {item.Id}. Saving will move it back to pending approval.";
    }

    private async Task SelectOwnerPoiForFoodAsync(OwnerPoiManagementItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Type != PoiType.Food)
        {
            StatusMessage = $"POI {item.Id} is not a food type. Food items are only available for food POIs.";
            return;
        }

        SelectedOwnerPoiIdForFood = item.Id;
        await LoadOwnerFoodItemsAsync();
        StatusMessage = $"Food menu workspace is now using POI {item.Id}.";
    }

    private async Task LoadOwnerFoodItemsAsync(CancellationToken cancellationToken = default)
    {
        OwnerFoodItems.Clear();

        var currentUserId = _userSessionService.CurrentUserId;
        if (!currentUserId.HasValue || string.IsNullOrWhiteSpace(SelectedOwnerPoiIdForFood))
        {
            return;
        }

        var selectedPoi = OwnerPoiItems.FirstOrDefault(x => string.Equals(x.Id, SelectedOwnerPoiIdForFood, StringComparison.OrdinalIgnoreCase));
        if (selectedPoi is null || selectedPoi.Type != PoiType.Food)
        {
            return;
        }

        var foodItems = await _poiWorkflowRepository.GetOwnerFoodItemsAsync(currentUserId.Value, SelectedOwnerPoiIdForFood, cancellationToken);
        foreach (var item in foodItems)
        {
            OwnerFoodItems.Add(new OwnerFoodItemEditorRowViewModel(
                item.Id,
                item.Name,
                item.Description ?? string.Empty,
                item.Price,
                item.Currency,
                item.IsAvailable,
                item.DisplayOrder));
        }
    }

    private async Task SaveOwnerFoodItemAsync()
    {
        var currentUserId = _userSessionService.CurrentUserId;
        if (!CanEditSettings || !currentUserId.HasValue || !IsOwnerUser)
        {
            StatusMessage = "Food menu workspace is not available in guest or non-owner mode.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedOwnerPoiIdForFood))
        {
            StatusMessage = "Please select a food POI before saving menu items.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FoodItemName))
        {
            StatusMessage = "Food item name is required.";
            return;
        }

        if (!TryParseDecimalValue(FoodItemPriceText, out var price) || price < 0)
        {
            StatusMessage = "Food item price must be a valid non-negative number.";
            return;
        }

        if (!TryParseIntValue(FoodItemDisplayOrderText, out var displayOrder) || displayOrder < 0)
        {
            StatusMessage = "Display order must be a valid non-negative integer.";
            return;
        }

        IsBusy = true;
        try
        {
            var isEditing = _editingFoodItemId.HasValue && _editingFoodItemId.Value > 0;
            var foodItemId = await _poiWorkflowRepository.SaveOwnerFoodItemAsync(
                currentUserId.Value,
                SelectedOwnerPoiIdForFood,
                _editingFoodItemId,
                FoodItemName,
                FoodItemDescription,
                price,
                FoodItemCurrency,
                FoodItemIsAvailable,
                displayOrder);

            StatusMessage = isEditing
                ? $"Food item #{foodItemId} updated successfully."
                : $"Food item #{foodItemId} created successfully.";

            ResetOwnerFoodItemEditor();
            await LoadOwnerFoodItemsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to save food item: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void EditOwnerFoodItem(OwnerFoodItemEditorRowViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _editingFoodItemId = item.Id;
        FoodItemName = item.Name;
        FoodItemDescription = item.Description;
        FoodItemPriceText = item.Price.ToString(CultureInfo.InvariantCulture);
        FoodItemCurrency = item.Currency;
        FoodItemIsAvailable = item.IsAvailable;
        FoodItemDisplayOrderText = item.DisplayOrder.ToString(CultureInfo.InvariantCulture);
        FoodItemActionText = $"Update Food Item #{item.Id}";
        StatusMessage = $"Editing food item #{item.Id}.";
    }

    private async Task DeleteOwnerFoodItemAsync(OwnerFoodItemEditorRowViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var currentUserId = _userSessionService.CurrentUserId;
        if (!CanEditSettings || !currentUserId.HasValue || !IsOwnerUser)
        {
            StatusMessage = "Food item deletion is not available in guest or non-owner mode.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _poiWorkflowRepository.DeleteOwnerFoodItemAsync(
                currentUserId.Value,
                SelectedOwnerPoiIdForFood,
                item.Id);

            StatusMessage = result.Message;
            if (!result.IsSuccess)
            {
                return;
            }

            await LoadOwnerFoodItemsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to delete food item: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetOwnerPoiEditor()
    {
        _editingOwnerPoiId = null;
        OwnerPoiType = PoiType.Food;
        OwnerPoiLatitudeText = string.Empty;
        OwnerPoiLongitudeText = string.Empty;
        OwnerPoiActivationRadiusText = "100";
        OwnerPoiPriorityText = "1";
        OwnerPoiQrCodeId = string.Empty;
        OwnerPoiBaseLanguageCode = "vi";
        OwnerPoiName = string.Empty;
        OwnerPoiDescription = string.Empty;
        OwnerPoiImageUrl = string.Empty;
        OwnerPoiAudioFileUrl = string.Empty;
        OwnerPoiTtsScript = string.Empty;
        OwnerPoiActionText = "Submit Owner POI";
    }

    private void ResetOwnerFoodItemEditor()
    {
        _editingFoodItemId = null;
        FoodItemName = string.Empty;
        FoodItemDescription = string.Empty;
        FoodItemPriceText = "0";
        FoodItemCurrency = "VND";
        FoodItemIsAvailable = true;
        FoodItemDisplayOrderText = "1";
        FoodItemActionText = "Add Food Item";
    }

    private static bool TryParseDoubleValue(string rawValue, out double value)
    {
        return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static bool TryParseIntValue(string rawValue, out int value)
    {
        return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            || int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
    }

    private static bool TryParseDecimalValue(string rawValue, out decimal value)
    {
        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private void RefreshCommandStates()
    {
        (SaveProfileCommand as Command)?.ChangeCanExecute();
        (ChangePasswordCommand as Command)?.ChangeCanExecute();
        (RegisterOwnerCommand as Command)?.ChangeCanExecute();
        (CancelOwnerRequestCommand as Command)?.ChangeCanExecute();
        (RefreshOwnerWorkspaceCommand as Command)?.ChangeCanExecute();
        (SubmitOwnerPoiCommand as Command)?.ChangeCanExecute();
        (ResetOwnerPoiEditorCommand as Command)?.ChangeCanExecute();
        (SelectOwnerPoiForEditCommand as Command<OwnerPoiManagementItemViewModel>)?.ChangeCanExecute();
        (SelectOwnerPoiForFoodCommand as Command<OwnerPoiManagementItemViewModel>)?.ChangeCanExecute();
        (SaveOwnerFoodItemCommand as Command)?.ChangeCanExecute();
        (EditOwnerFoodItemCommand as Command<OwnerFoodItemEditorRowViewModel>)?.ChangeCanExecute();
        (DeleteOwnerFoodItemCommand as Command<OwnerFoodItemEditorRowViewModel>)?.ChangeCanExecute();
        (ResetOwnerFoodItemEditorCommand as Command)?.ChangeCanExecute();
    }

    private void UpdateOwnerRegistrationState(OwnerRegistrationStatus? status)
    {
        _latestOwnerRequestStatus = status;
        HasPendingOwnerRequest = status == OwnerRegistrationStatus.Pending;

        OwnerRegistrationActionText = status switch
        {
            OwnerRegistrationStatus.Pending => "Update Pending Owner Request",
            OwnerRegistrationStatus.Approved => "Request Owner Profile Update",
            OwnerRegistrationStatus.Rejected => "Resubmit Owner Registration",
            _ => "Submit Owner Registration"
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
