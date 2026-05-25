namespace VictusLounge.Helpers;

public enum PcStatus
{
    Free,
    Busy,
    Reserved,
    Service
}

public enum BookingStatus
{
    Confirmed,
    PendingPayment,
    Cancelled
}

public enum SessionStatus
{
    Active,
    Closed,
    AwaitingPayment,
    Team
}

public enum UserRole
{
    Client,
    Admin,
    Owner
}

public static class PcStatuses
{
    public const string Free = "free";
    public const string Busy = "busy";
    public const string Reserved = "reserved";
    public const string Service = "service";
}

public static class BookingStatuses
{
    public const string Confirmed = "Confirmed";
    public const string PendingPayment = "PendingPayment";
    public const string Cancelled = "Cancelled";
}

public static class SessionStatuses
{
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Team = "Team";
}

public static class StatusMapper
{
    public static PcStatus ToPcStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            PcStatuses.Busy or "active" or "occupied" => PcStatus.Busy,
            PcStatuses.Reserved => PcStatus.Reserved,
            PcStatuses.Service or "maintenance" => PcStatus.Service,
            _ => PcStatus.Free
        };
    }

    public static string ToStorageValue(this PcStatus status)
    {
        return status switch
        {
            PcStatus.Busy => PcStatuses.Busy,
            PcStatus.Reserved => PcStatuses.Reserved,
            PcStatus.Service => PcStatuses.Service,
            _ => PcStatuses.Free
        };
    }

    public static BookingStatus ToBookingStatus(string? status)
    {
        return status switch
        {
            BookingStatuses.Confirmed => BookingStatus.Confirmed,
            BookingStatuses.Cancelled => BookingStatus.Cancelled,
            _ => BookingStatus.PendingPayment
        };
    }

    public static string ToStorageValue(this BookingStatus status)
    {
        return status switch
        {
            BookingStatus.Confirmed => BookingStatuses.Confirmed,
            BookingStatus.Cancelled => BookingStatuses.Cancelled,
            _ => BookingStatuses.PendingPayment
        };
    }

    public static SessionStatus ToSessionStatus(string? status)
    {
        return status switch
        {
            SessionStatuses.Closed => SessionStatus.Closed,
            SessionStatuses.AwaitingPayment => SessionStatus.AwaitingPayment,
            SessionStatuses.Team => SessionStatus.Team,
            _ => SessionStatus.Active
        };
    }

    public static string ToStorageValue(this SessionStatus status)
    {
        return status switch
        {
            SessionStatus.Closed => SessionStatuses.Closed,
            SessionStatus.AwaitingPayment => SessionStatuses.AwaitingPayment,
            SessionStatus.Team => SessionStatuses.Team,
            _ => SessionStatuses.Active
        };
    }

    public static UserRole ToUserRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "admin" => UserRole.Admin,
            "owner" => UserRole.Owner,
            _ => UserRole.Client
        };
    }

    public static string ToStorageValue(this UserRole role)
    {
        return role switch
        {
            UserRole.Admin => "Admin",
            UserRole.Owner => "Owner",
            _ => "Client"
        };
    }
}

public static class PaymentTypes
{
    public const string Bonus = "Bonus";
    public const string Card = "Card";
    public const string Cash = "Cash";
    public const string Online = "Online";
    public const string Pending = "Pending";
    public const string PendingErip = "PendingErip";
    public const string PendingCash = "PendingCash";
    public const string EventRegistration = "EventRegistration";
    public const string AdminLog = "AdminLog";
}
