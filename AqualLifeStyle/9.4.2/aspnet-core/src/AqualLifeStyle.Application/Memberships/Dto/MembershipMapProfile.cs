using System;
using AutoMapper;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Application.Memberships.Dto
{
    public class MembershipMapProfile : Profile
    {
        public MembershipMapProfile()
        {
            CreateMap<Membership, MembershipDto>()
                .AfterMap((src, dest) =>
                {
                    dest.ActivationDate = FormatDate(src.ActivationDate);
                    dest.LastObligationMetDate = FormatDate(src.LastObligationMetDate);
                });

            CreateMap<TierBenefits, TierBenefitsDto>()
                .AfterMap((src, dest) =>
                {
                    dest.Tier = (int)src.Tier;
                    dest.IsOrderWindowOpen = src.IsOrderWindowOpen();
                    dest.IsSavingsWindowOpen = src.IsSavingsWindowOpen();
                });
        }

        private static string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("u") : null;
        }
    }
}
