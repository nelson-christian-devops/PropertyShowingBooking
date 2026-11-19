using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PropertyShowingBooking.Domain.Entities;

namespace PropertyShowingBooking.Infrastructure.Persistence.Configurations;

public sealed class BookingHistoryConfiguration : IEntityTypeConfiguration<BookingHistory>
{
    public void Configure(EntityTypeBuilder<BookingHistory> builder)
    {
        builder.ToTable("BookingHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.BookingId).IsRequired();

        builder.Property(x => x.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Actor)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Reason)
            .HasMaxLength(1000);

        builder.Property(x => x.ChangedAt)
            .IsRequired()
            .HasColumnType("TEXT")
            .HasConversion(new ValueConverter<DateTimeOffset, string>(
                value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(x => x.BookingId);
        builder.HasIndex(x => x.ChangedAt);
    }
}
