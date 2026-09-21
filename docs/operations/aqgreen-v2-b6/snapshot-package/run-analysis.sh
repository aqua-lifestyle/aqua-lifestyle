#!/usr/bin/env bash
set -euo pipefail
umask 0077

: "${B6_PREAUTHORITY_PROVENANCE:?Invoke through the exact reviewed Git-object bootstrap}"
: "${B6_VERIFIED_ARTIFACT_ROOT:?Verified artifact root was not supplied}"
: "${B6_PREAUTHORITY_RUNNER_SHA256:?Verified Runner digest was not supplied}"
: "${B6_PREAUTHORITY_CORE_SHA256:?Verified Core digest was not supplied}"
: "${B6_PREAUTHORITY_EF_SHA256:?Verified EF digest was not supplied}"
[[ "${B6_PREAUTHORITY_PROVENANCE}" == GIT_OBJECT_GATE_Q_GATE_A_QUALIFIED_ARTIFACT_V1 ]]
: "${B6_SNAPSHOT_DIR:?Path to approved Zone 3 snapshot}"
: "${B6_BASE_MANIFEST:?Path to its BASE_SNAPSHOT manifest}"
: "${B6_ROOT_DISPOSITIONS:?Path to reviewed root dispositions}"

package_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${package_dir}/../../../.." && pwd)"
[[ -d "${B6_VERIFIED_ARTIFACT_ROOT}" && ! -L "${B6_VERIFIED_ARTIFACT_ROOT}" ]]
runner_dir="$(realpath -- "${B6_VERIFIED_ARTIFACT_ROOT}")"
[[ "${runner_dir}" == "${B6_VERIFIED_ARTIFACT_ROOT}" ]]
runner_dll="${runner_dir}/AqualLifeStyle.B6SnapshotRunner.dll"
inventory_sql="${repo_root}/docs/operations/aqgreen-v2-b6/legacy-inventory.sql"
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

if [[ -n "${B6_ANALYSIS_OUTPUT_DIR:-}" ]]; then
  [[ ! -e "${B6_ANALYSIS_OUTPUT_DIR}" && ! -L "${B6_ANALYSIS_OUTPUT_DIR}" ]]
  mkdir -m 700 -- "${B6_ANALYSIS_OUTPUT_DIR}"
  work_dir="${B6_ANALYSIS_OUTPUT_DIR}"
else
  work_dir="$(mktemp -d -t aqgreen-b6-analysis.XXXXXXXX)"
fi
runtime_state="${work_dir}/runtime-state.json"
cleanup() {
  if [[ -f "${runtime_state}" && ! -L "${runtime_state}" ]]; then
    b6 runtime-destroy --state "${runtime_state}" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

b6 verify --snapshot "${B6_SNAPSHOT_DIR}" --manifest "${B6_BASE_MANIFEST}"
b6 runtime-create --state "${runtime_state}" --role ANALYSIS
b6 restore --snapshot "${B6_SNAPSHOT_DIR}" --manifest "${B6_BASE_MANIFEST}" \
  --runtime-state "${runtime_state}" --schema b6_snapshot
b6 verify-db --runtime-state "${runtime_state}" --schema b6_snapshot --manifest "${B6_BASE_MANIFEST}"

cutoff="$(jq -r .analysisSnapshotCutoff "${B6_BASE_MANIFEST}")"
for run in 1 2; do
  b6 inventory --runtime-state "${runtime_state}" --schema b6_snapshot \
    --snapshot "${B6_SNAPSHOT_DIR}" --base-manifest "${B6_BASE_MANIFEST}" \
    --sql-file "${inventory_sql}" \
    --output "${work_dir}/inventory-${run}.json"
  b6 verify-inventory --snapshot "${B6_SNAPSHOT_DIR}" --base-manifest "${B6_BASE_MANIFEST}" \
    --inventory "${work_dir}/inventory-${run}.json" --runtime-state "${runtime_state}" \
    --schema b6_snapshot --sql-file "${inventory_sql}"
  b6 finalize-manifest --snapshot "${B6_SNAPSHOT_DIR}" --base-manifest "${B6_BASE_MANIFEST}" \
    --root-dispositions "${B6_ROOT_DISPOSITIONS}" \
    --inventory "${work_dir}/inventory-${run}.json" \
    --runtime-state "${runtime_state}" --schema b6_snapshot --sql-file "${inventory_sql}" \
    --analysis-output "${work_dir}/analysis-${run}.json" \
    --simulation-output "${work_dir}/simulation-${run}.json" \
    --impact-output "${work_dir}/impact-${run}.json" \
    --output "${work_dir}/final-manifest-${run}.json"
done

[[ "$(jq -r .inventorySemanticDigest "${work_dir}/inventory-1.json")" == "$(jq -r .inventorySemanticDigest "${work_dir}/inventory-2.json")" ]]
cmp "${work_dir}/inventory-1.json" "${work_dir}/inventory-2.json"
cmp "${work_dir}/analysis-1.json" "${work_dir}/analysis-2.json"
cmp "${work_dir}/simulation-1.json" "${work_dir}/simulation-2.json"
cmp "${work_dir}/impact-1.json" "${work_dir}/impact-2.json"
cmp "${work_dir}/final-manifest-1.json" "${work_dir}/final-manifest-2.json"
b6 verify --snapshot "${B6_SNAPSHOT_DIR}" --manifest "${work_dir}/final-manifest-1.json" \
  --root-dispositions "${B6_ROOT_DISPOSITIONS}" --inventory "${work_dir}/inventory-1.json" \
  --runtime-state "${runtime_state}" --schema b6_snapshot --sql-file "${inventory_sql}"
b6 handoff-receipt --snapshot "${B6_SNAPSHOT_DIR}" --manifest "${work_dir}/final-manifest-1.json" \
  --root-dispositions "${B6_ROOT_DISPOSITIONS}" \
  --inventory "${work_dir}/inventory-1.json" \
  --runtime-state "${runtime_state}" --schema b6_snapshot --sql-file "${inventory_sql}" \
  --approved-destination PACKAGE_OWNED_ISOLATED_POSTGRESQL_16 \
  --output "${work_dir}/handoff-receipt.json"
printf 'B6 .NET representative analysis repeatability and final verification: PASS\nOutputs: %s\n' "${work_dir}"
