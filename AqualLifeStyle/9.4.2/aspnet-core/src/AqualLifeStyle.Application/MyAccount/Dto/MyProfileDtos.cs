using System.ComponentModel.DataAnnotations;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization.Users;

namespace AqualLifeStyle.Application.MyAccount.Dto
{
    public class MyProfileDto
    {
        public string FirstName { get; set; }
        public string Surname { get; set; }
        public string EmailAddress { get; set; }
        public string ContactNumber { get; set; }
        public string HomeAddress { get; set; }
    }

    public class UpdateMyProfileInput
    {
        [Required, StringLength(AbpUserBase.MaxNameLength, MinimumLength = 1)]
        public string FirstName { get; set; }

        [Required, StringLength(AbpUserBase.MaxSurnameLength, MinimumLength = 1)]
        public string Surname { get; set; }

        [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)]
        public string EmailAddress { get; set; }

        [Required, Phone, StringLength(AbpUserBase.MaxPhoneNumberLength)]
        public string ContactNumber { get; set; }

        [Required, StringLength(User.MaxHomeAddressLength, MinimumLength = 3)]
        public string HomeAddress { get; set; }
    }
}
