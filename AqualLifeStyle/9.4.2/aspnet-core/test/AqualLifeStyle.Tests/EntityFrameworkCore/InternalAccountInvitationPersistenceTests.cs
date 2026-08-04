using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class InternalAccountInvitationPersistenceTests : AqualLifeStyleTestBase
    {
        [Fact]
        public async Task ModelMapsInvitationSecurityAndConcurrencyConstraints()
        {
            await UsingDbContextAsync(context =>
            {
                var entity = context.GetService<IDesignTimeModel>().Model
                    .FindEntityType(typeof(InternalAccountInvitation));
                entity.ShouldNotBeNull();
                entity.GetTableName().ShouldBe("InternalAccountInvitations");

                entity.FindProperty(nameof(InternalAccountInvitation.InvitedEmailAddress))
                    .GetMaxLength().ShouldBe(InternalAccountInvitation.MaxEmailAddressLength);
                entity.FindProperty(nameof(InternalAccountInvitation.PublicCodeHash))
                    .GetMaxLength().ShouldBe(InternalAccountInvitation.HashLength);
                entity.FindProperty(nameof(InternalAccountInvitation.SetupTokenHash))
                    .GetMaxLength().ShouldBe(InternalAccountInvitation.HashLength);
                entity.FindProperty(nameof(InternalAccountInvitation.RevocationReason))
                    .GetMaxLength().ShouldBe(InternalAccountInvitation.MaxRevocationReasonLength);
                entity.FindProperty(nameof(InternalAccountInvitation.Version))
                    .IsConcurrencyToken.ShouldBeTrue();

                var indexes = entity.GetIndexes().ToList();
                indexes.Single(index => index.Properties.Select(property => property.Name)
                        .SequenceEqual(new[] { nameof(InternalAccountInvitation.PublicCodeHash) }))
                    .IsUnique.ShouldBeTrue();

                var pendingIndex = indexes.Single(index => index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[]
                    {
                        nameof(InternalAccountInvitation.TenantId),
                        nameof(InternalAccountInvitation.UserId)
                    }));
                pendingIndex.IsUnique.ShouldBeTrue();
                pendingIndex.GetFilter().ShouldBe("\"Status\" = 0");

                var latestIndex = indexes.Single(index => index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[]
                    {
                        nameof(InternalAccountInvitation.TenantId),
                        nameof(InternalAccountInvitation.UserId),
                        nameof(InternalAccountInvitation.CreationTime)
                    }));
                latestIndex.IsDescending.ShouldBe(new[] { false, false, true });

                var foreignKeys = entity.GetForeignKeys().ToList();
                foreignKeys.Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User))
                    .DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
                foreignKeys.Single(foreignKey =>
                        foreignKey.PrincipalEntityType.ClrType == typeof(InternalAccountInvitation))
                    .DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);

                return Task.CompletedTask;
            });
        }
    }
}
