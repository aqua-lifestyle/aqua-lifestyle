#!/usr/bin/env bash
set -euo pipefail
umask 0077

: "${B6_PREAUTHORITY_PROVENANCE:?Invoke through the exact reviewed Git-object bootstrap}"
: "${B6_VERIFIED_ARTIFACT_ROOT:?Verified artifact root was not supplied}"
: "${B6_PREAUTHORITY_RUNNER_SHA256:?Verified Runner digest was not supplied}"
: "${B6_PREAUTHORITY_CORE_SHA256:?Verified Core digest was not supplied}"
: "${B6_PREAUTHORITY_EF_SHA256:?Verified EF digest was not supplied}"
[[ "${B6_PREAUTHORITY_PROVENANCE}" == GIT_OBJECT_GATE_Q_GATE_A_QUALIFIED_ARTIFACT_V1 ]]

[[ -d "${B6_VERIFIED_ARTIFACT_ROOT}" && ! -L "${B6_VERIFIED_ARTIFACT_ROOT}" ]]
runner_dir="$(realpath -- "${B6_VERIFIED_ARTIFACT_ROOT}")"
[[ "${runner_dir}" == "${B6_VERIFIED_ARTIFACT_ROOT}" ]]
runner_dll="${runner_dir}/AqualLifeStyle.B6SnapshotRunner.dll"
verify_artifact_set() {
  local name expected path
  while [[ "$#" -gt 0 ]]; do
    name="$1"; expected="$2"; shift 2
    path="${runner_dir}/${name}.dll"
    [[ -f "${path}" && ! -L "${path}" ]]
    [[ "$(sha256sum -- "${path}" | cut -d' ' -f1)" == "${expected}" ]]
  done
}
b6() {
  verify_artifact_set \
    AqualLifeStyle.B6SnapshotRunner "${B6_PREAUTHORITY_RUNNER_SHA256}" \
    AqualLifeStyle.Core "${B6_PREAUTHORITY_CORE_SHA256}" \
    AqualLifeStyle.EntityFrameworkCore "${B6_PREAUTHORITY_EF_SHA256}"
  dotnet "${runner_dll}" "$@"
}

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

b6 extract --dsn-env B6_SOURCE_DSN --output "${B6_RAW_ZONE2_DIR}" \
  --source-system-id "${B6_SOURCE_SYSTEM_ID}" --repository-commit "${B6_REPOSITORY_COMMIT}" \
  --tenant-id "${B6_TENANT_ID}" --population-scope ALL_TENANT_AQGREEN_PARTICIPATIONS \
  --analysis-cutoff "${B6_ANALYSIS_SNAPSHOT_CUTOFF}" --authorized-operator-ack
b6 sanitize --input "${B6_RAW_ZONE2_DIR}" --output "${B6_ZONE3_DIR}" \
  --key-file "${B6_PSEUDONYM_KEY_FILE}" --snapshot-id "${B6_SNAPSHOT_ID}"
b6 manifest --snapshot "${B6_ZONE3_DIR}" --output "${B6_ZONE3_DIR}.base-manifest.json"
b6 verify --snapshot "${B6_ZONE3_DIR}" --manifest "${B6_ZONE3_DIR}.base-manifest.json"
