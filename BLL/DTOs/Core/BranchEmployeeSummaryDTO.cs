using AutoWashPro.BLL.DTOs;
using System.Collections.Generic;
namespace AutoWashPro.BLL.DTOs
{
    public class BranchEmployeeSummaryDTO
    {
        public int TotalManagers { get; set; }
        public int TotalStaff { get; set; }
        public List<EmployeeProfileDTO> Managers { get; set; } = new List<EmployeeProfileDTO>();
        public List<EmployeeProfileDTO> Staff { get; set; } = new List<EmployeeProfileDTO>();
    }
}
