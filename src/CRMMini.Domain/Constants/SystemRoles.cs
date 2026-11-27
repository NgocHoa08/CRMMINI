namespace CRMMini.Domain.Constants;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly string[] All = [Admin, Manager, Staff];
}
