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
    Visit = 1
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

public enum LanguageOwnershipRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum SubscriptionTier
{
    Free = 0,
    Basic = 1,
    Premium = 2
}

public enum BillingPeriod
{
    Monthly = 0,
    Quarterly = 1,
    Yearly = 2
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Cancelled = 3,
    Refunded = 4
}

public enum PaymentProviderType
{
    Manual = 0,
    VnPay = 1,
    Momo = 2,
    Stripe = 3,
    Paypal = 4,
    ZaloPay = 5,
    Other = 99
}