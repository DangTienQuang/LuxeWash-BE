using System;
using System.Text.Json;

namespace AutoWashPro.BLL.DTOs.Operations
{
    public class OperationsOutboxEnvelope
    {
        public string EventId { get; set; } = null!;
        public string Type { get; set; } = null!;
        public int BranchId { get; set; }
        public DateTime OccurredAt { get; set; }
        public JsonElement Data { get; set; }
    }
}
