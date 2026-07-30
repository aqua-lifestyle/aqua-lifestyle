using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.ObjectMapping;
using Abp.UI;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Email;

namespace AqualLifeStyle.Application.Enquiries
{
    [AbpAuthorize(PermissionNames.Pages_Enquiries)]
    public class EnquiryAppService : AqualLifeStyleAppServiceBase, IEnquiryAppService
    {
        private readonly IEnquiryRepository _enquiryRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IObjectMapper _objectMapper;
        private readonly ITransactionalEmailOutbox _emailOutbox;
        private readonly TransactionalEmailTemplateBuilder _emailTemplates;

        public EnquiryAppService(
            IEnquiryRepository enquiryRepository,
            ICustomerRepository customerRepository,
            IObjectMapper objectMapper,
            ITransactionalEmailOutbox emailOutbox,
            TransactionalEmailTemplateBuilder emailTemplates)
        {
            _enquiryRepository = enquiryRepository;
            _customerRepository = customerRepository;
            _objectMapper = objectMapper;
            _emailOutbox = emailOutbox;
            _emailTemplates = emailTemplates;
        }

        public async Task<IReadOnlyList<EnquiryDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Enquiry lookup failed.");
            var enquiries = await _enquiryRepository.GetAllListAsync(e => e.TenantId == tenantId);
            return _objectMapper.Map<List<EnquiryDto>>(enquiries);
        }

        public async Task<EnquiryDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var enquiry = await GetEnquiryForCurrentTenantAsync(id);
            var customer = await _customerRepository.GetAsync(enquiry.CustomerId);
            if (!await CurrentUserCanAccessCustomerAsync(customer))
            {
                throw new UserFriendlyException("Enquiry lookup failed.", "You do not have permission to access this enquiry.");
            }

