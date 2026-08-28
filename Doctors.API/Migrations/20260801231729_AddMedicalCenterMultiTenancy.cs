using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctors.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicalCenterMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-existing rows (from before multitenancy existed) are backfilled to the
            // platform's well-known default tenant (see Identity.API's DefaultTenantSeeder)
            // so they aren't silently hidden behind Finbuckle's tenant query filter.
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MedicalServices",
                type: "text",
                nullable: false,
                defaultValue: "00000000-0000-0000-0000-000000000001");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "DoctorProfiles",
                type: "text",
                nullable: false,
                defaultValue: "00000000-0000-0000-0000-000000000001");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MedicalServices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DoctorProfiles");
        }
    }
}
