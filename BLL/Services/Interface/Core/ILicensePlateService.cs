using DAL.DTOs;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.Services
{
    public interface ILicensePlateService
    {
        Task<LicensePlateResult> DetectPlateAsync(byte[] imageBytes);
        Task<DualPlateResult> DetectDualPlateAsync(
            byte[]? frontImageBytes,
            byte[]? backImageBytes);
    }
    public class LicensePlateResult
    {
        public bool Detected { get; set; }
        public List<string> PlateTexts { get; set; } = new();
        public string PlateText => PlateTexts.FirstOrDefault() ?? string.Empty;
        public float Confidence { get; set; }
        public List<DAL.Entities.DetectionBox> Boxes { get; set; } = new();
    }
}
