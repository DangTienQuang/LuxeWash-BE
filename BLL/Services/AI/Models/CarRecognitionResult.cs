using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Models
{
    public class CarRecognitionResult
    {
        public CarBoundingBox Box { get; set; } = null!;
        public string? PredictedBrand { get; set; }
        public string? PredictedModelName { get; set; }
        public float ClassificationConfidence { get; set; }

        public int? CarModelId { get; set; }
        public string? CarModelStatus { get; set; }
        public bool IsNewlyRequestedModel { get; set; }
    }
}
