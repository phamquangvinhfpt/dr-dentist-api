using Finbuckle.MultiTenant.EntityFrameworkCore;
using FSH.WebApi.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FSH.WebApi.Infrastructure.Persistence.Configuration;

public class AppointmentConfig : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder
              .ToTable("Appointment", SchemaNames.Treatment)
              .IsMultiTenant();

        builder
            .Property(b => b.Notes)
                .HasMaxLength(256);
    }
}