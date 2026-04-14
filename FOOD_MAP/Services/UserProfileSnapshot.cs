namespace FOOD_MAP.Services;

using FOOD_MAP.Shared.Models;

public sealed record UserProfileSnapshot(
	int UserId,
	string UserName,
	string DisplayName,
	UserRole Role,
	string? OwnerIdentificationCode);
