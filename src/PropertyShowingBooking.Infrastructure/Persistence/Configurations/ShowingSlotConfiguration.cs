using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PropertyShowingBooking.Domain.Entities;

namespace PropertyShowingBooking.Infrastructure.Persistence.Configurations;

public sealed class ShowingSlotConfiguration : IEntityTypeConfiguration<ShowingSlot>
{
    public void Configure(EntityTypeBuilder<ShowingSlot> builder)
    {
        builder.ToTable("ShowingSlots");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PropertyId).IsRequired();
        builder.Property(x => x.StartsAt)
            .IsRequired()
            .HasColumnType("TEXT")
            .HasConversion(new ValueConverter<DateTimeOffset, string>(
                value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        builder.Property(x => x.DurationMinutes).IsRequired();
        builder.Property(x => x.Capacity).IsRequired();
        builder.Property(x => x.BookedSeats).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("TEXT");

        builder.Ignore(x => x.AvailableSeats);

        builder.HasIndex(x => x.PropertyId);
        builder.HasIndex(x => x.StartsAt);
        builder.HasIndex(x => new { x.PropertyId, x.StartsAt });
        builder.HasIndex(x => new { x.PropertyId, x.StartsAt, x.DurationMinutes })
            .IsUnique();
    }
}
