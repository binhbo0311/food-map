namespace FOOD_MAP.Services;

using FOOD_MAP.Shared.Models;

public interface IUserSessionService
{
    bool IsGuest { get; }

    int? CurrentUserId { get; }

    string DisplayName { get; }

    UserRole CurrentRole { get; }

    void SignIn(int userId, string displayName, UserRole role = UserRole.User);

    void UseGuestMode();
}
