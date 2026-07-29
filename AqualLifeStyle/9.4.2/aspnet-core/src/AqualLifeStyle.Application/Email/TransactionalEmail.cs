using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;

namespace AqualLifeStyle.Email
{
    public sealed class TransactionalEmail
    {
        public string Recipient { get; }
        public string Subject { get; }
        public string HtmlBody { get; }
        public string TextBody { get; }
        public string Reference { get; }

        public TransactionalEmail(string recipient, string subject, string htmlBody, string textBody, string reference)
        {
            Recipient = recipient;
            Subject = subject;
            HtmlBody = htmlBody;
            TextBody = textBody;
            Reference = reference;
        }
    }

    public interface ITransactionalEmailDeliveryGateway
    {
        Task<string> SendAsync(TransactionalEmail email, CancellationToken cancellationToken = default);
    }

    public sealed class TransactionalEmailTemplateBuilder : ITransientDependency
    {
        private const string Brand = "Aqua Lifestyle Club";

        public TransactionalEmail VerifyEmail(string name, string email, string verificationUrl, string reference)
            => Build(email, "Verify your Aqua Lifestyle Club email", name,
                "Verify your email address to finish setting up your Club Member account.",
                "Verify email", verificationUrl, reference);

        public TransactionalEmail PasswordReset(string name, string email, string resetUrl, string reference)
            => Build(email, "Reset your Aqua Lifestyle Club password", name,
                "A password reset was requested for your account. If this was not you, you can ignore this message.",
                "Reset password", resetUrl, reference);

        public TransactionalEmail EnquiryResponse(
            string name, string email, string originalEnquiry, string response, string reference)
        {
            var safeName = E(name);
            var safeQuestion = E(originalEnquiry);
            var safeResponse = E(response);
            return new TransactionalEmail(email, "The club team responded to your enquiry",
                $"<p>Hello {safeName},</p><p>The club team has responded to your enquiry.</p>" +
                $"<p><strong>Your enquiry</strong><br>{safeQuestion}</p>" +
                $"<p><strong>Response</strong><br>{safeResponse}</p><p>{Brand}</p>",
                $"Hello {name},\n\nYour enquiry:\n{originalEnquiry}\n\nResponse:\n{response}\n\n{Brand}", reference);
        }

        public TransactionalEmail PaymentConfirmation(
            string name, string email, string programme, decimal amount, string currency,
            string providerReference, DateTime confirmedAt, string reference)
        {
            var amountText = string.Format(CultureInfo.InvariantCulture, "{0} {1:N2}", currency, amount);
            var dateText = confirmedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            return new TransactionalEmail(email, $"{programme} payment confirmed",
                $"<p>Hello {E(name)},</p><p>Your <strong>{E(programme)}</strong> payment has been confirmed.</p>" +
                $"<p>Amount: {E(amountText)}<br>Reference: {E(providerReference)}<br>Confirmed: {E(dateText)}</p>" +
                $"<p>This confirmation is not a tax invoice.</p><p>{Brand}</p>",
                $"Hello {name},\n\nYour {programme} payment has been confirmed.\nAmount: {amountText}\n" +
                $"Reference: {providerReference}\nConfirmed: {dateText}\n\nThis confirmation is not a tax invoice.\n\n{Brand}", reference);
        }

        private static TransactionalEmail Build(
            string email, string subject, string name, string explanation, string action,
            string url, string reference)
        {
            return new TransactionalEmail(email, subject,
                $"<p>Hello {E(name)},</p><p>{E(explanation)}</p>" +
                $"<p><a href=\"{E(url)}\">{E(action)}</a></p>" +
                "<p>If the button does not work, copy this address into your browser:</p>" +
                $"<p>{E(url)}</p><p>{Brand}</p>",
                $"Hello {name},\n\n{explanation}\n\n{action}: {url}\n\n{Brand}", reference);
        }

        private static string E(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
