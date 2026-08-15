using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AllowEmptyShiftSwap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.AlterColumn<int>(
                name: "ToAssignmentId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "ToWorkDate",
                table: "ShiftSwapRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToWorkShiftId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequests_ToWorkShiftId",
                table: "ShiftSwapRequests",
                column: "ToWorkShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSwapRequests_WorkShifts_ToWorkShiftId",
                table: "ShiftSwapRequests",
                column: "ToWorkShiftId",
                principalTable: "WorkShifts",
                principalColumn: "WorkShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSwapRequests_WorkShifts_ToWorkShiftId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSwapRequests_ToWorkShiftId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropColumn(
                name: "ToWorkDate",
                table: "ShiftSwapRequests");

            migrationBuilder.DropColumn(
                name: "ToWorkShiftId",
                table: "ShiftSwapRequests");

            migrationBuilder.AlterColumn<int>(
                name: "ToAssignmentId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);


        }
    }
}
