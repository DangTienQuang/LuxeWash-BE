using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    [DbContext(typeof(AutoWashDbContext))]
    [Migration("20260923163000_ReleaseCompletedIncidentBookingMarkers")]
    public partial class ReleaseCompletedIncidentBookingMarkers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ActiveBookingId is a nullable unique marker: it prevents the same
            // booking from being handled by two live incident cases at once.
            // Older code did not release the marker after a terminal decision,
            // so completed cases could block a later, unrelated incident.
            migrationBuilder.Sql(
                "UPDATE `IncidentAffectedBookings` " +
                "SET `ActiveBookingId` = NULL " +
                "WHERE `Status` IN ('Transferred', 'Cancelled', 'Kept');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately irreversible: restoring these markers could violate
            // the unique index when a booking has more than one historical case.
        }
    }
}
