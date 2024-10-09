using System.Collections.ObjectModel;

namespace FSH.WebApi.Shared.Authorization;

public static class FSHAction
{
    public const string View = nameof(View);
    public const string Search = nameof(Search);
    public const string Create = nameof(Create);
    public const string Update = nameof(Update);
    public const string Delete = nameof(Delete);
    public const string Export = nameof(Export);
    public const string Generate = nameof(Generate);
    public const string Clean = nameof(Clean);
    public const string UpgradeSubscription = nameof(UpgradeSubscription);
    public const string Upload = nameof(Upload);
}

public static class FSHResource
{
    public const string Tenants = nameof(Tenants);
    public const string Dashboard = nameof(Dashboard);
    public const string Hangfire = nameof(Hangfire);
    public const string Users = nameof(Users);
    public const string UserRoles = nameof(UserRoles);
    public const string Roles = nameof(Roles);
    public const string RoleClaims = nameof(RoleClaims);
    public const string Files = nameof(Files);
    public const string Notifications = nameof(Notifications);
    public const string AuditLogs = nameof(AuditLogs);
}

public static class FSHPermissions
{
    private const string ROOT = nameof(ROOT);
    private const string ADMIN = nameof(ADMIN);
    private const string DENTIST = nameof(DENTIST);
    private const string STAFF = nameof(STAFF);
    private const string PATIENT = nameof(PATIENT);
    private const string GUEST = nameof(GUEST);

    private static readonly FSHPermission[] _all = new FSHPermission[]
    {
        new("View Dashboard", FSHAction.View, FSHResource.Dashboard),
        new("View Hangfire", FSHAction.View, FSHResource.Hangfire),

        // USERS
        new("View Users", FSHAction.View, FSHResource.Users),
        new("Search Users", FSHAction.Search, FSHResource.Users),
        new("Create Users", FSHAction.Create, FSHResource.Users),
        new("Update Users", FSHAction.Update, FSHResource.Users),
        new("Delete Users", FSHAction.Delete, FSHResource.Users),
        new("Export Users", FSHAction.Export, FSHResource.Users),

        // ROLES
        new("View UserRoles", FSHAction.View, FSHResource.UserRoles),
        new("Update UserRoles", FSHAction.Update, FSHResource.UserRoles),
        new("View Roles", FSHAction.View, FSHResource.Roles),
        new("Create Roles", FSHAction.Create, FSHResource.Roles),
        new("Update Roles", FSHAction.Update, FSHResource.Roles),
        new("Delete Roles", FSHAction.Delete, FSHResource.Roles),
        new("View RoleClaims", FSHAction.View, FSHResource.RoleClaims),
        new("Update RoleClaims", FSHAction.Update, FSHResource.RoleClaims),

        new("View Tenants", FSHAction.View, FSHResource.Tenants, new[] { ROOT }),
        new("Create Tenants", FSHAction.Create, FSHResource.Tenants, new[] { ROOT }),
        new("Update Tenants", FSHAction.Update, FSHResource.Tenants, new[] { ROOT }),
        new("Upgrade Tenant Subscription", FSHAction.UpgradeSubscription, FSHResource.Tenants, new[] { ROOT }),

        // FILES
        new("Upload files", FSHAction.Upload, FSHResource.Files, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // NOTIFICATIONS
        new("Send Notifications", FSHAction.Create, FSHResource.Notifications,  new[] { ROOT, STAFF, PATIENT }),

        // AUDIT LOGS
        new("View AuditLogs", FSHAction.View, FSHResource.AuditLogs),
    };

    public static IReadOnlyList<FSHPermission> All { get; } = new ReadOnlyCollection<FSHPermission>(_all);
    public static IReadOnlyList<FSHPermission> Root { get; } = new ReadOnlyCollection<FSHPermission>(_all.Where(p => p.role.Contains(ROOT)).ToArray());
    public static IReadOnlyList<FSHPermission> Admin { get; } = new ReadOnlyCollection<FSHPermission>(_all.Where(p => !p.role.Contains(ROOT)).ToArray());
    public static IReadOnlyList<FSHPermission> Dentist { get; } = new ReadOnlyCollection<FSHPermission>(_all.Where(p => p.role.Contains(DENTIST)).ToArray());
    public static IReadOnlyList<FSHPermission> Staff { get; } = new ReadOnlyCollection<FSHPermission>(_all.Where(p => p.role.Contains(STAFF)).ToArray());
    public static IReadOnlyList<FSHPermission> Patient { get; } = new ReadOnlyCollection<FSHPermission>(_all.Where(p => p.role.Contains(PATIENT)).ToArray());
    public static IReadOnlyList<FSHPermission> Guest { get; } = new ReadOnlyCollection<FSHPermission>(_all.Where(p => p.role.Contains(GUEST)).ToArray());

}

public record FSHPermission(string Description, string Action, string Resource, string[] role)
{
    public FSHPermission(string Description, string Action, string Resource)
        : this(Description, Action, Resource, Array.Empty<string>())
    {
    }

    public string Name => NameFor(Action, Resource);
    public static string NameFor(string action, string resource) => $"Permissions.{resource}.{action}";
}
