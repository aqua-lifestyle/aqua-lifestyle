using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class EntryMonthlyObligationDuePolicyConfiguration
        : IEntityTypeConfiguration<EntryMonthlyObligationDuePolicy>
    {
        public void Configure(EntityTypeBuilder<EntryMonthlyObligationDuePolicy> builder)
        {
            builder.ToTable("EntryMonthlyObligationDuePolicies", table =>
            {
                table.HasCheckConstraint(
                    "CK_EntryMonthlyObligationDuePolicies_DueDayOfMonth",
                    "\"DueDayOfMonth\" >= 1 AND \"DueDayOfMonth\" <= 28");
                table.HasCheckConstraint(
                    "CK_EntryMonthlyObligationDuePolicies_Version_NotBlank",
                    "length(trim(\"Version\")) > 0");
            });

            builder.Property(policy => policy.Version)
                .HasMaxLength(EntryMonthlyObligationDuePolicy.MaxVersionLength)
                .IsRequired();
            builder.Property(policy => policy.DueDayOfMonth).IsRequired();
            builder.Property(policy => policy.EffectiveFrom).IsRequired();

            builder.HasIndex(policy => policy.EffectiveFrom);
        }
    }
}
