using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class EntryProgrammeTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public decimal JoiningPaymentAmount { get; }
        public decimal JoiningInstallmentAmount { get; }
        public decimal RegistrationPaymentAmount { get; }
        public decimal ActivationPaymentAmount { get; }
        public decimal MonthlyCommitmentAmount { get; }
        public int GracePeriodDays { get; }
        public string Currency { get; }

        private EntryProgrammeTerms(
            string version,
            DateTime effectiveFrom,
            decimal joiningPaymentAmount,
            decimal joiningInstallmentAmount,
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

            if (joiningPaymentAmount > 0m)
            {
                EnsurePositive(joiningPaymentAmount, nameof(joiningPaymentAmount));
                if (joiningInstallmentAmount < 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(joiningInstallmentAmount));
                }
                if (joiningInstallmentAmount > 0m && joiningInstallmentAmount * 2m != joiningPaymentAmount)
                {
                    throw new ArgumentException("Two joining instalments must equal the joining total.");
                }
            }
            else
            {
                EnsurePositive(registrationPaymentAmount, nameof(registrationPaymentAmount));
                EnsurePositive(activationPaymentAmount, nameof(activationPaymentAmount));
            }
            EnsurePositive(monthlyCommitmentAmount, nameof(monthlyCommitmentAmount));
            if (gracePeriodDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gracePeriodDays), "Grace period days cannot be negative.");
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            JoiningPaymentAmount = joiningPaymentAmount;
            JoiningInstallmentAmount = joiningInstallmentAmount;
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
                joiningPaymentAmount: 0m,
                joiningInstallmentAmount: 0m,
                registrationPaymentAmount,
                activationPaymentAmount,
                monthlyCommitmentAmount,
                gracePeriodDays,
                currency);
        }

        public static EntryProgrammeTerms CreateSingleJoiningPayment(
            string version,
            DateTime effectiveFrom,
            decimal joiningPaymentAmount,
            decimal monthlyCommitmentAmount,
            int gracePeriodDays,
            string currency = "ZAR")
        {
            return new EntryProgrammeTerms(
                version,
                effectiveFrom,
                joiningPaymentAmount,
                joiningInstallmentAmount: 0m,
                registrationPaymentAmount: 0m,
                activationPaymentAmount: 0m,
                monthlyCommitmentAmount,
                gracePeriodDays,
                currency);
        }

        public static EntryProgrammeTerms CreateFlexibleJoiningPayment(
            string version,
            DateTime effectiveFrom,
            decimal joiningPaymentAmount,
            decimal joiningInstallmentAmount,
            decimal monthlyCommitmentAmount,
            int gracePeriodDays,
            string currency = "ZAR")
        {
            return new EntryProgrammeTerms(
                version,
                effectiveFrom,
                joiningPaymentAmount,
                joiningInstallmentAmount,
                registrationPaymentAmount: 0m,
                activationPaymentAmount: 0m,
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
