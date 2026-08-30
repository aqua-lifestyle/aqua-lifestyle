using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Entities;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenPlacementTopologyReader
        : IAQGreenPlacementTopologyReader, ITransientDependency
    {
        private const string AnchorAncestryCte =
            """
            WITH RECURSIVE scoped_placements AS (
                SELECT
                    p."TenantId",
                    p."PlacementTreeScopeId",
                    p."ParticipantId",
                    p."PlacementParentParticipantId",
                    p."PlacementSlot",
                    p."CanonicalPath",
                    p."PlacedAt",
                    p."RulesVersion",
                    (
                        p."CanonicalPath" IS NULL OR
                        p."CanonicalPath" !~ '^[1-5]*$' OR
                        (p."PlacementParentParticipantId" IS NULL AND
                            (p."PlacementSlot" IS NOT NULL OR
                             p."CanonicalPath" IS DISTINCT FROM '')) OR
                        (p."PlacementParentParticipantId" IS NOT NULL AND
                            (p."PlacementSlot" IS NULL OR
                             p."CanonicalPath" IS NULL OR
                             p."CanonicalPath" = '')) OR
                        (p."PlacementParentParticipantId" IS NOT NULL AND
                            p."ParticipantId" = p."PlacementParentParticipantId") OR
                        (p."PlacementSlot" IS NOT NULL AND
                            (p."PlacementSlot" < 1 OR p."PlacementSlot" > 5)) OR
                        p."PlacedAt" IS NULL OR
                        COALESCE(p."RulesVersion" !~ '[^[:space:]]', TRUE)
                    ) AS "HasInvalidShape"
                FROM public."AQGreenNetworkPlacements" AS p
                WHERE p."TenantId" = @tenantId
                  AND p."PlacementTreeScopeId" = @placementTreeScopeId
            ),
            ancestry AS (
                SELECT
                    p."TenantId",
                    p."PlacementTreeScopeId",
                    p."ParticipantId",
                    p."PlacementParentParticipantId",
                    p."PlacementSlot",
                    p."CanonicalPath",
                    p."PlacedAt",
                    p."RulesVersion",
                    0::integer AS "AncestryDepth",
                    ARRAY[p."ParticipantId"]::uuid[] AS "VisitedParticipantIds",
                    FALSE AS "HasCycle",
                    p."HasInvalidShape" AS "HasCorruptEdge"
                FROM scoped_placements AS p
                WHERE p."ParticipantId" = @participantId

                UNION ALL

                SELECT
                    parent."TenantId",
                    parent."PlacementTreeScopeId",
                    parent."ParticipantId",
                    parent."PlacementParentParticipantId",
                    parent."PlacementSlot",
                    parent."CanonicalPath",
                    parent."PlacedAt",
                    parent."RulesVersion",
                    child."AncestryDepth" + 1,
                    child."VisitedParticipantIds" || parent."ParticipantId",
                    parent."ParticipantId" = ANY(child."VisitedParticipantIds"),
                    (
                        child."HasCorruptEdge" OR
                        parent."HasInvalidShape" OR
                        child."PlacementParentParticipantId" IS DISTINCT FROM
                            parent."ParticipantId" OR
                        child."PlacementSlot" IS NULL OR
                        child."PlacementSlot" < 1 OR
                        child."PlacementSlot" > 5 OR
                        child."CanonicalPath" IS DISTINCT FROM
                            parent."CanonicalPath" || child."PlacementSlot"::text OR
                        child."PlacedAt" IS NULL OR
                        parent."PlacedAt" IS NULL OR
                        child."PlacedAt" < parent."PlacedAt"
                    ) AS "HasCorruptEdge"
                FROM ancestry AS child
                JOIN scoped_placements AS parent
                  ON parent."ParticipantId" = child."PlacementParentParticipantId"
                WHERE child."PlacementParentParticipantId" IS NOT NULL
                  AND NOT child."HasCycle"
            ),
            anchor_validation AS (
                SELECT
                    anchor."TenantId",
                    anchor."PlacementTreeScopeId",
                    anchor."ParticipantId",
                    anchor."PlacementParentParticipantId",
                    anchor."PlacementSlot",
                    anchor."CanonicalPath",
                    anchor."PlacedAt",
                    anchor."RulesVersion",
                    (
                        EXISTS (
                            SELECT 1
                            FROM ancestry AS evidence
                            WHERE evidence."HasCycle" OR evidence."HasCorruptEdge") OR
                        EXISTS (
                            SELECT 1
                            FROM ancestry AS child
                            WHERE child."PlacementParentParticipantId" IS NOT NULL
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM scoped_placements AS parent
                                  WHERE parent."ParticipantId" =
                                      child."PlacementParentParticipantId")) OR
                        1 <> (
                            SELECT COUNT(*)
                            FROM ancestry AS root
                            WHERE root."PlacementParentParticipantId" IS NULL
                              AND root."PlacementSlot" IS NULL
                              AND root."CanonicalPath" IS NOT DISTINCT FROM '')
                    ) AS "HasCorruptEdge"
                FROM ancestry AS anchor
                WHERE anchor."AncestryDepth" = 0
            )
            """;

        private const string PlacementSql = AnchorAncestryCte +
            """

            SELECT
                anchor."TenantId",
                anchor."PlacementTreeScopeId",
                anchor."ParticipantId",
                anchor."PlacementParentParticipantId",
                anchor."PlacementSlot",
                anchor."CanonicalPath",
                0::integer AS "RelativeDepth",
                TRUE AS "IsAnchor",
                FALSE AS "HasCycle",
                anchor."HasCorruptEdge"
            FROM anchor_validation AS anchor
            """;

        private const string ChildrenSql = AnchorAncestryCte +
            """

            SELECT
                a."TenantId",
                a."PlacementTreeScopeId",
                a."ParticipantId",
                a."PlacementParentParticipantId",
                a."PlacementSlot",
                a."CanonicalPath",
                0::integer AS "RelativeDepth",
                TRUE AS "IsAnchor",
                FALSE AS "HasCycle",
                a."HasCorruptEdge"
            FROM anchor_validation AS a
            UNION ALL
            SELECT
                child."TenantId",
                child."PlacementTreeScopeId",
                child."ParticipantId",
                child."PlacementParentParticipantId",
                child."PlacementSlot",
                child."CanonicalPath",
                1::integer AS "RelativeDepth",
                FALSE AS "IsAnchor",
                FALSE AS "HasCycle",
                (
                    a."HasCorruptEdge" OR
                    child."HasInvalidShape" OR
                    child."PlacementParentParticipantId" IS DISTINCT FROM
                        a."ParticipantId" OR
                    child."CanonicalPath" IS DISTINCT FROM
                        a."CanonicalPath" || child."PlacementSlot"::text OR
                    child."PlacedAt" IS NULL OR
                    a."PlacedAt" IS NULL OR
                    child."PlacedAt" < a."PlacedAt"
                ) AS "HasCorruptEdge"
            FROM anchor_validation AS a
            JOIN scoped_placements AS child
              ON child."PlacementParentParticipantId" = a."ParticipantId"
             AND child."PlacementParentParticipantId" IS NOT NULL
            ORDER BY "IsAnchor" DESC, "PlacementSlot" ASC
            """;

        private const string SubtreeSql = AnchorAncestryCte +
            """
            , topology AS (
                SELECT
                    anchor."TenantId",
                    anchor."PlacementTreeScopeId",
                    anchor."ParticipantId",
                    anchor."PlacementParentParticipantId",
                    anchor."PlacementSlot",
                    anchor."CanonicalPath",
                    anchor."PlacedAt",
                    anchor."RulesVersion",
                    0::integer AS "RelativeDepth",
                    ARRAY[anchor."ParticipantId"]::uuid[] AS "VisitedParticipantIds",
                    TRUE AS "IsAnchor",
                    FALSE AS "HasCycle",
                    anchor."HasCorruptEdge"
                FROM anchor_validation AS anchor

                UNION ALL

                SELECT
                    child."TenantId",
                    child."PlacementTreeScopeId",
                    child."ParticipantId",
                    child."PlacementParentParticipantId",
                    child."PlacementSlot",
                    child."CanonicalPath",
                    child."PlacedAt",
                    child."RulesVersion",
                    parent."RelativeDepth" + 1,
                    parent."VisitedParticipantIds" || child."ParticipantId",
                    FALSE AS "IsAnchor",
                    child."ParticipantId" = ANY(parent."VisitedParticipantIds") AS "HasCycle",
                    (
                        parent."HasCorruptEdge" OR
                        child."HasInvalidShape" OR
                        child."PlacementParentParticipantId" IS DISTINCT FROM
                            parent."ParticipantId" OR
                        child."CanonicalPath" IS DISTINCT FROM
                            parent."CanonicalPath" || child."PlacementSlot"::text OR
                        child."PlacedAt" IS NULL OR
                        parent."PlacedAt" IS NULL OR
                        child."PlacedAt" < parent."PlacedAt" OR
                        child."ParticipantId" = ANY(parent."VisitedParticipantIds")
                    ) AS "HasCorruptEdge"
                FROM topology AS parent
                JOIN scoped_placements AS child
                  ON child."PlacementParentParticipantId" = parent."ParticipantId"
                 AND child."PlacementParentParticipantId" IS NOT NULL
                WHERE NOT parent."HasCycle"
                  AND parent."RelativeDepth" < @maximumRelativeDepth
            )
            SELECT
                topology."TenantId",
                topology."PlacementTreeScopeId",
                topology."ParticipantId",
                topology."PlacementParentParticipantId",
                topology."PlacementSlot",
                topology."CanonicalPath",
                topology."RelativeDepth",
                topology."IsAnchor",
                topology."HasCycle",
                topology."HasCorruptEdge"
            FROM topology
            ORDER BY
                topology."RelativeDepth" ASC,
                topology."CanonicalPath" COLLATE "C" ASC
            """;

        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenPlacementTopologyReader(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<AQGreenPlacementTopologyNode> GetPlacementAsync(
            int tenantId,
            Guid placementTreeScopeId,
            Guid participantId,
            CancellationToken cancellationToken = default)
        {
            EnsureTenant(tenantId);
            EnsureScope(placementTreeScopeId);
            EnsureParticipant(participantId, nameof(participantId));
            var context = GetPostgreSqlContext();
            var rows = await QueryAsync(
                context,
                PlacementSql,
                tenantId,
                placementTreeScopeId,
                participantId,
                cancellationToken);

            EnsureSingleAnchor(rows, participantId);
            EnsureTopologyIsValid(rows, tenantId, placementTreeScopeId);
            return Map(rows[0]);
        }

        public async Task<IReadOnlyList<AQGreenPlacementTopologyNode>> GetChildrenAsync(
            int tenantId,
            Guid placementTreeScopeId,
            Guid parentParticipantId,
            CancellationToken cancellationToken = default)
        {
            EnsureTenant(tenantId);
            EnsureScope(placementTreeScopeId);
            EnsureParticipant(parentParticipantId, nameof(parentParticipantId));
            var context = GetPostgreSqlContext();
            var rows = await QueryAsync(
                context,
                ChildrenSql,
                tenantId,
                placementTreeScopeId,
                parentParticipantId,
                cancellationToken);

            EnsureSingleAnchor(rows, parentParticipantId);
            EnsureTopologyIsValid(rows, tenantId, placementTreeScopeId);
            return rows
                .Where(row => !row.IsAnchor)
                .Select(Map)
                .ToList();
        }

        public async Task<IReadOnlyList<AQGreenPlacementTopologyNode>>
            GetSubtreeInCanonicalOrderAsync(
                int tenantId,
                Guid placementTreeScopeId,
                Guid sponsorParticipantId,
                CancellationToken cancellationToken = default)
        {
            EnsureTenant(tenantId);
            EnsureScope(placementTreeScopeId);
            EnsureParticipant(sponsorParticipantId, nameof(sponsorParticipantId));
            var context = GetPostgreSqlContext();
            var rows = await QueryAsync(
                context,
                SubtreeSql,
                tenantId,
                placementTreeScopeId,
                sponsorParticipantId,
                int.MaxValue,
                cancellationToken);

            EnsureSingleAnchor(rows, sponsorParticipantId);
            EnsureTopologyIsValid(rows, tenantId, placementTreeScopeId);
            return rows.Select(Map).ToList();
        }

        public async Task<IReadOnlyList<AQGreenPlacementTopologyNode>>
            GetSubtreeInCanonicalOrderAsync(
                int tenantId,
                Guid placementTreeScopeId,
                Guid sponsorParticipantId,
                int maximumRelativeDepth,
                CancellationToken cancellationToken = default)
        {
            EnsureTenant(tenantId);
            EnsureScope(placementTreeScopeId);
            EnsureParticipant(sponsorParticipantId, nameof(sponsorParticipantId));
            if (maximumRelativeDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumRelativeDepth));
            var context = GetPostgreSqlContext();
            var rows = await QueryAsync(
                context,
                SubtreeSql,
                tenantId,
                placementTreeScopeId,
                sponsorParticipantId,
                maximumRelativeDepth,
                cancellationToken);

            EnsureSingleAnchor(rows, sponsorParticipantId);
            EnsureTopologyIsValid(rows, tenantId, placementTreeScopeId);
            return rows.Select(Map).ToList();
        }

        private AqualLifeStyleDbContext GetPostgreSqlContext()
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
            {
                throw new NotSupportedException(
                    "AQGreen placement topology traversal requires PostgreSQL.");
            }

            return context;
        }

        private static async Task<List<TopologyQueryRow>> QueryAsync(
            AqualLifeStyleDbContext context,
            string sql,
            int tenantId,
            Guid? placementTreeScopeId,
            Guid participantId,
            CancellationToken cancellationToken)
            => await QueryAsync(
                context,
                sql,
                tenantId,
                placementTreeScopeId,
                participantId,
                maximumRelativeDepth: null,
                cancellationToken);

        private static async Task<List<TopologyQueryRow>> QueryAsync(
            AqualLifeStyleDbContext context,
            string sql,
            int tenantId,
            Guid? placementTreeScopeId,
            Guid participantId,
            int? maximumRelativeDepth,
            CancellationToken cancellationToken)
        {
            var parameters = new List<object>
            {
                new NpgsqlParameter("tenantId", tenantId),
                new NpgsqlParameter("participantId", participantId)
            };
            if (placementTreeScopeId.HasValue)
            {
                parameters.Add(new NpgsqlParameter(
                    "placementTreeScopeId",
                    placementTreeScopeId.Value));
            }
            if (maximumRelativeDepth.HasValue)
            {
                parameters.Add(new NpgsqlParameter(
                    "maximumRelativeDepth",
                    maximumRelativeDepth.Value));
            }

            return await context.Database
                .SqlQueryRaw<TopologyQueryRow>(sql, parameters.ToArray())
                .ToListAsync(cancellationToken);
        }

        private static void EnsureSingleAnchor(
            IReadOnlyCollection<TopologyQueryRow> rows,
            Guid participantId)
        {
            if (rows.Count == 0)
            {
                throw new EntityNotFoundException(
                    typeof(AQGreenNetworkPlacement),
                    participantId);
            }

            if (rows.Count(row => row.IsAnchor) != 1)
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology is corrupt: the requested placement anchor is ambiguous.");
            }
        }

        private static void EnsureTopologyIsValid(
            IReadOnlyCollection<TopologyQueryRow> rows,
            int tenantId,
            Guid? placementTreeScopeId)
        {
            if (rows.Any(row =>
                    row.TenantId != tenantId ||
                    (placementTreeScopeId.HasValue &&
                     row.PlacementTreeScopeId != placementTreeScopeId.Value) ||
                    row.HasCycle ||
                    row.HasCorruptEdge) ||
                rows.GroupBy(row => row.ParticipantId).Any(group => group.Count() != 1) ||
                rows.GroupBy(row => row.CanonicalPath).Any(group => group.Count() != 1))
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology is corrupt and cannot be traversed safely.");
            }
        }

        private static AQGreenPlacementTopologyNode Map(TopologyQueryRow row) =>
            new(
                row.ParticipantId,
                row.PlacementTreeScopeId,
                row.PlacementParentParticipantId,
                row.PlacementSlot,
                row.RelativeDepth);

        private static void EnsureTenant(int tenantId)
        {
            if (tenantId <= 0)
                throw new ArgumentOutOfRangeException(nameof(tenantId));
        }

        private static void EnsureScope(Guid placementTreeScopeId)
        {
            if (placementTreeScopeId == Guid.Empty)
                throw new ArgumentException(
                    "A placement-tree scope is required.",
                    nameof(placementTreeScopeId));
        }

        private static void EnsureParticipant(Guid participantId, string parameterName)
        {
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    parameterName);
        }

        private sealed class TopologyQueryRow
        {
            public int TenantId { get; set; }
            public Guid PlacementTreeScopeId { get; set; }
            public Guid ParticipantId { get; set; }
            public Guid? PlacementParentParticipantId { get; set; }
            public int? PlacementSlot { get; set; }
            public string CanonicalPath { get; set; }
            public int RelativeDepth { get; set; }
            public bool IsAnchor { get; set; }
            public bool HasCycle { get; set; }
            public bool HasCorruptEdge { get; set; }
        }
    }
}
