using BLL.Services.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Interfaces
{
    public interface ICarDetectionService
    {
        List<CarBoundingBox> DetectCars(byte[] imageBytes, float confidenceThreshold = 0.4f, float iouThreshold = 0.45f);
    }

}
