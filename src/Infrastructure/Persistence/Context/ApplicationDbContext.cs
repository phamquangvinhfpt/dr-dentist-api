using Finbuckle.MultiTenant;
using FSH.WebApi.Application.Common.Events;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Notification;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Domain.Treatment;
using FSH.WebApi.Infrastructure.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.WebApi.Infrastructure.Persistence.Context;

public class ApplicationDbContext : BaseDbContext
{
    public ApplicationDbContext(ITenantInfo currentTenant, DbContextOptions options, ICurrentUser currentUser, ISerializerService serializer, IOptions<DatabaseSettings> dbSettings, IEventPublisher events)
        : base(currentTenant, options, currentUser, serializer, dbSettings, events)
    {
    }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<GeneralExamination> GeneralExaminations { get; set; }
    public DbSet<Indication> Indications { get; set; }
    public DbSet<Diagnosis> Diagnoses { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Procedure> Procedures { get; set; }
    public DbSet<ServiceProcedures> ServiceProcedures { get; set; }
    public DbSet<TreatmentPlan> TreatmentPlans { get; set; }
    public DbSet<TreatmentPlanProcedures> TreatmentPlanProcedures { get; set; }
    public DbSet<Prescription> Prescriptions { get; set; }
    public DbSet<PrescriptionItem> PrescriptionItems { get; set; }
    public DbSet<PatientImage> PatientImages { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PatientMessages> PatientMessages { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }
    public DbSet<ContactInfor> ContactInfor { get; set; }
    public DbSet<PatientFamily> PatientFamilys { get; set; }
    public DbSet<MedicalHistory> MedicalHistorys { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        _ = modelBuilder.HasDefaultSchema(SchemaNames.Catalog);
    }
}