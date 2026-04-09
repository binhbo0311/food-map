namespace FOOD_MAP.Services;

public interface IAuthService
{
    Task<(bool IsSuccess, string Message)> LoginAsync(string userName, string password, CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> RegisterAsync(string userName, string displayName, string password, CancellationToken cancellationToken = default);

    Task ContinueAsGuestAsync(CancellationToken cancellationToken = default);
}
