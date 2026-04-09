namespace FOOD_MAP.Services;

public interface IUserSessionService
{
    bool IsGuest { get; }

    int? CurrentUserId { get; }

    string DisplayName { get; }

    void SignIn(int userId, string displayName);

    void UseGuestMode();
}
