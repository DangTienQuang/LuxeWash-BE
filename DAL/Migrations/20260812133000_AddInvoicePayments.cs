using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    [DbContext(typeof(AutoWashDbContext))]
    [Migration("20260812133000_AddInvoicePayments")]
    public partial class AddInvoicePayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReferenceInvoiceId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReferenceInvoiceId",
                table: "Transactions",
                column: "ReferenceInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Invoices_ReferenceInvoiceId",
                table: "Transactions",
                column: "ReferenceInvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Invoices_ReferenceInvoiceId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ReferenceInvoiceId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReferenceInvoiceId",
                table: "Transactions");
        }
    }
}
