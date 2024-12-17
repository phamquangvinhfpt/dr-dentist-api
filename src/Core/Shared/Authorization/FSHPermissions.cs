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
    public const string Appointment = nameof(Appointment);
    public const string MedicalHistory = nameof(MedicalHistory);
    public const string MedicalRecord = nameof(MedicalRecord);
    public const string ContactInformation = nameof(ContactInformation);
    public const string Feedback = nameof(Feedback);
    public const string PatientMessages = nameof(PatientMessages);
    public const string Diagnosis = nameof(Diagnosis);
    public const string GeneralExamination = nameof(GeneralExamination);
    public const string Indication = nameof(Indication);
    public const string PatientImage = nameof(PatientImage);
    public const string PatientFamily = nameof(PatientFamily);
    public const string Payment = nameof(Payment);
    public const string Procedure = nameof(Procedure);
    public const string ServiceProcedures = nameof(ServiceProcedures);
    public const string Service = nameof(Service);
    public const string Prescription = nameof(Prescription);
    public const string PrescriptionItem = nameof(PrescriptionItem);
    public const string TreatmentPlanProcedures = nameof(TreatmentPlanProcedures);
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
        new("View Users", FSHAction.View, FSHResource.Users, new[] { ROOT, PATIENT, STAFF, DENTIST }),
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
        new("Export files", FSHAction.Export, FSHResource.Files, new[] { ROOT, ADMIN }),

        // NOTIFICATIONS
        new("Send Notifications", FSHAction.Create, FSHResource.Notifications,  new[] { ROOT, DENTIST, STAFF, PATIENT }),

        // AUDIT LOGS
        new("View AuditLogs", FSHAction.View, FSHResource.AuditLogs, new[] { ROOT }),

        // Appointment
        new("View Appointment", FSHAction.View, FSHResource.Appointment, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Appointment", FSHAction.Create, FSHResource.Appointment, new[] { ROOT, PATIENT, STAFF }),
        new("Update Appointment", FSHAction.Update, FSHResource.Appointment, new[] { ROOT, PATIENT, STAFF }),
        new("Delete Appointment", FSHAction.Delete, FSHResource.Appointment, new[] { ROOT, PATIENT, STAFF }),
        new("Search Appointment", FSHAction.Search, FSHResource.Appointment, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Medical History
        new("View Medical History", FSHAction.View, FSHResource.MedicalHistory, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Medical History", FSHAction.Create, FSHResource.MedicalHistory, new[] { DENTIST }),
        new("Update Medical History", FSHAction.Update, FSHResource.MedicalHistory, new[] { DENTIST }),
        new("Delete Medical History", FSHAction.Delete, FSHResource.MedicalHistory, new[] { ROOT, STAFF }),
        new("Search Medical History", FSHAction.Search, FSHResource.MedicalHistory, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Medical Record
        new("View Medical Record", FSHAction.View, FSHResource.MedicalRecord, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Medical Record", FSHAction.Create, FSHResource.MedicalRecord, new[] { DENTIST }),
        new("Update Medical Record", FSHAction.Update, FSHResource.MedicalRecord, new[] { DENTIST }),
        new("Delete Medical Record", FSHAction.Delete, FSHResource.MedicalRecord, new[] { ROOT, STAFF }),
        new("Search Medical Record", FSHAction.Search, FSHResource.MedicalRecord, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Contact Information
        new("View Contact Information", FSHAction.View, FSHResource.ContactInformation, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Contact Information", FSHAction.Create, FSHResource.ContactInformation, new[] { ROOT }),
        new("Update Contact Information", FSHAction.Update, FSHResource.ContactInformation, new[] { ROOT, STAFF }),
        new("Delete Contact Information", FSHAction.Delete, FSHResource.ContactInformation, new[] {ROOT }),

        // Feedback
        new("View Feedback", FSHAction.View, FSHResource.Feedback, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Feedback", FSHAction.Create, FSHResource.Feedback, new[] { PATIENT }),
        new("Update Feedback", FSHAction.Update, FSHResource.Feedback, new[] { PATIENT }),
        new("Delete Feedback", FSHAction.Delete, FSHResource.Feedback, new[] { PATIENT }),
        new("Search Feedback", FSHAction.Search, FSHResource.Feedback, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Patient Messages
        new("View Patient Messages", FSHAction.View, FSHResource.PatientMessages, new[] { PATIENT, STAFF, DENTIST }),
        new("Create Patient Messages", FSHAction.Create, FSHResource.PatientMessages, new[] { PATIENT, STAFF }),
        new("Update Patient Messages", FSHAction.Update, FSHResource.PatientMessages, new[] { PATIENT, STAFF }),
        new("Delete Patient Messages", FSHAction.Delete, FSHResource.PatientMessages, new[] { PATIENT, STAFF }),
        new("Search Patient Messages", FSHAction.Search, FSHResource.PatientMessages, new[] { PATIENT, STAFF }),

        // Diagnosis
        new("View Diagnosis", FSHAction.View, FSHResource.Diagnosis, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Diagnosis", FSHAction.Create, FSHResource.Diagnosis, new[] { DENTIST }),
        new("Update Diagnosis", FSHAction.Update, FSHResource.Diagnosis, new[] { DENTIST }),
        new("Delete Diagnosis", FSHAction.Delete, FSHResource.Diagnosis, new[] { DENTIST }),
        new("Search Diagnosis", FSHAction.Search, FSHResource.Diagnosis, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // General Examination
        new("View General Examination", FSHAction.View, FSHResource.GeneralExamination, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create General Examination", FSHAction.Create, FSHResource.GeneralExamination, new[] { DENTIST }),
        new("Update General Examination", FSHAction.Update, FSHResource.GeneralExamination, new[] { DENTIST }),
        new("Delete General Examination", FSHAction.Delete, FSHResource.GeneralExamination, new[] { ROOT }),
        new("Search General Examination", FSHAction.Search, FSHResource.GeneralExamination, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Indication
        new("View Indication", FSHAction.View, FSHResource.Indication, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Indication", FSHAction.Create, FSHResource.Indication, new[] { DENTIST }),
        new("Update Indication", FSHAction.Update, FSHResource.Indication, new[] { DENTIST }),
        new("Delete Indication", FSHAction.Delete, FSHResource.Indication, new[] { DENTIST }),
        new("Search Indication", FSHAction.Search, FSHResource.Indication, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Patient Image
        new("View Patient Image", FSHAction.View, FSHResource.PatientImage, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Patient Image", FSHAction.Create, FSHResource.PatientImage, new[] { DENTIST }),
        new("Update Patient Image", FSHAction.Update, FSHResource.PatientImage, new[] { DENTIST }),
        new("Delete Patient Image", FSHAction.Delete, FSHResource.PatientImage, new[] { DENTIST }),
        new("Search Patient Image", FSHAction.Search, FSHResource.PatientImage, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Patient Family
        new("View Patient Family", FSHAction.View, FSHResource.PatientFamily, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Patient Family", FSHAction.Create, FSHResource.PatientFamily, new[] { ROOT, PATIENT, STAFF }),
        new("Update Patient Family", FSHAction.Update, FSHResource.PatientFamily, new[] { ROOT, PATIENT, STAFF }),
        new("Delete Patient Family", FSHAction.Delete, FSHResource.PatientFamily, new[] { ROOT, PATIENT, STAFF }),
        new("Search Patient Family", FSHAction.Search, FSHResource.PatientFamily, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Payment
        new("View Payment", FSHAction.View, FSHResource.Payment, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Payment", FSHAction.Create, FSHResource.Payment, new[] { ROOT, PATIENT, STAFF }),
        new("Update Payment", FSHAction.Update, FSHResource.Payment, new[] { ROOT, PATIENT, STAFF }),
        new("Delete Payment", FSHAction.Delete, FSHResource.Payment, new[] { ROOT, STAFF }),
        new("Search Payment", FSHAction.Search, FSHResource.Payment, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Procedure
        new("View Procedure", FSHAction.View, FSHResource.Procedure, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Procedure", FSHAction.Create, FSHResource.Procedure, new[] { DENTIST }),
        new("Update Procedure", FSHAction.Update, FSHResource.Procedure, new[] { DENTIST }),
        new("Delete Procedure", FSHAction.Delete, FSHResource.Procedure, new[] { DENTIST }),
        new("Search Procedure", FSHAction.Search, FSHResource.Procedure, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Service
        new("View Service", FSHAction.View, FSHResource.Service, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Service", FSHAction.Create, FSHResource.Service, new[] { ROOT }),
        new("Update Service", FSHAction.Update, FSHResource.Service, new[] { ROOT }),
        new("Delete Service", FSHAction.Delete, FSHResource.Service, new[] { ROOT }),
        new("Search Service", FSHAction.Search, FSHResource.Service, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Service Procedures
        new("View Service Procedures", FSHAction.View, FSHResource.ServiceProcedures, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Service Procedures", FSHAction.Create, FSHResource.ServiceProcedures, new[] { ROOT }),
        new("Update Service Procedures", FSHAction.Update, FSHResource.ServiceProcedures, new[] { ROOT }),
        new("Delete Service Procedures", FSHAction.Delete, FSHResource.ServiceProcedures, new[] { ROOT }),
        new("Search Service Procedures", FSHAction.Search, FSHResource.ServiceProcedures, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Prescription
        new("View Prescription", FSHAction.View, FSHResource.Prescription, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Prescription", FSHAction.Create, FSHResource.Prescription, new[] { DENTIST }),
        new("Update Prescription", FSHAction.Update, FSHResource.Prescription, new[] { DENTIST }),
        new("Delete Prescription", FSHAction.Delete, FSHResource.Prescription, new[] { DENTIST }),
        new("Search Prescription", FSHAction.Search, FSHResource.Prescription, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Prescription Item
        new("View Prescription Item", FSHAction.View, FSHResource.PrescriptionItem, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Prescription Item", FSHAction.Create, FSHResource.PrescriptionItem, new[] { DENTIST }),
        new("Update Prescription Item", FSHAction.Update, FSHResource.PrescriptionItem, new[] { DENTIST }),
        new("Delete Prescription Item", FSHAction.Delete, FSHResource.PrescriptionItem, new[] { DENTIST }),
        new("Search Prescription Item", FSHAction.Search, FSHResource.PrescriptionItem, new[] { ROOT, PATIENT, STAFF, DENTIST }),

        // Treatment Plan Procedures
        new("View Treatment Plan Procedures", FSHAction.View, FSHResource.TreatmentPlanProcedures, new[] { ROOT, PATIENT, STAFF, DENTIST }),
        new("Create Treatment Plan Procedures", FSHAction.Create, FSHResource.TreatmentPlanProcedures, new[] { DENTIST }),
        new("Update Treatment Plan Procedures", FSHAction.Update, FSHResource.TreatmentPlanProcedures, new[] { DENTIST }),
        new("Delete Treatment Plan Procedures", FSHAction.Delete, FSHResource.TreatmentPlanProcedures, new[] { DENTIST }),
        new("Search Treatment Plan Procedures", FSHAction.Search, FSHResource.TreatmentPlanProcedures, new[] { ROOT, PATIENT, STAFF, DENTIST }),
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
