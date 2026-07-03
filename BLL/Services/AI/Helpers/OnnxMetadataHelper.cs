using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLL.Services.AI.Helpers
{
    public static class OnnxMetadataHelper
    {
        public static List<string> ExtractClassNames(InferenceSession session)
        {
            var metadata = session.ModelMetadata.CustomMetadataMap;

            if (!metadata.TryGetValue("names", out var namesRaw) || string.IsNullOrWhiteSpace(namesRaw))
                throw new InvalidOperationException("File ONNX không chứa metadata danh sách nhãn (names)");

            var pattern = new Regex(@"(\d+)\s*:\s*['""]([^'""]+)['""]");
            var matches = pattern.Matches(namesRaw);

            if (matches.Count == 0)
                throw new InvalidOperationException("Không thể phân tích danh sách nhãn từ metadata ONNX");

            var indexed = new SortedDictionary<int, string>();
            foreach (Match m in matches)
            {
                int index = int.Parse(m.Groups[1].Value);
                string label = m.Groups[2].Value;
                indexed[index] = label;
            }

            return indexed.Values.ToList();
        }
    }
}
