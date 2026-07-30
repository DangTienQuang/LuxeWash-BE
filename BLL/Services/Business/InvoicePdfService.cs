using BLL.Services.Interface;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class InvoicePdfService : IInvoicePdfService
    {
        private readonly IBusinessService _businessService;

        public InvoicePdfService(IBusinessService businessService)
        {
            _businessService = businessService;
        }

        public async Task<byte[]> GenerateInvoiceAsync(int invoiceId)
        {
            var invoice = await _businessService.GetInvoiceExportAsync(invoiceId);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    page.Header()
                        .Background("#1E3A8A")
                        .Padding(15)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Column(col =>
                                {
                                    col.Item()
                                        .Text("SMART AUTO WASH")
                                        .FontColor(Colors.White)
                                        .FontSize(24)
                                        .Bold();

                                    col.Item()
                                        .Text("Fleet Business Invoice")
                                        .FontColor(Colors.White);
                                });

                            row.ConstantItem(200)
                                .AlignRight()
                                .Column(col =>
                                {
                                    col.Item()
                                        .Text($"Invoice: {invoice.InvoiceCode}")
                                        .FontColor(Colors.White);

                                    col.Item()
                                        .Text(invoice.CreatedAt.ToString("dd/MM/yyyy"))
                                        .FontColor(Colors.White);
                                });
                        });

                    page.Content()
                        .PaddingVertical(15)
                        .Column(col =>
                        {
                            col.Item()
                                .Row(row =>
                                {
                                    row.RelativeItem()
                                        .Border(1)
                                        .Padding(10)
                                        .Column(left =>
                                        {
                                            left.Item().Text("BILL TO").Bold();
                                            left.Item().Text(invoice.BusinessName);
                                            left.Item().Text($"Tax: {invoice.TaxCode}");
                                            left.Item().Text(invoice.RepresentativeName);
                                        });

                                    row.ConstantItem(20);

                                    row.RelativeItem()
                                        .Border(1)
                                        .Padding(10)
                                        .Column(right =>
                                        {
                                            right.Item().Text("SERVICE INFO").Bold();
                                            right.Item().Text($"Branch: {invoice.BranchName}");
                                            right.Item().Text($"Vehicle: {invoice.LicensePlate}");
                                            right.Item().Text($"Type: {invoice.VehicleType}");
                                        });
                                });

                            col.Item().PaddingTop(20);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    void HeaderCell(string text)
                                    {
                                        header.Cell()
                                            .Background("#1E3A8A")
                                            .Padding(8)
                                            .Text(text)
                                            .FontColor(Colors.White)
                                            .Bold();
                                    }

                                    HeaderCell("Service");
                                    HeaderCell("Qty");
                                    HeaderCell("Unit Price");
                                    HeaderCell("Amount");
                                });

                                int index = 0;

                                foreach (var item in invoice.Items)
                                {
                                    var bg =
                                        index % 2 == 0
                                            ? Colors.White
                                            : Colors.Grey.Lighten4;

                                    table.Cell().Background(bg).Padding(5).Text(item.Description);
                                    table.Cell().Background(bg).Padding(5).Text(item.Quantity.ToString());
                                    table.Cell().Background(bg).Padding(5).Text($"{item.UnitPrice:N0}");
                                    table.Cell().Background(bg).Padding(5).Text($"{item.Amount:N0}");

                                    index++;
                                }
                            });

                            col.Item().PaddingTop(20);

                            col.Item()
                                .AlignRight()
                                .Width(220)
                                .Border(1)
                                .Padding(10)
                                .Column(total =>
                                {
                                    total.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text("Subtotal");
                                        r.ConstantItem(100)
                                            .AlignRight()
                                            .Text($"{invoice.Subtotal:N0}");
                                    });

                                    total.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text("VAT");
                                        r.ConstantItem(100)
                                            .AlignRight()
                                            .Text($"{invoice.TaxAmount:N0}");
                                    });

                                    total.Item().PaddingVertical(5);

                                    total.Item().LineHorizontal(1);

                                    total.Item().Row(r =>
                                    {
                                        r.RelativeItem()
                                            .Text("TOTAL")
                                            .Bold();

                                        r.ConstantItem(100)
                                            .AlignRight()
                                            .Text($"{invoice.TotalAmount:N0} VND")
                                            .Bold();
                                    });
                                });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Generated by SmartAutoWash Fleet Management System");
                        });
                });
            }).GeneratePdf();
        }
    }
}
