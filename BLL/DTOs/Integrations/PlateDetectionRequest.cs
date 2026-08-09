using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace DAL.DTOs
{
    public enum PlatePosition
    {
        Front,
        Back
    }
    public class SinglePlateResult
    {
        public bool Detected { get; set; }
        public string PlateText { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public PlatePosition Position { get; set; }
        public string PlateType { get; set; } = string.Empty;
    }
    public class DualPlateResult
    {
        public bool Detected { get; set; }
        public string FinalPlateText { get; set; } = string.Empty;
        public string ConfirmedBy { get; set; } = string.Empty;
        public SinglePlateResult? Front { get; set; }
        public SinglePlateResult? Back { get; set; }
    }
}
