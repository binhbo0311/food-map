using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.ViewModels;

public sealed class OwnerPoiManagementItemViewModel
{
    public string Id { get; }

    public PoiType Type { get; }

    public PoiApprovalStatus ApprovalStatus { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public int ActivationRadius { get; }

    public int Priority { get; }

    public string? QrCodeId { get; }

    public string BaseLocationName { get; }

    public string BaseDescription { get; }

    public string BaseImageUrl { get; }

    public string BaseAudioFileUrl { get; }

    public string BaseTtsScript { get; }

    public DateTimeOffset SubmittedUtc { get; }

    public int TourCount1Day { get; }

    public int TourCount7Days { get; }

    public int TourCount30Days { get; }

    public OwnerPoiManagementItemViewModel(
        string id,
        PoiType type,
        PoiApprovalStatus approvalStatus,
        double latitude,
        double longitude,
        int activationRadius,
        int priority,
        string? qrCodeId,
        string baseLocationName,
        string baseDescription,
        string baseImageUrl,
        string baseAudioFileUrl,
        string baseTtsScript,
        DateTimeOffset submittedUtc,
        int tourCount1Day,
        int tourCount7Days,
        int tourCount30Days)
    {
        Id = id;
        Type = type;
        ApprovalStatus = approvalStatus;
        Latitude = latitude;
        Longitude = longitude;
        ActivationRadius = activationRadius;
        Priority = priority;
        QrCodeId = qrCodeId;
        BaseLocationName = baseLocationName;
        BaseDescription = baseDescription;
        BaseImageUrl = baseImageUrl;
        BaseAudioFileUrl = baseAudioFileUrl;
        BaseTtsScript = baseTtsScript;
        SubmittedUtc = submittedUtc;
        TourCount1Day = System.Math.Max(tourCount1Day, 0);
        TourCount7Days = System.Math.Max(tourCount7Days, 0);
        TourCount30Days = System.Math.Max(tourCount30Days, 0);
    }

    public string SubmittedText => SubmittedUtc.ToString("yyyy-MM-dd HH:mm");

    public string StatusText => $"{Type} | {ApprovalStatus}";

    public string HeatmapSummaryText => $"Tours 1d/7d/30d: {TourCount1Day}/{TourCount7Days}/{TourCount30Days}";
}
