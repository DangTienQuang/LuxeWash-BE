using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Models
{
    public class ScenarioEvaluationResult
    {
        public int ScenarioId { get; set; }
        public string ScenarioCode { get; set; } = string.Empty;
        public string ScenarioName { get; set; } = string.Empty;
        public int Priority { get; set; }
        public double Confidence { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public Dictionary<string, object?> MatchedFeatures { get; set; } = new();
    }
}
