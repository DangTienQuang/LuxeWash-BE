using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AutoWashPro.DAL.Data;

#nullable disable

namespace AutoWashPro.DAL.Migrations
{
    /// <summary>
    /// Data-only migration: reclassifies OFFPEAK AI revenue-stimulus vouchers that were
    /// mistakenly stored as CampaignType = Manual into the correct CampaignType = Winback.
    ///
    /// Background: BranchRevenueAnalyticsService.GenerateComprehensiveStimulusAnalysisAsync
    /// used to create OFFPEAK vouchers with CampaignType.Manual. After a manager approved
    /// such a voucher, it leaked into the "redeemable voucher" list (API GET /vouchers/available)
    /// because that endpoint filters for CampaignType == Manual. The C# fix in that service
    /// has already been applied to keep newly generated OFFPEAK vouchers as Winback; this
    /// migration repairs the rows that were created before the fix.
    ///
    /// Both VoucherCampaignType values are stored as int:
    ///   Manual  = 0
    ///   Winback = 3
    /// </summary>
    [DbContext(typeof(AutoWashDbContext))]
    [Migration("20260816085125_FixOffpeakVoucherCampaignType")]
    public partial class FixOffpeakVoucherCampaignType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Reclassify OFFPEAK AI vouchers that were wrongly saved as Manual
            //    (Code starts with "OFFPEAK_B" + CampaignType = 0) -> Winback (3).
            // INSTR is safer than LIKE with underscore-wildcard escaping.
            migrationBuilder.Sql(@"
                UPDATE `Vouchers`
                SET `CampaignType` = 3
                WHERE `CampaignType` = 0
                  AND INSTR(`Code`, 'OFFPEAK_B') = 1;
            ");

            // 2) Defense-in-depth: if any CampaignType.Manual voucher happens to be tied to a
            //    Branch-specific AI winback proposal (BranchId NOT NULL + WINBACK/LOYAL/OFFPEAK
            //    prefix) but was saved as Manual, also flip it to Winback. This protects code
            //    paths that may reuse the same auto-generated naming scheme in the future.
            migrationBuilder.Sql(@"
                UPDATE `Vouchers`
                SET `CampaignType` = 3
                WHERE `CampaignType` = 0
                  AND `BranchId` IS NOT NULL
                  AND (INSTR(`Code`, 'WINBACK_') = 1
                    OR INSTR(`Code`, 'LOYAL_') = 1
                    OR INSTR(`Code`, 'OFFPEAK_B') = 1);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversal: bring those vouchers back to Manual if anyone needs to roll back.
            // NOTE: This will re-introduce the original bug, so it should only be used
            // when rolling back the code change as well.
            migrationBuilder.Sql(@"
                UPDATE `Vouchers`
                SET `CampaignType` = 0
                WHERE `CampaignType` = 3
                  AND INSTR(`Code`, 'OFFPEAK_B') = 1;
            ");
        }
    }
}

