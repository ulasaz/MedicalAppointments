using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointments.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAvailabilityDayOfWeekWithDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_DoctorId_DayOfWeek",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "AvailabilitySlots");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "AvailabilitySlots",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_DoctorId_Date",
                table: "AvailabilitySlots",
                columns: new[] { "DoctorId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_DoctorId_Date",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "AvailabilitySlots");

            migrationBuilder.AddColumn<int>(
                name: "DayOfWeek",
                table: "AvailabilitySlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_DoctorId_DayOfWeek",
                table: "AvailabilitySlots",
                columns: new[] { "DoctorId", "DayOfWeek" });
        }
    }
}
