using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class AIModerationService
        : IAIModerationService
    {
        private static readonly string[] _blockedWords =
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

        // Word-boundary matching (Unicode-aware, so it respects Vietnamese diacritics) avoids the
        // Scunthorpe problem: a Contains() check on "ngu" would also flag "nguyên", "người", "nguồn", etc.
        private static readonly Regex _blockedWordsRegex = new(
            string.Join("|", _blockedWords.Select(w => $@"\b{Regex.Escape(w)}\b")),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public bool IsBlocked(string message)
        {
            return _blockedWordsRegex.IsMatch(message);
        }

        public string? GetBlockedReason(
            string message)
        {
            return _blockedWordsRegex.IsMatch(message)
                ? "Nội dung không phù hợp."
                : null;
        }
    }
}
