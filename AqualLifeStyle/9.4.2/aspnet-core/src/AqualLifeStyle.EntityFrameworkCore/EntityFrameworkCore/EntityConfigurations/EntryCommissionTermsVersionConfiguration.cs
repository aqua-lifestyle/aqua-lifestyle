using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class EntryCommissionTermsVersionConfiguration
        : IEntityTypeConfiguration<EntryCommissionTermsVersion>
    {
        public void Configure(EntityTypeBuilder<EntryCommissionTermsVersion> builder)
        {
            builder.ToTable("EntryCommissionTermsVersions", table =>
            {
                table.HasCheckConstraint(
                    "CK_EntryCommissionTermsVersions_Version_NotBlank",
                    "length(trim(\"Version\")) > 0");
                table.HasCheckConstraint(
                    "CK_EntryCommissionTermsVersions_LevelOneAmount_Positive",
                    "\"LevelOneComponentAmount\" > 0");
                table.HasCheckConstraint(
                    "CK_EntryCommissionTermsVersions_LevelTwoAmount_Positive",
                    "\"LevelTwoComponentAmount\" > 0");
                table.HasCheckConstraint(
                    "CK_EntryCommissionTermsVersions_LevelThreeAmount_Positive",
                    "\"LevelThreeComponentAmount\" > 0");
                table.HasCheckConstraint(
                    "CK_EntryCommissionTermsVersions_Currency_ThreeLetters",
                    "length(\"Currency\") = 3");
            });

            builder.Property(version => version.Version)
                .HasMaxLength(EntryCommissionTermsVersion.MaxVersionLength)
                .IsRequired();
            builder.Property(version => version.EffectiveAt).IsRequired();
            builder.Property(version => version.LevelOneComponentAmount)
                .IsRequired();
            builder.Property(version => version.LevelTwoComponentAmount)
                .IsRequired();
            builder.Property(version => version.LevelThreeComponentAmount)
                .IsRequired();
            builder.Property(version => version.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.HasIndex(version => version.Version).IsUnique();
            builder.HasIndex(version => version.EffectiveAt).IsUnique();
        }
    }
}
