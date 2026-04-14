namespace FOOD_MAP.Services;

using FOOD_MAP.Shared.Models;

public sealed class UserSessionService : IUserSessionService
{
    public bool IsGuest { get; private set; } = true;

    public int? CurrentUserId { get; private set; }

    public string DisplayName { get; private set; } = "Guest";

    public UserRole CurrentRole { get; private set; } = UserRole.User;

    public void SignIn(int userId, string displayName, UserRole role = UserRole.User)
    {
        // Chuyển session sang chế độ đăng nhập và lưu thông tin user hiện tại.
        IsGuest = false;
        CurrentUserId = userId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "User" : displayName.Trim();
        CurrentRole = role;
    }

    public void UseGuestMode()
    {
        // Đặt lại session guest để app vẫn hoạt động nhưng không lưu dữ liệu cá nhân.
        IsGuest = true;
        CurrentUserId = null;
        DisplayName = "Guest";
        CurrentRole = UserRole.User;
    }
}
