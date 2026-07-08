using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Orders.Dto;
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

        public OrderIntentAppService(
            IOrderIntentRepository orderIntentRepository,
            IEnquiryRepository enquiryRepository,
            ICustomerRepository customerRepository,
            IProductRepository productRepository,
            IMembershipRepository membershipRepository)
        {
            _orderIntentRepository = orderIntentRepository;
            _enquiryRepository = enquiryRepository;
            _customerRepository = customerRepository;
            _productRepository = productRepository;
            _membershipRepository = membershipRepository;
        }

        public async Task<IReadOnlyList<OrderIntentDto>> GetAllAsync()
        {
            var orderIntents = await _orderIntentRepository.GetAllListAsync();
            return orderIntents.Select(MapToDto).ToList();
        }

        public async Task<OrderIntentDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var orderIntent = await _orderIntentRepository.GetAsync(id);
            if (orderIntent == null)
            {
                throw new AqualLifeStyleNotFoundException("OrderIntent", id);
            }

            return MapToDto(orderIntent);
        }

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

            await _orderIntentRepository.InsertAsync(orderIntent);
            return MapToDto(orderIntent);
        }

        public async Task<OrderIntentDto> CancelAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var orderIntent = await GetOrderIntentOrThrowAsync(id);

            try
            {
                orderIntent.Cancel(DateTime.UtcNow);
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleInvalidStateException(ex.Message);
            }

            await _orderIntentRepository.UpdateAsync(orderIntent);
            return MapToDto(orderIntent);
        }

        public async Task<OrderIntentDto> CompleteAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var orderIntent = await GetOrderIntentOrThrowAsync(id);

            try
            {
                orderIntent.Complete(DateTime.UtcNow);
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleInvalidStateException(ex.Message);
            }

            await _orderIntentRepository.UpdateAsync(orderIntent);
            return MapToDto(orderIntent);
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

        private static OrderIntentDto MapToDto(OrderIntent orderIntent)
        {
            return new OrderIntentDto
            {
                Id = orderIntent.Id,
                CustomerId = orderIntent.CustomerId,
                ProductId = orderIntent.ProductId,
                EnquiryId = orderIntent.EnquiryId,
                UnitPrice = orderIntent.UnitPrice,
                ReservedPrice = orderIntent.ReservedPrice,
                Status = (int)orderIntent.Status,
                StatusText = orderIntent.Status.ToString(),
                CreatedAt = orderIntent.CreatedAt,
                ReservedAt = orderIntent.ReservedAt,
                CancelledAt = orderIntent.CancelledAt,
                CompletedAt = orderIntent.CompletedAt
            };
        }
    }
}
