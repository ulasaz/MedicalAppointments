using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointments.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilitySlotAllowedVisitTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows predate the concept of a per-window restriction, so backfill them
            // to "both types allowed" — the behavior they already had before this column existed.
            migrationBuilder.AddColumn<int[]>(
                name: "AllowedVisitTypes",
                table: "AvailabilitySlots",
                type: "integer[]",
                nullable: false,
                defaultValue: new[] { 0, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedVisitTypes",
                table: "AvailabilitySlots");
        }
    }
}
