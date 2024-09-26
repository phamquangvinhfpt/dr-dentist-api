using System.Collections.ObjectModel;

namespace FSH.WebApi.Shared.Authorization;

public static class FSHRoles
{
    public const string Admin = nameof(Admin);
    public const string Dentist = nameof(Dentist);
    public const string Staff = nameof(Staff);
    public const string Patient = nameof(Patient);
    public const string Guest = nameof(Guest);

    public static IReadOnlyList<string> DefaultRoles { get; } = new ReadOnlyCollection<string>(new[]
    {
        Admin,
        Dentist,
        Staff,
        Patient,
        Guest
    });

    public static bool IsDefault(string roleName) => DefaultRoles.Any(r => r == roleName);
}