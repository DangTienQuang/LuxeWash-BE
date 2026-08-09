using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.Services
{
    public interface IAIIntentService
    {
        Task<string> DetectIntentAsync(
            string message);
    }
}
