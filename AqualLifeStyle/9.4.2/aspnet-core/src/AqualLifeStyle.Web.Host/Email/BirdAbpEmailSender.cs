using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Abp.Net.Mail;
using AqualLifeStyle.Email;

namespace AqualLifeStyle.Web.Host.Email
{
    /// <summary>
    /// Keeps ABP framework email consumers behind the same Bird integration
    /// point. Application business notifications use the durable outbox.
    /// </summary>
    public sealed class BirdAbpEmailSender : EmailSenderBase
    {
        private readonly ITransactionalEmailDeliveryGateway _gateway;

        public BirdAbpEmailSender(
            IEmailSenderConfiguration configuration,
            ITransactionalEmailDeliveryGateway gateway)
            : base(configuration)
        {
            _gateway = gateway;
        }

        protected override async Task SendEmailAsync(MailMessage mail)
        {
            if (mail.To.Count != 1)
                throw new InvalidOperationException("Bird transactional email requires exactly one recipient.");
            if (mail.CC.Count > 0 || mail.Bcc.Count > 0)
                throw new InvalidOperationException("Bird transactional email does not support CC or Bcc recipients.");
            var body = mail.Body ?? string.Empty;
            await _gateway.SendAsync(new TransactionalEmail(
                mail.To.Single().Address,
                mail.Subject,
                mail.IsBodyHtml ? body : "<p>" + System.Net.WebUtility.HtmlEncode(body) + "</p>",
                mail.IsBodyHtml ? "Please view this message in an HTML-compatible email client." : body,
                "abp-email:" + Guid.NewGuid().ToString("N")));
        }

        protected override void SendEmail(MailMessage mail)
            => throw new NotSupportedException("Use the asynchronous email API.");
    }
}
