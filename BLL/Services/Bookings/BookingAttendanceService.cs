using AutoWashPro.DAL.Data;
using BLL.DTOs;
using BLL.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class BookingAttendanceService : IBookingAttendanceService
    {
        private readonly AutoWashDbContext _context;

        public BookingAttendanceService(AutoWashDbContext context)
        {
            _context = context;
        }
    }
}
