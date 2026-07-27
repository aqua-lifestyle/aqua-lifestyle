namespace AqualLifeStyle.Payments.Yoco
{
    public static class YocoCheckoutMetadata
    {
        public const string DirectOnyxCheckoutIntentId = "directOnyxCheckoutIntentId";
        public const string AQGreenJoiningCheckoutId = "aqGreenJoiningCheckoutId";
        public const string ProviderCheckoutId = "checkoutId";

        public static bool IsSupportedReference(string key) =>
            key == DirectOnyxCheckoutIntentId || key == AQGreenJoiningCheckoutId;
    }
}
