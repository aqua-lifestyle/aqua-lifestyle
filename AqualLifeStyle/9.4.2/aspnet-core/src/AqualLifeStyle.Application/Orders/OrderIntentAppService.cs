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
    public class OrderIntentAppService : AqualLifeStyleAppServiceBase, IOrderIntentAppService
    {
        private readonly IOrderIntentRepository _orderIntentRepository;
        private readonly IEnquiryRepository _enquiryRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IProductRepository _productRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IObjectMapper _objectMapper;
        protected virtual DateTime UtcNow => DateTime.UtcNow;

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

        [AbpAuthorize(AquaPermissions.Orders.View)]
        public async Task<IReadOnlyList<OrderIntentDto>> GetAllAsync()
        {
            var orderIntents = await _orderIntentRepository.GetAllListAsync();
            return _objectMapper.Map<List<OrderIntentDto>>(orderIntents);
        }

        [AbpAuthorize(AquaPermissions.Orders.ViewSelf)]
        public async Task<IReadOnlyList<OrderIntentDto>> GetMineAsync()
        {
            var customer = await GetCurrentCustomerAsync("Order lookup failed.");
            var orderIntents = await _orderIntentRepository.GetAllListAsync(
                orderIntent => orderIntent.CustomerId == customer.Id);
            return _objectMapper.Map<List<OrderIntentDto>>(orderIntents);
        }

        [AbpAuthorize(AquaPermissions.Orders.View)]
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
        public async Task<OrderIntentDto> CreateForCurrentCustomerAsync(int productId)
        {
            AqualLifeStyleValidator.ValidId(productId, nameof(productId));
            if (!AbpSession.UserId.HasValue)
            {
                throw new AqualLifeStyleAuthorizationException("Order placement requires an authenticated customer.");
            }

            var tenantId = GetRequiredTenantId("Order placement failed.");
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.UserId == AbpSession.UserId.Value);
            if (customer == null)
            {
                throw new UserFriendlyException("Order placement failed.", "No customer profile is linked to this account.");
            }

            if (!customer.IsActive)
            {
                throw new AqualLifeStyleBusinessRuleException("Inactive customers cannot create order intents.");
            }

            var product = await _productRepository.GetAsync(productId);
            if (product == null || !product.IsActive)
            {
                throw new AqualLifeStyleBusinessRuleException("The selected product is not available.");
            }

            if (!customer.MembershipId.HasValue)
            {
                throw new AqualLifeStyleBusinessRuleException("An active membership is required before placing an order.");
            }

            var membership = await _membershipRepository.GetAsync(customer.MembershipId.Value);
            EnsureMembershipAllowsReservation(customer, product, membership);

            var openOrderIntentCount = await _orderIntentRepository.CountOpenForCustomerAsync(customer.Id);
            var maxConcurrentOrders = membership.GetMaxConcurrentOrders();
            if (openOrderIntentCount >= maxConcurrentOrders)
            {
                throw new AqualLifeStyleBusinessRuleException("Customer has reached the maximum number of open order intents for their tier.");
            }

            var now = UtcNow;
            var reservedPrice = membership.ApplyTierDiscount(product.Price);
            var orderIntent = OrderIntent.CreateReserved(
                customer.Id,
                product.Id,
                null,
                product.Price,
                reservedPrice,
                now);
            orderIntent.Id = await _orderIntentRepository.InsertAndGetIdAsync(orderIntent);
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

            var now = UtcNow;
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
                orderIntent.Cancel(UtcNow);
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
                orderIntent.Complete(UtcNow);
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

        private async Task<Customer> GetCurrentCustomerAsync(string operation)
        {
            if (!AbpSession.UserId.HasValue)
                throw new AqualLifeStyleAuthorizationException($"{operation} A user context is required.");

            var tenantId = GetRequiredTenantId(operation);
            var customer = await _customerRepository.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.UserId == AbpSession.UserId.Value);
            if (customer == null)
                throw new UserFriendlyException(operation, "No customer profile is linked to this account.");

            return customer;
        }

        private void EnsureMembershipAllowsReservation(Customer customer, Product product, Membership membership)
        {
            var eligibilityManager = new ProductEligibilityManager(_membershipRepository);
            var canViewProduct = eligibilityManager.CanViewProduct(customer, product, membership);
            if (!canViewProduct)
            {
                throw new AqualLifeStyleBusinessRuleException("Customer membership does not allow this product reservation.");
            }

            if (membership != null && !membership.IsOrderWindowOpen(UtcNow))
            {
                throw new AqualLifeStylePreconditionException("Customer membership order window is currently closed.", "ORDER_WINDOW_CLOSED");
            }
        }

    }
}
