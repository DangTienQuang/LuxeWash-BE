using BLL.DTOs.Business;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IInvoiceDispatchService
    {
        Task<InvoiceDispatchResponseDTO> SendInvoiceEmailAsync(int invoiceId);
    }
}
