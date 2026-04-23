using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.Shared.Services;

public sealed record PortalSessionSnapshot(int UserId, string UserName, string DisplayName, UserRole Role);

public sealed class PortalUserSessionState
{
    public int? UserId { get; private set; }

    public string UserName { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = "Guest";

    public UserRole? Role { get; private set; }

    public bool IsAuthenticated => UserId.HasValue;

    public bool IsAdmin => Role == UserRole.Admin;

    public bool IsOwner => Role == UserRole.Owner;

    public bool CanAccessCms => IsAdmin || IsOwner;

    public event Action? Changed;

    public void SetAuthenticated(PortalSessionSnapshot snapshot)
    {
        UserId = snapshot.UserId;
        UserName = snapshot.UserName;
        DisplayName = snapshot.DisplayName;
        Role = snapshot.Role;
        Changed?.Invoke();
    }

    public void SetGuest()
    {
        UserId = null;
        UserName = string.Empty;
        DisplayName = "Guest";
        Role = null;
        Changed?.Invoke();
    }
}
