using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyShowingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RejectDuplicateShowingSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ShowingSlots_PropertyId_StartsAt_DurationMinutes",
                table: "ShowingSlots",
                columns: new[] { "PropertyId", "StartsAt", "DurationMinutes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShowingSlots_PropertyId_StartsAt_DurationMinutes",
                table: "ShowingSlots");
        }
    }
}
