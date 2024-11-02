using Finbuckle.MultiTenant.EntityFrameworkCore;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.WebApi.Infrastructure.Persistence.Configuration;

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder
            .ToTable("Users", SchemaNames.Identity)
            .IsMultiTenant();

        builder
            .Property(u => u.ObjectId)
                .HasMaxLength(256);

        builder.Property(u => u.Address)
            .HasMaxLength(256);
    }
}

public class MedicalHistoryConfig : IEntityTypeConfiguration<MedicalHistory>
{
    public void Configure(EntityTypeBuilder<MedicalHistory> builder)
    {
        builder
            .ToTable("MedicalHistory", SchemaNames.Identity)
            .IsMultiTenant();

        builder
            .HasOne(b => b.PatientProfile)
            .WithOne(b => b.MedicalHistory)
            .HasForeignKey<MedicalHistory>(b => b.PatientProfileId);

        builder
            .Property(b => b.Note)
            .HasMaxLength(256);
    }
}

public class PatientFamilyConfig : IEntityTypeConfiguration<PatientFamily>
{
    public void Configure(EntityTypeBuilder<PatientFamily> builder)
    {
        builder
            .ToTable("PatientFamily", SchemaNames.Identity)
            .IsMultiTenant();

        builder
            .HasOne(b => b.PatientProfile)
            .WithOne(b => b.PatientFamily)
            .HasForeignKey<PatientFamily>(b => b. PatientProfileId);

        builder
            .Property(b => b.Email)
            .HasMaxLength(100);
    }
}

public class ApplicationRoleConfig : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder) =>
        builder
            .ToTable("Roles", SchemaNames.Identity)
            .IsMultiTenant()
                .AdjustUniqueIndexes();
}

public class ApplicationRoleClaimConfig : IEntityTypeConfiguration<ApplicationRoleClaim>
{
    public void Configure(EntityTypeBuilder<ApplicationRoleClaim> builder) =>
        builder
            .ToTable("RoleClaims", SchemaNames.Identity)
            .IsMultiTenant();
}

public class IdentityUserRoleConfig : IEntityTypeConfiguration<IdentityUserRole<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> builder) =>
        builder
            .ToTable("UserRoles", SchemaNames.Identity)
            .IsMultiTenant();
}

public class IdentityUserClaimConfig : IEntityTypeConfiguration<IdentityUserClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> builder) =>
        builder
            .ToTable("UserClaims", SchemaNames.Identity)
            .IsMultiTenant();
}

public class IdentityUserLoginConfig : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> builder) =>
        builder
            .ToTable("UserLogins", SchemaNames.Identity)
            .IsMultiTenant();
}

public class IdentityUserTokenConfig : IEntityTypeConfiguration<IdentityUserToken<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> builder) =>
        builder
            .ToTable("UserTokens", SchemaNames.Identity)
            .IsMultiTenant();
}

public class DoctorProfileConfig : IEntityTypeConfiguration<DoctorProfile>
{
    public void Configure(EntityTypeBuilder<DoctorProfile> builder)
    {
        builder
            .ToTable("DoctorProfile", SchemaNames.Identity)
            .IsMultiTenant();

        builder
            .HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<DoctorProfile>("DoctorId");

        builder
            .Property(b => b.SeftDescription)
            .HasColumnType("text");
    }
}

public class PatientProfileConfig : IEntityTypeConfiguration<PatientProfile>
{
    public void Configure(EntityTypeBuilder<PatientProfile> builder)
    {
        builder
            .ToTable("PatientProfile", SchemaNames.Identity)
            .IsMultiTenant();

        builder
            .HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<PatientProfile>("UserId");
    }
}

public class WorkingCalendarConfig : IEntityTypeConfiguration<WorkingCalendar>
{
    public void Configure(EntityTypeBuilder<WorkingCalendar> builder)
    {
        builder
            .ToTable("WorkingCalendar", SchemaNames.Identity)
            .IsMultiTenant();

        builder
            .HasOne<DoctorProfile>()
            .WithMany()
            .HasForeignKey("DoctorId");

        builder
            .HasOne<Appointment>()
            .WithOne()
            .HasForeignKey<WorkingCalendar>("AppointmentId");
    }
}