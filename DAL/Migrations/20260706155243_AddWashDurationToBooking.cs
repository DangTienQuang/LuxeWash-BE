using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddWashDurationToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LuxuryVehicleCount",
                table: "CustomerFeatureProfiles",
                newName: "VoucherBookings");

            migrationBuilder.RenameColumn(
                name: "AverageVehicleAge",
                table: "CustomerFeatureProfiles",
                newName: "VehicleTypeConsistency");

            migrationBuilder.AddColumn<string>(
                name: "RuleVersion",
                table: "KnowledgeScenarios",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PropertyName",
                table: "FeatureDefinitions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "FeatureDefinitions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "AverageDiscountReceived",
                table: "CustomerFeatureProfiles",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageOriginalSpend",
                table: "CustomerFeatureProfiles",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AveragePointDiscount",
                table: "CustomerFeatureProfiles",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "AverageServicesPerBooking",
                table: "CustomerFeatureProfiles",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageVoucherDiscount",
                table: "CustomerFeatureProfiles",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BasicServiceBookings",
                table: "CustomerFeatureProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BranchLoyaltyRate",
                table: "CustomerFeatureProfiles",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DifferentVehicleTypes",
                table: "CustomerFeatureProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FavoriteBranchVisits",
                table: "CustomerFeatureProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FavoriteServiceUsage",
                table: "CustomerFeatureProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FleetUsageRate",
                table: "CustomerFeatureProfiles",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "PointBookings",
                table: "CustomerFeatureProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "PointUsageRate",
                table: "CustomerFeatureProfiles",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "PreferredVehicleTypeId",
                table: "CustomerFeatureProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "PremiumServiceRate",
                table: "CustomerFeatureProfiles",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPointSavings",
                table: "CustomerFeatureProfiles",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalServicesPurchased",
                table: "CustomerFeatureProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalVoucherSavings",
                table: "CustomerFeatureProfiles",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ActualDurationMinutes",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedTime",
                table: "Bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartTime",
                table: "Bookings",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RuleVersion",
                table: "KnowledgeScenarios");

            migrationBuilder.DropColumn(
                name: "PropertyName",
                table: "FeatureDefinitions");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "FeatureDefinitions");

            migrationBuilder.DropColumn(
                name: "AverageDiscountReceived",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "AverageOriginalSpend",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "AveragePointDiscount",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "AverageServicesPerBooking",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "AverageVoucherDiscount",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "BasicServiceBookings",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "BranchLoyaltyRate",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "DifferentVehicleTypes",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "FavoriteBranchVisits",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "FavoriteServiceUsage",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "FleetUsageRate",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "PointBookings",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "PointUsageRate",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredVehicleTypeId",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "PremiumServiceRate",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "TotalPointSavings",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "TotalServicesPurchased",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "TotalVoucherSavings",
                table: "CustomerFeatureProfiles");

            migrationBuilder.DropColumn(
                name: "ActualDurationMinutes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CompletedTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ProcessingStartTime",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "VoucherBookings",
                table: "CustomerFeatureProfiles",
                newName: "LuxuryVehicleCount");

            migrationBuilder.RenameColumn(
                name: "VehicleTypeConsistency",
                table: "CustomerFeatureProfiles",
                newName: "AverageVehicleAge");
        }
    }
}
