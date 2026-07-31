namespace AqualLifeStyle.Authorization.Accounts.Dto
{
    public class RegisterOutput
    {
        public bool CanLogin { get; set; }
        public bool RequiresEmailVerification { get; set; }
    }
}
