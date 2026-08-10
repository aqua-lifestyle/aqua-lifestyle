using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class OnyxCommissionTermsVersionConfiguration
        : IEntityTypeConfiguration<OnyxCommissionTermsVersion>
    {
        public void Configure(EntityTypeBuilder<OnyxCommissionTermsVersion> builder)
        {
            builder.ToTable("OnyxCommissionTermsVersions", table =>
            {
                table.HasCheckConstraint(
                    "CK_OnyxCommissionTermsVersions_Version_NotBlank",
                    "length(trim(\"Version\")) > 0");
                table.HasCheckConstraint(
                    "CK_OnyxCommissionTermsVersions_LevelOneRate_Positive",
                    "\"LevelOnePerPersonRate\" > 0");
                table.HasCheckConstraint(
                    "CK_OnyxCommissionTermsVersions_LevelTwoRate_Positive",
                    "\"LevelTwoPerPersonRate\" > 0");
                table.HasCheckConstraint(
                    "CK_OnyxCommissionTermsVersions_LevelThreeRate_Positive",
                    "\"LevelThreePerPersonRate\" > 0");
                table.HasCheckConstraint(
                    "CK_OnyxCommissionTermsVersions_LevelFourRate_Positive",
                    "\"LevelFourPerPersonRate\" > 0");
                table.HasCheckConstraint(
                    "CK_OnyxCommissionTermsVersions_LevelFiveRate_Positive",
                    "\"LevelFivePerPersonRate\" > 0");
                table.HasCheckConstraint(
                    "CK_OnyxCommissionTermsVersions_Currency_ThreeLetters",
                    "length(\"Currency\") = 3");
            });

            builder.Property(version => version.Version)
                .HasMaxLength(OnyxCommissionTermsVersion.MaxVersionLength)
                .IsRequired();
            builder.Property(version => version.EffectiveAt).IsRequired();
            builder.Property(version => version.LevelOnePerPersonRate).IsRequired();
            builder.Property(version => version.LevelTwoPerPersonRate).IsRequired();
            builder.Property(version => version.LevelThreePerPersonRate).IsRequired();
            builder.Property(version => version.LevelFourPerPersonRate).IsRequired();
            builder.Property(version => version.LevelFivePerPersonRate).IsRequired();
            builder.Property(version => version.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.HasIndex(version => version.Version).IsUnique();
            builder.HasIndex(version => version.EffectiveAt).IsUnique();
        }
    }
}
