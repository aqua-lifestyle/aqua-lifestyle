namespace AqualLifeStyle.Payments.Yoco
{
    public static class YocoCheckoutMetadata
    {
        public const string DirectOnyxCheckoutIntentId = "directOnyxCheckoutIntentId";
        public const string AQGreenJoiningCheckoutId = "aqGreenJoiningCheckoutId";
        public const string AQGreenMonthlyObligationCheckoutId =
            "aqGreenMonthlyObligationCheckoutId";
        public const string ProviderCheckoutId = "checkoutId";
        public const string Purpose = "purpose";
        public const string AQGreenJoiningPurpose = "AQGreenJoining";
        public const string DirectOnyxPurpose = "OnyxDirectEntry";
        public const string AQGreenMonthlyObligationPurpose =
            "AQGreenMonthlyCommitment";

        public static bool IsSupportedReference(string key) =>
            key == DirectOnyxCheckoutIntentId ||
            key == AQGreenJoiningCheckoutId ||
            key == AQGreenMonthlyObligationCheckoutId;
    }
}
