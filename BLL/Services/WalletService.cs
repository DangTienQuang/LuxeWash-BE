using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.Constants;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using Microsoft.Extensions.Logging;
using AutoWashPro.BLL.Exceptions;
using BLL.Helpers;

namespace AutoWashPro.BLL.Services
{
    public class WalletService : IWalletService
    {
        private readonly AutoWashDbContext _context;
        private readonly PayOSClient _payOSClient;
        private readonly ILogger<WalletService> _logger;
        private readonly ITierService _tierService;
        private readonly IEmailService _emailService;
        private readonly global::BLL.Services.Interface.ILaneSchedulerService _laneSchedulerService;

        public WalletService(AutoWashDbContext context, PayOSClient payOSClient, ILogger<WalletService> logger, ITierService tierService, IEmailService emailService, global::BLL.Services.Interface.ILaneSchedulerService laneSchedulerService)
        {
            _context = context;
            _payOSClient = payOSClient;
            _logger = logger;
            _tierService = tierService;
            _emailService = emailService;
            _laneSchedulerService = laneSchedulerService;
        }

        public async Task<WalletResponseDTO> GetWalletInfoAsync(int userId)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId, Balance = 0, Status = "Active" };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }

            var profile = await _context.CustomerProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);

            return new WalletResponseDTO
            {
                Balance = wallet.Balance,
                TotalPoints = profile?.TotalPoint ?? 0,
                PromotionPoints = profile?.PromotionPoint ?? 0
            };
        }

        public async Task<TopUpResponseDTO> CreateTopUpLinkAsync(int userId, TopUpRequestDTO request)
        {
            var result = await CreatePaymentQrAsync(userId, new PaymentQrRequestDTO
            {
                PaymentType = "Topup",
                Amount = request.Amount,
                CancelUrl = request.CancelUrl,
                ReturnUrl = request.ReturnUrl
            });

            return new TopUpResponseDTO
            {
                PaymentUrl = result.PaymentUrl,
                OrderCode = result.OrderCode
            };
        }

        public async Task<PaymentQrResponseDTO> CreatePaymentQrAsync(int userId, PaymentQrRequestDTO request)
        {
            var userExists = await _context.Users.AnyAsync(u => u.UserId == userId);
            if (!userExists)
                throw new NotFoundException("User corresponding to token not found. Please log in again.");

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId, Balance = 0, Status = "Active" };
                _context.Wallets.Add(wallet);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    throw new BadRequestException($"Could not create wallet for user. DB Error: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            var paymentType = NormalizePaymentType(request.PaymentType);
            decimal amount;
            int? referenceBookingId = null;
            string transactionType;
            string transactionDescription;
            string paymentDescription;

            if (paymentType == "Topup")
            {
                if (!request.Amount.HasValue || request.Amount.Value <= 0)
                    throw new BadRequestException("Please enter a valid wallet deposit amount.");

                amount = request.Amount.Value;
                transactionType = "Topup";
                transactionDescription = "Yeu cau nap tien";
                paymentDescription = "Topup wallet";
            }
            else
            {
                if (!request.BookingId.HasValue)
                    throw new BadRequestException("Please provide BookingId when paying for a booking.");

                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.BookingId == request.BookingId.Value && b.UserId == userId);

                if (booking == null)
                    throw new NotFoundException("Booking not found or you do not have permission to pay.");

                if (booking.Status == "Cancelled" || booking.Status == "CancelledBySystem" || booking.Status == "NoShow")
                    throw new BadRequestException("Cannot pay for a cancelled or no-show booking.");

                if (await HasCompletedBookingPaymentAsync(booking.BookingId))
                    throw new BadRequestException("This booking has already been paid.");

                if (booking.FinalAmount <= 0)
                {
                    return new PaymentQrResponseDTO
                    {
                        PaymentUrl = "",
                        OrderCode = "",
                        PaymentType = "BookingPayment",
                        Amount = 0,
                        BookingId = booking.BookingId
                    };
                }

                amount = booking.FinalAmount;
                referenceBookingId = booking.BookingId;
                transactionType = "BookingPayment";
                transactionDescription = $"Yeu cau thanh toan booking #{booking.BookingId}";
                paymentDescription = $"Booking #{booking.BookingId}";
            }

            var payOsAmount = ToPayOsAmount(amount);
            var orderCode = GenerateOrderCode();

            var transaction = new Transaction
            {
                WalletId = wallet.WalletId,
                Amount = payOsAmount,
                TransactionType = transactionType,
                Description = transactionDescription,
                PaymentMethod = "PayOS",
                ReferenceBookingId = referenceBookingId,
                OrderCode = orderCode.ToString(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _context.Transactions.Add(transaction);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new BadRequestException($"Could not create payment transaction. Check if Transactions table has OrderCode, ReferenceBookingId, Status columns. DB Error: {ex.InnerException?.Message ?? ex.Message}");
            }

            var paymentRequest = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = payOsAmount,
                Description = paymentDescription,
                CancelUrl = request.CancelUrl,
                ReturnUrl = request.ReturnUrl
            };

            var createPaymentResult = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);

            return new PaymentQrResponseDTO
            {
                PaymentUrl = createPaymentResult.CheckoutUrl,
                OrderCode = orderCode.ToString(),
                PaymentType = transactionType,
                Amount = payOsAmount,
                BookingId = referenceBookingId
            };
        }

        public async Task ProcessPayOsWebhookAsync(WebhookTopUpDTO webhookData)
        {
            if (webhookData.Data == null)
            {
                _logger.LogWarning("Webhook contains no data. Code: {Code}", webhookData.Code);
                return;
            }

            WebhookData data;
            try
            {
                data = await VerifyPayOsWebhookAsync(webhookData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid PayOS webhook.");
                throw new UnauthorizedException("Invalid PayOS webhook.");
            }

            if (webhookData.Code != "00" || !webhookData.Success)
            {
                _logger.LogWarning("Webhook reported unsuccessful payment. Code: {Code}", webhookData.Code);
                return;
            }

            var orderCodeStr = data.OrderCode.ToString();

            var transaction = await _context.Transactions
                .Include(t => t.Wallet)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCodeStr);

            if (transaction == null)
            {
                _logger.LogWarning("Transaction with OrderCode not found: {OrderCode}", data.OrderCode);
                throw new NotFoundException("PayOS transaction corresponding to orderCode not found.");
            }

            if (transaction.Status != "Pending")
            {
                _logger.LogInformation("Transaction {OrderCode} is currently in {Status} status.", data.OrderCode, transaction.Status);
                return;
            }

            await ConfirmTransactionPaymentAsync(transaction.TransactionId, data.Amount, orderCodeStr);
        }

        public async Task<List<TransactionResponseDTO>> GetTransactionsAsync(int userId)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null) return new List<TransactionResponseDTO>();

            return await _context.Transactions
                .Where(t => t.WalletId == wallet.WalletId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TransactionResponseDTO
                {
                    TransactionId = t.TransactionId,
                    Amount = t.Amount,
                    TransactionType = t.TransactionType,
                    Description = t.Description,
                    Status = t.Status,
                    OrderCode = t.OrderCode,
                    ReferenceBookingId = t.ReferenceBookingId,
                    CreatedAt = t.CreatedAt
                }).ToListAsync();
        }

        private async Task SendBookingPaymentConfirmationEmailAsync(int bookingId)
        {
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.BookingDetails)
                        .ThenInclude(bd => bd.Service)
                    .Include(b => b.User)
                        .ThenInclude(u => u.CustomerProfile)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId);

                if (booking?.User == null || string.IsNullOrWhiteSpace(booking.User.Email))
                {
                    _logger.LogWarning("Cannot send email for booking #{BookingId}: invalid user/email.", bookingId);
                    return;
                }

                var customerName = booking.User.CustomerProfile?.FullName ?? "Quy khach";
                var emailHtml = EmailTemplateBuilder.BuildBookingConfirmationEmail(
                    booking,
                    booking.BookingDetails.ToList(),
                    customerName);

                await _emailService.SendEmailAsync(
                    booking.User.Email,
                    $"[SmartWash] Booking successful - #{booking.BookingId}",
                    emailHtml);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send confirmation email for booking #{BookingId} after QR payment.", bookingId);
            }
        }

        public async Task<List<PointHistoryResponseDTO>> GetPointsHistoryAsync(int userId)
        {
            return await _context.PointLedgers
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.TransactionDate)
                .Select(p => new PointHistoryResponseDTO
                {
                    LedgerId = p.LedgerId,
                    PointsAdded = p.PointsAdded,
                    PointsDeducted = p.PointsDeducted,
                    Reason = p.Reason,
                    TransactionDate = p.TransactionDate
                }).ToListAsync();
        }

        public async Task DeductSpendablePointsAsync(int userId, int pointsToDeduct, string reason)
        {
            if (pointsToDeduct <= 0) throw new BadRequestException("Deducted points must be greater than 0.");

            try
            {
                var profile = await _context.CustomerProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);
                if (profile == null) throw new NotFoundException("Customer profile not found.");

                if (profile.TotalPoint < pointsToDeduct)
                    throw new BadRequestException($"Insufficient available points. You have {profile.TotalPoint} points.");

                profile.TotalPoint -= pointsToDeduct;

                _context.PointLedgers.Add(new PointLedger
                {
                    UserId = userId,
                    PointsDeducted = pointsToDeduct,
                    Reason = reason,
                    TransactionDate = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BadRequestException("Data was modified by another transaction. Please try again.");
            }
        }

        public async Task RefundSpendablePointsAsync(int userId, int pointsToRefund, string reason, int? referenceBookingId = null)
        {
            if (pointsToRefund <= 0) throw new BadRequestException("Refunded points must be greater than 0.");

            try
            {
                var profile = await _context.CustomerProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);
                if (profile == null) throw new NotFoundException("Customer profile not found.");

                profile.TotalPoint += pointsToRefund;

                _context.PointLedgers.Add(new PointLedger
                {
                    UserId = userId,
                    PointsAdded = pointsToRefund,
                    Reason = reason,
                    ReferenceBookingId = referenceBookingId,
                    TransactionDate = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BadRequestException("Data was modified by another transaction. Please try again.");
            }
        }

        public async Task MarkTransactionTerminalAsync(int transactionId, string terminalStatus)
        {
            var tx = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId);
            if (tx == null) return;
            if (string.Equals(tx.Status, "Completed", StringComparison.OrdinalIgnoreCase)) return;

            tx.Status = terminalStatus;
            await _context.SaveChangesAsync();
        }

        public async Task ConfirmTransactionPaymentAsync(int transactionId, decimal? webhookAmount, string orderCode)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.Wallet)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
                
                if (transaction == null) return;
                if (transaction.Status == "Completed") return;

                if (webhookAmount.HasValue && transaction.Amount != webhookAmount.Value)
                {
                    throw new BadRequestException("Webhook amount does not match the pending transaction.");
                }

                if (transaction.TransactionType == "Topup")
                {
                    if (transaction.Wallet == null)
                        throw new BadRequestException("Wallet top-up transaction is missing wallet info.");

                    transaction.Status = "Completed";
                    transaction.Description = $"Top-up successful (Code: {orderCode})";
                    transaction.Wallet.Balance += transaction.Amount;
                }
                else if (transaction.TransactionType == "BookingPayment" || transaction.TransactionType == "WalkInPayment")
                {
                    if (!transaction.ReferenceBookingId.HasValue)
                        throw new BadRequestException("Booking payment transaction is missing booking ID.");

                    transaction.Status = "Completed";
                    transaction.Description = transaction.TransactionType == "WalkInPayment" 
                        ? $"Walk-in payment successful (Code: {orderCode})" 
                        : $"Booking payment successful (Code: {orderCode})";

                    var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == transaction.ReferenceBookingId.Value);
                    if (booking != null)
                    {
                        booking.UpdatedAt = DateTime.UtcNow;

                        var otherPendingBookingPayments = await _context.Transactions
                            .Where(t => t.ReferenceBookingId == booking.BookingId
                                     && t.TransactionId != transaction.TransactionId
                                     && (t.TransactionType == "BookingPayment" || t.TransactionType == "WalkInPayment")
                                     && t.Status == "Pending")
                            .ToListAsync();

                        foreach (var pendingPayment in otherPendingBookingPayments)
                        {
                            pendingPayment.Status = "Expired";
                        }

                        if (transaction.TransactionType == "WalkInPayment" && booking.ProcessingLaneId == null && booking.Status == "CheckedIn")
                        {
                            var bestLaneId = await _laneSchedulerService.GetBestAvailableLaneAsync(booking.BranchId, booking.BookingType == "Business");
                            if (bestLaneId > 0)
                            {
                                booking.ProcessingLaneId = bestLaneId;
                            }
                        }
                    }
                }
                else
                {
                    throw new BadRequestException("Transaction type not supported for webhook.");
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                if ((transaction.TransactionType == "BookingPayment" || transaction.TransactionType == "WalkInPayment") && transaction.ReferenceBookingId.HasValue)
                {
                    _ = Task.Run(() => SendBookingPaymentConfirmationEmailAsync(transaction.ReferenceBookingId.Value));
                }
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        public async Task<int> AwardCompletionPointsAsync(int userId, int pointsEarned, int bookingId)
        {
            if (pointsEarned <= 0) throw new BadRequestException("Bonus points must be greater than 0.");

            try
            {
                var profile = await _context.CustomerProfiles
                    .Include(cp => cp.Tier)
                    .FirstOrDefaultAsync(cp => cp.UserId == userId);

                if (profile == null) throw new NotFoundException("Customer profile not found.");

                profile.TotalPoint += pointsEarned;
                // LUỒNG 1: Tiền tệ (Đưa vào ví và sổ cái, hạn 1 năm)
                profile.PromotionPoint += pointsEarned;

                _context.PointLedgers.Add(new PointLedger
                {
                    UserId = userId,
                    PointsAdded = pointsEarned,
                    Reason = $"{PointConstants.CompletionReasonPrefix} #{bookingId}",
                    ExpiryDate = DateTime.UtcNow.AddYears(1),
                    ReferenceBookingId = bookingId,
                    TransactionDate = DateTime.UtcNow
                });

                // LUỒNG 2: Danh vọng (Cộng thẳng vào điểm xét hạng năm nay)
                profile.CurrentYearTierPoints += pointsEarned;

                await _tierService.EvaluateTierForProfileAsync(profile.UserId);

                await _context.SaveChangesAsync();
                return pointsEarned;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BadRequestException("Data was modified by another transaction. Please try again.");
            }
        }
        public async Task RefundBalanceAsync(int userId, decimal amount, string reason)
        {
            if (amount <= 0) throw new BadRequestException("Refund amount must be greater than 0.");

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null) throw new NotFoundException("User wallet not found.");

            wallet.Balance += amount;

            var transaction = new Transaction
            {
                WalletId = wallet.WalletId,
                Amount = amount,
                TransactionType = "Refund",
                Description = reason,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
        }

        private static long GenerateOrderCode()
        {
            var timestampPart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000_000;
            var randomPart = Random.Shared.Next(10, 99);
            return timestampPart * 100 + randomPart;
        }

        private static int ToPayOsAmount(decimal amount)
        {
            if (amount <= 0)
                throw new BadRequestException("PayOS payment amount must be greater than 0.");

            if (amount != decimal.Truncate(amount))
                throw new BadRequestException("PayOS only supports whole VND amounts, without decimal parts.");

            if (amount > int.MaxValue)
                throw new BadRequestException("PayOS payment amount exceeds the supported limit.");

            return decimal.ToInt32(amount);
        }

        private static string NormalizePaymentType(string paymentType)
        {
            if (string.Equals(paymentType, "Topup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentType, "TopUp", StringComparison.OrdinalIgnoreCase))
                return "Topup";

            if (string.Equals(paymentType, "BookingPayment", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentType, "Booking", StringComparison.OrdinalIgnoreCase))
                return "BookingPayment";

            throw new BadRequestException("PaymentType only supports Topup or BookingPayment.");
        }

        private Task<bool> HasCompletedBookingPaymentAsync(int bookingId)
        {
            return _context.Transactions.AnyAsync(t =>
                t.ReferenceBookingId == bookingId
                && t.Status == "Completed"
                && (t.TransactionType == "Payment"
                    || t.TransactionType == "BookingPayment"
                    || t.TransactionType == "WalkInPayment"));
        }

        private async Task<WebhookData> VerifyPayOsWebhookAsync(WebhookTopUpDTO webhookData)
        {
            var data = new WebhookData
            {
                OrderCode = webhookData.Data!.OrderCode,
                Amount = webhookData.Data.Amount,
                Description = webhookData.Data.Description,
                AccountNumber = webhookData.Data.AccountNumber ?? "",
                Reference = webhookData.Data.Reference ?? "",
                TransactionDateTime = webhookData.Data.TransactionDateTime ?? "",
                Currency = webhookData.Data.Currency ?? "VND",
                PaymentLinkId = webhookData.Data.PaymentLinkId ?? "",
                Code = webhookData.Data.Code ?? ""
            };

            SetWebhookDataPropertyIfExists(data, "Description2", webhookData.Data.Desc ?? "");
            SetWebhookDataPropertyIfExists(data, "VirtualAccountNumber", webhookData.Data.VirtualAccountNumber ?? "");
            SetWebhookDataPropertyIfExists(data, "CounterAccountBankId", webhookData.Data.CounterAccountBankId ?? "");
            SetWebhookDataPropertyIfExists(data, "CounterAccountBankName", webhookData.Data.CounterAccountBankName ?? "");
            SetWebhookDataPropertyIfExists(data, "CounterAccountName", webhookData.Data.CounterAccountName ?? "");
            SetWebhookDataPropertyIfExists(data, "CounterAccountNumber", webhookData.Data.CounterAccountNumber ?? "");
            SetWebhookDataPropertyIfExists(data, "VirtualAccountName", webhookData.Data.VirtualAccountName ?? "");

            var webhook = new Webhook
            {
                Code = webhookData.Code,
                Description = webhookData.Desc ?? webhookData.Description ?? "",
                Success = webhookData.Success,
                Signature = webhookData.Signature,
                Data = data
            };

            SetWebhookPropertyIfExists(webhook, "Desc", webhookData.Desc ?? webhookData.Description ?? "");

            return await _payOSClient.Webhooks.VerifyAsync(webhook);
        }

        private static void SetWebhookPropertyIfExists(Webhook webhook, string propertyName, string value)
        {
            var property = typeof(Webhook).GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(webhook, value);
            }
        }

        private static void SetWebhookDataPropertyIfExists(WebhookData data, string propertyName, string value)
        {
            var property = typeof(WebhookData).GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(data, value);
            }
        }
    }
}
