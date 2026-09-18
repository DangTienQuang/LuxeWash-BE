using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoWashPro.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingMaterialUsages_Bookings_BookingId1",
                table: "BookingMaterialUsages");

            migrationBuilder.DropIndex(
                name: "IX_BookingMaterialUsages_BookingId1",
                table: "BookingMaterialUsages");

            migrationBuilder.DropColumn(
                name: "BookingId1",
                table: "BookingMaterialUsages");

            migrationBuilder.AddColumn<int>(
                name: "ExcludedBookingId",
                table: "UserVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceIncidentAffectedBookingId",
                table: "UserVouchers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BranchIncidents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scope = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EstimatedEndAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActualEndAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchIncidents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LaneSlotCapacities",
                columns: table => new
                {
                    LaneId = table.Column<int>(type: "int", nullable: false),
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    MaxWeightUnits = table.Column<int>(type: "int", nullable: false),
                    IsCalibrated = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaneSlotCapacities", x => new { x.LaneId, x.SlotId });
                    table.ForeignKey(
                        name: "FK_LaneSlotCapacities_Lanes_LaneId",
                        column: x => x.LaneId,
                        principalTable: "Lanes",
                        principalColumn: "LaneId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LaneSlotCapacities_TimeSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "SlotId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IncidentAffectedBookings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IncidentId = table.Column<long>(type: "bigint", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    ActiveBookingId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseDeadlineAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DecidedAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Decision = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalBranchId = table.Column<int>(type: "int", nullable: false),
                    OriginalScheduledTimeVn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TargetBranchId = table.Column<int>(type: "int", nullable: true),
                    TargetSlotId = table.Column<int>(type: "int", nullable: true),
                    CompensationUserVoucherId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentAffectedBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentAffectedBookings_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentAffectedBookings_BranchIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "BranchIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentAffectedBookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IncidentChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IncidentId = table.Column<long>(type: "bigint", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldEstimatedEndAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    NewEstimatedEndAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OccurredAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SnapshotJson = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentChanges_BranchIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "BranchIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IncidentLanes",
                columns: table => new
                {
                    IncidentId = table.Column<long>(type: "bigint", nullable: false),
                    LaneId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentLanes", x => new { x.IncidentId, x.LaneId });
                    table.ForeignKey(
                        name: "FK_IncidentLanes_BranchIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "BranchIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentLanes_Lanes_LaneId",
                        column: x => x.LaneId,
                        principalTable: "Lanes",
                        principalColumn: "LaneId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IncidentCaseCauses",
                columns: table => new
                {
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    IncidentId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentCaseCauses", x => new { x.CaseId, x.IncidentId });
                    table.ForeignKey(
                        name: "FK_IncidentCaseCauses_BranchIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "BranchIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentCaseCauses_IncidentAffectedBookings_CaseId",
                        column: x => x.CaseId,
                        principalTable: "IncidentAffectedBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IncidentDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AffectedBookingId = table.Column<long>(type: "bigint", nullable: false),
                    Channel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventKind = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IncidentVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentDeliveries_IncidentAffectedBookings_AffectedBookingId",
                        column: x => x.AffectedBookingId,
                        principalTable: "IncidentAffectedBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IncidentFinancialOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    ExternalReference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtVn = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentFinancialOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentFinancialOperations_IncidentAffectedBookings_CaseId",
                        column: x => x.CaseId,
                        principalTable: "IncidentAffectedBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_ExcludedBookingId",
                table: "UserVouchers",
                column: "ExcludedBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_SourceIncidentAffectedBookingId",
                table: "UserVouchers",
                column: "SourceIncidentAffectedBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchIncidents_BranchId_Status_StartedAtVn_EstimatedEndAtVn",
                table: "BranchIncidents",
                columns: new[] { "BranchId", "Status", "StartedAtVn", "EstimatedEndAtVn" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAffectedBookings_ActiveBookingId",
                table: "IncidentAffectedBookings",
                column: "ActiveBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAffectedBookings_BookingId",
                table: "IncidentAffectedBookings",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAffectedBookings_IncidentId_BookingId",
                table: "IncidentAffectedBookings",
                columns: new[] { "IncidentId", "BookingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAffectedBookings_Status_ResponseDeadlineAtVn",
                table: "IncidentAffectedBookings",
                columns: new[] { "Status", "ResponseDeadlineAtVn" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAffectedBookings_UserId_Status",
                table: "IncidentAffectedBookings",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentCaseCauses_IncidentId",
                table: "IncidentCaseCauses",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentChanges_IncidentId_OccurredAtVn",
                table: "IncidentChanges",
                columns: new[] { "IncidentId", "OccurredAtVn" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentDeliveries_AffectedBookingId_Channel_EventKind_Incid~",
                table: "IncidentDeliveries",
                columns: new[] { "AffectedBookingId", "Channel", "EventKind", "IncidentVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentFinancialOperations_CaseId_Kind",
                table: "IncidentFinancialOperations",
                columns: new[] { "CaseId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentLanes_LaneId",
                table: "IncidentLanes",
                column: "LaneId");

            migrationBuilder.CreateIndex(
                name: "IX_LaneSlotCapacities_LaneId_SlotId",
                table: "LaneSlotCapacities",
                columns: new[] { "LaneId", "SlotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LaneSlotCapacities_SlotId",
                table: "LaneSlotCapacities",
                column: "SlotId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserVouchers_Bookings_ExcludedBookingId",
                table: "UserVouchers",
                column: "ExcludedBookingId",
                principalTable: "Bookings",
                principalColumn: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserVouchers_IncidentAffectedBookings_SourceIncidentAffected~",
                table: "UserVouchers",
                column: "SourceIncidentAffectedBookingId",
                principalTable: "IncidentAffectedBookings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserVouchers_Bookings_ExcludedBookingId",
                table: "UserVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserVouchers_IncidentAffectedBookings_SourceIncidentAffected~",
                table: "UserVouchers");

            migrationBuilder.DropTable(
                name: "IncidentCaseCauses");

            migrationBuilder.DropTable(
                name: "IncidentChanges");

            migrationBuilder.DropTable(
                name: "IncidentDeliveries");

            migrationBuilder.DropTable(
                name: "IncidentFinancialOperations");

            migrationBuilder.DropTable(
                name: "IncidentLanes");

            migrationBuilder.DropTable(
                name: "LaneSlotCapacities");

            migrationBuilder.DropTable(
                name: "IncidentAffectedBookings");

            migrationBuilder.DropTable(
                name: "BranchIncidents");

            migrationBuilder.DropIndex(
                name: "IX_UserVouchers_ExcludedBookingId",
                table: "UserVouchers");

            migrationBuilder.DropIndex(
                name: "IX_UserVouchers_SourceIncidentAffectedBookingId",
                table: "UserVouchers");

            migrationBuilder.DropColumn(
                name: "ExcludedBookingId",
                table: "UserVouchers");

            migrationBuilder.DropColumn(
                name: "SourceIncidentAffectedBookingId",
                table: "UserVouchers");

            migrationBuilder.AddColumn<int>(
                name: "BookingId1",
                table: "BookingMaterialUsages",
                type: "int",
                nullable: true);

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
        }
    }
}
