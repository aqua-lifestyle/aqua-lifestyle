using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.MultiTenancy;
using Abp.UI;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Application.Admin.Customers
{
    public interface ICustomerMembershipPlanAssignmentValidator
    {
        Task EnsureAvailableForAreaAsync(int membershipPlanId, int areaId, string operation);
    }

    public class CustomerMembershipPlanAssignmentValidator : ICustomerMembershipPlanAssignmentValidator, ITransientDependency
    {
        private readonly IMembershipRepository _membershipRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public CustomerMembershipPlanAssignmentValidator(
            IMembershipRepository membershipRepository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _membershipRepository = membershipRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task EnsureAvailableForAreaAsync(int membershipPlanId, int areaId, string operation)
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                var membershipPlan = await _membershipRepository.FirstOrDefaultAsync(plan =>
                    plan.Id == membershipPlanId &&
                    (!plan.TenantId.HasValue || plan.TenantId == areaId));
                if (membershipPlan == null)
                    throw new UserFriendlyException($"{operation} failed.", "The selected membership plan is unavailable or inactive.");

                try
                {
                    membershipPlan.EnsureCanBeAssignedToCustomer();
                }
                catch (System.InvalidOperationException exception)
                {
                    throw new UserFriendlyException($"{operation} failed.", exception.Message);
                }
            }
        }
    }
}
