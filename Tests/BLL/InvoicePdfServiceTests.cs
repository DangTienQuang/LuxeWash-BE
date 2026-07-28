using BLL.DTOs.Business;
using BLL.Services;
using BLL.Services.Interface;
using FluentAssertions;
using Moq;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace AutoWashPro.Tests.BLL
{
    public class InvoicePdfServiceTests
    {
        private readonly Mock<IBusinessService> _businessMock;
        private readonly InvoicePdfService _sut;

        public InvoicePdfServiceTests()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            _businessMock = new Mock<IBusinessService>();
            _sut = new InvoicePdfService(_businessMock.Object);
        }

        [Fact]
        public async Task GenerateInvoiceAsync_ValidInvoice_ReturnsNonEmptyPdf()
        {
            var invoice = new InvoiceExportDTO
            {
                InvoiceCode = "INV001",
                CreatedAt = DateTime.UtcNow,
                BusinessName = "Fleet Co",
                TaxCode = "123456",
                RepresentativeName = "John Doe",
                BranchName = "Branch A",
                LicensePlate = "51A12345",
                VehicleType = "Van",
                Subtotal = 100000,
                TaxAmount = 10000,
                TotalAmount = 110000,
                Items = new List<InvoiceItemDTO>
                {
                    new InvoiceItemDTO
                    {
                        Description = "Wash",
                        Quantity = 1,
                        UnitPrice = 100000,
                        Amount = 100000
                    }
                }
            };
            _businessMock.Setup(b => b.GetInvoiceExportAsync(1)).ReturnsAsync(invoice);

            var result = await _sut.GenerateInvoiceAsync(1);

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public async Task GenerateInvoiceAsync_MultipleItems_GeneratesSuccessfully()
        {
            var invoice = new InvoiceExportDTO
            {
                InvoiceCode = "INV002",
                CreatedAt = DateTime.UtcNow,
                BusinessName = "Fleet Co",
                TaxCode = "123456",
                RepresentativeName = "John Doe",
                BranchName = "Branch A",
                LicensePlate = "51A12345",
                VehicleType = "Van",
                Subtotal = 300000,
                TaxAmount = 30000,
                TotalAmount = 330000,
                Items = new List<InvoiceItemDTO>
                {
                    new InvoiceItemDTO
                    {
                        Description = "Wash",
                        Quantity = 1,
                        UnitPrice = 100000,
                        Amount = 100000
                    },
                    new InvoiceItemDTO
                    {
                        Description = "Wax",
                        Quantity = 2,
                        UnitPrice = 100000,
                        Amount = 200000
                    }
                }
            };
            _businessMock.Setup(b => b.GetInvoiceExportAsync(2)).ReturnsAsync(invoice);

            var result = await _sut.GenerateInvoiceAsync(2);

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public async Task GenerateInvoiceAsync_NoItems_GeneratesSuccessfullyWithEmptyTable()
        {
            var invoice = new InvoiceExportDTO
            {
                InvoiceCode = "INV003",
                CreatedAt = DateTime.UtcNow,
                BusinessName = "Fleet Co",
                TaxCode = "123456",
                RepresentativeName = "John Doe",
                BranchName = "Branch A",
                LicensePlate = "51A12345",
                VehicleType = "Van",
                Subtotal = 0,
                TaxAmount = 0,
                TotalAmount = 0,
                Items = new List<InvoiceItemDTO>()
            };
            _businessMock.Setup(b => b.GetInvoiceExportAsync(3)).ReturnsAsync(invoice);

            var result = await _sut.GenerateInvoiceAsync(3);

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }
    }
}