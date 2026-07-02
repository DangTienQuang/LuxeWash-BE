using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAIv1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIAuditLogs",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    EngineVersion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModelVersion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MatchedScenarios = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SelectedScenarioId = table.Column<int>(type: "int", nullable: false),
                    DecisionScore = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "double", nullable: false),
                    DecisionReason = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Prompt = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AIResponse = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExecutionTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    Success = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIAuditLogs", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK_AIAuditLogs_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomerBehaviorHistories",
                columns: table => new
                {
                    BehaviorHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    BehaviorType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviousValue = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentValue = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Confidence = table.Column<double>(type: "double", nullable: false),
                    Explanation = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetectedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetectedOn = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBehaviorHistories", x => x.BehaviorHistoryId);
                    table.ForeignKey(
                        name: "FK_CustomerBehaviorHistories_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomerFeatureProfiles",
                columns: table => new
                {
                    FeatureProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    VisitCount = table.Column<int>(type: "int", nullable: false),
                    CompletedVisitCount = table.Column<int>(type: "int", nullable: false),
                    CancelledVisitCount = table.Column<int>(type: "int", nullable: false),
                    NoShowCount = table.Column<int>(type: "int", nullable: false),
                    DaysSinceLastVisit = table.Column<int>(type: "int", nullable: false),
                    AverageVisitGap = table.Column<double>(type: "double", nullable: false),
                    LongestVisitGap = table.Column<int>(type: "int", nullable: false),
                    ShortestVisitGap = table.Column<int>(type: "int", nullable: false),
                    VisitTrend = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AverageSpend = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HighestSpend = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LowestSpend = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LifetimeSpend = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LifetimeBookings = table.Column<int>(type: "int", nullable: false),
                    FavoriteServiceId = table.Column<int>(type: "int", nullable: true),
                    FavoriteBranchId = table.Column<int>(type: "int", nullable: true),
                    FavoriteVisitDay = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FavoriteVisitHour = table.Column<int>(type: "int", nullable: true),
                    WeekendVisitRate = table.Column<double>(type: "double", nullable: false),
                    MorningVisitRate = table.Column<double>(type: "double", nullable: false),
                    AfternoonVisitRate = table.Column<double>(type: "double", nullable: false),
                    EveningVisitRate = table.Column<double>(type: "double", nullable: false),
                    RainVisitRate = table.Column<double>(type: "double", nullable: false),
                    CouponUsageRate = table.Column<double>(type: "double", nullable: false),
                    PromotionResponseRate = table.Column<double>(type: "double", nullable: false),
                    MembershipTierId = table.Column<int>(type: "int", nullable: true),
                    CurrentPoints = table.Column<int>(type: "int", nullable: false),
                    WalletBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VehicleCount = table.Column<int>(type: "int", nullable: false),
                    LuxuryVehicleCount = table.Column<int>(type: "int", nullable: false),
                    AverageVehicleAge = table.Column<double>(type: "double", nullable: false),
                    AverageRating = table.Column<double>(type: "double", nullable: false),
                    ReferralCount = table.Column<int>(type: "int", nullable: false),
                    ReferralSuccessRate = table.Column<double>(type: "double", nullable: false),
                    PriceSensitivityScore = table.Column<double>(type: "double", nullable: false),
                    PremiumPreferenceScore = table.Column<double>(type: "double", nullable: false),
                    LoyaltyScore = table.Column<double>(type: "double", nullable: false),
                    EngagementScore = table.Column<double>(type: "double", nullable: false),
                    PredictedChurnScore = table.Column<double>(type: "double", nullable: false),
                    PredictedUpgradeScore = table.Column<double>(type: "double", nullable: false),
                    PredictedLifetimeValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedNextVisit = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastFeatureCalculation = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFeatureProfiles", x => x.FeatureProfileId);
                    table.ForeignKey(
                        name: "FK_CustomerFeatureProfiles_Branches_FavoriteBranchId",
                        column: x => x.FavoriteBranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerFeatureProfiles_Services_FavoriteServiceId",
                        column: x => x.FavoriteServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerFeatureProfiles_Tiers_MembershipTierId",
                        column: x => x.MembershipTierId,
                        principalTable: "Tiers",
                        principalColumn: "TierId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerFeatureProfiles_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FeatureDefinitions",
                columns: table => new
                {
                    FeatureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FeatureCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceTable = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CalculationMethod = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAIFeature = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureDefinitions", x => x.FeatureId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "KnowledgeCategories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeCategories", x => x.CategoryId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "KnowledgeScenarios",
                columns: table => new
                {
                    ScenarioId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    ScenarioCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScenarioName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessGoal = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(3000)", maxLength: 3000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CooldownDays = table.Column<int>(type: "int", nullable: false),
                    ConfidenceThreshold = table.Column<double>(type: "double", nullable: false),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ModelVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSystemScenario = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TriggerCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeScenarios", x => x.ScenarioId);
                    table.ForeignKey(
                        name: "FK_KnowledgeScenarios_KnowledgeCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "KnowledgeCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AIDecisionHistories",
                columns: table => new
                {
                    DecisionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ScenarioId = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: true),
                    ServiceId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Confidence = table.Column<double>(type: "double", nullable: false),
                    FinalScore = table.Column<int>(type: "int", nullable: false),
                    DecisionReason = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedPrompt = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LLMResponse = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NotificationSent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CustomerOpened = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CustomerClicked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Accepted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Redeemed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RevenueGenerated = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIDecisionHistories", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_AIDecisionHistories_KnowledgeScenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "KnowledgeScenarios",
                        principalColumn: "ScenarioId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AIDecisionHistories_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId");
                    table.ForeignKey(
                        name: "FK_AIDecisionHistories_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AIDecisionHistories_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AILearnings",
                columns: table => new
                {
                    LearningId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScenarioId = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: true),
                    TimesTriggered = table.Column<int>(type: "int", nullable: false),
                    NotificationsSent = table.Column<int>(type: "int", nullable: false),
                    NotificationsOpened = table.Column<int>(type: "int", nullable: false),
                    ClickedCount = table.Column<int>(type: "int", nullable: false),
                    AcceptedCount = table.Column<int>(type: "int", nullable: false),
                    RedeemedCount = table.Column<int>(type: "int", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageConfidence = table.Column<double>(type: "double", nullable: false),
                    SuccessRate = table.Column<double>(type: "double", nullable: false),
                    RedemptionRate = table.Column<double>(type: "double", nullable: false),
                    ClickThroughRate = table.Column<double>(type: "double", nullable: false),
                    AcceptanceRate = table.Column<double>(type: "double", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AILearnings", x => x.LearningId);
                    table.ForeignKey(
                        name: "FK_AILearnings_KnowledgeScenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "KnowledgeScenarios",
                        principalColumn: "ScenarioId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AILearnings_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ScenarioActions",
                columns: table => new
                {
                    ActionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScenarioId = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    CooldownDays = table.Column<int>(type: "int", nullable: false),
                    ExpectedConversion = table.Column<double>(type: "double", nullable: false),
                    ExpectedRevenue = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    MaxPerCustomer = table.Column<int>(type: "int", nullable: false),
                    AllowStacking = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    StopProcessing = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioActions", x => x.ActionId);
                    table.ForeignKey(
                        name: "FK_ScenarioActions_KnowledgeScenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "KnowledgeScenarios",
                        principalColumn: "ScenarioId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioActions_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ScenarioConditions",
                columns: table => new
                {
                    ConditionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScenarioId = table.Column<int>(type: "int", nullable: false),
                    FeatureId = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ComparisonValue = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LogicalGroup = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Required = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioConditions", x => x.ConditionId);
                    table.ForeignKey(
                        name: "FK_ScenarioConditions_FeatureDefinitions_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "FeatureDefinitions",
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioConditions_KnowledgeScenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "KnowledgeScenarios",
                        principalColumn: "ScenarioId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ScenarioExclusions",
                columns: table => new
                {
                    ExclusionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScenarioId = table.Column<int>(type: "int", nullable: false),
                    FeatureId = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ComparisonValue = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioExclusions", x => x.ExclusionId);
                    table.ForeignKey(
                        name: "FK_ScenarioExclusions_FeatureDefinitions_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "FeatureDefinitions",
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioExclusions_KnowledgeScenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "KnowledgeScenarios",
                        principalColumn: "ScenarioId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ScenarioExplanations",
                columns: table => new
                {
                    ScenarioId = table.Column<int>(type: "int", nullable: false),
                    Reasoning = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessContext = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpectedOutcome = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LLMNotes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioExplanations", x => x.ScenarioId);
                    table.ForeignKey(
                        name: "FK_ScenarioExplanations_KnowledgeScenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "KnowledgeScenarios",
                        principalColumn: "ScenarioId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ScenarioMessageTemplates",
                columns: table => new
                {
                    TemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScenarioId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PushMessage = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SmsMessage = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailMessage = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioMessageTemplates", x => x.TemplateId);
                    table.ForeignKey(
                        name: "FK_ScenarioMessageTemplates_KnowledgeScenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "KnowledgeScenarios",
                        principalColumn: "ScenarioId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AIAuditLogs_CustomerId_CreatedAt",
                table: "AIAuditLogs",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIDecisionHistories_CustomerId_CreatedAt",
                table: "AIDecisionHistories",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIDecisionHistories_ScenarioId",
                table: "AIDecisionHistories",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_AIDecisionHistories_ServiceId",
                table: "AIDecisionHistories",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AIDecisionHistories_VoucherId",
                table: "AIDecisionHistories",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_AILearnings_ScenarioId_VoucherId",
                table: "AILearnings",
                columns: new[] { "ScenarioId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_AILearnings_VoucherId",
                table: "AILearnings",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBehaviorHistories_CustomerId_BehaviorType_DetectedOn",
                table: "CustomerBehaviorHistories",
                columns: new[] { "CustomerId", "BehaviorType", "DetectedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeatureProfiles_CustomerId",
                table: "CustomerFeatureProfiles",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeatureProfiles_FavoriteBranchId",
                table: "CustomerFeatureProfiles",
                column: "FavoriteBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeatureProfiles_FavoriteServiceId",
                table: "CustomerFeatureProfiles",
                column: "FavoriteServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeatureProfiles_MembershipTierId",
                table: "CustomerFeatureProfiles",
                column: "MembershipTierId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeScenarios_CategoryId",
                table: "KnowledgeScenarios",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioActions_ScenarioId",
                table: "ScenarioActions",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioActions_VoucherId",
                table: "ScenarioActions",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioConditions_FeatureId",
                table: "ScenarioConditions",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioConditions_ScenarioId",
                table: "ScenarioConditions",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioExclusions_FeatureId",
                table: "ScenarioExclusions",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioExclusions_ScenarioId",
                table: "ScenarioExclusions",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioMessageTemplates_ScenarioId",
                table: "ScenarioMessageTemplates",
                column: "ScenarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIAuditLogs");

            migrationBuilder.DropTable(
                name: "AIDecisionHistories");

            migrationBuilder.DropTable(
                name: "AILearnings");

            migrationBuilder.DropTable(
                name: "CustomerBehaviorHistories");

            migrationBuilder.DropTable(
                name: "CustomerFeatureProfiles");

            migrationBuilder.DropTable(
                name: "ScenarioActions");

            migrationBuilder.DropTable(
                name: "ScenarioConditions");

            migrationBuilder.DropTable(
                name: "ScenarioExclusions");

            migrationBuilder.DropTable(
                name: "ScenarioExplanations");

            migrationBuilder.DropTable(
                name: "ScenarioMessageTemplates");

            migrationBuilder.DropTable(
                name: "FeatureDefinitions");

            migrationBuilder.DropTable(
                name: "KnowledgeScenarios");

            migrationBuilder.DropTable(
                name: "KnowledgeCategories");
        }
    }
}
