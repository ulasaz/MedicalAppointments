using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctors.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorProfilePhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "DoctorProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoData",
                table: "DoctorProfiles",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "PhotoData",
                table: "DoctorProfiles");
        }
    }
}
