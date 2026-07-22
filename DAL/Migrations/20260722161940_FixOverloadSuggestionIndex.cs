using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class FixOverloadSuggestionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OverloadSuggestions_Bookings_BookingId",
                table: "OverloadSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_OverloadSuggestions_BookingId",
                table: "OverloadSuggestions");

            migrationBuilder.CreateIndex(
                name: "IX_OverloadSuggestions_BookingId",
                table: "OverloadSuggestions",
                column: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_OverloadSuggestions_Bookings_BookingId",
                table: "OverloadSuggestions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "BookingId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // P2: Data may now contain multiple OverloadSuggestions per BookingId (one-to-many).
            // Rolling back to a UNIQUE index here would fail if duplicate data exists.
            // If rollback to one-to-one is absolutely required, you must implement data cleanup 
            // before creating the unique index:
            // migrationBuilder.Sql("DELETE FROM OverloadSuggestions WHERE ...");

            migrationBuilder.DropForeignKey(
                name: "FK_OverloadSuggestions_Bookings_BookingId",
                table: "OverloadSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_OverloadSuggestions_BookingId",
                table: "OverloadSuggestions");

            migrationBuilder.CreateIndex(
                name: "IX_OverloadSuggestions_BookingId",
                table: "OverloadSuggestions",
                column: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_OverloadSuggestions_Bookings_BookingId",
                table: "OverloadSuggestions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "BookingId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
