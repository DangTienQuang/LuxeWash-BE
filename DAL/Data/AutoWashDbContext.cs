using AutoWashPro.DAL.Entities;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.DAL.Data
{
    public class AutoWashDbContext : DbContext
    {
        public AutoWashDbContext(DbContextOptions<AutoWashDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<CustomerProfile> CustomerProfiles { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<VehicleType> VehicleTypes { get; set; }
        public DbSet<Tier> Tiers { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ServicePrice> ServicePrices { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<PointLedger> PointLedgers { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingDetail> BookingDetails { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<UserVoucher> UserVouchers { get; set; }
        public DbSet<TimeSlot> TimeSlots { get; set; }
        public DbSet<DailySlotCapacity> DailySlotCapacities { get; set; }
        public DbSet<AIConversationLog> AIConversationLogs { get; set; }
        public DbSet<AIKnowledgeBase> AIKnowledgeBases { get; set; }
        public DbSet<StaffProfile> StaffProfiles { get; set; }
        public DbSet<ManagerProfile> ManagerProfiles { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<StaffShiftAssignment> StaffShiftAssignments { get; set; }
        public DbSet<OvertimeRequest> OvertimeRequests { get; set; }
        public DbSet<ShiftSwapRequest> ShiftSwapRequests { get; set; }
        public DbSet<CarModel> CarModels { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Lane> Lanes { get; set; }
        public DbSet<StaffLaneAssignment> StaffLaneAssignments { get; set; }
        public DbSet<EmployeeProfile> EmployeeProfiles { get; set; }
        public DbSet<BusinessProfile> BusinessProfiles { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<BookingDocument> BookingDocuments { get; set; }
        public DbSet<FleetVehicle> FleetVehicles { get; set; }
        public DbSet<FleetImportBatch> FleetImportBatches { get; set; }
        public DbSet<FleetImportError> FleetImportErrors { get; set; }
        public DbSet<FleetWashLog> FleetWashLogs { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<MaterialUnit> MaterialUnits { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<MaterialBatch> MaterialBatches { get; set; }
        public DbSet<WarehouseStock> WarehouseStocks { get; set; }
        public DbSet<ServiceMaterialUsage> ServiceMaterialUsages { get; set; }
        public DbSet<VehicleConditionMaterialMultiplier> VehicleConditionMaterialMultipliers { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<BookingMaterialUsage> BookingMaterialUsages { get; set; }
        public DbSet<ExtraMaterialUsageRequest> ExtraMaterialUsageRequests { get; set; }
        public DbSet<CustomerFeatureProfile> CustomerFeatureProfiles { get; set; }
        public DbSet<CustomerBehaviorHistory> CustomerBehaviorHistories { get; set; }
        public DbSet<FeatureDefinition> FeatureDefinitions { get; set; }
        public DbSet<KnowledgeCategory> KnowledgeCategories { get; set; }
        public DbSet<KnowledgeScenario> KnowledgeScenarios { get; set; }
        public DbSet<ScenarioCondition> ScenarioConditions { get; set; }
        public DbSet<ScenarioExclusion> ScenarioExclusions { get; set; }
        public DbSet<ScenarioAction> ScenarioActions { get; set; }
        public DbSet<ScenarioMessageTemplate> ScenarioMessageTemplates { get; set; }
        public DbSet<ScenarioExplanation> ScenarioExplanations { get; set; }
        public DbSet<AIDecisionHistory> AIDecisionHistories { get; set; }
        public DbSet<AILearning> AILearnings { get; set; }
        public DbSet<AIAuditLog> AIAuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Vehicle>()
                .HasIndex(v => v.LicensePlate)
                .IsUnique();

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Vehicle)
                .WithMany()
                .HasForeignKey(b => b.VehicleId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique();

            modelBuilder.Entity<Voucher>()
                .HasIndex(v => v.Code)
                .IsUnique();

            modelBuilder.Entity<MaterialUnit>()
                .HasIndex(u => u.Code)
                .IsUnique();

            modelBuilder.Entity<UserVoucher>()
                .HasIndex(uv => new { uv.UserId, uv.VoucherId, uv.TriggerKey })
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasOne(u => u.CustomerProfile)
                .WithOne(c => c.User)
                .HasForeignKey<CustomerProfile>(c => c.UserId);

            modelBuilder.Entity<User>()
                .HasOne(u => u.StaffProfile)
                .WithOne(s => s.User)
                .HasForeignKey<StaffProfile>(s => s.UserId);

            modelBuilder.Entity<User>()
                .HasOne(u => u.ManagerProfile)
                .WithOne(m => m.User)
                .HasForeignKey<ManagerProfile>(m => m.UserId);

            modelBuilder.Entity<StaffShiftAssignment>()
                .HasIndex(s => new { s.StaffUserId, s.WorkShiftId, s.WorkDate })
                .IsUnique();

            modelBuilder.Entity<StaffShiftAssignment>()
                .HasOne(s => s.StaffUser)
                .WithMany()
                .HasForeignKey(s => s.StaffUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StaffShiftAssignment>()
                .HasOne(s => s.WorkShift)
                .WithMany(w => w.Assignments)
                .HasForeignKey(s => s.WorkShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OvertimeRequest>()
                .HasOne(o => o.StaffUser)
                .WithMany()
                .HasForeignKey(o => o.StaffUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ShiftSwapRequest>()
                .HasOne(s => s.FromAssignment)
                .WithMany()
                .HasForeignKey(s => s.FromAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ShiftSwapRequest>()
                .HasOne(s => s.ToAssignment)
                .WithMany()
                .HasForeignKey(s => s.ToAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.EmployeeProfile)
                .WithOne(e => e.User)
                .HasForeignKey<EmployeeProfile>(e => e.EmployeeId);

            modelBuilder.Entity<DailySlotCapacity>()
                .HasIndex(d => new { d.SlotId, d.Date, d.BranchId })
                .IsUnique();

            modelBuilder.Entity<ServicePrice>()
                .HasOne(sp => sp.Branch)
                .WithMany()
                .HasForeignKey(sp => sp.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TimeSlot>()
                .HasOne(ts => ts.Branch)
                .WithMany()
                .HasForeignKey(ts => ts.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DailySlotCapacity>()
                .HasOne(dsc => dsc.Branch)
                .WithMany()
                .HasForeignKey(dsc => dsc.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookingDetail>()
                .HasOne(bd => bd.Booking)
                .WithMany(b => b.BookingDetails)
                .HasForeignKey(bd => bd.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingDetail>()
                .HasOne(bd => bd.Service)
                .WithMany()
                .HasForeignKey(bd => bd.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.ActualVehicleType)
                .WithMany()
                .HasForeignKey(b => b.ActualVehicleTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.ProcessingLane)
                .WithMany(l => l.ProcessingBookings)
                .HasForeignKey(b => b.ProcessingLaneId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.ProcessingStaff)
                .WithMany(u => u.ProcessedBookings)
                .HasForeignKey(b => b.ProcessingStaffId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Branch)
                .WithMany(br => br.Bookings)
                .HasForeignKey(b => b.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StaffLaneAssignment>()
                .HasOne(sla => sla.Staff)
                .WithMany(u => u.LaneAssignments)
                .HasForeignKey(sla => sla.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StaffLaneAssignment>()
                .HasOne(sla => sla.Lane)
                .WithMany(l => l.StaffAssignments)
                .HasForeignKey(sla => sla.LaneId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeeProfile>()
                .HasOne(e => e.Branch)
                .WithMany(b => b.Employees)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Lane>()
                .HasOne(l => l.Branch)
                .WithMany(b => b.Lanes)
                .HasForeignKey(l => l.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<User>()
                .HasOne(u => u.BusinessProfile)
                .WithOne(b => b.User)
                .HasForeignKey<BusinessProfile>(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.BusinessProfile)
                .WithMany()
                .HasForeignKey(b => b.BusinessProfileId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Booking)
                .WithMany()
                .HasForeignKey(i => i.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Invoice>()
                .HasIndex(x => x.InvoiceCode)
                .IsUnique();
            modelBuilder.Entity<InvoiceItem>()
                .HasOne(ii => ii.Invoice)
                .WithMany(i => i.InvoiceItems)
                .HasForeignKey(ii => ii.InvoiceId);
            modelBuilder.Entity<InvoiceItem>()
                .HasOne(ii => ii.BookingDetail)
                .WithMany()
                .HasForeignKey(ii => ii.BookingDetailId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<BookingDocument>()
                .HasOne(d => d.Booking)
                .WithMany()
                .HasForeignKey(d => d.BookingId);
            modelBuilder.Entity<BusinessProfile>()
                .HasOne(bp => bp.ReviewedByUser)
                .WithMany()
                .HasForeignKey(bp => bp.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<FleetVehicle>()
                .HasIndex(x => x.LicensePlate)
                .IsUnique();
            modelBuilder.Entity<FleetVehicle>()
                .HasOne(x => x.BusinessProfile)
                .WithMany()
                .HasForeignKey(x => x.BusinessProfileId);
            modelBuilder.Entity<FleetVehicle>()
                .HasOne(x => x.VehicleType)
                .WithMany()
                .HasForeignKey(x => x.VehicleTypeId);
            modelBuilder.Entity<FleetImportBatch>()
                .HasOne(x => x.BusinessProfile)
                .WithMany()
                .HasForeignKey(x => x.BusinessProfileId);
            modelBuilder.Entity<FleetImportError>()
                .HasOne(x => x.FleetImportBatch)
                .WithMany()
                .HasForeignKey(x => x.FleetImportBatchId);

            modelBuilder.Entity<FleetWashLog>()
                .HasOne(x => x.FleetVehicle)
                .WithMany()
                .HasForeignKey(x => x.FleetVehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FleetWashLog>()
                .HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FleetWashLog>()
                .HasOne(x => x.Booking)
                .WithMany()
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.FleetVehicle)
                .WithMany()
                .HasForeignKey(b => b.FleetVehicleId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Warehouse>()
                .HasIndex(w => new { w.Type, w.BranchId })
                .IsUnique();

            modelBuilder.Entity<Warehouse>()
                .HasOne(w => w.Branch)
                .WithMany()
                .HasForeignKey(w => w.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WarehouseStock>()
                .HasIndex(s => new { s.WarehouseId, s.MaterialId })
                .IsUnique();

            modelBuilder.Entity<MaterialBatch>()
                .HasIndex(b => b.BatchCode)
                .IsUnique();

            modelBuilder.Entity<MaterialBatch>()
                .HasOne(b => b.Warehouse)
                .WithMany()
                .HasForeignKey(b => b.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServiceMaterialUsage>()
                .HasIndex(u => new { u.ServiceId, u.VehicleTypeId, u.MaterialId })
                .IsUnique();

            modelBuilder.Entity<VehicleConditionMaterialMultiplier>()
                .HasIndex(m => m.VehicleCondition)
                .IsUnique();

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.Booking)
                .WithMany()
                .HasForeignKey(t => t.BookingId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<BookingMaterialUsage>()
                .HasOne(u => u.Booking)
                .WithMany()
                .HasForeignKey(u => u.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingMaterialUsage>()
                .HasOne(u => u.BookingDetail)
                .WithMany()
                .HasForeignKey(u => u.BookingDetailId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<BookingMaterialUsage>()
                .HasOne(u => u.MaterialBatch)
                .WithMany()
                .HasForeignKey(u => u.MaterialBatchId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ExtraMaterialUsageRequest>()
                .HasIndex(r => new { r.BranchId, r.Status, r.CreatedAt });

            modelBuilder.Entity<ExtraMaterialUsageRequest>()
                .HasOne(r => r.Booking)
                .WithMany()
                .HasForeignKey(r => r.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExtraMaterialUsageRequest>()
                .HasOne(r => r.StaffUser)
                .WithMany()
                .HasForeignKey(r => r.StaffUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExtraMaterialUsageRequest>()
                .HasOne(r => r.ReviewedByManager)
                .WithMany()
                .HasForeignKey(r => r.ReviewedByManagerId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CustomerFeatureProfile>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CustomerFeatureProfile>()
                .HasOne(x => x.FavoriteService)
                .WithMany()
                .HasForeignKey(x => x.FavoriteServiceId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<CustomerFeatureProfile>()
                .HasOne(x => x.FavoriteBranch)
                .WithMany()
                .HasForeignKey(x => x.FavoriteBranchId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<CustomerFeatureProfile>()
                .HasOne(x => x.MembershipTier)
                .WithMany()
                .HasForeignKey(x => x.MembershipTierId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<CustomerBehaviorHistory>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CustomerFeatureProfile>()
                .HasIndex(x => x.CustomerId)
                .IsUnique();
            modelBuilder.Entity<CustomerBehaviorHistory>()
                .HasIndex(x => new
                {
                    x.CustomerId,
                    x.BehaviorType,
                    x.DetectedOn
                });
            modelBuilder.Entity<AIDecisionHistory>()
                .HasIndex(x => new
                {
                    x.CustomerId,
                    x.CreatedAt
                });
            modelBuilder.Entity<AIDecisionHistory>()
                .HasIndex(x => x.ScenarioId);

            modelBuilder.Entity<AILearning>()
                .HasIndex(x => new
                {
                    x.ScenarioId,
                    x.VoucherId
                });
            modelBuilder.Entity<AIAuditLog>()
                .HasIndex(x => new
                {
                    x.CustomerId,
                    x.CreatedAt
                });
        }
    }
}
