#!/usr/bin/env bash
set -euo pipefail
umask 0077

package_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${package_dir}/../../../.." && pwd)"
tests_project="${repo_root}/AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner.Tests/AqualLifeStyle.B6SnapshotRunner.Tests.csproj"
runner_dll="${repo_root}/AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner/bin/Release/net8.0/AqualLifeStyle.B6SnapshotRunner.dll"
inventory_sql="${repo_root}/docs/operations/aqgreen-v2-b6/legacy-inventory.sql"
test_root="$(mktemp -d -t aqgreen-b6-dotnet.XXXXXXXX)"
source_state="${test_root}/source-runtime.json"
analysis_state="${test_root}/analysis-runtime.json"
export B6_EXECUTION_EVIDENCE_CLASSIFICATION=PRECOMMIT_SYNTHETIC_VALIDATION

cleanup() {
  if [[ -f "${analysis_state}" && ! -L "${analysis_state}" && -f "${runner_dll}" ]]; then
    dotnet "${runner_dll}" runtime-destroy --state "${analysis_state}" >/dev/null 2>&1 || true
  fi
  if [[ -f "${source_state}" && ! -L "${source_state}" && -f "${runner_dll}" ]]; then
    dotnet "${runner_dll}" runtime-destroy --state "${source_state}" >/dev/null 2>&1 || true
  fi
  if [[ "${test_root}" == /tmp/aqgreen-b6-dotnet.* ]]; then rm -rf -- "${test_root}"; fi
}
trap cleanup EXIT

dotnet build "${tests_project}" --configuration Release
b6() { dotnet "${runner_dll}" "$@"; }

b6 runtime-create --state "${source_state}" --role SOURCE
b6 fixture-migrate --runtime-state "${source_state}"
b6 fixture-load --runtime-state "${source_state}" --sql-file "${package_dir}/fixture/synthetic-data.sql"
b6 generate-key --output "${test_root}/operator-secret"

repository_commit="$(git -C "${repo_root}" rev-parse HEAD)"
b6 extract --source-runtime-state "${source_state}" --output "${test_root}/raw" \
  --source-system-id B6-DISPOSABLE-POSTGRES --repository-commit "${repository_commit}" \
  --tenant-id 1 --population-scope ALL_TENANT_AQGREEN_PARTICIPATIONS \
  --analysis-cutoff 2026-09-03T00:00:00Z --authorized-operator-ack

for run in 1 2; do
  b6 sanitize --input "${test_root}/raw" --output "${test_root}/snapshot-${run}" \
    --key-file "${test_root}/operator-secret" --snapshot-id B6-SYNTHETIC-0001 \
    --created-at 2026-09-03T00:00:00Z
  b6 manifest --snapshot "${test_root}/snapshot-${run}" --output "${test_root}/base-manifest-${run}.json"
  b6 verify --snapshot "${test_root}/snapshot-${run}" --manifest "${test_root}/base-manifest-${run}.json" \
    --forbidden-values "${package_dir}/fixture/forbidden-values.txt"
done
diff -ru "${test_root}/snapshot-1" "${test_root}/snapshot-2"
cmp "${test_root}/base-manifest-1.json" "${test_root}/base-manifest-2.json"

export B6_FIXTURE_RAW="${test_root}/raw"
export B6_FIXTURE_SNAPSHOT="${test_root}/snapshot-1"
export B6_FIXTURE_BASE_MANIFEST="${test_root}/base-manifest-1.json"
export B6_FIXTURE_ROOT_DISPOSITIONS="${test_root}/root-dispositions.json"
dotnet test "${tests_project}" --configuration Release --no-build \
  --filter FullyQualifiedName~B6SyntheticRootDispositionWriterTest
unset B6_FIXTURE_RAW B6_FIXTURE_SNAPSHOT B6_FIXTURE_BASE_MANIFEST B6_FIXTURE_ROOT_DISPOSITIONS

b6 runtime-create --state "${analysis_state}" --role ANALYSIS
b6 restore --snapshot "${test_root}/snapshot-1" --manifest "${test_root}/base-manifest-1.json" \
  --runtime-state "${analysis_state}" --schema b6_snapshot \
  --forbidden-values "${package_dir}/fixture/forbidden-values.txt"
b6 verify-db --runtime-state "${analysis_state}" --schema b6_snapshot \
  --manifest "${test_root}/base-manifest-1.json"

for run in 1 2; do
  b6 inventory --runtime-state "${analysis_state}" --schema b6_snapshot \
    --snapshot "${test_root}/snapshot-1" --base-manifest "${test_root}/base-manifest-1.json" \
    --sql-file "${inventory_sql}" \
    --output "${test_root}/inventory-${run}.json"
  b6 verify-inventory --snapshot "${test_root}/snapshot-1" \
    --base-manifest "${test_root}/base-manifest-1.json" --inventory "${test_root}/inventory-${run}.json" \
    --runtime-state "${analysis_state}" --schema b6_snapshot --sql-file "${inventory_sql}"
  b6 finalize-manifest --snapshot "${test_root}/snapshot-1" \
    --base-manifest "${test_root}/base-manifest-1.json" \
    --root-dispositions "${test_root}/root-dispositions.json" \
    --inventory "${test_root}/inventory-${run}.json" \
    --runtime-state "${analysis_state}" --schema b6_snapshot --sql-file "${inventory_sql}" \
    --analysis-output "${test_root}/analysis-${run}.json" \
    --simulation-output "${test_root}/simulation-${run}.json" \
    --impact-output "${test_root}/impact-${run}.json" \
    --output "${test_root}/final-manifest-${run}.json"
