using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public interface ILLMService
    {
        Task<string> GenerateReplyAsync(
            string systemPrompt,
            string userPrompt);
    }
}
