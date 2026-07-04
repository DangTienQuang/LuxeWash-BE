using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryOperationalControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingMaterialUsages_MaterialBatches_MaterialBatchId",
                table: "BookingMaterialUsages");

            migrationBuilder.AddColumn<string>(
                name: "ConsumptionType",
                table: "Materials",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PerBooking")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SourceMaterialBatchId",
                table: "MaterialBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeStock",
                table: "Branches",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "NegativeStockLimit",
                table: "Branches",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MaterialBatchId",
                table: "BookingMaterialUsages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedUnitCost",
                table: "BookingMaterialUsages",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCostPending",
                table: "BookingMaterialUsages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ExtraMaterialUsageRequests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    StaffUserId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedByManagerId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ManagerNote = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraMaterialUsageRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_ExtraMaterialUsageRequests_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtraMaterialUsageRequests_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtraMaterialUsageRequests_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "MaterialId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtraMaterialUsageRequests_Users_ReviewedByManagerId",
                        column: x => x.ReviewedByManagerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExtraMaterialUsageRequests_Users_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialBatches_SourceMaterialBatchId",
                table: "MaterialBatches",
                column: "SourceMaterialBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraMaterialUsageRequests_BookingId",
                table: "ExtraMaterialUsageRequests",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraMaterialUsageRequests_BranchId_Status_CreatedAt",
                table: "ExtraMaterialUsageRequests",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraMaterialUsageRequests_MaterialId",
                table: "ExtraMaterialUsageRequests",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraMaterialUsageRequests_ReviewedByManagerId",
                table: "ExtraMaterialUsageRequests",
                column: "ReviewedByManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraMaterialUsageRequests_StaffUserId",
                table: "ExtraMaterialUsageRequests",
                column: "StaffUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingMaterialUsages_MaterialBatches_MaterialBatchId",
                table: "BookingMaterialUsages",
                column: "MaterialBatchId",
                principalTable: "MaterialBatches",
                principalColumn: "MaterialBatchId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialBatches_MaterialBatches_SourceMaterialBatchId",
                table: "MaterialBatches",
                column: "SourceMaterialBatchId",
                principalTable: "MaterialBatches",
                principalColumn: "MaterialBatchId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingMaterialUsages_MaterialBatches_MaterialBatchId",
                table: "BookingMaterialUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialBatches_MaterialBatches_SourceMaterialBatchId",
                table: "MaterialBatches");

            migrationBuilder.DropTable(
                name: "ExtraMaterialUsageRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialBatches_SourceMaterialBatchId",
                table: "MaterialBatches");

            migrationBuilder.DropColumn(
                name: "ConsumptionType",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "SourceMaterialBatchId",
                table: "MaterialBatches");

            migrationBuilder.DropColumn(
                name: "AllowNegativeStock",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "NegativeStockLimit",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "EstimatedUnitCost",
                table: "BookingMaterialUsages");

            migrationBuilder.DropColumn(
                name: "IsCostPending",
                table: "BookingMaterialUsages");

            migrationBuilder.AlterColumn<int>(
                name: "MaterialBatchId",
                table: "BookingMaterialUsages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingMaterialUsages_MaterialBatches_MaterialBatchId",
                table: "BookingMaterialUsages",
                column: "MaterialBatchId",
                principalTable: "MaterialBatches",
                principalColumn: "MaterialBatchId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
