using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Runtime.Caching;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Application.Memberships
{
    [Serializable]
    public class ActiveMembershipCacheItem
    {
        public int? MembershipId { get; }

        public ActiveMembershipCacheItem(int? membershipId)
        {
            MembershipId = membershipId;
        }
    }

    public interface IActiveMembershipCache
    {
        Task<int?> GetFirstActiveMembershipIdAsync(int? tenantId);
        void Remove(int? tenantId);
    }

    public class ActiveMembershipCache : IActiveMembershipCache, ITransientDependency
    {
        private const string CacheName = "ActiveMembershipLookupCache";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private readonly IMembershipRepository _membershipRepository;
        private readonly ITypedCache<string, ActiveMembershipCacheItem> _cache;

        public ActiveMembershipCache(IMembershipRepository membershipRepository, ICacheManager cacheManager)
        {
            _membershipRepository = membershipRepository;
            _cache = cacheManager.GetCache<string, ActiveMembershipCacheItem>(CacheName);
        }

        public async Task<int?> GetFirstActiveMembershipIdAsync(int? tenantId)
        {
            var cacheKey = GetCacheKey(tenantId);
            var cachedItem = await _cache.GetOrDefaultAsync(cacheKey);
            if (cachedItem != null)
            {
                return cachedItem.MembershipId;
            }

            var membership = await _membershipRepository.GetFirstActiveAsync(tenantId);
            var cacheItem = new ActiveMembershipCacheItem(membership?.Id);
            await _cache.SetAsync(cacheKey, cacheItem, CacheDuration);
            return cacheItem.MembershipId;
        }

        public void Remove(int? tenantId)
        {
            _cache.Remove(GetCacheKey(tenantId));
        }

        private static string GetCacheKey(int? tenantId)
        {
            return tenantId?.ToString() ?? "host";
        }
    }
}
