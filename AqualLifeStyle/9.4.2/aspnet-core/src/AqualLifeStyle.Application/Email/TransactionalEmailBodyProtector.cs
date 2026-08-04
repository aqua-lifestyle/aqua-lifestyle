using System;
using Abp.Dependency;
using Microsoft.AspNetCore.DataProtection;

namespace AqualLifeStyle.Email
{
    public interface ITransactionalEmailBodyProtector
    {
        string Protect(string body);
        string Unprotect(string storedBody);
    }

    public sealed class TransactionalEmailBodyProtector
        : ITransactionalEmailBodyProtector, ITransientDependency
    {
        public const string EnvelopePrefix = "aqua-email-dp:v1:";
        private const string Purpose = "AqualLifeStyle.TransactionalEmailOutbox.Body.v1";

        private readonly IDataProtector _protector;

        public TransactionalEmailBodyProtector(IDataProtectionProvider dataProtectionProvider)
        {
            if (dataProtectionProvider == null)
                throw new ArgumentNullException(nameof(dataProtectionProvider));

            _protector = dataProtectionProvider.CreateProtector(Purpose);
        }

        public string Protect(string body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return EnvelopePrefix + _protector.Protect(body);
        }

        public string Unprotect(string storedBody)
        {
            if (storedBody == null) throw new ArgumentNullException(nameof(storedBody));

            // The generic outbox predates body protection. Only an explicit envelope is
            // interpreted as protected so malformed protected data can never fall back.
            return storedBody.StartsWith(EnvelopePrefix, StringComparison.Ordinal)
                ? _protector.Unprotect(storedBody.Substring(EnvelopePrefix.Length))
                : storedBody;
        }
    }
}
