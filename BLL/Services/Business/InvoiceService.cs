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
    }
}