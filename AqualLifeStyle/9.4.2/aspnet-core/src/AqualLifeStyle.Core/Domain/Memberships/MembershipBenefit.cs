using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Domain.Memberships
{
    public class MembershipBenefit : Entity<int>
    {
        public MembershipType MembershipType { get; private set; }
        public string BenefitName { get; private set; }
        public string Description { get; private set; }
        public decimal? BenefitValue { get; private set; } // e.g., discount percentage or points multiplier
        public bool IsActive { get; private set; }

        protected MembershipBenefit() { }

        private MembershipBenefit(MembershipType membershipType, string benefitName, string description, decimal? benefitValue = null, bool isActive = true)
        {
            ValidateBenefitName(benefitName);
            MembershipType = membershipType;
            BenefitName = benefitName.Trim();
            Description = description?.Trim();
            BenefitValue = benefitValue;
            IsActive = isActive;
        }

        public static MembershipBenefit Create(MembershipType membershipType, string benefitName, string description, decimal? benefitValue = null)
        {
            return new MembershipBenefit(membershipType, benefitName, description, benefitValue, true);
        }

        public void UpdateBenefit(string benefitName, string description, decimal? benefitValue)
        {
            ValidateBenefitName(benefitName);
            BenefitName = benefitName.Trim();
            Description = description?.Trim();
            BenefitValue = benefitValue;
        }

        public void Activate() => IsActive = true;

        public void Deactivate() => IsActive = false;

        private void ValidateBenefitName(string benefitName)
        {
            if (string.IsNullOrWhiteSpace(benefitName))
            {
                throw new ArgumentException("Benefit name is required.", nameof(benefitName));
            }
        }
    }
}
