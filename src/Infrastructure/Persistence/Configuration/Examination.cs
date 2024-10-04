using Finbuckle.MultiTenant.EntityFrameworkCore;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Domain.Treatment;
using FSH.WebApi.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Namotion.Reflection;
namespace FSH.WebApi.Infrastructure.Persistence.Configuration;

public class GeneralExaminationConfig : IEntityTypeConfiguration<GeneralExamination>
{
    public void Configure(EntityTypeBuilder<GeneralExamination> builder)
    {
        builder
              .ToTable("GeneralExamination", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .HasOne(b => b.Appointment)
            .WithOne(b => b.GeneralExamination)
            .HasForeignKey<GeneralExamination>(b => b.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(b => b.Indications)
            .WithOne(b => b.GeneralExamination)
            .HasForeignKey(tp => tp.GeneralExaminationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(b => b.Diagnoses)
            .WithOne(b => b.GeneralExamination)
            .HasForeignKey(tp => tp.GeneralExaminationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(tp => tp.Payment)
            .WithOne(ge => ge.GeneralExamination)
            .HasForeignKey<Payment>(tp => tp.GeneralExaminationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(tp => tp.Prescription)
            .WithOne(ge => ge.GeneralExamination)
            .HasForeignKey<Prescription>(tp => tp.GeneralExaminationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.DentistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Property(b => b.TreatmentPlanNotes)
                .HasMaxLength(256);

        builder
            .Property(b => b.ExamContent)
                .HasMaxLength(256);
    }
}

public class IndicationConfig : IEntityTypeConfiguration<Indication>
{
    public void Configure(EntityTypeBuilder<Indication> builder)
    {
        builder
              .ToTable("Indication", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .HasMany(b => b.Images)
            .WithOne(b => b.Indication)
            .HasForeignKey(b => b.IndicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Property(b => b.IndicationType)
            .HasColumnType("text[]");

        builder
            .Property(b => b.Description)
                .HasMaxLength(256);
    }
}

public class DiagnosisConfig : IEntityTypeConfiguration<Diagnosis>
{
    public void Configure(EntityTypeBuilder<Diagnosis> builder)
    {
        builder
              .ToTable("Diagnosis", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .HasOne<GeneralExamination>()
            .WithMany(b => b.Diagnoses)
            .HasForeignKey(b => b.GeneralExaminationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Property(b => b.TeethConditions)
            .HasColumnType("text[]");
    }
}

public class ServicesConfig : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder
              .ToTable("Service", SchemaNames.Service)
              .IsMultiTenant();

        builder
            .Property(b => b.ServiceName)
                .HasMaxLength(256);

        builder
            .Property(b => b.ServiceDescription)
                .HasMaxLength(256);
    }
}

public class ProceduresConfig : IEntityTypeConfiguration<Procedure>
{
    public void Configure(EntityTypeBuilder<Procedure> builder)
    {
        builder
              .ToTable("Procedure", SchemaNames.Service)
              .IsMultiTenant();

        builder
            .Property(b => b.Name)
                .HasMaxLength(256);

        builder
            .Property(b => b.Description)
                .HasMaxLength(256);
    }
}

public class ServiceProceduresConfig : IEntityTypeConfiguration<ServiceProcedures>
{
    public void Configure(EntityTypeBuilder<ServiceProcedures> builder)
    {
        builder
            .ToTable("ServiceProcedures", SchemaNames.Service)
                .IsMultiTenant();

        builder
            .HasOne<Service>()
            .WithMany(b => b.ServiceProcedures)
            .HasForeignKey(b => b.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Procedure>()
            .WithMany()
            .HasForeignKey(b => b.ProcedureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TreatmentPlanProceduresConfig : IEntityTypeConfiguration<TreatmentPlanProcedures>
{
    public void Configure(EntityTypeBuilder<TreatmentPlanProcedures> builder)
    {
        builder
              .ToTable("TreatmentPlanProcedures", SchemaNames.Treatment)
              .IsMultiTenant();
        builder
            .HasOne<Diagnosis>()
            .WithMany(b => b.TreatmentPlanProcedures)
            .HasForeignKey(b => b.DiagnosisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Procedure>()
            .WithMany()
            .HasForeignKey(b => b.ProcedureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.RescheduledBy)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PrescriptionsConfig : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder
              .ToTable("Prescription", SchemaNames.Treatment)
        .IsMultiTenant();

        builder
            .Property(b => b.Notes)
                .HasMaxLength(256);
    }
}

public class PrescriptionItemsConfig : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder
              .ToTable("PrescriptionItem", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .HasOne<Prescription>()
            .WithMany(b => b.Items)
            .HasForeignKey(b => b.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PatientImageConfig : IEntityTypeConfiguration<PatientImage>
{
    public void Configure(EntityTypeBuilder<PatientImage> builder)
    {
        builder
              .ToTable("PatientImage", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .Property(b => b.ImageUrl)
                .HasMaxLength(256);
    }
}

public class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder
              .ToTable("Payment", SchemaNames.Payment)
              .IsMultiTenant();
    }
}

public class PatientMessagesConfig : IEntityTypeConfiguration<PatientMessages>
{
    public void Configure(EntityTypeBuilder<PatientMessages> builder)
    {
        builder
              .ToTable("PatientMessage", SchemaNames.CustomerService)
              .IsMultiTenant();

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.StaffId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Property(b => b.Message)
                .HasMaxLength(256);
    }
}

public class FeedbackConfig : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder
              .ToTable("Feedback", SchemaNames.CustomerService)
              .IsMultiTenant();

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Service>()
            .WithMany()
            .HasForeignKey(b => b.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Property(b => b.Message)
                .HasMaxLength(256);
    }
}

public class ContactInforConfig : IEntityTypeConfiguration<ContactInfor>
{
    public void Configure(EntityTypeBuilder<ContactInfor> builder)
    {
        builder
              .ToTable("ContactInfor", SchemaNames.CustomerService)
              .IsMultiTenant();
    }
}