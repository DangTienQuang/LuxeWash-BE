using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
        Task SendEmailWithAttachmentAsync(
            string toEmail,
            string subject,
            string htmlMessage,
            byte[] attachment,
            string attachmentFileName,
            string attachmentContentType);
    }
}
