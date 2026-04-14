namespace FOOD_MAP.Shared.Models;

public enum UserRole
{
    User = 0,
    Owner = 1,
    Admin = 2
}

public enum PoiType
{
    Food = 0,
    Visit = 1,
    StayIn = 2
}

public enum PoiApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum OwnerRegistrationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}