using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctors.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorConditionsAndMedicalServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows predate this column, so backfill them to an empty list
            // rather than leaving nulls in a NOT NULL column.
            migrationBuilder.AddColumn<List<string>>(
                name: "ConditionsTreated",
                table: "DoctorProfiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "MedicalServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AllowedVisitTypes = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalServices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalServices_DoctorProfileId",
                table: "MedicalServices",
                column: "DoctorProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicalServices");

            migrationBuilder.DropColumn(
                name: "ConditionsTreated",
                table: "DoctorProfiles");
        }
    }
}
