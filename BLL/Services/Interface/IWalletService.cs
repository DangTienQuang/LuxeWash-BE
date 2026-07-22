using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IWalletService
    {
        Task<WalletResponseDTO> GetWalletInfoAsync(int userId);
        Task<PaymentQrResponseDTO> CreatePaymentQrAsync(int userId, PaymentQrRequestDTO request);
        Task<TopUpResponseDTO> CreateTopUpLinkAsync(int userId, TopUpRequestDTO request);
        Task ProcessPayOsWebhookAsync(WebhookTopUpDTO webhookData);
        Task<List<TransactionResponseDTO>> GetTransactionsAsync(int userId);
        Task<List<PointHistoryResponseDTO>> GetPointsHistoryAsync(int userId);
        Task DeductSpendablePointsAsync(int userId, int pointsToDeduct, string reason);
        Task RefundSpendablePointsAsync(int userId, int pointsToRefund, string reason, int? referenceBookingId = null);
        Task RefundBalanceAsync(int userId, decimal amount, string reason);
        Task<int> AwardCompletionPointsAsync(int userId, int pointsEarned, int bookingId);
        Task ConfirmTransactionPaymentAsync(int transactionId, decimal? webhookAmount, string orderCode);
        Task MarkTransactionTerminalAsync(int transactionId, string terminalStatus);
    }
}
