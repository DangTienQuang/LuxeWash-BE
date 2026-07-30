using AutoWashPro.BLL.Services;
using BLL.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class AIIntentService : IAIIntentService
    {
        private readonly ILLMService _llm;

        public AIIntentService(
            ILLMService llm)
        {
            _llm = llm;
        }

        public async Task<string> DetectIntentAsync(
            string message)
        {
            try
            {
                var systemPrompt = """
                    Bạn là AI classifier.

                    Chỉ trả về DUY NHẤT 1 trong các intent sau:

                    CHECK_POINTS
                    CHECK_TIER
                    LAST_VISIT
                    REFERRAL
                    RECOMMENDATION
                    UNKNOWN

                    Không giải thích.
                    Không thêm chữ khác.
                    """;

                var userPrompt =
                    $"Message: {message}";

                var result =
                    await _llm.GenerateReplyAsync(
                        systemPrompt,
                        userPrompt);

                result = result
                    .Trim()
                    .ToUpper();

                var validIntents = new[]
                {
                    AIIntent.CheckPoints,
                    AIIntent.CheckTier,
                    AIIntent.LastVisit,
                    AIIntent.Referral,
                    AIIntent.Recommendation,
                    AIIntent.Unknown
                };

                if (validIntents.Contains(result))
                {
                    return result;
                }

                return AIIntent.Unknown;
            }
            catch
            {
                return AIIntent.Unknown;
            }
        }
    }
}
