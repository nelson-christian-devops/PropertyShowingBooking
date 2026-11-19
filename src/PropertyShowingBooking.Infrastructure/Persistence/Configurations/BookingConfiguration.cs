using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Infrastructure.Persistence.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SlotId).IsRequired();

        builder.Property(x => x.RenterEmail)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(x => x.PartySize).IsRequired();
        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("TEXT");
        builder.Property(x => x.UpdatedAt).IsRequired().HasColumnType("TEXT");

        builder.HasOne<ShowingSlot>()
            .WithMany()
            .HasForeignKey(x => x.SlotId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(x => x.SlotId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.SlotId, x.RenterEmail })
            .IsUnique()
            .HasFilter("[Status] = 'Confirmed'");
    }
}
