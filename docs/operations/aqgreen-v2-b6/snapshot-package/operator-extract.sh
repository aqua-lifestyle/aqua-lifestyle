#!/usr/bin/env bash
set -euo pipefail
umask 0077
export B6_EXECUTION_EVIDENCE_CLASSIFICATION=OPERATIONAL_EVIDENCE

# DO NOT RUN WITHOUT PRODUCTION/DATA AUTHORIZATION.
: "${B6_SOURCE_DSN:?Set to the authorised read-only PostgreSQL DSN}"
: "${B6_SOURCE_SYSTEM_ID:?Set to an approved opaque source-system identifier}"
: "${B6_REPOSITORY_COMMIT:?Set to the reviewed full lowercase Git commit SHA}"
: "${B6_RAW_ZONE2_DIR:?Set to a new secured Zone 2 directory}"
: "${B6_PSEUDONYM_KEY_FILE:?Set to an access-controlled key file outside Git/logs}"
: "${B6_ZONE3_DIR:?Set to a new approved Zone 3 handoff directory}"
: "${B6_SNAPSHOT_ID:?Set to an approved opaque snapshot identifier}"
: "${B6_TENANT_ID:?Set to the explicitly authorised TenantId}"
: "${B6_ANALYSIS_SNAPSHOT_CUTOFF:?Set to the approved UTC analysis cutoff}"

package_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${package_dir}/../../../.." && pwd)"
runner_project="${repo_root}/AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner/AqualLifeStyle.B6SnapshotRunner.csproj"
runner_dll="${repo_root}/AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner/bin/Release/net8.0/AqualLifeStyle.B6SnapshotRunner.dll"
dotnet build "${runner_project}" --configuration Release
b6() { dotnet "${runner_dll}" "$@"; }

b6 extract --dsn-env B6_SOURCE_DSN --output "${B6_RAW_ZONE2_DIR}" \
  --source-system-id "${B6_SOURCE_SYSTEM_ID}" --repository-commit "${B6_REPOSITORY_COMMIT}" \
  --tenant-id "${B6_TENANT_ID}" --population-scope ALL_TENANT_AQGREEN_PARTICIPATIONS \
  --analysis-cutoff "${B6_ANALYSIS_SNAPSHOT_CUTOFF}" --authorized-operator-ack
b6 sanitize --input "${B6_RAW_ZONE2_DIR}" --output "${B6_ZONE3_DIR}" \
  --key-file "${B6_PSEUDONYM_KEY_FILE}" --snapshot-id "${B6_SNAPSHOT_ID}"
b6 manifest --snapshot "${B6_ZONE3_DIR}" --output "${B6_ZONE3_DIR}.base-manifest.json"
b6 verify --snapshot "${B6_ZONE3_DIR}" --manifest "${B6_ZONE3_DIR}.base-manifest.json"
