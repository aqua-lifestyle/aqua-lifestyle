namespace AqualLifeStyle.Application.MyAccount.Dto
{
    public class ChangeMyPasswordResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; }

        public static ChangeMyPasswordResult Success() => new ChangeMyPasswordResult
        {
            Succeeded = true,
            Message = "Your password was changed. Sign in again with your new password."
        };

        public static ChangeMyPasswordResult Failure(string message) => new ChangeMyPasswordResult
        {
            Succeeded = false,
            Message = message
        };
    }
}
