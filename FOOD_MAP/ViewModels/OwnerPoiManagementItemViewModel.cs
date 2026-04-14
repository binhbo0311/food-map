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
        DateTimeOffset submittedUtc)
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
    }

    public string SubmittedText => SubmittedUtc.ToString("yyyy-MM-dd HH:mm");

    public string StatusText => $"{Type} | {ApprovalStatus}";
}
