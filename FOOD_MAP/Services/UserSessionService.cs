namespace FOOD_MAP.Services;

public sealed class UserSessionService : IUserSessionService
{
    public bool IsGuest { get; private set; } = true;

    public int? CurrentUserId { get; private set; }

    public string DisplayName { get; private set; } = "Guest";

    public void SignIn(int userId, string displayName)
    {
        // Chuyển session sang chế độ đăng nhập và lưu thông tin user hiện tại.
        IsGuest = false;
        CurrentUserId = userId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "User" : displayName.Trim();
    }

    public void UseGuestMode()
    {
        // Đặt lại session guest để app vẫn hoạt động nhưng không lưu dữ liệu cá nhân.
        IsGuest = true;
        CurrentUserId = null;
        DisplayName = "Guest";
    }
}
