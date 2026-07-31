using AqualLifeStyle.Domain.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AccountEmailThrottleConfiguration
        : IEntityTypeConfiguration<AccountEmailThrottle>
    {
        public void Configure(EntityTypeBuilder<AccountEmailThrottle> builder)
        {
            builder.ToTable("AccountEmailThrottles");
            builder.Property(throttle => throttle.Id)
                .HasMaxLength(AccountEmailThrottle.MaxKeyLength);
            builder.HasIndex(throttle => throttle.ExpiresAt);
        }
    }
}
