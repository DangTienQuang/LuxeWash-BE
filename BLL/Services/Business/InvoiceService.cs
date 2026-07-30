using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs;
using BLL.Services.Interface;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly AutoWashDbContext _context;

        public InvoiceService(AutoWashDbContext context)
        {
            _context = context;
        }

        //public async Task<InvoiceResponseDTO> GenerateInvoiceAsync(CreateInvoiceDTO dto)
        //{
        //    var booking = await _context.Bookings
        //        .FirstOrDefaultAsync(x => x.BookingId == dto.BookingId);

        //    if (booking == null)
        //    {
        //        throw new Exception("Booking not found.");
        //    }

        //    // Get ONLY completed booking details
        //    var completedDetails = await _context.BookingDetails
        //        .Include(x => x.Service)
        //        .Where(x =>
        //            x.BookingId == dto.BookingId &&
        //            x.AttendanceStatus == "Completed")
        //        .ToListAsync();

        //    if (!completedDetails.Any())
        //    {
        //        throw new Exception(
        //            "No completed booking details found.");
        //    }

        //    // Calculate subtotal
        //    decimal subtotal = completedDetails.Sum(x =>
        //        x.ActualPrice ?? x.Price);

        //    // Calculate total
        //    decimal total = subtotal + dto.TaxAmount;

        //    // Generate invoice code
        //    string invoiceCode =
        //        $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}";

        //    // Create invoice
        //    var invoice = new Invoice
        //    {
        //        InvoiceCode = invoiceCode,
        //        BookingId = booking.BookingId,
        //        BusinessProfileId = booking.BusinessProfileId,
        //        InvoiceType = "Service",
        //        Subtotal = subtotal,
        //        TaxAmount = dto.TaxAmount,
        //        TotalAmount = total,
        //        Status = "Issued",
        //        IssuedAt = DateTime.UtcNow
        //    };

        //    _context.Invoices.Add(invoice);

        //    await _context.SaveChangesAsync();

        //    // Create invoice items
        //    var invoiceItems = new List<InvoiceItem>();

        //    foreach (var detail in completedDetails)
        //    {
        //        decimal finalPrice =
        //            detail.ActualPrice ?? detail.Price;

        //        var item = new InvoiceItem
        //        {
        //            InvoiceId = invoice.InvoiceId,
        //            BookingDetailId = detail.DetailId,
        //            Description =
        //                $"{detail.Service.ServiceName} - {detail.LicensePlate}",
        //            Quantity = 1,
        //            UnitPrice = finalPrice,
        //            Amount = finalPrice
        //        };

        //        invoiceItems.Add(item);

        //        // Link invoice to booking detail
        //        detail.InvoiceId = invoice.InvoiceId;
        //    }

        //    _context.InvoiceItems.AddRange(invoiceItems);

        //    await _context.SaveChangesAsync();

        //    return new InvoiceResponseDTO
        //    {
        //        InvoiceId = invoice.InvoiceId,
        //        InvoiceCode = invoice.InvoiceCode,
        //        TotalAmount = invoice.TotalAmount,
        //        IssuedAt = invoice.IssuedAt
        //    };
        //}
    }
}