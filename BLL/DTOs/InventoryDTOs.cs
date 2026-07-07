using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class MaterialDTO
    {
        public int MaterialId { get; set; }
        public string Name { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public string? Description { get; set; }
        public bool RequiresExpiryTracking { get; set; }
        public decimal DefaultMinStockLevel { get; set; }
        public int ExpiryWarningDays { get; set; }
        public bool IsActive { get; set; }
    }

    public class MaterialUnitDTO
    {
        public int UnitId { get; set; }
        public string Code { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string MeasurementType { get; set; } = null!;
        public bool IsActive { get; set; }
    }

    public class CreateMaterialUnitDTO
    {
        [Required, MaxLength(50)]
        public string Code { get; set; } = null!;

        [Required, MaxLength(100)]
        public string DisplayName { get; set; } = null!;

        [Required, MaxLength(50)]
        public string MeasurementType { get; set; } = null!;
    }

    public class UpdateMaterialUnitDTO
    {
        [Required, MaxLength(100)]
        public string DisplayName { get; set; } = null!;

        [Required, MaxLength(50)]
        public string MeasurementType { get; set; } = null!;

        public bool IsActive { get; set; } = true;
    }

    public class CreateMaterialDTO
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Category { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Unit { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool RequiresExpiryTracking { get; set; }

        [Range(0, double.MaxValue)]
        public decimal DefaultMinStockLevel { get; set; }

        [Range(0, 3650)]
        public int ExpiryWarningDays { get; set; } = 30;
    }

    public class UpdateMaterialDTO : CreateMaterialDTO
    {
        public bool IsActive { get; set; } = true;
    }

    public class ImportMaterialBatchDTO
    {
        [Required]
        public int MaterialId { get; set; }

        [Required, MaxLength(50)]
        public string BatchCode { get; set; } = null!;

        [Range(0.0001, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal UnitCost { get; set; }

        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        [MaxLength(100)]
        public string? SupplierName { get; set; }
    }

    public class WarehouseStockDTO
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = null!;
        public string WarehouseType { get; set; } = null!;
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal CurrentQuantity { get; set; }
        public decimal MinStockLevel { get; set; }
        public bool IsLowStock { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MaterialBatchDTO
    {
        public int MaterialBatchId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = null!;
        public string BatchCode { get; set; } = null!;
        public decimal ImportedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public DateOnly? ManufactureDate { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public string? SupplierName { get; set; }
        public string Status { get; set; } = null!;
        public DateTime ImportedAt { get; set; }
    }

    public class DiscardMaterialBatchDTO
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class AdjustBranchInventoryDTO
    {
        public int MaterialId { get; set; }

        public int? MaterialBatchId { get; set; }

        [Range(typeof(decimal), "-999999999", "999999999")]
        public decimal QuantityChange { get; set; }

        [Required, MaxLength(500)]
        public string Reason { get; set; } = null!;
    }

    public class InventoryTransactionDTO
    {
        public int InventoryTransactionId { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = null!;
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public int? MaterialBatchId { get; set; }
        public string? BatchCode { get; set; }
        public int? BookingId { get; set; }
        public string TransactionType { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal CostAmount { get; set; }
        public decimal BeforeQuantity { get; set; }
        public decimal AfterQuantity { get; set; }
        public string? Note { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ServiceMaterialUsageDTO
    {
        public int ServiceMaterialUsageId { get; set; }
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = null!;
        public int? VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public decimal BaseQuantity { get; set; }
        public string Unit { get; set; } = null!;
        public bool IsActive { get; set; }
    }

    public class UpsertServiceMaterialUsageDTO
    {
        public int? VehicleTypeId { get; set; }

        public int? MaterialId { get; set; }

        public decimal? BaseQuantity { get; set; }

        public List<UpsertServiceMaterialUsageItemDTO>? Items { get; set; }
    }

    public class UpsertServiceMaterialUsageItemDTO
    {
        [Required]
        public int MaterialId { get; set; }

        [Range(0.0001, double.MaxValue)]
        public decimal BaseQuantity { get; set; }
    }

    public class VehicleConditionMaterialMultiplierDTO
    {
        public int Id { get; set; }
        public string VehicleCondition { get; set; } = null!;
        public decimal Multiplier { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateVehicleConditionMaterialMultiplierDTO
    {
        [Range(0.1, 10)]
        public decimal Multiplier { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class ReportExtraMaterialUsageDTO
    {
        public int MaterialId { get; set; }

        [Range(0.0001, double.MaxValue)]
        public decimal Quantity { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }

    public class ExtraMaterialUsageRequestDTO
    {
        public int RequestId { get; set; }
        public int BookingId { get; set; }
        public int StaffUserId { get; set; }
        public int BranchId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal Quantity { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = null!;
        public int? ReviewedByManagerId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ManagerNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ReviewExtraMaterialUsageRequestDTO
    {
        [MaxLength(500)]
        public string? ManagerNote { get; set; }
    }

    public class BranchInventorySettingDTO
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public bool AllowNegativeStock { get; set; }
        public decimal? NegativeStockLimit { get; set; }
    }

    public class UpdateBranchInventorySettingDTO
    {
        public bool AllowNegativeStock { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? NegativeStockLimit { get; set; }
    }

    public class InventoryReportDTO
    {
        public decimal Revenue { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossMargin { get; set; }
        public int CompletedBookings { get; set; }
    }
}
