using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoWashPro.DAL.Migrations
{
    /// <inheritdoc />
    public partial class BookingMaterialUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookingId1",
                table: "BookingMaterialUsages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AppliedVoucherId",
                table: "Bookings",
                column: "AppliedVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingMaterialUsages_BookingId1",
                table: "BookingMaterialUsages",
                column: "BookingId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingMaterialUsages_Bookings_BookingId1",
                table: "BookingMaterialUsages",
                column: "BookingId1",
                principalTable: "Bookings",
                principalColumn: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Vouchers_AppliedVoucherId",
                table: "Bookings",
                column: "AppliedVoucherId",
                principalTable: "Vouchers",
                principalColumn: "VoucherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingMaterialUsages_Bookings_BookingId1",
                table: "BookingMaterialUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Vouchers_AppliedVoucherId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_AppliedVoucherId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_BookingMaterialUsages_BookingId1",
                table: "BookingMaterialUsages");

            migrationBuilder.DropColumn(
                name: "BookingId1",
                table: "BookingMaterialUsages");
        }
    }
}
