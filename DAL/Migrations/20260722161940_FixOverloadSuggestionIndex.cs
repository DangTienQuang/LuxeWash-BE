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
            migrationBuilder.DropIndex(
                name: "IX_OverloadSuggestions_BookingId",
                table: "OverloadSuggestions");

            migrationBuilder.CreateIndex(
                name: "IX_OverloadSuggestions_BookingId",
                table: "OverloadSuggestions",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OverloadSuggestions_BookingId",
                table: "OverloadSuggestions");

            migrationBuilder.CreateIndex(
                name: "IX_OverloadSuggestions_BookingId",
                table: "OverloadSuggestions",
                column: "BookingId",
                unique: true);
        }
    }
}
