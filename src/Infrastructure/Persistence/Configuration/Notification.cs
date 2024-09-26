using Finbuckle.MultiTenant.EntityFrameworkCore;
using FSH.WebApi.Domain.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.WebApi.Infrastructure.Persistence.Configuration;

internal class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder
            .ToTable("Notification", SchemaNames.Notification)
            .IsMultiTenant();

        builder
            .Property(b => b.Title)
            .HasMaxLength(256);

        builder
            .Property(b => b.Message)
            .HasMaxLength(2048);

        builder
            .Property(b => b.Label)
            .HasConversion<string>()
            .HasColumnType("varchar(50)");

        builder
            .Property(b => b.IsRead)
            .HasDefaultValue(false);

        builder
            .Property(b => b.Url)
            .HasMaxLength(2048);
    }
}