using Abp.MultiTenancy;
using AqualLifeStyle.Authorization.Users;

namespace AqualLifeStyle.MultiTenancy
{
    public class Tenant : AbpTenant<User>
    {
        public int? AreaLeaderId { get; private set; }

        public Tenant()
        {            
        }

        public Tenant(string tenancyName, string name)
            : base(tenancyName, name)
        {
        }

        public void AssignAreaLeader(int areaLeaderId)
        {
            if (areaLeaderId <= 0) throw new System.ArgumentException("AreaLeaderId must be valid.", nameof(areaLeaderId));
            AreaLeaderId = areaLeaderId;
        }
    }
}
