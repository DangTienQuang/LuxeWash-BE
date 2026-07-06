using System;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    [DbContext(typeof(AutoWashDbContext))]
    [Migration("20260706040000_AddMaterialUnitsMasterData")]
    public partial class AddMaterialUnitsMasterData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "Materials",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "MaterialUnits",
                columns: table => new
                {
                    UnitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    MeasurementType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialUnits", x => x.UnitId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialUnits_Code",
                table: "MaterialUnits",
                column: "Code",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO MaterialUnits (Code, DisplayName, MeasurementType, IsActive, CreatedAt)
                VALUES
                    ('milliliter', 'Milliliter', 'Volume', TRUE, UTC_TIMESTAMP(6)),
                    ('liter', 'Liter', 'Volume', TRUE, UTC_TIMESTAMP(6)),
                    ('gram', 'Gram', 'Weight', TRUE, UTC_TIMESTAMP(6)),
                    ('kilogram', 'Kilogram', 'Weight', TRUE, UTC_TIMESTAMP(6)),
                    ('piece', 'Piece', 'Count', TRUE, UTC_TIMESTAMP(6));
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialUnits");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "Materials",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);
        }
    }
}
