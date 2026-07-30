using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class AIModerationService
        : IAIModerationService
    {
        private readonly List<string> _blockedWords =
        [
            "fuck",
            "bitch",
            "địt",
            "ngu",
            "cộng sản",
            "phản động",
            "sex",
            "porn",
            "hitler",
            "terrorist",
            "hack",
            "sql injection",
            "ignore previous instructions",
            "bypass",
            "jailbreak"
        ];

        public bool IsBlocked(string message)
        {
            var lower = message.ToLower();

            return _blockedWords.Any(
                x => lower.Contains(x));
        }

        public string? GetBlockedReason(
            string message)
        {
            var lower = message.ToLower();

            var matched = _blockedWords
                .FirstOrDefault(
                    x => lower.Contains(x));

            if (matched == null)
                return null;

            return
                "Nội dung không phù hợp.";
        }
    }
}