            return _objectMapper.Map<EnquiryDto>(enquiry);
        }

        [AbpAuthorize(AquaPermissions.Enquiries.Create)]
        public async Task CreateAsync(CreateEnquiryDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.CustomerId, nameof(input.CustomerId));
            AqualLifeStyleValidator.ValidId(input.ProductId, nameof(input.ProductId));
            AqualLifeStyleValidator.NotNullOrEmpty(input.Message, nameof(input.Message));

            var customer = await _customerRepository.GetAsync(input.CustomerId);
            if (!await CurrentUserCanAccessCustomerAsync(customer))
            {
                throw new UserFriendlyException("Enquiry creation failed.", "You do not have permission to create an enquiry for this customer.");
            }

            var tenantId = GetRequiredTenantId("Enquiry creation failed.");
            var enquiry = Enquiry.Create(tenantId, input.CustomerId, input.ProductId, input.Message);
            await _enquiryRepository.InsertAsync(enquiry);
        }

        [AbpAuthorize(AquaPermissions.Enquiries.Update)]
        public async Task<EnquiryDto> RespondAsync(int id, RespondToEnquiryDto input)
        {
            AqualLifeStyleValidator.ValidId(id);
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.NotNullOrEmpty(input.Response, nameof(input.Response));

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            try
            {
                enquiry.MarkAsResponded(input.Response);
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleInvalidStateException("Enquiry", enquiry.Status.ToString(), "respond");
            }

            await _enquiryRepository.UpdateAsync(enquiry);
            var customer = await _customerRepository.GetAsync(enquiry.CustomerId);
            var emailKey = $"enquiry-response:{enquiry.Id}:{enquiry.ResponseVersion}";
            await _emailOutbox.EnqueueAsync(enquiry.TenantId, "EnquiryResponse", emailKey,
                _emailTemplates.EnquiryResponse(
                    customer.Name, customer.Email.Value, enquiry.Message, enquiry.Response, emailKey));
            return _objectMapper.Map<EnquiryDto>(enquiry);
        }

        [AbpAuthorize(AquaPermissions.Enquiries.Resolve)]
        public async Task<EnquiryDto> CloseAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            enquiry.Close();
            await _enquiryRepository.UpdateAsync(enquiry);
            return _objectMapper.Map<EnquiryDto>(enquiry);
        }

        [AbpAuthorize(AquaPermissions.Enquiries.Update)]
        public async Task<EnquiryDto> ReopenAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            try
            {
                enquiry.Reopen();
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleInvalidStateException("Enquiry", enquiry.Status.ToString(), "reopen");
            }

            await _enquiryRepository.UpdateAsync(enquiry);
            return _objectMapper.Map<EnquiryDto>(enquiry);
        }

        [AbpAuthorize(AquaPermissions.Enquiries.Update)]
        public async Task<EnquiryDto> AssignToMemberAsync(int id, AssignEnquiryDto input)
        {
            AqualLifeStyleValidator.ValidId(id);
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.MemberId, nameof(input.MemberId));

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            try
            {
                enquiry.AssignToMember(input.MemberId);
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleBusinessRuleException(ex.Message);
            }

            await _enquiryRepository.UpdateAsync(enquiry);
            return _objectMapper.Map<EnquiryDto>(enquiry);
        }

        [AbpAuthorize(AquaPermissions.Enquiries.Resolve)]
        public async Task<EnquiryDto> ConvertToCustomerAsync(int id, ConvertEnquiryToCustomerDto input)
        {
            AqualLifeStyleValidator.ValidId(id);

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            try
            {
                enquiry.ConvertToCustomer();
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleBusinessRuleException(ex.Message);
            }

            await _enquiryRepository.UpdateAsync(enquiry);

            return _objectMapper.Map<EnquiryDto>(enquiry);
        }

        [AbpAuthorize(AquaPermissions.Enquiries.Update)]
        public async Task<EnquiryDto> ClearAssignmentAsync(int id, ClearAssignmentDto input)
        {
            AqualLifeStyleValidator.ValidId(id);

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            try
            {
                enquiry.ClearAssignment();
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleBusinessRuleException(ex.Message);
            }

            await _enquiryRepository.UpdateAsync(enquiry);
            return _objectMapper.Map<EnquiryDto>(enquiry);
        }

        /// <summary>
        /// Record a follow-up attempt on an enquiry with outcome tracking.
        /// </summary>
        [AbpAuthorize(AquaPermissions.Enquiries.Update)]
        public async Task<EnquiryFollowUpDto> RecordFollowUpAsync(int id, CreateEnquiryFollowUpDto input)
        {
            AqualLifeStyleValidator.ValidId(id);
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.NotNullOrEmpty(input.FollowUpNotes, nameof(input.FollowUpNotes));

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            try
            {
                var followUpOutcome = (EnquiryFollowUpOutcome)input.Outcome;
                enquiry.RecordFollowUp(input.FollowUpByMemberId, input.FollowUpNotes, followUpOutcome);
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleBusinessRuleException(ex.Message);
            }

            await _enquiryRepository.UpdateAsync(enquiry);

            var followUp = enquiry.GetLatestFollowUp();
            return _objectMapper.Map<EnquiryFollowUpDto>(followUp);
        }

        /// <summary>
        /// Get all follow-ups for an enquiry.
        /// </summary>
        public async Task<List<EnquiryFollowUpDto>> GetFollowUpsAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            return enquiry.FollowUps
                .OrderByDescending(f => f.FollowUpDate)
                .Select(followUp => _objectMapper.Map<EnquiryFollowUpDto>(followUp))
                .ToList();
        }

        /// <summary>
        /// Get enquiries that are ready for sales action based on conversion probability.
        /// </summary>
        public async Task<List<EnquiryDto>> GetSalesReadyEnquiriesAsync()
        {
            var tenantId = GetRequiredTenantId("Enquiry lookup failed.");
            var enquiries = await _enquiryRepository.GetAllListAsync(e => e.TenantId == tenantId);
            return enquiries
                .Where(e => e.IsSalesReady())
                .Select(enquiry => _objectMapper.Map<EnquiryDto>(enquiry))
                .ToList();
        }

        private async Task<Enquiry> GetEnquiryForCurrentTenantAsync(int id)
        {
            var tenantId = GetRequiredTenantId("Enquiry lookup failed.");
            var enquiry = await _enquiryRepository.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
            if (enquiry == null)
            {
                throw new AqualLifeStyleNotFoundException("Enquiry", id);
            }

            return enquiry;
        }

        protected override Exception CreateMissingTenantContextException(string operation)
        {
            return new AqualLifeStyleAuthorizationException($"{operation} A tenant context is required.");
        }

    }
}
