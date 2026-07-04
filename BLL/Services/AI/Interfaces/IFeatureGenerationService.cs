using DAL.Entities.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Interfaces
{
    public interface IFeatureGenerationService
    {
        Task GenerateFeaturesAsync(int customerId);
        Task GenerateAllCustomersAsync();
    }
}
