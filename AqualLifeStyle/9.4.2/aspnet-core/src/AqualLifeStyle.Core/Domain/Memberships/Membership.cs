using System;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Domain.Memberships
{
    public class Membership : Entity<int>
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public bool IsActive { get; private set; }
        public MembershipType MembershipType { get; private set; }
        public DateTime? ActivationDate { get; private set; }
        public decimal MonthlyObligationAmount { get; private set; }
        public DateTime? LastObligationMetDate { get; private set; }

        protected Membership()
        {
        }

        private Membership(string name, string description, MembershipType membershipType, bool isActive = true)
        {
            SetName(name);
            Description = description?.Trim();
            MembershipType = membershipType;
            IsActive = isActive;
            MonthlyObligationAmount = GetDefaultMonthlyObligation(membershipType);
        }

        public static Membership Create(string name, string description, MembershipType membershipType = MembershipType.Standard)
        {
            return new Membership(name, description, membershipType, true);
        }

        public void Rename(string name)
        {
            SetName(name);
        }

        public void UpdateDescription(string description)
        {
            Description = description?.Trim();
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void ChangeType(MembershipType membershipType)
        {
            MembershipType = membershipType;
            MonthlyObligationAmount = GetDefaultMonthlyObligation(membershipType);
        }

        public void SetActivationDate(DateTime activationDate)
        {
            if (activationDate == default)
            {
                throw new ArgumentException("Activation date must be valid.", nameof(activationDate));
            }

            ActivationDate = activationDate;
        }

        public void SetMonthlyObligation(decimal amount)
        {
            if (amount < 0)
            {
                throw new ArgumentException("Monthly obligation amount cannot be negative.", nameof(amount));
            }

            MonthlyObligationAmount = amount;
        }

        public void MarkObligationMet(DateTime asOf)
        {
            if (asOf == default)
            {
                throw new ArgumentException("Date must be valid.", nameof(asOf));
            }

            LastObligationMetDate = asOf;
        }

        public bool IsObligationMetForMonth(DateTime month)
        {
            if (!ActivationDate.HasValue)
            {
                return false;
            }

            if (!LastObligationMetDate.HasValue)
            {
                return false;
            }

            var targetMonth = new DateTime(month.Year, month.Month, 1);
            var obligationMonth = new DateTime(LastObligationMetDate.Value.Year, LastObligationMetDate.Value.Month, 1);

            return obligationMonth >= targetMonth;
        }

        public void EnsureCanBeAssignedToCustomer()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("Inactive memberships cannot be assigned to new customers.");
            }
        }

        private void SetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Membership name is required.", nameof(name));
            }

            Name = name.Trim();
        }

        private static decimal GetDefaultMonthlyObligation(MembershipType type)
        {
            return type switch
            {
                MembershipType.Standard => 100m,
                MembershipType.Premium => 250m,
                MembershipType.Vip => 500m,
                _ => 100m
            };
        }
    }
}
