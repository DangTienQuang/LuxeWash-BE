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
    [Migration("20260703090000_SwitchInventoryToBranchManaged")]
    public partial class SwitchInventoryToBranchManaged : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialTransferRequestItems");

            migrationBuilder.DropTable(
                name: "MaterialTransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialBatches_MaterialBatches_SourceMaterialBatchId",
                table: "MaterialBatches");

            migrationBuilder.DropIndex(
                name: "IX_MaterialBatches_SourceMaterialBatchId",
                table: "MaterialBatches");

            migrationBuilder.DropColumn(
                name: "SourceMaterialBatchId",
                table: "MaterialBatches");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceMaterialBatchId",
                table: "MaterialBatches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaterialTransferRequests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    AdminNote = table.Column<string>(type: "longtext", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Note = table.Column<string>(type: "longtext", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialTransferRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_MaterialTransferRequests_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialTransferRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialTransferRequestItems",
                columns: table => new
                {
                    RequestItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    ApprovedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialTransferRequestItems", x => x.RequestItemId);
                    table.ForeignKey(
                        name: "FK_MaterialTransferRequestItems_MaterialTransferRequests_Reques~",
                        column: x => x.RequestId,
                        principalTable: "MaterialTransferRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialTransferRequestItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "MaterialId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialBatches_SourceMaterialBatchId",
                table: "MaterialBatches",
                column: "SourceMaterialBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTransferRequestItems_MaterialId",
                table: "MaterialTransferRequestItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTransferRequestItems_RequestId",
                table: "MaterialTransferRequestItems",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTransferRequests_BranchId",
                table: "MaterialTransferRequests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTransferRequests_RequestedByUserId",
                table: "MaterialTransferRequests",
                column: "RequestedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialBatches_MaterialBatches_SourceMaterialBatchId",
                table: "MaterialBatches",
                column: "SourceMaterialBatchId",
                principalTable: "MaterialBatches",
                principalColumn: "MaterialBatchId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
