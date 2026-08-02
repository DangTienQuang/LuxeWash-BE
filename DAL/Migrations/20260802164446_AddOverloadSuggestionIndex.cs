using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOverloadSuggestionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropIndex(
            //     name: "IX_OverloadSuggestions_BookingId",
            //     table: "OverloadSuggestions");

            migrationBuilder.CreateIndex(
                name: "IX_OverloadSuggestions_BookingId_IsProcessed_ExpiresAt",
                table: "OverloadSuggestions",
                columns: new[] { "BookingId", "IsProcessed", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OverloadSuggestions_BookingId_IsProcessed_ExpiresAt",
                table: "OverloadSuggestions");

            // migrationBuilder.CreateIndex(
            //     name: "IX_OverloadSuggestions_BookingId",
            //     table: "OverloadSuggestions",
            //     column: "BookingId");
        }
    }
}
