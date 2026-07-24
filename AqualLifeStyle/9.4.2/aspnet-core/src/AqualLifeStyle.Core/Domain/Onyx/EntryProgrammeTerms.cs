using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class EntryProgrammeTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public decimal RegistrationPaymentAmount { get; }
        public decimal ActivationPaymentAmount { get; }
        public decimal MonthlyCommitmentAmount { get; }
        public int GracePeriodDays { get; }
        public string Currency { get; }

        private EntryProgrammeTerms(
            string version,
            DateTime effectiveFrom,
            decimal registrationPaymentAmount,
            decimal activationPaymentAmount,
            decimal monthlyCommitmentAmount,
            int gracePeriodDays,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("A terms version is required.", nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException("An effective date is required.", nameof(effectiveFrom));
            }

            EnsurePositive(registrationPaymentAmount, nameof(registrationPaymentAmount));
            EnsurePositive(activationPaymentAmount, nameof(activationPaymentAmount));
            EnsurePositive(monthlyCommitmentAmount, nameof(monthlyCommitmentAmount));
            if (gracePeriodDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gracePeriodDays), "Grace period days cannot be negative.");
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            RegistrationPaymentAmount = registrationPaymentAmount;
            ActivationPaymentAmount = activationPaymentAmount;
            MonthlyCommitmentAmount = monthlyCommitmentAmount;
            GracePeriodDays = gracePeriodDays;
            Currency = NormalizeCurrency(currency);
        }

        public static EntryProgrammeTerms Create(
            string version,
            DateTime effectiveFrom,
            decimal registrationPaymentAmount,
            decimal activationPaymentAmount,
            decimal monthlyCommitmentAmount,
            int gracePeriodDays,
            string currency = "ZAR")
        {
            return new EntryProgrammeTerms(
                version,
                effectiveFrom,
                registrationPaymentAmount,
                activationPaymentAmount,
                monthlyCommitmentAmount,
                gracePeriodDays,
                currency);
        }

        private static void EnsurePositive(decimal amount, string parameterName)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "The amount must be greater than zero.");
            }
        }

        private static string NormalizeCurrency(string currency)
        {
            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException("A three-letter currency code is required.", nameof(currency));
            }

            return currency.Trim().ToUpperInvariant();
        }
    }
}
