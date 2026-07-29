using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryAndBackfillOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "OutboxMessages",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill LaneOccupancies from active Bookings
            migrationBuilder.Sql(@"
                INSERT INTO LaneOccupancies (LaneId, BranchId, BookingId, FleetWashLogId, LicensePlate, OccupiedAt)
                SELECT 
                    b.ProcessingLaneId, 
                    b.BranchId, 
                    b.BookingId, 
                    NULL, 
                    COALESCE(b.LicensePlate, 'UNKNOWN'), 
                    COALESCE(b.ProcessingStartTime, b.UpdatedAt, b.CreatedAt)
                FROM Bookings b
                WHERE b.Status = 'Processing' AND b.ProcessingLaneId IS NOT NULL;
            ");

            // Backfill LaneOccupancies from active FleetWashLogs
            migrationBuilder.Sql(@"
                INSERT INTO LaneOccupancies (LaneId, BranchId, BookingId, FleetWashLogId, LicensePlate, OccupiedAt)
                SELECT 
                    f.LaneId, 
                    f.BranchId, 
                    f.BookingId, 
                    f.FleetWashLogId, 
                    'UNKNOWN', 
                    f.CheckInTime
                FROM FleetWashLogs f
                WHERE f.Status = 'Processing' AND f.LaneId IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM LaneOccupancies o WHERE o.LaneId = f.LaneId);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "OutboxMessages");
        }
    }
}
