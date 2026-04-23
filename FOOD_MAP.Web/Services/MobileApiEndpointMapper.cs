using FOOD_MAP.Shared.Contracts;
using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.Shared.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FOOD_MAP.Web.Services;

public static class MobileApiEndpointMapper
{
    private static readonly TimeSpan TourDuplicateWindow = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RouteArrivalDuplicateWindow = TimeSpan.FromMinutes(6);

    public static void MapMobileApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/mobile");

        MapAuthEndpoints(api);
        MapProfileEndpoints(api);
        MapActiveUserEndpoints(api);
        MapPoiEndpoints(api);
        MapActivityEndpoints(api);
        MapWorkflowEndpoints(api);
    }

    private static void MapAuthEndpoints(RouteGroupBuilder api)
    {
        api.MapPost("/auth/login", async (
            AuthLoginRequestDto request,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            var normalizedUserName = TextInputNormalizer.NormalizeSingleLine(request.UserName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.Ok(new AuthLoginResponseDto(false, "Vui lòng nhập đầy đủ tài khoản và mật khẩu.", null, null, null));
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var passwordHash = HashPassword(request.Password);

            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserName.ToLower() == normalizedUserName && x.PasswordHash == passwordHash,
                    cancellationToken);

            if (user is null)
            {
                return Results.Ok(new AuthLoginResponseDto(false, "Thông tin đăng nhập không đúng.", null, null, null));
            }

            return Results.Ok(new AuthLoginResponseDto(true, $"Xin chào {user.DisplayName}.", user.Id, user.DisplayName, user.Role));
        });

        api.MapPost("/auth/register", async (
            AuthRegisterRequestDto request,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            var normalizedUserName = TextInputNormalizer.NormalizeSingleLine(request.UserName).ToLowerInvariant();
            var normalizedDisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? TextInputNormalizer.NormalizeSingleLine(request.UserName)
                : TextInputNormalizer.NormalizeSingleLine(request.DisplayName);

            if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.Ok(new ApiResultDto(false, "Vui lòng nhập đầy đủ tài khoản và mật khẩu."));
            }

            if (request.Password.Length < 6)
            {
                return Results.Ok(new ApiResultDto(false, "Mật khẩu phải có ít nhất 6 ký tự."));
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var userNameExists = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(x => x.UserName.ToLower() == normalizedUserName, cancellationToken);

            if (userNameExists)
            {
                return Results.Ok(new ApiResultDto(false, "Tên tài khoản đã tồn tại. Vui lòng chọn tên khác."));
            }

            dbContext.Users.Add(new User
            {
                UserName = normalizedUserName,
                DisplayName = normalizedDisplayName,
                PasswordHash = HashPassword(request.Password),
                CreatedUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new ApiResultDto(true, "Tạo tài khoản thành công. Bạn có thể đăng nhập ngay bây giờ."));
        });
    }

    private static void MapProfileEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/profile/{userId:int}", async (
            int userId,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var profile = await dbContext.Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new UserProfileDto(x.Id, x.UserName, x.DisplayName, x.Role, x.OwnerIdentificationCode))
                .FirstOrDefaultAsync(cancellationToken);

            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        api.MapPut("/profile/{userId:int}/display-name", async (
            int userId,
            UpdateDisplayNameRequestDto request,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            var normalizedDisplayName = TextInputNormalizer.NormalizeSingleLine(request.DisplayName);
            if (string.IsNullOrWhiteSpace(normalizedDisplayName))
            {
                return Results.Ok(new ApiResultDto(false, "Display name cannot be empty."));
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

            if (user is null)
            {
                return Results.Ok(new ApiResultDto(false, "User was not found."));
            }

            user.DisplayName = normalizedDisplayName;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new ApiResultDto(true, "Profile updated successfully."));
        });

        api.MapPut("/profile/{userId:int}/password", async (
            int userId,
            ChangePasswordRequestDto request,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return Results.Ok(new ApiResultDto(false, "Please fill in both current and new passwords."));
            }

            if (request.NewPassword.Length < 6)
            {
                return Results.Ok(new ApiResultDto(false, "New password must be at least 6 characters."));
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (user is null)
            {
                return Results.Ok(new ApiResultDto(false, "User was not found."));
            }

            var currentPasswordHash = HashPassword(request.CurrentPassword);
            if (!string.Equals(user.PasswordHash, currentPasswordHash, StringComparison.Ordinal))
            {
                return Results.Ok(new ApiResultDto(false, "Current password is incorrect."));
            }

            user.PasswordHash = HashPassword(request.NewPassword);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new ApiResultDto(true, "Password changed successfully."));
        });
    }

    private static void MapActiveUserEndpoints(RouteGroupBuilder api)
    {
        api.MapPost("/active-users/heartbeat/mobile", async (
            MobileHeartbeatRequestDto request,
            IActiveUserTrackerService activeUserTrackerService,
            CancellationToken cancellationToken) =>
        {
            await activeUserTrackerService.TrackMobileHeartbeatAsync(request.UserId, request.SessionKey, cancellationToken);
            return Results.Ok();
        });

        api.MapGet("/active-users/summary", async (
            IActiveUserTrackerService activeUserTrackerService,
            CancellationToken cancellationToken) =>
        {
            var summary = await activeUserTrackerService.GetSummaryAsync(cancellationToken);
            return Results.Ok(summary);
        });
    }

    private static void MapPoiEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/pois", async (
            string languageCode,
            int? userId,
            IDbContextFactory<AppDbContext> dbContextFactory,
            ISubscriptionService subscriptionService,
            CancellationToken cancellationToken) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
            var subscriptionPolicy = await subscriptionService.GetCurrentPolicyAsync(userId, cancellationToken);

            var pois = await dbContext.Pois
                .AsNoTracking()
                .Where(x => x.ApprovalStatus == PoiApprovalStatus.Approved)
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            if (pois.Count == 0)
            {
                return Results.Ok(Array.Empty<PoiListItemDto>());
            }

            var poiIds = pois.Select(x => x.Id).ToArray();
            var translations = await dbContext.PoiTranslations
                .AsNoTracking()
                .Include(x => x.Language)
                .Where(x => poiIds.Contains(x.PoiId))
                .ToListAsync(cancellationToken);

            var items = new List<PoiListItemDto>(pois.Count);
            foreach (var poi in pois)
            {
                var translation = translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, normalizedLanguageCode))
                    ?? translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, "en"))
                    ?? translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, "vi"))
                    ?? translations.FirstOrDefault(x => x.PoiId == poi.Id);

                var displayName = translation?.LocationName ?? $"POI #{poi.Id}";
                var description = translation?.Description ?? string.Empty;
                var narrationText = string.IsNullOrWhiteSpace(translation?.TtsScript)
                    ? description
                    : translation!.TtsScript;

                items.Add(new PoiListItemDto(
                    poi.Id,
                    poi.Type,
                    poi.Latitude,
                    poi.Longitude,
                    poi.ActivationRadius,
                    poi.Priority,
                    displayName,
                    $"Activation radius: {poi.ActivationRadius}m",
                    description,
                    narrationText,
                    translation?.ImageUrl ?? string.Empty,
                    translation?.RichContentHtml ?? string.Empty));
            }

            if (subscriptionPolicy.MaxAccessiblePoiCount.HasValue)
            {
                items = items.Take(subscriptionPolicy.MaxAccessiblePoiCount.Value).ToList();
            }

            return Results.Ok(items);
        });

        api.MapGet("/pois/{poiId}/food-items", async (
            string poiId,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(poiId))
            {
                return Results.Ok(Array.Empty<FoodMenuItemDto>());
            }

            var normalizedPoiId = poiId.Trim().ToUpperInvariant();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var items = await dbContext.FoodItems
                .AsNoTracking()
                .Where(x => x.PoiId == normalizedPoiId)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Id)
                .Select(x => new FoodMenuItemDto(
                    x.Id,
                    x.Name,
                    x.Description ?? string.Empty,
                    x.Price,
                    x.Currency,
                    x.IsAvailable))
                .ToListAsync(cancellationToken);

            return Results.Ok(items);
        });

        api.MapGet("/pois/{poiId}/languages", async (
            string poiId,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(poiId))
            {
                return Results.Ok(Array.Empty<PoiAvailableLanguageOptionDto>());
            }

            var normalizedPoiId = poiId.Trim().ToUpperInvariant();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var poiExists = await dbContext.Pois
                .AsNoTracking()
                .AnyAsync(x => x.Id == normalizedPoiId && x.ApprovalStatus == PoiApprovalStatus.Approved, cancellationToken);

            if (!poiExists)
            {
                return Results.Ok(Array.Empty<PoiAvailableLanguageOptionDto>());
            }

            var translations = await dbContext.PoiTranslations
                .AsNoTracking()
                .Include(x => x.Language)
                .Where(x => x.PoiId == normalizedPoiId)
                .ToListAsync(cancellationToken);

            var languageOptions = translations
                .Where(x => !string.IsNullOrWhiteSpace(x.Language?.LanguageCode))
                .Select(x =>
                {
                    var normalizedCode = NormalizeLanguageCode(x.Language!.LanguageCode);
                    var languageName = string.IsNullOrWhiteSpace(x.Language.LanguageName)
                        ? normalizedCode.ToUpperInvariant()
                        : x.Language.LanguageName.Trim();

                    return new PoiAvailableLanguageOptionDto(normalizedCode, languageName);
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.LanguageCode))
                .GroupBy(x => x.LanguageCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(x => GetLanguagePriority(x.LanguageCode))
                .ThenBy(x => x.LanguageName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Results.Ok(languageOptions);
        });

        api.MapGet("/pois/{poiId}/scan", async (
            string poiId,
            string languageCode,
            int? userId,
            IDbContextFactory<AppDbContext> dbContextFactory,
            ISubscriptionService subscriptionService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(poiId))
            {
                return Results.NotFound();
            }

            var normalizedPoiId = poiId.Trim().ToUpperInvariant();
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var poi = await dbContext.Pois
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == normalizedPoiId && x.ApprovalStatus == PoiApprovalStatus.Approved, cancellationToken);

            if (poi is null)
            {
                return Results.NotFound();
            }

            var subscriptionPolicy = await subscriptionService.GetCurrentPolicyAsync(userId, cancellationToken);
            if (subscriptionPolicy.MaxAccessiblePoiCount.HasValue)
            {
                var accessiblePoiIds = await dbContext.Pois
                    .AsNoTracking()
                    .Where(x => x.ApprovalStatus == PoiApprovalStatus.Approved)
                    .OrderBy(x => x.Priority)
                    .ThenBy(x => x.Id)
                    .Select(x => x.Id)
                    .Take(subscriptionPolicy.MaxAccessiblePoiCount.Value)
                    .ToListAsync(cancellationToken);

                if (!accessiblePoiIds.Contains(normalizedPoiId, StringComparer.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }
            }

            var translations = await dbContext.PoiTranslations
                .AsNoTracking()
                .Include(x => x.Language)
                .Where(x => x.PoiId == poi.Id)
                .ToListAsync(cancellationToken);

            var translation = translations.FirstOrDefault(x => IsLanguageMatch(x.Language?.LanguageCode, normalizedLanguageCode))
                ?? translations.FirstOrDefault(x => IsLanguageMatch(x.Language?.LanguageCode, "en"))
                ?? translations.FirstOrDefault(x => IsLanguageMatch(x.Language?.LanguageCode, "vi"))
                ?? translations.FirstOrDefault();

            if (translation is null)
            {
                return Results.NotFound();
            }

            var foodItems = Array.Empty<FoodMenuItemDto>();
            if (poi.Type == PoiType.Food)
            {
                foodItems = await dbContext.FoodItems
                    .AsNoTracking()
                    .Where(x => x.PoiId == poi.Id)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.Id)
                    .Select(x => new FoodMenuItemDto(
                        x.Id,
                        x.Name,
                        x.Description ?? string.Empty,
                        x.Price,
                        x.Currency,
                        x.IsAvailable))
                    .ToArrayAsync(cancellationToken);
            }

            return Results.Ok(new PoiScanResultDto(
                poi.Id,
                poi.Type,
                translation.LocationName,
                translation.Description,
                translation.RichContentHtml,
                string.IsNullOrWhiteSpace(translation.TtsScript) ? translation.Description : translation.TtsScript,
                foodItems));
        });
    }

    private static void MapActivityEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/users/{userId:int}/activity/favorites", async (
            int userId,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var favoritePoiIds = await dbContext.UserFavorites
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.PoiId)
                .ToListAsync(cancellationToken);

            return Results.Ok(favoritePoiIds);
        });

        api.MapGet("/users/{userId:int}/activity/visited", async (
            int userId,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var visitedPoiIds = await dbContext.UserTours
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.PoiId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return Results.Ok(visitedPoiIds);
        });

        api.MapGet("/users/{userId:int}/activity/tour-count", async (
            int userId,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var count = await dbContext.UserTours
                .AsNoTracking()
                .CountAsync(x => x.UserId == userId, cancellationToken);

            return Results.Ok(count);
        });

        api.MapPost("/users/{userId:int}/activity/favorite", async (
            int userId,
            SetFavoriteRequestDto request,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            var normalizedPoiId = NormalizePoiId(request.PoiId);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var existingFavorite = await dbContext.UserFavorites
                .FirstOrDefaultAsync(x => x.UserId == userId && x.PoiId == normalizedPoiId, cancellationToken);

            if (request.IsFavorite && existingFavorite is null)
            {
                dbContext.UserFavorites.Add(new UserFavorite
                {
                    UserId = userId,
                    PoiId = normalizedPoiId,
                    CreatedUtc = DateTimeOffset.UtcNow
                });
            }

            if (!request.IsFavorite && existingFavorite is not null)
            {
                dbContext.UserFavorites.Remove(existingFavorite);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(request.IsFavorite);
        });

        api.MapPost("/users/{userId:int}/activity/tour", async (
            int userId,
            AddTourRequestDto request,
            IDbContextFactory<AppDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            var normalizedPoiId = NormalizePoiId(request.PoiId);
            var normalizedLanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode)
                ? "vi"
                : NormalizeLanguageCode(request.LanguageCode);
            var normalizedTriggerType = string.IsNullOrWhiteSpace(request.TriggerType)
                ? "manual"
                : request.TriggerType.Trim().ToLowerInvariant();

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var duplicateWindow = ResolveTourDuplicateWindow(normalizedTriggerType);
            var duplicateThresholdUtc = DateTimeOffset.UtcNow - duplicateWindow;

            var isDuplicateTour = await dbContext.UserTours
                .AsNoTracking()
                .AnyAsync(
                    x => x.UserId == userId
                        && x.PoiId == normalizedPoiId
                        && x.TriggerType == normalizedTriggerType
                        && x.VisitedUtc >= duplicateThresholdUtc,
                    cancellationToken);

            if (isDuplicateTour)
            {
                return Results.Ok(new ApiResultDto(true, "Duplicate tour ignored."));
            }

            dbContext.UserTours.Add(new UserTour
            {
                UserId = userId,
                PoiId = normalizedPoiId,
                LanguageCode = normalizedLanguageCode,
                TriggerType = normalizedTriggerType,
                VisitedUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new ApiResultDto(true, "Tour recorded."));
        });
    }

    private static void MapWorkflowEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/workflow/subscription-plans/{actorUserId:int}", async (
            int actorUserId,
            ISubscriptionPlanService subscriptionPlanService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var plans = await subscriptionPlanService.GetVisiblePlansAsync(actorUserId, cancellationToken);
                return Results.Ok(plans.Select(ToSubscriptionPlanDto).ToList());
            }
            catch (InvalidOperationException ex)
            {
                return Results.Ok(new ApiResultDto(false, ex.Message));
            }
        });

        api.MapPost("/workflow/subscription-plans", async (
            CreateSubscriptionPlanRequestDto request,
            ISubscriptionPlanService subscriptionPlanService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var createdPlan = await subscriptionPlanService.CreatePlanAsync(
                    request.AdminUserId,
                    new SubscriptionPlanWriteModel(
                        request.PlanCode,
                        request.DisplayName,
                        request.Tier,
                        request.BillingPeriod,
                        request.FixedPrice,
                        request.Currency,
                        request.MaxAccessiblePoiCount,
                        request.MaxOwnerPoiCount,
                        request.MaxActivationRadiusMeters,
                        request.IsActive),
                    cancellationToken);

                return Results.Ok(ToSubscriptionPlanDto(createdPlan));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Ok(new ApiResultDto(false, ex.Message));
            }
        });

        api.MapPut("/workflow/subscription-plans/{planId:int}", async (
            int planId,
            UpdateSubscriptionPlanRequestDto request,
            ISubscriptionPlanService subscriptionPlanService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var updatedPlan = await subscriptionPlanService.UpdatePlanAsync(
                    request.AdminUserId,
                    planId,
                    new SubscriptionPlanWriteModel(
                        request.PlanCode,
                        request.DisplayName,
                        request.Tier,
                        request.BillingPeriod,
                        request.FixedPrice,
                        request.Currency,
                        request.MaxAccessiblePoiCount,
                        request.MaxOwnerPoiCount,
                        request.MaxActivationRadiusMeters,
                        request.IsActive),
                    cancellationToken);

                return Results.Ok(ToSubscriptionPlanDto(updatedPlan));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Ok(new ApiResultDto(false, ex.Message));
            }
        });

        api.MapGet("/workflow/next-poi-id", async (
            PoiType type,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var poiId = await repository.GenerateNextPoiIdAsync(type, cancellationToken);
            return Results.Ok(poiId);
        });

        api.MapGet("/workflow/languages", async (
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var languages = await repository.GetAvailableLanguagesAsync(cancellationToken);
            return Results.Ok(languages.Select(ToLanguageDto).ToList());
        });

        api.MapGet("/workflow/owner-heatmap/{ownerUserId:int}", async (
            int ownerUserId,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var summaries = await repository.GetOwnerPoiHeatmapSummaryAsync(ownerUserId, cancellationToken);
            return Results.Ok(summaries.Select(ToOwnerPoiHeatmapDto).ToList());
        });

        api.MapPost("/workflow/language-ownership", async (
            SubmitLanguageOwnershipRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var requestId = await repository.SubmitLanguageOwnershipRequestAsync(request.OwnerUserId, request.LanguageCode, cancellationToken);
            return Results.Ok(requestId);
        });

        api.MapGet("/workflow/language-ownership/owner/{ownerUserId:int}", async (
            int ownerUserId,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var requests = await repository.GetOwnerLanguageOwnershipRequestsAsync(ownerUserId, cancellationToken);
            return Results.Ok(requests.Select(ToLanguageOwnershipRequestDto).ToList());
        });

        api.MapGet("/workflow/language-ownership/pending", async (
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var requests = await repository.GetPendingLanguageOwnershipRequestsAsync(cancellationToken);
            return Results.Ok(requests.Select(ToLanguageOwnershipRequestDto).ToList());
        });

        api.MapPost("/workflow/language-ownership/{requestId:int}/approve", async (
            int requestId,
            ApproveRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.ApproveLanguageOwnershipRequestAsync(requestId, request.AdminUserId, cancellationToken);
            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });

        api.MapPost("/workflow/language-ownership/{requestId:int}/reject", async (
            int requestId,
            RejectRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.RejectLanguageOwnershipRequestAsync(requestId, request.AdminUserId, request.RejectionReason, cancellationToken);
            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });

        api.MapPost("/workflow/owner-registration", async (
            SubmitOwnerRegistrationRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var requestId = await repository.SubmitOwnerRegistrationRequestAsync(
                request.UserId,
                request.BusinessName,
                request.BusinessAddress,
                request.ContactPhone,
                request.Notes,
                cancellationToken);

            return Results.Ok(requestId);
        });

        api.MapGet("/workflow/owner-registration/latest/{userId:int}", async (
            int userId,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var request = await repository.GetLatestOwnerRegistrationRequestForUserAsync(userId, cancellationToken);
            return request is null ? Results.NotFound() : Results.Ok(ToOwnerRegistrationRequestDto(request));
        });

        api.MapPost("/workflow/owner-registration/{userId:int}/cancel", async (
            int userId,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.CancelPendingOwnerRegistrationRequestAsync(userId, cancellationToken);
            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });

        api.MapGet("/workflow/owner-registration/pending", async (
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var requests = await repository.GetPendingOwnerRegistrationRequestsAsync(cancellationToken);
            return Results.Ok(requests.Select(ToOwnerRegistrationRequestDto).ToList());
        });

        api.MapPost("/workflow/owner-registration/{requestId:int}/approve", async (
            int requestId,
            ApproveRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.ApproveOwnerRegistrationAsync(requestId, request.AdminUserId, cancellationToken);
            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });

        api.MapPost("/workflow/owner-registration/{requestId:int}/reject", async (
            int requestId,
            RejectRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.RejectOwnerRegistrationAsync(requestId, request.AdminUserId, request.RejectionReason, cancellationToken);
            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });

        api.MapPost("/workflow/owner-poi", async (
            SubmitOwnerPoiRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var poiId = await repository.SubmitOwnerPoiAsync(
                request.OwnerUserId,
                request.Type,
                request.Latitude,
                request.Longitude,
                request.ActivationRadius,
                request.Priority,
                request.QrCodeId,
                request.BaseLanguageCode,
                request.LocationName,
                request.Description,
                request.ImageUrl,
                request.AudioFileUrl,
                request.TtsScript,
                cancellationToken);

            return Results.Ok(poiId);
        });

        api.MapGet("/workflow/owner-poi/{ownerUserId:int}", async (
            int ownerUserId,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var pois = await repository.GetOwnerPoisAsync(ownerUserId, cancellationToken);
            return Results.Ok(pois.Select(ToPoiDto).ToList());
        });

        api.MapPut("/workflow/owner-poi/{ownerUserId:int}/{poiId}", async (
            int ownerUserId,
            string poiId,
            UpdateOwnerPoiBasicInfoRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.UpdateOwnerPoiBasicInfoAsync(
                ownerUserId,
                poiId,
                request.Type,
                request.Latitude,
                request.Longitude,
                request.ActivationRadius,
                request.Priority,
                request.QrCodeId,
                cancellationToken);

            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });

        api.MapGet("/workflow/owner-food/{ownerUserId:int}/{poiId}", async (
            int ownerUserId,
            string poiId,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var foodItems = await repository.GetOwnerFoodItemsAsync(ownerUserId, poiId, cancellationToken);
            return Results.Ok(foodItems.Select(ToFoodItemDto).ToList());
        });

        api.MapPost("/workflow/owner-food", async (
            SaveOwnerFoodItemRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var foodItemId = await repository.SaveOwnerFoodItemAsync(
                request.OwnerUserId,
                request.PoiId,
                request.FoodItemId,
                request.Name,
                request.Description,
                request.Price,
                request.Currency,
                request.IsAvailable,
                request.DisplayOrder,
                cancellationToken);

            return Results.Ok(foodItemId);
        });

        api.MapDelete("/workflow/owner-food/{ownerUserId:int}/{poiId}/{foodItemId:int}", async (
            int ownerUserId,
            string poiId,
            int foodItemId,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.DeleteOwnerFoodItemAsync(ownerUserId, poiId, foodItemId, cancellationToken);
            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });

        api.MapGet("/workflow/pois/pending", async (
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var pois = await repository.GetPendingPoisAsync(cancellationToken);
            return Results.Ok(pois.Select(ToPoiDto).ToList());
        });

        api.MapPost("/workflow/pois/{poiId}/approve", async (
            string poiId,
            ApproveRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.ApprovePoiAsync(poiId, request.AdminUserId, cancellationToken);
            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });

        api.MapPost("/workflow/pois/{poiId}/reject", async (
            string poiId,
            ApproveRequestDto request,
            IPoiWorkflowRepository repository,
            CancellationToken cancellationToken) =>
        {
            var result = await repository.RejectPoiAsync(poiId, request.AdminUserId, cancellationToken);
            return Results.Ok(new ApiResultDto(result.IsSuccess, result.Message));
        });
    }

    private static TimeSpan ResolveTourDuplicateWindow(string triggerType)
    {
        return string.Equals(triggerType, "route_arrival", StringComparison.OrdinalIgnoreCase)
            ? RouteArrivalDuplicateWindow
            : TourDuplicateWindow;
    }

    private static int GetLanguagePriority(string languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        return normalized switch
        {
            "vi" => 0,
            "en" => 1,
            _ => 2
        };
    }

    private static bool IsLanguageMatch(string? candidateLanguageCode, string expectedLanguageCode)
    {
        var normalizedCandidate = NormalizeLanguageCode(candidateLanguageCode);
        return string.Equals(normalizedCandidate, expectedLanguageCode, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var normalized = languageCode.Trim().ToLowerInvariant();

        if (normalized.StartsWith("en", StringComparison.Ordinal) || normalized.Contains("english", StringComparison.Ordinal))
        {
            return "en";
        }

        if (normalized.StartsWith("fr", StringComparison.Ordinal) || normalized.Contains("french", StringComparison.Ordinal))
        {
            return "fr";
        }

        if (normalized.StartsWith("vi", StringComparison.Ordinal) || normalized.Contains("viet", StringComparison.Ordinal))
        {
            return "vi";
        }

        return normalized;
    }

    private static string NormalizePoiId(string poiId)
    {
        return string.IsNullOrWhiteSpace(poiId)
            ? string.Empty
            : poiId.Trim().ToUpperInvariant();
    }

    private static string HashPassword(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(plainText.Trim());
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static LanguageDto ToLanguageDto(Language language)
    {
        return new LanguageDto(language.Id, language.LanguageCode, language.LanguageName);
    }

    private static UserSummaryDto? ToUserSummaryDto(User? user)
    {
        if (user is null)
        {
            return null;
        }

        return new UserSummaryDto(user.Id, user.UserName, user.DisplayName);
    }

    private static PoiTranslationDto ToPoiTranslationDto(POITranslation translation)
    {
        var languageCode = translation.Language?.LanguageCode ?? string.Empty;
        var languageName = translation.Language?.LanguageName ?? string.Empty;

        return new PoiTranslationDto(
            translation.Id,
            translation.PoiId,
            translation.LanguageId,
            languageCode,
            languageName,
            translation.LocationName,
            translation.Description,
            translation.ImageUrl,
            translation.AudioFileUrl,
            translation.TtsScript,
            translation.RichContentHtml);
    }

    private static PoiDto ToPoiDto(POI poi)
    {
        var translations = poi.PoiTranslations
            .Select(ToPoiTranslationDto)
            .OrderBy(x => x.LanguageCode)
            .ThenBy(x => x.Id)
            .ToList();

        return new PoiDto(
            poi.Id,
            poi.Type,
            poi.Latitude,
            poi.Longitude,
            poi.ActivationRadius,
            poi.Priority,
            poi.ApprovalStatus,
            poi.SubmittedUtc,
            poi.ReviewedUtc,
            poi.OwnerId,
            poi.ReviewedByAdminUserId,
            poi.QRCodeId,
            translations,
            ToUserSummaryDto(poi.Owner),
            ToUserSummaryDto(poi.ReviewedByAdminUser));
    }

    private static FoodItemDto ToFoodItemDto(FoodItem item)
    {
        return new FoodItemDto(
            item.Id,
            item.PoiId,
            item.OwnerId,
            item.Name,
            item.Description,
            item.Price,
            item.Currency,
            item.IsAvailable,
            item.DisplayOrder,
            item.CreatedUtc,
            item.UpdatedUtc,
            ToUserSummaryDto(item.Owner));
    }

    private static LanguageOwnershipRequestDto ToLanguageOwnershipRequestDto(LanguageOwnershipRequest request)
    {
        return new LanguageOwnershipRequestDto(
            request.Id,
            request.OwnerUserId,
            request.LanguageId,
            request.Status,
            request.RequestedUtc,
            request.ReviewedUtc,
            request.ReviewedByAdminUserId,
            request.RejectionReason,
            ToUserSummaryDto(request.OwnerUser),
            request.Language is null ? null : ToLanguageDto(request.Language),
            ToUserSummaryDto(request.ReviewedByAdminUser));
    }

    private static OwnerRegistrationRequestDto ToOwnerRegistrationRequestDto(OwnerRegistrationRequest request)
    {
        return new OwnerRegistrationRequestDto(
            request.Id,
            request.UserId,
            request.BusinessName,
            request.BusinessAddress,
            request.ContactPhone,
            request.Notes,
            request.Status,
            request.RequestedUtc,
            request.ReviewedUtc,
            request.ReviewedByAdminUserId,
            request.RejectionReason,
            request.ApprovedOwnerCode,
            ToUserSummaryDto(request.User),
            ToUserSummaryDto(request.ReviewedByAdminUser));
    }

    private static SubscriptionPlanDto ToSubscriptionPlanDto(SubscriptionPlan plan)
    {
        return new SubscriptionPlanDto(
            plan.Id,
            plan.PlanCode,
            plan.DisplayName,
            plan.Tier,
            plan.BillingPeriod,
            plan.FixedPrice,
            plan.Currency,
            plan.MaxAccessiblePoiCount,
            plan.MaxOwnerPoiCount,
            plan.MaxActivationRadiusMeters,
            plan.IsActive,
            plan.CreatedUtc,
            plan.UpdatedUtc);
    }

    private static OwnerPoiHeatmapDto ToOwnerPoiHeatmapDto(PoiHeatmapBucketSummary summary)
    {
        return new OwnerPoiHeatmapDto(
            summary.PoiId,
            summary.TourCount1Day,
            summary.TourCount7Days,
            summary.TourCount30Days);
    }
}