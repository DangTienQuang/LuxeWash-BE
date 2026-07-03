using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Interfaces
{
    public interface IScenarioExecutionLogger
    {
        Task LogAsync(int customerId, int scenarioId, double confidence);
    }
}