done
[[ "$(jq -r .inventorySemanticDigest "${test_root}/inventory-1.json")" == "$(jq -r .inventorySemanticDigest "${test_root}/inventory-2.json")" ]]
cmp "${test_root}/inventory-1.json" "${test_root}/inventory-2.json"
cmp "${test_root}/analysis-1.json" "${test_root}/analysis-2.json"
cmp "${test_root}/simulation-1.json" "${test_root}/simulation-2.json"
cmp "${test_root}/impact-1.json" "${test_root}/impact-2.json"
cmp "${test_root}/final-manifest-1.json" "${test_root}/final-manifest-2.json"
b6 verify --snapshot "${test_root}/snapshot-1" --manifest "${test_root}/final-manifest-1.json" \
  --root-dispositions "${test_root}/root-dispositions.json" --inventory "${test_root}/inventory-1.json" \
  --runtime-state "${analysis_state}" --schema b6_snapshot --sql-file "${inventory_sql}"
b6 handoff-receipt --snapshot "${test_root}/snapshot-1" \
  --manifest "${test_root}/final-manifest-1.json" \
  --root-dispositions "${test_root}/root-dispositions.json" \
  --inventory "${test_root}/inventory-1.json" \
  --runtime-state "${analysis_state}" --schema b6_snapshot --sql-file "${inventory_sql}" \
  --approved-destination PACKAGE_OWNED_ISOLATED_POSTGRESQL_18 \
  --generated-at 2026-09-03T02:00:00Z --output "${test_root}/receipt.json"

b6 fixture-load --runtime-state "${source_state}" --sql-file "${package_dir}/fixture/drift-data.sql"
b6 extract --source-runtime-state "${source_state}" --output "${test_root}/raw-drift" \
  --source-system-id B6-DISPOSABLE-POSTGRES --repository-commit "${repository_commit}" \
  --tenant-id 1 --population-scope ALL_TENANT_AQGREEN_PARTICIPATIONS \
  --analysis-cutoff 2026-09-03T00:00:00Z --authorized-operator-ack
b6 sanitize --input "${test_root}/raw-drift" --output "${test_root}/snapshot-drift" \
  --key-file "${test_root}/operator-secret" --snapshot-id B6-SYNTHETIC-DRIFT \
  --created-at 2026-09-03T00:00:00Z
b6 manifest --snapshot "${test_root}/snapshot-drift" --output "${test_root}/base-manifest-drift.json"
b6 restore --snapshot "${test_root}/snapshot-drift" --manifest "${test_root}/base-manifest-drift.json" \
  --runtime-state "${analysis_state}" --schema b6_drift
b6 inventory --snapshot "${test_root}/snapshot-drift" --base-manifest "${test_root}/base-manifest-drift.json" \
  --runtime-state "${analysis_state}" --schema b6_drift \
  --sql-file "${inventory_sql}" \
  --output "${test_root}/inventory-drift.json"

export B6_TEST_RAW="${test_root}/raw"
export B6_TEST_SNAPSHOT="${test_root}/snapshot-1"
export B6_TEST_BASE_MANIFEST="${test_root}/base-manifest-1.json"
export B6_TEST_FINAL_MANIFEST="${test_root}/final-manifest-1.json"
export B6_TEST_ROOT_DISPOSITIONS="${test_root}/root-dispositions.json"
export B6_TEST_ANALYSIS="${test_root}/analysis-1.json"
export B6_TEST_SIMULATION="${test_root}/simulation-1.json"
export B6_TEST_IMPACT="${test_root}/impact-1.json"
export B6_TEST_INVENTORY="${test_root}/inventory-1.json"
export B6_TEST_DRIFT_INVENTORY="${test_root}/inventory-drift.json"
export B6_TEST_RECEIPT="${test_root}/receipt.json"
export B6_TEST_RUNTIME_STATE="${analysis_state}"
export B6_TEST_SECRET="${test_root}/operator-secret"
export B6_TEST_RUNNER="${runner_dll}"
export B6_TEST_DRIFT_SNAPSHOT="${test_root}/snapshot-drift"
export B6_TEST_DRIFT_MANIFEST="${test_root}/base-manifest-drift.json"
test_results_dir="${B6_TEST_RESULTS_DIR:-${test_root}/test-results}"
[[ "${test_results_dir}" == /* && ! -e "${test_results_dir}" && ! -L "${test_results_dir}" ]]
mkdir -m 0700 "${test_results_dir}"
test_trx="${test_results_dir}/b6-package-tests.trx"
dotnet test "${tests_project}" --configuration Release --no-build \
  --filter "${B6_TEST_FILTER:-FullyQualifiedName~B6PrimitiveContractTests|FullyQualifiedName~B6ArtifactContractTests|FullyQualifiedName~B6Pg18CompatibilityTests}" \
  --logger "trx;LogFileName=$(basename "${test_trx}")" --results-directory "${test_results_dir}"
test_result_arguments=(verify-test-run --trx "${test_trx}")
if [[ -n "${B6_EXPECTED_TEST_COUNT:-}" ]]; then
  test_result_arguments+=(--expected-count "${B6_EXPECTED_TEST_COUNT}")
fi
b6 "${test_result_arguments[@]}"

jq -e '.counts.acceptedComponents == 1 and .counts.quarantinedComponents >= 1 and .counts.legacyUnknownAcquisition > 0' \
  "${test_root}/analysis-1.json" >/dev/null
jq -e '.manifestStage == "FINAL_ANALYSIS" and .analysisDigests.impactReportDigest != null' \
  "${test_root}/final-manifest-1.json" >/dev/null
printf 'AQGreen B6 .NET REAL-SCHEMA PostgreSQL snapshot package E2E: PASS\n'
