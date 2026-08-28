using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicalCenterBrandingOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BannerImageContentType",
                table: "MedicalCenters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "BannerImageData",
                table: "MedicalCenters",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerVideoUrl",
                table: "MedicalCenters",
                type: "text",
                nullable: true);

            // Existing centers get the same defaults new ones get in code (MedicalCenter.cs),
            // so they aren't left with an empty string that fails the allow-list validation
            // the moment anyone tries to save their branding again.
            migrationBuilder.AddColumn<string>(
                name: "ButtonRadius",
                table: "MedicalCenters",
                type: "text",
                nullable: false,
                defaultValue: "pill");

            migrationBuilder.AddColumn<string>(
                name: "FontFamily",
                table: "MedicalCenters",
                type: "text",
                nullable: false,
                defaultValue: "Inter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BannerImageContentType",
                table: "MedicalCenters");

            migrationBuilder.DropColumn(
                name: "BannerImageData",
                table: "MedicalCenters");

            migrationBuilder.DropColumn(
                name: "BannerVideoUrl",
                table: "MedicalCenters");

            migrationBuilder.DropColumn(
                name: "ButtonRadius",
                table: "MedicalCenters");

            migrationBuilder.DropColumn(
                name: "FontFamily",
                table: "MedicalCenters");
        }
    }
}
