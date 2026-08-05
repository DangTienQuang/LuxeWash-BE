using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckInOutImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckInImageUrl",
                table: "FleetWashLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CheckOutImageUrl",
                table: "FleetWashLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CheckInImageUrl",
                table: "Bookings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CheckOutImageUrl",
                table: "Bookings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInImageUrl",
                table: "FleetWashLogs");

            migrationBuilder.DropColumn(
                name: "CheckOutImageUrl",
                table: "FleetWashLogs");

            migrationBuilder.DropColumn(
                name: "CheckInImageUrl",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CheckOutImageUrl",
                table: "Bookings");
        }
    }
}
