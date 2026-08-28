using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctors.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorProfilePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PriceOnlineCents",
                table: "DoctorProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceStationaryCents",
                table: "DoctorProfiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceOnlineCents",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "PriceStationaryCents",
                table: "DoctorProfiles");
        }
    }
}
