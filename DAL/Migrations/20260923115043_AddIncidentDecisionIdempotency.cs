using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentDecisionIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionIdempotencyKey",
                table: "IncidentAffectedBookings",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAffectedBookings_DecisionIdempotencyKey",
                table: "IncidentAffectedBookings",
                column: "DecisionIdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IncidentAffectedBookings_DecisionIdempotencyKey",
                table: "IncidentAffectedBookings");

            migrationBuilder.DropColumn(
                name: "DecisionIdempotencyKey",
                table: "IncidentAffectedBookings");
        }
    }
}
