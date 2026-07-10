using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Events.Bus;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Enquiries
{
    public class EnquiryAppService : AqualLifeStyleAppServiceBase, IEnquiryAppService
    {
        private readonly IEnquiryRepository _enquiryRepository;
        private readonly IEventBus _eventBus;

        public EnquiryAppService(IEnquiryRepository enquiryRepository, IEventBus eventBus)
        {
            _enquiryRepository = enquiryRepository;
            _eventBus = eventBus;
        }

        public async Task<IReadOnlyList<EnquiryDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Enquiry lookup failed.");
            var enquiries = await _enquiryRepository.GetAllListAsync(e => e.TenantId == tenantId);
            return enquiries.Select(MapToDto).ToList();
        }

        public async Task<EnquiryDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            return MapToDto(enquiry);
        }

        public async Task CreateAsync(CreateEnquiryDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.CustomerId, nameof(input.CustomerId));
            AqualLifeStyleValidator.ValidId(input.ProductId, nameof(input.ProductId));
            AqualLifeStyleValidator.NotNullOrEmpty(input.Message, nameof(input.Message));

            var tenantId = GetRequiredTenantId("Enquiry creation failed.");
            var enquiry = Enquiry.Create(tenantId, input.CustomerId, input.ProductId, input.Message);
            await _enquiryRepository.InsertAsync(enquiry);
        }

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
            return MapToDto(enquiry);
        }

        public async Task<EnquiryDto> CloseAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            enquiry.Close();
            await _enquiryRepository.UpdateAsync(enquiry);
            return MapToDto(enquiry);
        }

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
            return MapToDto(enquiry);
        }

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
            return MapToDto(enquiry);
        }

        public async Task<EnquiryDto> ConvertToCustomerAsync(int id, ConvertEnquiryToCustomerDto input)
        {
            AqualLifeStyleValidator.ValidId(id);

            var enquiry = await GetEnquiryForCurrentTenantAsync(id);

            try
            {
                enquiry.ConvertToCustomer(enquiry.ReferredByFacilitatorId);
            }
            catch (InvalidOperationException ex)
            {
                throw new AqualLifeStyleBusinessRuleException(ex.Message);
            }

            await _enquiryRepository.UpdateAsync(enquiry);

            if (_eventBus != null)
            {
                var convertedEvent = new EnquiryConvertedEvent(
                    enquiry.Id,
                    enquiry.CustomerId,
                    enquiry.ProductId,
                    enquiry.ReferredByFacilitatorId,
                    enquiry.ConvertedAt ?? System.DateTime.UtcNow,
                    enquiry.TenantId);

                CurrentUnitOfWork.Completed += (sender, args) => _eventBus.Trigger(convertedEvent);
            }

            return MapToDto(enquiry);
        }

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
            return MapToDto(enquiry);
        }

        /// <summary>
        /// Record a follow-up attempt on an enquiry with outcome tracking.
        /// </summary>
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
            return MapFollowUpToDto(followUp);
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
                .Select(MapFollowUpToDto)
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
                .Select(MapToDto)
                .ToList();
        }

        private int GetRequiredTenantId(string operation)
        {
            if (!AbpSession.TenantId.HasValue)
            {
                throw new AqualLifeStyleAuthorizationException($"{operation} A tenant context is required.");
            }

            return AbpSession.TenantId.Value;
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

        private static EnquiryDto MapToDto(Enquiry enquiry)
        {
            return new EnquiryDto
            {
                Id = enquiry.Id,
                CustomerId = enquiry.CustomerId,
                ProductId = enquiry.ProductId,
                Message = enquiry.Message,
                Response = enquiry.Response,
                Status = (int)enquiry.Status,
                CreatedAt = enquiry.CreatedAt.ToString("u"),
                IsClosed = enquiry.Status == EnquiryStatus.Closed,
                IsPending = enquiry.Status == EnquiryStatus.Pending,
                AssignedToMemberId = enquiry.AssignedToMemberId,
                IsConverted = enquiry.IsConverted,
                ConvertedAt = enquiry.ConvertedAt?.ToString("u"),
                ConversionProbability = enquiry.ConversionProbability,
                LastFollowUpDate = enquiry.LastFollowUpDate,
                FollowUpCount = enquiry.GetFollowUpCount(),
                IsSalesReady = enquiry.IsSalesReady(),
                FollowUps = enquiry.FollowUps
                    .OrderByDescending(f => f.FollowUpDate)
                    .Select(MapFollowUpToDto)
                    .ToList()
            };
        }

        private static EnquiryFollowUpDto MapFollowUpToDto(EnquiryFollowUp followUp)
        {
            return new EnquiryFollowUpDto
            {
                Id = followUp.Id,
                EnquiryId = followUp.EnquiryId,
                FollowUpDate = followUp.FollowUpDate,
                FollowUpByMemberId = followUp.FollowUpByMemberId,
                FollowUpNotes = followUp.FollowUpNotes,
                Outcome = (int)followUp.Outcome,
                OutcomeText = followUp.Outcome.ToString(),
                ConversionProbability = followUp.ConversionProbability,
                IsResolved = followUp.IsResolved
            };
        }
    }
}
