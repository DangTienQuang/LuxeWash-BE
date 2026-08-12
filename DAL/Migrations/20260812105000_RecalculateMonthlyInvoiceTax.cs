using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AutoWashPro.DAL.Data;

#nullable disable

namespace DAL.Migrations
{
    [DbContext(typeof(AutoWashDbContext))]
    [Migration("20260812105000_RecalculateMonthlyInvoiceTax")]
    public partial class RecalculateMonthlyInvoiceTax : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE Invoices
                SET TaxAmount = ROUND(Subtotal * 0.08, 0),
                    TotalAmount = Subtotal + ROUND(Subtotal * 0.08, 0)
                WHERE InvoiceType = 'MonthlyStatement'
                  AND TaxAmount = 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE Invoices
                SET TotalAmount = Subtotal,
                    TaxAmount = 0
                WHERE InvoiceType = 'MonthlyStatement';
                """);
        }
    }
}
