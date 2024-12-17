using Finbuckle.MultiTenant.EntityFrameworkCore;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Domain.Treatment;
using FSH.WebApi.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FSH.WebApi.Infrastructure.Persistence.Configuration;

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
            .HasOne(b => b.MedicalRecord)
            .WithOne(b => b.Indication)
            .HasForeignKey<Indication>(b => b.RecordId)
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
            .HasOne<MedicalRecord>()
            .WithMany(p => p.Diagnosises)
            .HasForeignKey(b => b.RecordId);

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
        builder
            .HasOne<TypeService>()
            .WithMany()
            .HasForeignKey(p => p.TypeServiceID).IsRequired(false);
    }
}

public class TypeServiceConfig : IEntityTypeConfiguration<TypeService>
{
    public void Configure(EntityTypeBuilder<TypeService> builder)
    {
        builder
              .ToTable("TypeService", SchemaNames.Service)
              .IsMultiTenant();

        builder
            .Property(b => b.TypeName)
                .HasMaxLength(256);

        builder
            .Property(b => b.TypeDescription)
                .HasMaxLength(256);
    }
}

public class RoomConfig : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder
              .ToTable("Room", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .Property(b => b.RoomName)
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
            .HasOne(b => b.Appointment)
            .WithMany(b => b.TreatmentPlanProcedures)
            .HasForeignKey(b => b.AppointmentID)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.ServiceProcedure)
            .WithMany(b => b.TreatmentPlanProcedures)
            .HasForeignKey(b => b.ServiceProcedureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<DoctorProfile>()
            .WithMany(b => b.TreatmentPlanProcedures)
            .HasForeignKey(b => b.DoctorID)
            .IsRequired(false)
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

        builder
            .HasOne(b => b.TreatmentPlanProcedures)
            .WithOne(b => b.Prescription)
            .HasForeignKey<Prescription>(b => b.TreatmentID)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<PatientProfile>()
            .WithMany(b => b.Prescriptions)
            .HasForeignKey(b => b.PatientID).IsRequired(false);
        builder
            .HasOne<DoctorProfile>()
            .WithMany(b => b.Prescriptions)
            .HasForeignKey(b => b.DoctorID).IsRequired(false);
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

        builder
            .HasOne(b => b.Appointment)
            .WithOne(b => b.Payment)
            .HasForeignKey<Payment>(b => b.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(b => b.Service)
            .WithMany(b => b.Payments)
            .HasForeignKey(b => b.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.PatientProfile)
            .WithMany(b => b.Payments)
            .HasForeignKey(b => b.PatientProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentDetailConfig : IEntityTypeConfiguration<PaymentDetail>
{
    public void Configure(EntityTypeBuilder<PaymentDetail> builder)
    {
        builder
              .ToTable("PaymentDetail", SchemaNames.Payment)
              .IsMultiTenant();

        builder
            .HasOne(b => b.Payment)
            .WithMany(b => b.PaymentDetails)
            .HasForeignKey(b => b.PaymentID)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.Procedure)
            .WithMany(b => b.PaymentDetails)
            .HasForeignKey(b => b.ProcedureID)
            .OnDelete(DeleteBehavior.Cascade);
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
            .HasForeignKey(b => b.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.ReceiverId)
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
            .HasOne(b => b.PatientProfile)
            .WithMany(b => b.Feedbacks)
            .HasForeignKey(b => b.PatientProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.DoctorProfile)
            .WithMany(b => b.Feedbacks)
            .HasForeignKey(b => b.DoctorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Service>()
            .WithMany()
            .HasForeignKey(b => b.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Property(b => b.Message)
                .HasMaxLength(256);

        builder
            .HasOne<Appointment>()
            .WithOne()
            .HasForeignKey<Feedback>("AppointmentId").IsRequired(false);
    }
}

public class ContactInforConfig : IEntityTypeConfiguration<ContactInfor>
{
    public void Configure(EntityTypeBuilder<ContactInfor> builder)
    {
        builder
              .ToTable("ContactInfor", SchemaNames.CustomerService)
              .IsMultiTenant();

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.StaffId).IsRequired(false);

        builder
            .Property(b => b.ImageUrl).IsRequired(false);
    }
}

public class MedicalRecordConfig : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> builder)
    {
        builder
              .ToTable("MedicalRecord", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .HasOne(b => b.DoctorProfile)
            .WithMany(b => b.MedicalRecords)
            .HasForeignKey(b => b.DoctorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.PatientProfile)
            .WithMany()
            .HasForeignKey(b => b.PatientProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.Appointment)
            .WithOne(b => b.MedicalRecord)
            .HasForeignKey<MedicalRecord>(b => b.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BasicExaminationConfig : IEntityTypeConfiguration<BasicExamination>
{
    public void Configure(EntityTypeBuilder<BasicExamination> builder)
    {
        builder
              .ToTable("BasicExamination", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .HasOne(b => b.MedicalRecord)
            .WithOne(b => b.BasicExamination)
            .HasForeignKey<BasicExamination>(b => b.RecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransactionConfig : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder
            .ToTable("Transactions", SchemaNames.Payment)
            .IsMultiTenant();
    }
}