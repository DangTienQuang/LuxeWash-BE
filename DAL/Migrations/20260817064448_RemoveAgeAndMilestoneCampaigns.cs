using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoWashPro.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAgeAndMilestoneCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MilestoneUsageCount",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "TargetAge",
                table: "Vouchers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MilestoneUsageCount",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetAge",
                table: "Vouchers",
                type: "int",
                nullable: true);
        }
    }
}
