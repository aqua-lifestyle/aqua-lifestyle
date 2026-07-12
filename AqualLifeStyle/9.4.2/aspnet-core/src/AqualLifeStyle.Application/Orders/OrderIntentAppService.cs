using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.ObjectMapping;
using Abp.UI;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Orders.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Orders;
using AqualLifeStyle.Domain.Products;

namespace AqualLifeStyle.Application.Orders
{
    [AbpAuthorize(PermissionNames.Pages_Orders)]
    public class OrderIntentAppService : AqualLifeStyleAppServiceBase, IOrderIntentAppService
    {
        private readonly IOrderIntentRepository _orderIntentRepository;
        private readonly IEnquiryRepository _enquiryRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IProductRepository _productRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IObjectMapper _objectMapper;

        public OrderIntentAppService(
            IOrderIntentRepository orderIntentRepository,
            IEnquiryRepository enquiryRepository,
            ICustomerRepository customerRepository,
            IProductRepository productRepository,
            IMembershipRepository membershipRepository,
            IObjectMapper objectMapper)
        {
            _orderIntentRepository = orderIntentRepository;
            _enquiryRepository = enquiryRepository;
            _customerRepository = customerRepository;
            _productRepository = productRepository;
            _membershipRepository = membershipRepository;
            _objectMapper = objectMapper;
        }

        public async Task<IReadOnlyList<OrderIntentDto>> GetAllAsync()
        {
            var orderIntents = await _orderIntentRepository.GetAllListAsync();
            return _objectMapper.Map<List<OrderIntentDto>>(orderIntents);
        }

        public async Task<OrderIntentDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var orderIntent = await _orderIntentRepository.GetAsync(id);
            if (orderIntent == null)
            {
                throw new AqualLifeStyleNotFoundException("OrderIntent", id);
            }

            return _objectMapper.Map<OrderIntentDto>(orderIntent);
        }

        [AbpAuthorize(AquaPermissions.Orders.Place)]
        public async Task<OrderIntentDto> CreateFromEnquiryAsync(int enquiryId)
        {
            AqualLifeStyleValidator.ValidId(enquiryId, nameof(enquiryId));

            var enquiry = await _enquiryRepository.GetAsync(enquiryId);
            if (enquiry == null)
            {
                throw new AqualLifeStyleNotFoundException("Enquiry", enquiryId);
            }

            if (!enquiry.IsConverted)
            {
                throw new AqualLifeStyleBusinessRuleException("Only converted enquiries can create order intents.");
            }

            var existingOrderIntent = await _orderIntentRepository.GetByEnquiryIdAsync(enquiryId);
            if (existingOrderIntent != null)
            {
                throw new AqualLifeStyleDuplicateException("OrderIntent", "EnquiryId", enquiryId);
            }

            var customer = await _customerRepository.GetAsync(enquiry.CustomerId);
            if (customer == null)
            {
                throw new AqualLifeStyleDependencyException("Customer", enquiry.CustomerId.ToString());
            }

            if (!await CurrentUserCanAccessCustomerAsync(customer))
            {
                throw new UserFriendlyException("Order intent creation failed.", "You do not have permission to create an order for this customer.");
            }

            if (!customer.IsActive)
            {
                throw new AqualLifeStyleBusinessRuleException("Inactive customers cannot create order intents.");
            }

            var product = await _productRepository.GetAsync(enquiry.ProductId);
            if (product == null)
            {
                throw new AqualLifeStyleDependencyException("Product", enquiry.ProductId.ToString());
            }

            if (!product.IsActive)
            {
                throw new AqualLifeStyleBusinessRuleException("Inactive products cannot be reserved.");
            }

            var membership = customer.MembershipId.HasValue
                ? await _membershipRepository.GetAsync(customer.MembershipId.Value)
                : null;

            EnsureMembershipAllowsReservation(customer, product, membership);

            var openOrderIntentCount = await _orderIntentRepository.CountOpenForCustomerAsync(customer.Id);
            var maxConcurrentOrders = membership?.GetMaxConcurrentOrders() ?? 1;
            if (openOrderIntentCount >= maxConcurrentOrders)
            {
                throw new AqualLifeStyleBusinessRuleException("Customer has reached the maximum number of open order intents for their tier.");
            }

            var now = DateTime.UtcNow;
            var reservedPrice = membership?.ApplyTierDiscount(product.Price) ?? product.Price;
            var orderIntent = OrderIntent.CreateReserved(
                enquiry.CustomerId,
                enquiry.ProductId,
                enquiry.Id,
                product.Price,
                reservedPrice,
                now);

            orderIntent.Id = await _orderIntentRepository.InsertAndGetIdAsync(orderIntent);

            return _objectMapper.Map<OrderIntentDto>(orderIntent);
        }

        [AbpAuthorize(AquaPermissions.Orders.Process)]
        public async Task<OrderIntentDto> CancelAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var orderIntent = await GetOrderIntentOrThrowAsync(id);
            var customer = await _customerRepository.GetAsync(orderIntent.CustomerId);
            if (!await CurrentUserCanAccessCustomerAsync(customer))
            {
                throw new UserFriendlyException("Order intent cancellation failed.", "You do not have permission to cancel this order intent.");
            }

            try
            {
                orderIntent.Cancel(DateTime.UtcNow);
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleInvalidStateException(ex.Message);
            }

            await _orderIntentRepository.UpdateAsync(orderIntent);
            return _objectMapper.Map<OrderIntentDto>(orderIntent);
        }

        [AbpAuthorize(AquaPermissions.Orders.Process)]
        public async Task<OrderIntentDto> CompleteAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var orderIntent = await GetOrderIntentOrThrowAsync(id);
            var customer = await _customerRepository.GetAsync(orderIntent.CustomerId);
            if (!await CurrentUserCanAccessCustomerAsync(customer))
            {
                throw new UserFriendlyException("Order intent completion failed.", "You do not have permission to complete this order intent.");
            }

            try
            {
                orderIntent.Complete(DateTime.UtcNow);
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleInvalidStateException(ex.Message);
            }

            await _orderIntentRepository.UpdateAsync(orderIntent);
            return _objectMapper.Map<OrderIntentDto>(orderIntent);
        }

        private async Task<OrderIntent> GetOrderIntentOrThrowAsync(int id)
        {
            var orderIntent = await _orderIntentRepository.GetAsync(id);
            if (orderIntent == null)
            {
                throw new AqualLifeStyleNotFoundException("OrderIntent", id);
            }

            return orderIntent;
        }

        private void EnsureMembershipAllowsReservation(Customer customer, Product product, Membership membership)
        {
            var eligibilityManager = new ProductEligibilityManager(_membershipRepository);
            var canViewProduct = eligibilityManager.CanViewProduct(customer, product, membership);
            if (!canViewProduct)
            {
                throw new AqualLifeStyleBusinessRuleException("Customer membership does not allow this product reservation.");
            }

            if (membership != null && !membership.IsOrderWindowOpen())
            {
                throw new AqualLifeStylePreconditionException("Customer membership order window is currently closed.", "ORDER_WINDOW_CLOSED");
            }
        }

    }
}
