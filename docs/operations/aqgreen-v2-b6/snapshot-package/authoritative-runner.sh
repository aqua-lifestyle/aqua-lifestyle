#!/usr/bin/env bash
set -euo pipefail
umask 0077
export LC_ALL=C
exec {terminal_output_fd}>&2

readonly expected_g_count='197'
readonly expected_g_sha256='a32d7081f7c687c9ece1511122dd772ac17e7cfc682943e5706ac0caef6fc4a0'
readonly expected_tq_count='186'
readonly expected_tq_sha256='d94d67e2dc223c42a69f3cf75e943919eeae26f8ef3ffe8b9f40f8ece1b9672b'
readonly expected_u_count='215'
readonly expected_u_sha256='372c918aa794e976601ee292caa9c56a884784ebcfe04cd53981f76d3e982dd1'
readonly expected_u1_count='1'
readonly expected_u1_sha256='9598c6dd7cab8f0dc62a31be8c73a3c030c5d92d26d120689f06bf1b60b0980a'
readonly expected_u2_count='2'
readonly expected_u2_sha256='84ba6476727924aecc6b94207f6de1fdc1135804edbea4d32b7984ede77a6b35'
readonly expected_feed_manifest_sha256='1a56a8bad2fed7875f3326f620ce40af7c0f805d7f16696efa7f1eceaf816df7'
readonly expected_toolchain_manifest_sha256='1c609222deecbbc9ef65d311528ad1eb19ff26adf70227752ee95cca73a5910c'
readonly expected_core_lock_sha256='9f467700282dc48f8bdc172eb8f1830a3167cbc4efaa223a328b1ff00c4acddc'
readonly expected_ef_lock_sha256='2b1925d618a39d10fa57e3e8455f06c3565de75ccdc3ae4a9021e357c9d07356'
readonly expected_runner_lock_sha256='401fafbcfa35347666a4de3fcfa92be9c66399663d8f8410eca420879c3d7b7b'
readonly expected_test_lock_sha256='e575bab05aa361751f35ce31340880c0286e6133c7f36b7e3e1fb2ff8bbf0a47'
readonly expected_signer_policy_sha256='0d131692c7d4035f4ed890d6b32fb127bd6fe423db700b2a0812c2438ff66f79'
readonly expected_simulate_input_sha256='5ab46a2fe268ad525c42ae06a47fb5a105802b37d0733c48fcdc093a14398a5c'
readonly expected_sdk_version='8.0.130'
readonly expected_runtime_version='8.0.30'
readonly expected_nuget_version='6.8.2.32767'
readonly expected_dotnet_host_sha256='045a8891a4e84d5acec53b3650e2ea4210bc6d3fc4ddc2d6c8de63662afa56aa'
readonly expected_mawk_sha256='3850bf893d3f4f8ec462c8b59d6d66fdbbe8ddc0715b3d404d9cc574526b781c'
readonly expected_mawk_version='mawk 1.3.4 20240123'
readonly expected_mawk_package_version='1.3.4.20240123-1build1'
readonly freshness_limit_seconds='3600'
readonly gate_a_semantic_projection_schema='B6GateASemanticProjectionV1'
readonly expected_gate_a_semantic_projection_sha256='747ec92ca0d9f50eccf82079bc8f878a97a3b59a270c6e10b7b1cd3b54ce728a'
readonly provenance_contract='GIT_OBJECT_GATE_Q_GATE_A_QUALIFIED_ARTIFACT_V1'
readonly package_relative='docs/operations/aqgreen-v2-b6/snapshot-package'
readonly backend_relative='AqualLifeStyle/9.4.2/aspnet-core'
readonly runner_relative="${backend_relative}/tools/AqualLifeStyle.B6SnapshotRunner/AqualLifeStyle.B6SnapshotRunner.csproj"
readonly test_relative="${backend_relative}/tools/AqualLifeStyle.B6SnapshotRunner.Tests/AqualLifeStyle.B6SnapshotRunner.Tests.csproj"
readonly qualified_mawk='/usr/bin/mawk'
readonly bwrap='/usr/bin/bwrap'

usage() {
  printf 'Usage: GIT_NO_REPLACE_OBJECTS=1 git show <reviewed-commit>:%s | bash -s -- --repository <root> --reviewed-commit <sha> --frozen-dotnet-root <root> --toolchain-manifest <file> --acquisition-feed <flat-U215-feed> --feed-manifest <file> --evidence-dir <new-dir> <operator-extract|run-analysis|runner> [runner arguments]\n' "${package_relative}/authoritative-runner.sh" >&2
}

phase='BOOTSTRAP'
first_gate_failure='NONE'
evidence_dir=''
private_root=''
qualification_result='FAIL'
operation_result='NOT_STARTED'
overall_result='FAIL'
terminal_finalized='NO'
terminal_finalization_active='NO'
intended_exit_code=''
bound_exit_code=''
transaction_id='UNAVAILABLE'
transaction_cleanup_result='NOT_STARTED'
network_reconciliation_result='NOT_STARTED'
pending_terminal_signal_code=''
pending_terminal_signal_name=''
active_bwrap_pid=''
active_bwrap_starttime=''

sha256_file() { sha256sum -- "$1" | { read -r digest _; printf '%s' "${digest}"; }; }

discard_failed_atomic_write() {
  local destination="$1" temporary="$2"
  if [[ -n "${temporary}" ]]; then rm -f -- "${temporary}" || return 1; fi
  if [[ -e "${destination}" || -L "${destination}" ]]; then rm -f -- "${destination}" || return 1; fi
  [[ ! -e "${destination}" && ! -L "${destination}" ]]
}

atomic_write() {
  local destination="$1" source="$2" temporary='' destination_directory source_sha256
  destination_directory="$(dirname -- "${destination}")" || return 1
  [[ -f "${source}" && ! -L "${source}" && -d "${destination_directory}" && ! -L "${destination_directory}" ]] || { discard_failed_atomic_write "${destination}" "${temporary}" || true; return 1; }
  source_sha256="$(sha256_file "${source}")" || { discard_failed_atomic_write "${destination}" "${temporary}" || true; return 1; }
  temporary="$(mktemp -p "${destination_directory}" .b6-atomic.XXXXXXXX)" || { discard_failed_atomic_write "${destination}" "${temporary}" || true; return 1; }
  if ! cp -- "${source}" "${temporary}"; then discard_failed_atomic_write "${destination}" "${temporary}" || true; return 1; fi
  if ! chmod 600 "${temporary}"; then discard_failed_atomic_write "${destination}" "${temporary}" || true; return 1; fi
  if [[ "$(stat -c '%a %h' -- "${temporary}" 2>/dev/null || true)" != '600 1' || "$(sha256_file "${temporary}" 2>/dev/null || true)" != "${source_sha256}" ]]; then discard_failed_atomic_write "${destination}" "${temporary}" || true; return 1; fi
  if ! mv -fT -- "${temporary}" "${destination}"; then discard_failed_atomic_write "${destination}" "${temporary}" || true; return 1; fi
  temporary=''
  if [[ ! -f "${destination}" || -L "${destination}" || "$(stat -c '%a %h' -- "${destination}" 2>/dev/null || true)" != '600 1' || "$(sha256_file "${destination}" 2>/dev/null || true)" != "${source_sha256}" ]]; then discard_failed_atomic_write "${destination}" "${temporary}" || true; return 1; fi
}

write_checksum_manifest() {
  local name="$1" temporary
  temporary="$(mktemp -p "${private_root}" .b6-manifest.XXXXXXXX)" || return 1
  [[ -z "$(find -P "${evidence_dir}" -mindepth 1 ! -type d ! -type f -print -quit)" ]] || { rm -f -- "${temporary}"; return 1; }
  while IFS= read -r -d '' evidence_file; do
    [[ "$(stat -c %h -- "${evidence_file}")" == 1 ]] || { rm -f -- "${temporary}"; return 1; }
  done < <(find -P "${evidence_dir}" -type f -print0)
  (cd "${evidence_dir}" && find -P . -type f ! -path ./SHA256SUMS ! -path ./qualification-SHA256SUMS -print0 | sort -z | xargs -0 -r sha256sum --) > "${temporary}" || { rm -f -- "${temporary}"; return 1; }
  if [[ "${name}" == SHA256SUMS && -f "${evidence_dir}/qualification-SHA256SUMS" ]]; then
    (cd "${evidence_dir}" && sha256sum -- ./qualification-SHA256SUMS) >> "${temporary}" || { rm -f -- "${temporary}"; return 1; }
    sort -o "${temporary}" "${temporary}" || { rm -f -- "${temporary}"; return 1; }
  fi
  chmod 600 "${temporary}" || { rm -f -- "${temporary}"; return 1; }
  atomic_write "${evidence_dir}/${name}" "${temporary}" || { rm -f -- "${temporary}"; return 1; }
  rm -f -- "${temporary}" || true
  (cd "${evidence_dir}" && sha256sum -c "${name}" >/dev/null) || return 1
}

enter_finalization_critical_section() {
  trap '' INT TERM
  terminal_finalization_active='YES'
}

stage_terminal_receipt() {
  local process_exit_code="$1" final_phase="$2" final_result="$3" verdict_tmp="$4" terminal_timestamp qualification_manifest_sha256='UNAVAILABLE'
  terminal_timestamp="$(date -u +%Y-%m-%dT%H:%M:%SZ)" || return 1
  if [[ -f "${evidence_dir}/qualification-SHA256SUMS" && ! -L "${evidence_dir}/qualification-SHA256SUMS" ]]; then
    qualification_manifest_sha256="$(sha256_file "${evidence_dir}/qualification-SHA256SUMS")" || return 1
  fi
  printf 'schema\tAQGREEN_B6_TERMINAL_RECEIPT_V2\ntransaction_id\t%s\nfinal_phase\t%s\nqualification_result\t%s\noperation_result\t%s\ncleanup_reconciliation_result\t%s\nfinal_transaction_result\t%s\nprocess_exit_code\t%s\nterminal_timestamp_utc\t%s\nqualification_manifest_sha256\t%s\nfinal_manifest_name\tSHA256SUMS\nfinal_manifest_binding\tTERMINAL_RECEIPT_INCLUDED_BY_SHA256\nfirst_gate_failure\t%s\n' \
    "${transaction_id}" "${final_phase}" "${qualification_result}" "${operation_result}" "${transaction_cleanup_result}" "${final_result}" "${process_exit_code}" "${terminal_timestamp}" "${qualification_manifest_sha256}" "${first_gate_failure}" > "${verdict_tmp}" || return 1
  chmod 600 "${verdict_tmp}" || return 1
}

stage_terminal_manifest() {
  local receipt_stage="$1" manifest_stage="$2" receipt_sha256
  [[ -f "${receipt_stage}" && ! -L "${receipt_stage}" ]] || return 1
  [[ -z "$(find -P "${evidence_dir}" -mindepth 1 ! -type d ! -type f -print -quit)" ]] || return 1
  while IFS= read -r -d '' evidence_file; do
    [[ "$(stat -c %h -- "${evidence_file}")" == 1 ]] || return 1
  done < <(find -P "${evidence_dir}" -type f -print0)
  (cd "${evidence_dir}" && find -P . -type f ! -path ./SHA256SUMS ! -path ./qualification-SHA256SUMS ! -path ./final-verdict.tsv -print0 | sort -z | xargs -0 -r sha256sum --) > "${manifest_stage}" || return 1
  if [[ -f "${evidence_dir}/qualification-SHA256SUMS" && ! -L "${evidence_dir}/qualification-SHA256SUMS" ]]; then
    (cd "${evidence_dir}" && sha256sum -- ./qualification-SHA256SUMS) >> "${manifest_stage}" || return 1
  fi
  receipt_sha256="$(sha256_file "${receipt_stage}")" || return 1
  printf '%s  ./final-verdict.tsv\n' "${receipt_sha256}" >> "${manifest_stage}" || return 1
  sort -o "${manifest_stage}" "${manifest_stage}" || return 1
  chmod 600 "${manifest_stage}" || return 1
}

verify_terminal_manifest() {
  local manifest="$1" receipt_stage="$2" expected path source actual count=0
  [[ -f "${manifest}" && ! -L "${manifest}" && -f "${receipt_stage}" && ! -L "${receipt_stage}" ]] || return 1
  while read -r expected path; do
    [[ "${expected}" =~ ^[0-9a-f]{64}$ && "${path}" == ./* && "${path}" != *'/../'* && "${path}" != './..' ]] || return 1
    if [[ "${path}" == ./final-verdict.tsv ]]; then source="${receipt_stage}"; else source="${evidence_dir}/${path#./}"; fi
    [[ -f "${source}" && ! -L "${source}" ]] || return 1
    actual="$(sha256_file "${source}")" || return 1
    [[ "${actual}" == "${expected}" ]] || return 1
    ((count += 1))
  done < "${manifest}"
  (( count > 0 ))
}

commit_terminal_result() {
  local process_exit_code="$1" final_phase="$2" final_result collision='NO' receipt_stage manifest_stage
  [[ "${terminal_finalized}" == NO ]] || return 1
  [[ "${terminal_finalization_active}" == YES ]] || return 1
  [[ -n "${evidence_dir}" && -d "${evidence_dir}" && ! -L "${evidence_dir}" ]] || return 1
  [[ -n "${private_root}" && -d "${private_root}" && ! -L "${private_root}" ]] || return 1

  for terminal_destination in "${evidence_dir}/final-verdict.tsv" "${evidence_dir}/SHA256SUMS"; do
    if [[ -e "${terminal_destination}" || -L "${terminal_destination}" ]]; then
      collision='YES'
      rm -f -- "${terminal_destination}" || return 1
      [[ ! -e "${terminal_destination}" && ! -L "${terminal_destination}" ]] || return 1
    fi
  done
  if [[ "${collision}" == YES ]]; then
    qualification_result='FAIL'; operation_result='FAIL'; overall_result='FAIL'; process_exit_code=3; final_phase='TERMINAL_EVIDENCE_COLLISION'
    [[ "${first_gate_failure}" != NONE ]] || first_gate_failure='TERMINAL_EVIDENCE'
  fi

  final_result='FAIL'
  if (( process_exit_code == 0 )) && [[ "${qualification_result}" == PASS && "${operation_result}" == PASS && "${transaction_cleanup_result}" == PASS ]]; then
    final_result='PASS'
  fi
  if [[ "${qualification_result}" == PASS ]]; then
    if [[ ! -f "${evidence_dir}/qualification-SHA256SUMS" ]] || ! (cd "${evidence_dir}" && sha256sum -c qualification-SHA256SUMS >/dev/null); then
      qualification_result='FAIL'; operation_result='FAIL'; overall_result='FAIL'; final_result='FAIL'; process_exit_code=3; final_phase='TERMINAL_EVIDENCE'
      [[ "${first_gate_failure}" != NONE ]] || first_gate_failure='TERMINAL_EVIDENCE'
    fi
  fi
  if [[ "${final_result}" == PASS ]]; then overall_result='PASS'; else overall_result='FAIL'; (( process_exit_code != 0 )) || process_exit_code=3; fi

  receipt_stage="$(mktemp -p "${private_root}" .b6-terminal-receipt.XXXXXXXX)" || return 1
  manifest_stage="$(mktemp -p "${private_root}" .b6-terminal-manifest.XXXXXXXX)" || return 1
  stage_terminal_receipt "${process_exit_code}" "${final_phase}" "${final_result}" "${receipt_stage}" || return 1
  stage_terminal_manifest "${receipt_stage}" "${manifest_stage}" || return 1
  atomic_write "${evidence_dir}/SHA256SUMS" "${manifest_stage}" || return 1
  cmp -s "${manifest_stage}" "${evidence_dir}/SHA256SUMS" || return 1
  verify_terminal_manifest "${evidence_dir}/SHA256SUMS" "${receipt_stage}" || return 1
  atomic_write "${evidence_dir}/final-verdict.tsv" "${receipt_stage}" || return 1
  terminal_finalized='YES'; bound_exit_code="${process_exit_code}"
}

fail() {
  local message="$1"
  [[ "${first_gate_failure}" != NONE ]] || first_gate_failure="${phase}"
  printf 'B6_PHASE_ERROR phase=%s message=%s\nB6_FIRST_GATE_FAILURE=%s\n' "${phase}" "${message}" "${first_gate_failure}" >&2
  overall_result='FAIL'
  if [[ -n "${private_root}" && -d "${private_root}" && -n "${evidence_dir}" && -d "${evidence_dir}" ]]; then
    for failure_log in "${private_root}"/*.log; do [[ -f "${failure_log}" && ! -L "${failure_log}" ]] && cp -- "${failure_log}" "${evidence_dir}/$(basename -- "${failure_log}")"; done
  fi
  intended_exit_code=2
  exit "${intended_exit_code}"
}

process_starttime() {
  local pid="$1" record rest
  [[ "${pid}" =~ ^[1-9][0-9]*$ && -r "/proc/${pid}/stat" ]] || return 1
  IFS= read -r record < "/proc/${pid}/stat" || return 1
  rest="${record##*) }"
  set -- ${rest}
  [[ "$#" -ge 20 ]] || return 1
  printf '%s' "${20}"
}

transaction_critical_cleanup() {
  local cleanup_failed='NO'
  [[ "${network_reconciliation_result}" != IN_PROGRESS ]] || cleanup_failed='YES'
  if [[ -n "${active_bwrap_pid}" && -n "${active_bwrap_starttime}" && -r "/proc/${active_bwrap_pid}/stat" ]]; then
    if [[ "$(process_starttime "${active_bwrap_pid}" 2>/dev/null || true)" == "${active_bwrap_starttime}" ]]; then
      kill -TERM "${active_bwrap_pid}" 2>/dev/null || cleanup_failed='YES'
      wait "${active_bwrap_pid}" 2>/dev/null || true
    else cleanup_failed='YES'; fi
  fi
  if [[ -n "${active_bwrap_pid}" && -n "${active_bwrap_starttime}" && -r "/proc/${active_bwrap_pid}/stat" && "$(process_starttime "${active_bwrap_pid}" 2>/dev/null || true)" == "${active_bwrap_starttime}" ]]; then
    cleanup_failed='YES'
  fi
  active_bwrap_pid=''; active_bwrap_starttime=''
  if [[ "${cleanup_failed}" == YES ]]; then transaction_cleanup_result='FAIL'; return 1; fi
  transaction_cleanup_result='PASS'
}

best_effort_housekeeping() {
  if [[ -n "${private_root}" && -d "${private_root}" && ! -L "${private_root}" ]]; then
    chmod -R u+w -- "${private_root}" 2>/dev/null || true
    rm -rf -- "${private_root}" || return 1
  fi
}

record_termination_request() {
  local process_exit_code="$1" signal_name="$2"
  [[ -n "${pending_terminal_signal_code}" ]] || { pending_terminal_signal_code="${process_exit_code}"; pending_terminal_signal_name="${signal_name}"; }
}

terminal_exit() {
  local observed_exit_code=$? process_exit_code
  trap - EXIT
  process_exit_code="${intended_exit_code:-${observed_exit_code}}"
  if (( process_exit_code != 0 )); then overall_result='FAIL'; [[ "${operation_result}" != PASS ]] || operation_result='FAIL'; fi
  if ! transaction_critical_cleanup; then
    overall_result='FAIL'; [[ "${operation_result}" != PASS ]] || operation_result='FAIL'; (( process_exit_code != 0 )) || process_exit_code=3
    [[ "${first_gate_failure}" != NONE ]] || first_gate_failure='TRANSACTION_CLEANUP'
    phase='TRANSACTION_CLEANUP_FAILED'
  fi
  enter_finalization_critical_section
  if [[ -n "${pending_terminal_signal_code}" ]]; then
    process_exit_code="${pending_terminal_signal_code}"; operation_result='FAIL'; overall_result='FAIL'; phase="SIGNAL_${pending_terminal_signal_name}"
    [[ "${first_gate_failure}" != NONE ]] || first_gate_failure="${phase}"
    pending_terminal_signal_code=''; pending_terminal_signal_name=''
  fi
  if ! commit_terminal_result "${process_exit_code}" "${phase}"; then
    qualification_result='FAIL'; operation_result='FAIL'; overall_result='FAIL'; bound_exit_code=3
    [[ "${first_gate_failure}" != NONE ]] || first_gate_failure='TERMINAL_EVIDENCE'
  fi
  best_effort_housekeeping || true
  if [[ "${terminal_finalized}" == YES && "${overall_result}" == PASS ]]; then
    printf 'B6_QUALIFICATION=PASS evidence-dir=%s transaction-id=%s\n' "${evidence_dir}" "${transaction_id}" >&"${terminal_output_fd}" || true
  elif [[ "${terminal_finalized}" == YES ]]; then
    printf 'B6_QUALIFICATION=FAIL evidence-dir=%s transaction-id=%s exit-code=%s\n' "${evidence_dir}" "${transaction_id}" "${bound_exit_code}" >&"${terminal_output_fd}" || true
  else
    printf 'B6_QUALIFICATION=FAIL finalization=FAILED evidence-dir=%s transaction-id=%s exit-code=%s\n' "${evidence_dir}" "${transaction_id}" "${bound_exit_code}" >&"${terminal_output_fd}" || true
  fi
  exit "${bound_exit_code}"
}
trap terminal_exit EXIT
trap 'record_termination_request 130 SIGINT' INT
trap 'record_termination_request 143 SIGTERM' TERM

canonical_directory() {
  local input="$1" label="$2" resolved
  [[ "${input}" == /* && -d "${input}" && ! -L "${input}" ]] || fail "${label} must be an absolute real directory"
  resolved="$(realpath -e -- "${input}")"
  [[ "${resolved}" == "${input}" ]] || fail "${label} must already be canonical"
  printf '%s' "${resolved}"
}

canonical_file() {
  local input="$1" label="$2" resolved
  [[ "${input}" == /* && -f "${input}" && ! -L "${input}" ]] || fail "${label} must be an absolute regular file"
  resolved="$(realpath -e -- "${input}")"
  [[ "${resolved}" == "${input}" ]] || fail "${label} must already be canonical"
  printf '%s' "${resolved}"
}

paths_overlap() { [[ "$1" == "$2" || "$1" == "$2"/* || "$2" == "$1"/* ]]; }

if [[ "$#" -lt 15 ]]; then usage; exit 2; fi
repository=''; reviewed_commit=''; frozen_dotnet_root=''; toolchain_manifest=''
acquisition_feed=''; feed_manifest=''; declare -A seen_options=()
while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --repository|--reviewed-commit|--frozen-dotnet-root|--toolchain-manifest|--acquisition-feed|--feed-manifest|--evidence-dir)
      option="$1"; [[ "$#" -ge 2 ]] || fail "${option} requires a value"
      [[ -z "${seen_options[${option}]+x}" ]] || fail "${option} may be supplied only once"
      seen_options["${option}"]=1; value="$2"; shift 2
      case "${option}" in
        --repository) repository="${value}" ;; --reviewed-commit) reviewed_commit="${value}" ;;
        --frozen-dotnet-root) frozen_dotnet_root="${value}" ;; --toolchain-manifest) toolchain_manifest="${value}" ;;
        --acquisition-feed) acquisition_feed="${value}" ;; --feed-manifest) feed_manifest="${value}" ;;
        --evidence-dir) evidence_dir="${value}" ;;
      esac ;;
    *) break ;;
  esac
done
for required in repository reviewed_commit frozen_dotnet_root toolchain_manifest acquisition_feed feed_manifest evidence_dir; do
  [[ -n "${!required}" ]] || fail "missing required option ${required}"
done
[[ "$#" -ge 1 ]] || fail 'missing operation'; operation="$1"; shift
case "${operation}" in operator-extract|run-analysis|runner) ;; *) fail "unsupported operation ${operation}" ;; esac
[[ "${operation}" == runner || "$#" -eq 0 ]] || fail "${operation} accepts no trailing arguments"
[[ "${operation}" != runner || "$#" -gt 0 ]] || fail 'runner requires a B6 Runner command'
[[ "${reviewed_commit}" =~ ^[0-9a-f]{40}$ ]] || fail 'reviewed commit must be a full lowercase Git SHA'

repository="$(canonical_directory "${repository}" repository)"
frozen_dotnet_root="$(canonical_directory "${frozen_dotnet_root}" frozen-dotnet-root)"
acquisition_feed="$(canonical_directory "${acquisition_feed}" acquisition-feed)"
toolchain_manifest="$(canonical_file "${toolchain_manifest}" toolchain-manifest)"
feed_manifest="$(canonical_file "${feed_manifest}" feed-manifest)"
[[ "${evidence_dir}" == /* && ! -e "${evidence_dir}" && ! -L "${evidence_dir}" ]] || fail 'evidence-dir must be a new absolute path'
evidence_parent="$(realpath -e -- "$(dirname -- "${evidence_dir}")")"
evidence_leaf="$(basename -- "${evidence_dir}")"
[[ "${evidence_leaf}" =~ ^[A-Za-z0-9._-]{1,128}$ && "${evidence_leaf}" != . && "${evidence_leaf}" != .. ]] || fail 'evidence-dir leaf is not controlled'
evidence_dir="${evidence_parent}/${evidence_leaf}"
resolved_root="$(git -C "${repository}" rev-parse --show-toplevel)"
resolved_commit="$(GIT_NO_REPLACE_OBJECTS=1 git -C "${repository}" rev-parse "${reviewed_commit}^{commit}")"
[[ "${resolved_root}" == "${repository}" && "${resolved_commit}" == "${reviewed_commit}" ]] || fail 'repository or Git object is not exact'
paths_overlap "${evidence_dir}" "${repository}" && fail 'evidence directory overlaps repository'
for authority_path in "${frozen_dotnet_root}" "${acquisition_feed}"; do
  paths_overlap "${authority_path}" "${repository}" && fail 'authority input overlaps repository'
  paths_overlap "${authority_path}" "${evidence_dir}" && fail 'authority input overlaps evidence directory'
done
for authority_file in "${toolchain_manifest}" "${feed_manifest}"; do
  paths_overlap "${authority_file}" "${repository}" && fail 'authority input file overlaps repository'
  paths_overlap "${authority_file}" "${evidence_dir}" && fail 'authority input file overlaps evidence directory'
done
mkdir -m 700 -- "${evidence_dir}"

source_dsn="${B6_SOURCE_DSN-}"; source_system_id="${B6_SOURCE_SYSTEM_ID-}"; raw_zone2_dir="${B6_RAW_ZONE2_DIR-}"
pseudonym_key_file="${B6_PSEUDONYM_KEY_FILE-}"; zone3_dir="${B6_ZONE3_DIR-}"; snapshot_id="${B6_SNAPSHOT_ID-}"
tenant_id="${B6_TENANT_ID-}"; analysis_cutoff="${B6_ANALYSIS_SNAPSHOT_CUTOFF-}"; snapshot_dir="${B6_SNAPSHOT_DIR-}"
base_manifest="${B6_BASE_MANIFEST-}"; root_dispositions="${B6_ROOT_DISPOSITIONS-}"; analysis_output_dir="${B6_ANALYSIS_OUTPUT_DIR-}"
docker_host="${DOCKER_HOST-}"; docker_context="${DOCKER_CONTEXT-}"; xdg_runtime_dir="${XDG_RUNTIME_DIR-}"
unset B6_SOURCE_DSN B6_SOURCE_SYSTEM_ID B6_REPOSITORY_COMMIT B6_RAW_ZONE2_DIR B6_PSEUDONYM_KEY_FILE B6_ZONE3_DIR \
  B6_SNAPSHOT_ID B6_TENANT_ID B6_ANALYSIS_SNAPSHOT_CUTOFF B6_SNAPSHOT_DIR B6_BASE_MANIFEST B6_ROOT_DISPOSITIONS \
  B6_ANALYSIS_OUTPUT_DIR B6_EXECUTION_EVIDENCE_CLASSIFICATION B6_PREAUTHORITY_PROVENANCE \
  B6_PREAUTHORITY_REPOSITORY_COMMIT B6_PREAUTHORITY_RUNNER_SHA256 B6_PREAUTHORITY_CORE_SHA256 \
  B6_PREAUTHORITY_EF_SHA256 B6_VERIFIED_ARTIFACT_ROOT

private_root="$(mktemp -d -t aqua-b6-qualified.XXXXXXXX)"; chmod 700 "${private_root}"
source_q="${private_root}/source-q"; source_a="${private_root}/source-a"; source_b="${private_root}/source-b"
git_home="${private_root}/git-home"; git_tmp="${private_root}/git-tmp"; git_template="${private_root}/empty-git-template"
mkdir -m 700 "${git_home}" "${git_tmp}" "${git_template}"

input_frozen_dotnet_root="${frozen_dotnet_root}"; input_toolchain_manifest="${toolchain_manifest}"; input_feed_manifest="${feed_manifest}"
toolchain_manifest="${private_root}/frozen-toolchain-manifest.tsv"; frozen_dotnet_root="${private_root}/frozen-dotnet"
[[ "$(sha256_file "${input_toolchain_manifest}")" == "${expected_toolchain_manifest_sha256}" ]] || fail 'toolchain manifest mismatch before custody'
cp --reflink=never -- "${input_toolchain_manifest}" "${toolchain_manifest}"
mkdir -m 700 "${frozen_dotnet_root}"
cp -a --reflink=never --no-preserve=ownership -- "${input_frozen_dotnet_root}/." "${frozen_dotnet_root}/"
chmod "$(stat -c %a -- "${input_frozen_dotnet_root}")" "${frozen_dotnet_root}"
[[ "$(sha256_file "${toolchain_manifest}")" == "${expected_toolchain_manifest_sha256}" ]] || fail 'toolchain manifest custody mismatch'
feed_manifest="${private_root}/feed-manifest.tsv"
[[ "$(sha256_file "${input_feed_manifest}")" == "${expected_feed_manifest_sha256}" ]] || fail 'feed manifest mismatch before custody'
cp --reflink=never -- "${input_feed_manifest}" "${feed_manifest}"
[[ "$(sha256_file "${feed_manifest}")" == "${expected_feed_manifest_sha256}" ]] || fail 'feed manifest custody mismatch'

phase='MAWK_AUTHORITY'
[[ -x "${qualified_mawk}" && -f "${qualified_mawk}" && ! -L "${qualified_mawk}" ]] || fail 'qualified mawk executable mismatch'
[[ "$(sha256_file "${qualified_mawk}")" == "${expected_mawk_sha256}" ]] || fail 'qualified mawk SHA-256 mismatch'
mawk_output="$(${qualified_mawk} -W version 2>&1)" || fail 'qualified mawk version command failed'
mawk_version="${mawk_output%%$'\n'*}"
[[ "${mawk_version}" == "${expected_mawk_version}" ]] || fail 'qualified mawk version mismatch'
mawk_package="$(/usr/bin/dpkg-query -W -f='${Status}\t${Version}\n' mawk 2>/dev/null || true)"
[[ "${mawk_package}" == $'install ok installed\t'"${expected_mawk_package_version}" ]] || fail 'qualified mawk package version mismatch'
printf 'path\t%s\nsha256\t%s\nversion\t%s\npackage_version\t%s\n' "${qualified_mawk}" "${expected_mawk_sha256}" "${mawk_version}" "${expected_mawk_package_version}" > "${evidence_dir}/mawk-identity.tsv"

phase='TOOLCHAIN_AUTHORITY'
[[ "$(sha256_file "${toolchain_manifest}")" == "${expected_toolchain_manifest_sha256}" ]] || fail 'toolchain manifest mismatch'
[[ "$(stat -c %a -- "${frozen_dotnet_root}")" == 555 ]] || fail 'frozen toolchain root mode mismatch'
actual_toolchain="${private_root}/actual-toolchain-manifest.tsv"
printf 'type\tmode\tsize\tsha256\tlink_target\tpath\n' > "${actual_toolchain}"
while IFS= read -r -d '' relative; do
  [[ -n "${relative}" && "${relative}" != *$'\t'* && "${relative}" != *$'\n'* && "${relative}" != *$'\r'* ]] || fail 'unrepresentable toolchain path'
  candidate="${frozen_dotnet_root}/${relative}"; mode="$(stat -c %a -- "${candidate}")"
  if [[ -L "${candidate}" ]]; then
    link_target="$(readlink -- "${candidate}")"
    [[ "${link_target}" != *$'\t'* && "${link_target}" != *$'\n'* && "${link_target}" != *$'\r'* ]] || fail 'unrepresentable toolchain link target'
    link_sha="$(printf '%s' "${link_target}" | sha256sum | { read -r digest _; printf '%s' "${digest}"; })"
    printf 'L\t%s\t-\t%s\t%s\t%s\n' "${mode}" "${link_sha}" "${link_target}" "${relative}" >> "${actual_toolchain}"
  elif [[ -f "${candidate}" ]]; then
    printf 'F\t%s\t%s\t%s\t-\t%s\n' "${mode}" "$(stat -c %s -- "${candidate}")" "$(sha256_file "${candidate}")" "${relative}" >> "${actual_toolchain}"
  elif [[ -d "${candidate}" ]]; then
    printf 'D\t%s\t-\t-\t-\t%s\n' "${mode}" "${relative}" >> "${actual_toolchain}"
  else
    fail "unsupported frozen-toolchain entry ${relative}"
  fi
done < <(find -P "${frozen_dotnet_root}" -mindepth 1 -printf '%P\0' | sort -z)
inventory_counts="$(${qualified_mawk} -F '\t' '
  NR==FNR {if (FNR>1) expected[$6]=$0; next}
  FNR>1 {actual[$6]=$0}
  END {
    for (path in expected) {
      if (!(path in actual)) missing++
      else if (expected[path] != actual[path]) mismatch++
    }
    for (path in actual) if (!(path in expected)) unexpected++
    printf "%d\t%d\t%d\n", missing, unexpected, mismatch
  }' "${toolchain_manifest}" "${actual_toolchain}")" || fail 'toolchain inventory comparison failed'
IFS=$'\t' read -r toolchain_missing toolchain_unexpected toolchain_mismatch <<<"${inventory_counts}"
printf 'expected_manifest_sha256\t%s\nactual_manifest_sha256\t%s\nmissing\t%s\nunexpected\t%s\nidentity_mismatch\t%s\n' \
  "${expected_toolchain_manifest_sha256}" "$(sha256_file "${actual_toolchain}")" "${toolchain_missing}" "${toolchain_unexpected}" "${toolchain_mismatch}" > "${evidence_dir}/toolchain-inventory-result.tsv"
[[ "${toolchain_missing}" == 0 && "${toolchain_unexpected}" == 0 && "${toolchain_mismatch}" == 0 ]] || fail 'actual toolchain inventory differs from authority'
cmp -s "${toolchain_manifest}" "${actual_toolchain}" || fail 'canonical toolchain inventory ordering differs from authority'
frozen_dotnet="${frozen_dotnet_root}/dotnet"
[[ -x "${frozen_dotnet}" && ! -L "${frozen_dotnet}" && "$(sha256_file "${frozen_dotnet}")" == "${expected_dotnet_host_sha256}" ]] || fail 'dotnet host mismatch'
sdk_version="$(${frozen_dotnet} --version)"; sdk_inventory="$(${frozen_dotnet} --list-sdks)"; runtime_inventory="$(${frozen_dotnet} --list-runtimes)"; nuget_inventory="$(${frozen_dotnet} nuget --version)"
cp -- "${actual_toolchain}" "${evidence_dir}/actual-toolchain-manifest.tsv"
cp -- "${toolchain_manifest}" "${evidence_dir}/frozen-toolchain-manifest.tsv"
printf 'sdk_version\t%s\nsdk_inventory\t%s\nruntime_inventory_sha256\t%s\nnuget_inventory_sha256\t%s\n' "${sdk_version}" "${sdk_inventory}" "$(printf '%s' "${runtime_inventory}" | sha256sum | { read -r d _; printf '%s' "${d}"; })" "$(printf '%s' "${nuget_inventory}" | sha256sum | { read -r d _; printf '%s' "${d}"; })" > "${evidence_dir}/toolchain-discovery.tsv"
[[ "${sdk_version}" == "${expected_sdk_version}" ]] || fail 'SDK selection mismatch'
[[ "${sdk_inventory}" == "${expected_sdk_version} [${frozen_dotnet_root}/sdk]" ]] || fail 'SDK inventory mismatch'
[[ "${runtime_inventory}" == $'Microsoft.AspNetCore.App '"${expected_runtime_version}"$' ['"${frozen_dotnet_root}"$'/shared/Microsoft.AspNetCore.App]\nMicrosoft.NETCore.App '"${expected_runtime_version}"$' ['"${frozen_dotnet_root}"$'/shared/Microsoft.NETCore.App]' ]] || fail 'runtime inventory mismatch'
[[ "${nuget_inventory}" == $'NuGet Command Line\n'"${expected_nuget_version}" ]] || fail 'NuGet inventory mismatch'

private_git() { env -i PATH="/usr/bin:/bin" HOME="${git_home}" TMPDIR="${git_tmp}" GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null GIT_ATTR_NOSYSTEM=1 GIT_NO_REPLACE_OBJECTS=1 git "$@"; }
verify_materialized_reviewed_tree() {
  local source="$1" expected_tree actual_tree record metadata path mode object stage actual count=0
  expected_tree="$(private_git -C "${source}" rev-parse "${reviewed_commit}^{tree}")"; actual_tree="$(private_git -C "${source}" write-tree)"
  [[ "${actual_tree}" == "${expected_tree}" ]] || fail 'materialized Git index differs from reviewed tree'
  while IFS= read -r -d '' record; do
    metadata="${record%%$'\t'*}"; path="${record#*$'\t'}"; read -r mode object stage <<<"${metadata}"
    [[ "${stage}" == 0 && ( "${mode}" == 100644 || "${mode}" == 100755 || "${mode}" == 120000 ) ]] || fail "unsupported reviewed entry ${path}"
    actual="$(private_git -C "${source}" hash-object --no-filters -- "${path}")"; [[ "${actual}" == "${object}" ]] || fail "materialized file differs ${path}"
    count=$((count + 1))
  done < <(private_git -C "${source}" ls-files -s -z)
  [[ "${count}" -gt 0 && -z "$(private_git -C "${source}" status --porcelain=v1 --untracked-files=all)" ]] || fail 'materialized source is not clean'
}
materialize_reviewed_tree() {
  local destination="$1"
  private_git clone --no-checkout --no-local --template="${git_template}" --config core.hooksPath=/dev/null "${repository}" "${destination}" >&2
  private_git -C "${destination}" -c core.hooksPath=/dev/null -c core.fsmonitor=false checkout --detach --force "${reviewed_commit}" >&2
  [[ "$(private_git -C "${destination}" rev-parse HEAD)" == "${reviewed_commit}" ]] || fail 'reviewed source mismatch'
  verify_materialized_reviewed_tree "${destination}"
}

phase='REVIEWED_SOURCE'
materialize_reviewed_tree "${source_q}"; materialize_reviewed_tree "${source_a}"; materialize_reviewed_tree "${source_b}"
authority_dir="${source_q}/${package_relative}/authority"; g_manifest="${authority_dir}/G197.tsv"; tq_manifest="${authority_dir}/TQ186.tsv"; u_manifest="${authority_dir}/U215.tsv"; u1_manifest="${authority_dir}/U1.tsv"; u2_manifest="${authority_dir}/U2.tsv"
for contract in "${g_manifest}:${expected_g_count}:${expected_g_sha256}" "${tq_manifest}:${expected_tq_count}:${expected_tq_sha256}" "${u_manifest}:${expected_u_count}:${expected_u_sha256}" "${u1_manifest}:${expected_u1_count}:${expected_u1_sha256}" "${u2_manifest}:${expected_u2_count}:${expected_u2_sha256}"; do
  path="${contract%%:*}"; remainder="${contract#*:}"; expected_count="${remainder%%:*}"; expected_sha="${remainder#*:}"
  [[ -f "${path}" && ! -L "${path}" && "$(wc -l < "${path}")" == "${expected_count}" && "$(sha256_file "${path}")" == "${expected_sha}" ]] || fail "authority manifest mismatch ${path}"
done
nuget_config="${source_q}/${package_relative}/qualification-nuget.config"
[[ -f "${nuget_config}" && ! -L "${nuget_config}" && "$(sha256_file "${nuget_config}")" == "${expected_signer_policy_sha256}" ]] || fail 'signer policy identity mismatch'
for fingerprint in 0E5F38F57DC1BCC806D8494F4F90FBCEDD988B46760709CBEEC6F4219AA6157D 1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D 5A2901D6ADA3D18260B9C6DFE2133C95D74B9EEF6AE0E5DC334C8454D1477DF4; do grep -Fq "fingerprint=\"${fingerprint}\"" "${nuget_config}" || fail 'signer fingerprint mismatch'; done
grep -Fq 'signatureValidationMode" value="require"' "${nuget_config}" || fail 'signature validation not required'
grep -Fq 'allowUntrustedRoot="false"' "${nuget_config}" || fail 'trusted root requirement missing'
signer_policy_sha256="$(sha256_file "${nuget_config}")"; cp -- "${nuget_config}" "${evidence_dir}/qualification-nuget.config"

phase='A_L_PRODUCER_CUSTODY'
[[ "$(sha256_file "${feed_manifest}")" == "${expected_feed_manifest_sha256}" ]] || fail 'U215 feed manifest mismatch'
[[ "$(head -n 1 "${feed_manifest}")" == $'id\tversion\tarchive\traw_sha256\traw_sha512_base64\tcontent_hash\tarchive_nuspec_sha256\tinstalled_nuspec_sha256\tsource' ]] || fail 'feed header mismatch'
[[ -z "$(find -P "${acquisition_feed}" -mindepth 1 ! -type f -print -quit)" ]] || fail 'acquisition feed contains unsafe entries'
custody_feed="${private_root}/a-l-custody-feed"; custody_packages="${private_root}/a-l-custody-packages"
mkdir -m 700 "${custody_feed}" "${custody_packages}" "${private_root}/online-trust-home" "${private_root}/online-trust-tmp"
feed_details="${private_root}/feed-details.tsv"; feed_coordinates="${private_root}/feed-coordinates.tsv"; : > "${feed_details}"; : > "${feed_coordinates}"
feed_rows=0
while IFS=$'\t' read -r id version archive raw_sha256 raw_sha512 content_hash archive_nuspec installed_nuspec source; do
  [[ "${id}" =~ ^[a-z0-9][a-z0-9_.-]*$ && "${version}" =~ ^[0-9A-Za-z][0-9A-Za-z.+-]*$ && "${archive}" == "${id}.${version}.nupkg" && "${source}" == 'https://api.nuget.org/v3/index.json' ]] || fail "feed row mismatch ${id}/${version}"
  archive_path="${acquisition_feed}/${archive}"
  [[ -f "${archive_path}" && ! -L "${archive_path}" && "$(sha256_file "${archive_path}")" == "${raw_sha256}" ]] || fail "feed bytes mismatch ${archive}"
  cp --reflink=never -- "${archive_path}" "${custody_feed}/${archive}"
  [[ "$(sha256_file "${custody_feed}/${archive}")" == "${raw_sha256}" ]] || fail "custody copy mismatch ${archive}"
  printf '%s\t%s\n' "${id}" "${version}" >> "${feed_coordinates}"
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "${id}" "${version}" "${archive}" "${raw_sha256}" "${raw_sha512}" "${content_hash}" "${archive_nuspec}" "${installed_nuspec}" "${source}" >> "${feed_details}"
  feed_rows=$((feed_rows + 1))
done < <(tail -n +2 "${feed_manifest}")
[[ "${feed_rows}" == "${expected_u_count}" && "$(find -P "${acquisition_feed}" -mindepth 1 -maxdepth 1 -type f | wc -l)" == "${expected_u_count}" ]] || fail 'feed inventory mismatch'
sort -o "${feed_coordinates}" "${feed_coordinates}"; cmp -s "${feed_coordinates}" "${u_manifest}" || fail 'feed coordinates differ from U215'

a_l_home="${private_root}/online-trust-home"; a_l_tmp="${private_root}/online-trust-tmp"
a_l_http="${private_root}/online-trust-http"; a_l_artifacts="${private_root}/a-l-artifacts"
mkdir -m 700 "${a_l_http}" "${a_l_artifacts}"
a_l_restore_log="${private_root}/a-l-locked-acquisition.log"
if ! env -i PATH="${frozen_dotnet_root}:/usr/bin:/bin" HOME="${a_l_home}" TMPDIR="${a_l_tmp}" \
    DOTNET_ROOT="${frozen_dotnet_root}" DOTNET_ROOT_X64="${frozen_dotnet_root}" DOTNET_CLI_HOME="${a_l_home}" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1 \
    DOTNET_NUGET_SIGNATURE_VERIFICATION=true NUGET_CERT_REVOCATION_MODE=online \
    NUGET_PACKAGES="${custody_packages}" NUGET_HTTP_CACHE_PATH="${a_l_http}" \
    "${frozen_dotnet}" restore "${source_q}/${test_relative}" --configfile "${nuget_config}" \
      --packages "${custody_packages}" --locked-mode --source "${custody_feed}" --disable-build-servers \
      -p:ArtifactsPath="${a_l_artifacts}" -p:ImportDirectoryBuildProps=false \
      -p:ImportDirectoryBuildTargets=false >"${a_l_restore_log}" 2>&1; then
  fail 'native locked A-L acquisition failed'
fi
cp -- "${a_l_restore_log}" "${evidence_dir}/a-l-locked-acquisition.log"

validate_package_root() {
  local source="$1" expected_manifest="$2" raw_output="$3" label="$4" installed_source="$5" id version directory archive expected_row archive_name raw_sha raw_sha512 content_hash archive_nuspec installed_nuspec package_source archive_nuspec_entry actual_archive_nuspec actual_raw_sha512 expected_files actual_files entry installed_path count=0
  [[ -z "$(find -P "${source}" -mindepth 1 -type l -print -quit)" && -z "$(find -P "${source}" -mindepth 1 ! -type d ! -type f -print -quit)" ]] || fail "${label} contains unsafe entries"
  : > "${raw_output}"
  while IFS=$'\t' read -r id version; do
    expected_row="$(${qualified_mawk} -F '\t' -v id="${id}" -v version="${version}" '$1==id && $2==version {print; found++} END {if(found!=1) exit 1}' "${feed_details}")" || fail "${label} feed identity missing ${id}/${version}"
    IFS=$'\t' read -r _ _ archive_name raw_sha raw_sha512 content_hash archive_nuspec installed_nuspec package_source <<<"${expected_row}"
    directory="${source}/${id}/${version}"; archive="${directory}/${archive_name}"
    [[ -f "${archive}" && ! -L "${archive}" && -f "${archive}.sha512" && ! -L "${archive}.sha512" && -f "${directory}/${id}.nuspec" && ! -L "${directory}/${id}.nuspec" && -f "${directory}/.nupkg.metadata" && ! -L "${directory}/.nupkg.metadata" ]] || fail "${label} missing ${id}/${version}"
    [[ "$(sha256_file "${archive}")" == "${raw_sha}" ]] || fail "${label} archive mismatch ${id}/${version}"
    actual_raw_sha512="$(/usr/bin/openssl dgst -sha512 -binary "${archive}" | /usr/bin/base64 -w0)"
    [[ "${actual_raw_sha512}" == "${raw_sha512}" ]] || fail "${label} raw SHA-512 mismatch ${id}/${version}"
    [[ "$(tr -d '\r\n' < "${archive}.sha512")" == "${content_hash}" ]] || fail "${label} content hash mismatch ${id}/${version}"
    [[ "$(sha256_file "${directory}/${id}.nuspec")" == "${installed_nuspec}" ]] || fail "${label} nuspec mismatch ${id}/${version}"
    archive_nuspec_entry="$(/usr/bin/unzip -Z1 "${archive}" | ${qualified_mawk} -v expected="${id}.nuspec" 'tolower($0)==expected {print; found++} END {if(found!=1) exit 1}')" || fail "${label} archive nuspec missing ${id}/${version}"
    actual_archive_nuspec="$(/usr/bin/unzip -p "${archive}" "${archive_nuspec_entry}" | sha256sum | { read -r digest _; printf '%s' "${digest}"; })"
    [[ "${actual_archive_nuspec}" == "${archive_nuspec}" ]] || fail "${label} archive nuspec mismatch ${id}/${version}"
    ${qualified_mawk} -v expected_hash="${content_hash}" -v expected_source="${installed_source}" 'BEGIN {found_version=0; found_hash=0; found_source=0} {line=$0; gsub(/[[:space:]]/,"",line); if(index(line,"\"version\":2")>0) found_version=1; if(index(line,"\"contentHash\":\"" expected_hash "\"")>0) found_hash=1; if(index(line,"\"source\":\"" expected_source "\"")>0) found_source=1} END {exit !(found_version && found_hash && found_source)}' "${directory}/.nupkg.metadata" || fail "${label} installed-source metadata mismatch ${id}/${version}"
    expected_files="${private_root}/.expected-files-${count}.txt"; actual_files="${private_root}/.actual-files-${count}.txt"; : > "${expected_files}"
    while IFS= read -r entry; do
      [[ -n "${entry}" && "${entry}" != */ ]] || continue
      [[ "${entry}" != /* && "${entry}" != *\\* && "${entry}" != .. && "${entry}" != ../* && "${entry}" != */../* && "${entry}" != */.. && "${entry}" != *$'\t'* && "${entry}" != *$'\r'* ]] || fail "${label} unsafe archive path ${id}/${version}"
      case "${entry}" in _rels/*|'[Content_Types].xml'|package/services/metadata/core-properties/*) continue ;; esac
      if [[ "${entry,,}" == "${id}.nuspec" ]]; then installed_path="${id}.nuspec"; else installed_path="${entry}"; fi
      printf '%s\n' "${installed_path}" >> "${expected_files}"
      [[ -f "${directory}/${installed_path}" && ! -L "${directory}/${installed_path}" ]] || fail "${label} extracted file missing ${id}/${version}/${installed_path}"
      if [[ "${entry,,}" != "${id}.nuspec" ]]; then
        [[ "$(/usr/bin/unzip -p "${archive}" "${entry}" | sha256sum | { read -r digest _; printf '%s' "${digest}"; })" == "$(sha256_file "${directory}/${installed_path}")" ]] || fail "${label} extracted file mismatch ${id}/${version}/${installed_path}"
      fi
    done < <(/usr/bin/unzip -Z1 "${archive}")
    printf '%s\n%s\n%s\n' "${archive_name}" "${archive_name}.sha512" '.nupkg.metadata' >> "${expected_files}"
    sort -u -o "${expected_files}" "${expected_files}"
    (cd "${directory}" && find -P . -type f -printf '%P\n' | sort) > "${actual_files}"
    cmp -s "${expected_files}" "${actual_files}" || fail "${label} installed tree inventory mismatch ${id}/${version}"
    rm -f -- "${expected_files}" "${actual_files}"
    cmp -s "${archive}" "${custody_feed}/${archive_name}" || fail "${label} archive differs from verified custody feed ${id}/${version}"
    printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "${id}" "${version}" "${raw_sha}" "${raw_sha512}" "${content_hash}" "${archive_nuspec}" "${package_source}" >> "${raw_output}"
    count=$((count + 1))
  done < "${expected_manifest}"
  [[ "${count}" == "$(wc -l < "${expected_manifest}")" ]] || fail "${label} count mismatch"
  while IFS= read -r -d '' directory; do
    id="$(basename -- "$(dirname -- "${directory}")")"; version="$(basename -- "${directory}")"
    grep -Fqx "${id}"$'\t'"${version}" "${expected_manifest}" || fail "${label} unexpected ${id}/${version}"
  done < <(find -P "${source}" -mindepth 2 -maxdepth 2 -type d -print0)
  sort -o "${raw_output}" "${raw_output}"
}

write_package_tree_manifest() {
  local source="$1" output="$2" relative candidate mode
  printf 'type\tmode\tsize\tsha256\tpath\n' > "${output}"
  while IFS= read -r -d '' relative; do
    candidate="${source}/${relative}"; mode="$(stat -c %a -- "${candidate}")"
    if [[ -f "${candidate}" && ! -L "${candidate}" ]]; then
      printf 'F\t%s\t%s\t%s\t%s\n' "${mode}" "$(stat -c %s -- "${candidate}")" "$(sha256_file "${candidate}")" "${relative}" >> "${output}"
    elif [[ -d "${candidate}" && ! -L "${candidate}" ]]; then
      printf 'D\t%s\t-\t-\t%s\n' "${mode}" "${relative}" >> "${output}"
    else fail "unsafe package-tree entry ${relative}"; fi
  done < <(find -P "${source}" -mindepth 1 -printf '%P\0' | sort -z)
}

validate_package_root "${custody_packages}" "${u_manifest}" "${private_root}/custody-u215.tsv" 'A-L custody U215' "${custody_feed}"
transaction_id="$(tr -d '-' < /proc/sys/kernel/random/uuid)"
[[ "${transaction_id}" =~ ^[0-9a-f]{32}$ ]] || fail 'parent transaction ID generation failed'
boot_id="$(tr -d '\r\n' < /proc/sys/kernel/random/boot_id)"; parent_pid="$$"; parent_starttime="$(process_starttime "$$")"
monotonic_centiseconds() {
  ${qualified_mawk} '{split($1,p,"."); fraction=p[2] "00"; printf "%.0f\n", (p[1]*100)+substr(fraction,1,2); exit}' /proc/uptime
}
trust_ledger="${private_root}/online-trust-ledger.tsv"; trust_logs_manifest="${private_root}/online-trust-logs.tsv"
printf 'transaction_id\tid\tversion\traw_sha256\traw_sha512_base64\tcontent_hash\tarchive_nuspec_sha256\tinstalled_nuspec_sha256\tsource\tsignature_classification\tverification_outcome\texit_code\tstarted_utc\tcompleted_utc\tauthor_signature\trepository_signature\trepository_signer_sha256\tnuget_version\tdotnet_host_sha256\ttoolchain_manifest_sha256\tsigner_policy_sha256\tverification_log_sha256\n' > "${trust_ledger}"
printf 'id\tversion\tverification_log_sha256\n' > "${trust_logs_manifest}"
mkdir -m 700 "${evidence_dir}/online-trust-logs"
trust_validation_started_at=''; trust_validation_completed_at=''; trust_validation_completed_monotonic=''
while IFS=$'\t' read -r id version archive raw_sha256 raw_sha512 content_hash archive_nuspec installed_nuspec source; do
  log="${evidence_dir}/online-trust-logs/${id}.${version}.verify.log"; started="$(date -u +%Y-%m-%dT%H:%M:%SZ)"; [[ -n "${trust_validation_started_at}" ]] || trust_validation_started_at="${started}"
  if ! env -i PATH="${frozen_dotnet_root}:/usr/bin:/bin" HOME="${private_root}/online-trust-home" TMPDIR="${private_root}/online-trust-tmp" DOTNET_ROOT="${frozen_dotnet_root}" DOTNET_ROOT_X64="${frozen_dotnet_root}" DOTNET_CLI_HOME="${private_root}/online-trust-home" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_NOLOGO=1 DOTNET_NUGET_SIGNATURE_VERIFICATION=true NUGET_CERT_REVOCATION_MODE=online "${frozen_dotnet}" nuget verify --all --configfile "${nuget_config}" --verbosity detailed "${custody_feed}/${archive}" > "${log}" 2>&1; then
    fail "native online trust verification failed ${id}/${version}"
  fi
  completed="$(date -u +%Y-%m-%dT%H:%M:%SZ)"; trust_validation_completed_at="${completed}"; trust_validation_completed_monotonic="$(monotonic_centiseconds)"
  "${qualified_mawk}" -v expected="${id}.${version}" '
    BEGIN {wanted="successfully verified package \047" tolower(expected) "\047."}
    tolower($0)==wanted {found++}
    END {exit !(found==1)}' "${log}" || fail "native verification success record missing ${id}/${version}"
  grep -Fqx "Service index: ${source}" "${log}" || fail "repository source evidence missing ${id}/${version}"
  repository_signer="$(${qualified_mawk} '/^Signature type: Repository$/ {repository=1; next} repository && /^  SHA256 hash: [0-9A-F]+$/ {print $3; found++; exit} END {if(found!=1) exit 1}' "${log}")" || fail "repository signer evidence missing ${id}/${version}"
  case "${repository_signer}" in
    0E5F38F57DC1BCC806D8494F4F90FBCEDD988B46760709CBEEC6F4219AA6157D|1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D|5A2901D6ADA3D18260B9C6DFE2133C95D74B9EEF6AE0E5DC334C8454D1477DF4) ;;
    *) fail "unapproved repository signer ${id}/${version}" ;;
  esac
  author_signature=no; grep -Fqx 'Signature type: Author' "${log}" && author_signature=yes
  log_sha="$(sha256_file "${log}")"
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\tSIGNED\tSUCCESS\t0\t%s\t%s\t%s\tyes\t%s\t%s\t%s\t%s\t%s\t%s\n' "${transaction_id}" "${id}" "${version}" "${raw_sha256}" "${raw_sha512}" "${content_hash}" "${archive_nuspec}" "${installed_nuspec}" "${source}" "${started}" "${completed}" "${author_signature}" "${repository_signer}" "${expected_nuget_version}" "${expected_dotnet_host_sha256}" "${expected_toolchain_manifest_sha256}" "${expected_signer_policy_sha256}" "${log_sha}" >> "${trust_ledger}"
  printf '%s\t%s\t%s\n' "${id}" "${version}" "${log_sha}" >> "${trust_logs_manifest}"
done < "${feed_details}"
[[ "$(($(wc -l < "${trust_ledger}") - 1))" == "${expected_u_count}" ]] || fail 'online trust row count mismatch'

private_u_source="${private_root}/promoted-u215"; private_g_source="${private_root}/promoted-g197"; mkdir -m 700 "${private_u_source}" "${private_g_source}"
copy_authority_packages() { local manifest="$1" destination="$2" id version; while IFS=$'\t' read -r id version; do mkdir -p -m 700 "${destination}/${id}"; cp -R --no-dereference -- "${custody_packages}/${id}/${version}" "${destination}/${id}/${version}"; done < "${manifest}"; }
copy_authority_packages "${u_manifest}" "${private_u_source}"; copy_authority_packages "${g_manifest}" "${private_g_source}"
validate_package_root "${private_u_source}" "${u_manifest}" "${private_root}/promoted-u215.tsv" 'promoted U215' "${custody_feed}"
validate_package_root "${private_g_source}" "${g_manifest}" "${private_root}/promoted-g197.tsv" 'promoted G197' "${custody_feed}"
chmod -R a-w "${private_u_source}" "${private_g_source}"
write_package_tree_manifest "${private_u_source}" "${evidence_dir}/promoted-u215-tree.tsv"
cp -- "${feed_manifest}" "${evidence_dir}/feed-manifest.tsv"; cp -- "${trust_ledger}" "${evidence_dir}/online-trust-ledger.tsv"; cp -- "${trust_logs_manifest}" "${evidence_dir}/online-trust-logs.tsv"
ledger_sha256="$(sha256_file "${evidence_dir}/online-trust-ledger.tsv")"; logs_manifest_sha256="$(sha256_file "${evidence_dir}/online-trust-logs.tsv")"; promoted_manifest_sha256="$(sha256_file "${evidence_dir}/promoted-u215-tree.tsv")"; acquisition_log_sha256="$(sha256_file "${evidence_dir}/a-l-locked-acquisition.log")"
receipt="${evidence_dir}/a-l-transaction-receipt.tsv"; receipt_tmp="${private_root}/a-l-transaction-receipt.tsv"
printf 'schema\tAQGREEN_B6_A_L_TRANSACTION_V1\ntransaction_id\t%s\nparent_pid\t%s\nparent_starttime\t%s\nboot_id\t%s\nfeed_manifest_sha256\t%s\nlocked_acquisition_log_sha256\t%s\ntrust_ledger_sha256\t%s\ntrust_logs_manifest_sha256\t%s\npromoted_u215_tree_manifest_sha256\t%s\nsigner_policy_sha256\t%s\ntoolchain_manifest_sha256\t%s\ntrust_validation_started_utc\t%s\ntrust_validation_completed_utc\t%s\ntrust_validation_completed_monotonic_centiseconds\t%s\npackage_count\t%s\nresult\tPASS\n' "${transaction_id}" "${parent_pid}" "${parent_starttime}" "${boot_id}" "${expected_feed_manifest_sha256}" "${acquisition_log_sha256}" "${ledger_sha256}" "${logs_manifest_sha256}" "${promoted_manifest_sha256}" "${signer_policy_sha256}" "${expected_toolchain_manifest_sha256}" "${trust_validation_started_at}" "${trust_validation_completed_at}" "${trust_validation_completed_monotonic}" "${expected_u_count}" > "${receipt_tmp}"
atomic_write "${receipt}" "${receipt_tmp}"

receipt_value() {
  local key="$1" file="$2"
  ${qualified_mawk} -F '\t' -v key="${key}" '$1==key {value=$2; found++} END {if(found!=1 || value=="") exit 1; print value}' "${file}"
}
validate_civil_timestamp() {
  local value="$1"
  [[ "${value}" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$ ]] || return 1
  [[ "$(date -u -d "${value}" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null)" == "${value}" ]]
}
receipt_error=''
verify_transaction_receipt() {
  local value id version expected_log actual_log ledger_log_binding
  receipt_error=''
  [[ -f "${receipt}" && ! -L "${receipt}" && "$(stat -c %a -- "${receipt}")" == 600 && "$(stat -c %h -- "${receipt}")" == 1 ]] || { receipt_error='receipt-file'; return 1; }
  [[ "$(receipt_value schema "${receipt}" 2>/dev/null || true)" == AQGREEN_B6_A_L_TRANSACTION_V1 ]] || { receipt_error='receipt-schema'; return 1; }
  for binding in "transaction_id:${transaction_id}" "parent_pid:${parent_pid}" "parent_starttime:${parent_starttime}" "boot_id:${boot_id}" "feed_manifest_sha256:${expected_feed_manifest_sha256}" "locked_acquisition_log_sha256:${acquisition_log_sha256}" "trust_ledger_sha256:${ledger_sha256}" "trust_logs_manifest_sha256:${logs_manifest_sha256}" "promoted_u215_tree_manifest_sha256:${promoted_manifest_sha256}" "signer_policy_sha256:${signer_policy_sha256}" "toolchain_manifest_sha256:${expected_toolchain_manifest_sha256}" "package_count:${expected_u_count}" "result:PASS"; do
    key="${binding%%:*}"; expected="${binding#*:}"; [[ "$(receipt_value "${key}" "${receipt}" 2>/dev/null || true)" == "${expected}" ]] || { receipt_error="receipt-${key}"; return 1; }
  done
  value="$(receipt_value trust_validation_started_utc "${receipt}" 2>/dev/null || true)"; validate_civil_timestamp "${value}" && [[ "${value}" == "${trust_validation_started_at}" ]] || { receipt_error='receipt-started-time'; return 1; }
  value="$(receipt_value trust_validation_completed_utc "${receipt}" 2>/dev/null || true)"; validate_civil_timestamp "${value}" || { receipt_error='receipt-completed-time'; return 1; }
  [[ "${value}" == "${trust_validation_completed_at}" && "$(receipt_value trust_validation_completed_monotonic_centiseconds "${receipt}" 2>/dev/null || true)" == "${trust_validation_completed_monotonic}" ]] || { receipt_error='receipt-trust-completion-binding'; return 1; }
  [[ "$(tr -d '\r\n' < /proc/sys/kernel/random/boot_id)" == "${boot_id}" && "$(process_starttime "$$" 2>/dev/null || true)" == "${parent_starttime}" ]] || { receipt_error='receipt-restart'; return 1; }
  [[ "$(sha256_file "${evidence_dir}/a-l-locked-acquisition.log")" == "${acquisition_log_sha256}" && "$(sha256_file "${evidence_dir}/online-trust-ledger.tsv")" == "${ledger_sha256}" && "$(sha256_file "${evidence_dir}/online-trust-logs.tsv")" == "${logs_manifest_sha256}" && "$(sha256_file "${evidence_dir}/promoted-u215-tree.tsv")" == "${promoted_manifest_sha256}" ]] || { receipt_error='receipt-bound-file'; return 1; }
  ledger_log_binding="${private_root}/ledger-log-binding.tsv"
  "${qualified_mawk}" -F '\t' -v transaction="${transaction_id}" 'NR==1 {next} $1!=transaction {exit 2} {print $2 "\t" $3 "\t" $NF; count++} END {if(count==0) exit 1}' "${evidence_dir}/online-trust-ledger.tsv" > "${ledger_log_binding}" || { receipt_error='receipt-ledger-transaction'; return 1; }
  tail -n +2 "${evidence_dir}/online-trust-logs.tsv" > "${private_root}/retained-log-binding.tsv"
  cmp -s "${ledger_log_binding}" "${private_root}/retained-log-binding.tsv" || { receipt_error='receipt-ledger-log-binding'; return 1; }
  write_package_tree_manifest "${private_u_source}" "${private_root}/promoted-u215-tree.replay.tsv"
  cmp -s "${private_root}/promoted-u215-tree.replay.tsv" "${evidence_dir}/promoted-u215-tree.tsv" || { receipt_error='receipt-promoted-tree'; return 1; }
  while IFS=$'\t' read -r id version expected_log; do
    [[ "${id}" == id && "${version}" == version ]] && continue
    actual_log="${evidence_dir}/online-trust-logs/${id}.${version}.verify.log"
    [[ -f "${actual_log}" && ! -L "${actual_log}" && "$(sha256_file "${actual_log}")" == "${expected_log}" ]] || { receipt_error="receipt-log-${id}-${version}"; return 1; }
  done < "${evidence_dir}/online-trust-logs.tsv"
  return 0
}

record_trust_freshness() {
  local checkpoint="$1" freshness_tmp final_package_consumption_at final_monotonic='' trust_epoch final_epoch
  local freshness_result='FAIL' freshness_reason='UNKNOWN' elapsed_centiseconds=0 civil_elapsed_seconds=0 clock_delta_centiseconds=0 clock_continuity='PASS'
  final_package_consumption_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  if ! verify_transaction_receipt; then freshness_reason="${receipt_error}"
  elif ! final_monotonic="$(monotonic_centiseconds)" || [[ ! "${final_monotonic}" =~ ^[0-9]+$ ]]; then freshness_reason='MONOTONIC_TIME_UNAVAILABLE'
  elif ! validate_civil_timestamp "${trust_validation_completed_at}" || ! validate_civil_timestamp "${final_package_consumption_at}"; then freshness_reason='MALFORMED_OR_MISSING_CIVIL_TIME'
  elif (( final_monotonic < trust_validation_completed_monotonic )); then freshness_reason='MONOTONIC_REVERSAL_OR_RESTART'
  else
    elapsed_centiseconds=$((final_monotonic-trust_validation_completed_monotonic))
    trust_epoch="$(date -u -d "${trust_validation_completed_at}" +%s)"; final_epoch="$(date -u -d "${final_package_consumption_at}" +%s)"
    civil_elapsed_seconds=$((final_epoch-trust_epoch))
    clock_delta_centiseconds=$((civil_elapsed_seconds*100-elapsed_centiseconds))
    (( clock_delta_centiseconds >= 0 )) || clock_delta_centiseconds=$((-clock_delta_centiseconds))
    if (( final_epoch < trust_epoch )); then clock_continuity='FAIL'; freshness_reason='CIVIL_CLOCK_REVERSAL'
    elif (( clock_delta_centiseconds > 200 )); then clock_continuity='FAIL'; freshness_reason='CLOCK_DISCONTINUITY'
    elif (( elapsed_centiseconds > freshness_limit_seconds * 100 )); then freshness_reason='POLICY_LIMIT_EXCEEDED'
    else freshness_result='PASS'; freshness_reason='WITHIN_POLICY_BOUND'; fi
  fi
  freshness_tmp="${private_root}/trust-freshness.tsv"
  printf 'Checkpoint\t%s\nTrustValidationCompletedAt\t%s\nFinalPackageConsumptionAt\t%s\nElapsedDurationCentiseconds\t%s\nElapsedDuration\t%s.%02d seconds\nCivilElapsedDurationSeconds\t%s\nCivilMonotonicDeltaCentiseconds\t%s\nPolicyLimit\t%s seconds\nBoundaryRule\tELAPSED_LE_POLICY_LIMIT\nTimingSource\tLINUX_MONOTONIC_UPTIME\nClockContinuity\t%s\nTransactionId\t%s\nResult\t%s\nReason\t%s\n' "${checkpoint}" "${trust_validation_completed_at}" "${final_package_consumption_at}" "${elapsed_centiseconds}" "$((elapsed_centiseconds/100))" "$((elapsed_centiseconds%100))" "${civil_elapsed_seconds}" "${clock_delta_centiseconds}" "${freshness_limit_seconds}" "${clock_continuity}" "${transaction_id}" "${freshness_result}" "${freshness_reason}" > "${freshness_tmp}"
  atomic_write "${evidence_dir}/trust-freshness.tsv" "${freshness_tmp}"
  [[ "${freshness_result}" == PASS ]]
}

core_lock_relative="${backend_relative}/src/AqualLifeStyle.Core/packages.lock.json"; ef_lock_relative="${backend_relative}/src/AqualLifeStyle.EntityFrameworkCore/packages.lock.json"
runner_lock_relative="${backend_relative}/tools/AqualLifeStyle.B6SnapshotRunner/packages.lock.json"; test_lock_relative="${backend_relative}/tools/AqualLifeStyle.B6SnapshotRunner.Tests/packages.lock.json"
verify_locks() {
  local source="$1" path expected
  while IFS=$'\t' read -r path expected; do [[ -f "${source}/${path}" && ! -L "${source}/${path}" && "$(sha256_file "${source}/${path}")" == "${expected}" ]] || fail "lock mismatch ${path}"; done <<LOCKS
${core_lock_relative}	${expected_core_lock_sha256}
${ef_lock_relative}	${expected_ef_lock_sha256}
${runner_lock_relative}	${expected_runner_lock_sha256}
${test_lock_relative}	${expected_test_lock_sha256}
LOCKS
}
verify_locks "${source_q}"; verify_locks "${source_a}"; verify_locks "${source_b}"

[[ -x "${bwrap}" && -f "${bwrap}" && ! -L "${bwrap}" ]] || fail 'Bubblewrap unavailable'
bootstrap_uid="$(id -u)"; bootstrap_gid="$(id -g)"
[[ "${bootstrap_uid}" == 0 && "${bootstrap_gid}" == 0 ]] || fail 'trusted bootstrap must have effective UID/GID 0 for exact namespace maps'
network_witness_script="${private_root}/network-witness.sh"
cat > "${network_witness_script}" <<'WITNESS'
#!/usr/bin/env bash
set -euo pipefail
readonly qualified_mawk='/usr/bin/mawk'
witness_fd="$1"; release_fd="$2"; shift 2
[[ "$(id -u)" == 65534 && "$(id -g)" == 65534 && "$(id -G)" == 65534 ]]
grep -Eq '^Uid:[[:space:]]+65534[[:space:]]+65534[[:space:]]+65534[[:space:]]+65534$' /proc/self/status
grep -Eq '^Gid:[[:space:]]+65534[[:space:]]+65534[[:space:]]+65534[[:space:]]+65534$' /proc/self/status
"${qualified_mawk}" 'NF {n++; line=$1 " " $2 " " $3} END {exit !(n==1 && line=="65534 0 1")}' /proc/self/uid_map
"${qualified_mawk}" 'NF {n++; line=$1 " " $2 " " $3} END {exit !(n==1 && line=="65534 0 1")}' /proc/self/gid_map
grep -Eq '^NoNewPrivs:[[:space:]]+1$' /proc/self/status
for capability in CapInh CapPrm CapEff CapBnd CapAmb; do grep -Eq "^${capability}:[[:space:]]+0000000000000000$" /proc/self/status; done
"${qualified_mawk}" -F: 'NR>2 {gsub(/ /,"",$1); if($1!="lo") exit 1; seen++} END {exit !(seen==1)}' /proc/net/dev
"${qualified_mawk}" 'NR>1 {exit 1}' /proc/net/route
"${qualified_mawk}" 'NF && $10!="lo" {exit 1}' /proc/net/ipv6_route
starttime="$("${qualified_mawk}" '{print $22}' /proc/self/stat)"
uid_map="$("${qualified_mawk}" 'NF {print $1 " " $2 " " $3}' /proc/self/uid_map)"
gid_map="$("${qualified_mawk}" 'NF {print $1 " " $2 " " $3}' /proc/self/gid_map)"
{
  printf 'pid\t%s\nppid\t%s\nstarttime\t%s\nuid\t%s\ngid\t%s\ngroups\t%s\nuid_map\t%s\ngid_map\t%s\nnetwork_namespace\t%s\n' "$$" "$PPID" "${starttime}" "$(id -u)" "$(id -g)" "$(id -G)" "${uid_map}" "${gid_map}" "$(readlink /proc/self/ns/net)"
  "${qualified_mawk}" -F: '/^(CapInh|CapPrm|CapEff|CapBnd|CapAmb|NoNewPrivs)$/ {gsub(/^[[:space:]]+|[[:space:]]+$/,"",$2); print $1 "\t" $2}' /proc/self/status
  printf 'END\n'
} >&"${witness_fd}"
eval "exec ${witness_fd}>&-"
IFS= read -r release <&"${release_fd}"
[[ "${release}" == RELEASE ]]
eval "exec ${release_fd}<&-"
exec "$@"
WITNESS
chmod 500 "${network_witness_script}"

json_number() {
  local key="$1" file="$2"
  ${qualified_mawk} -v key="${key}" '{pattern="\"" key "\"[[:space:]]*:[[:space:]]*[0-9]+"; if(match($0,pattern)){text=substr($0,RSTART,RLENGTH); sub(/^.*:/,"",text); gsub(/[[:space:]]/,"",text); value=text; found++}} END {if(found!=1) exit 1; print value}' "${file}"
}

run_network_denied() {
  local label="$1" workdir="$2" mode path launcher_pid launcher_start init_pid init_start init_ppid child_pid child_start child_ppid child_list parent_net child_net command_status status_exit witness_tmp witness_line
  shift 2; local -a mount_arguments=() command=(); local -A destination_dirs=([/tmp]=1 ["${private_root}"]=1)
  while [[ "$#" -gt 0 && "$1" != -- ]]; do
    mode="$1"; path="$2"; shift 2
    [[ "${mode}" == --ro || "${mode}" == --rw ]] || fail "unsupported confinement mount mode ${mode}"
    [[ "${path}" == /* && -e "${path}" && ! -L "${path}" ]] || fail "unsafe confinement mount ${path}"
    if [[ "${mode}" == --ro ]]; then mount_arguments+=(--ro-bind "${path}" "${path}"); else mount_arguments+=(--bind "${path}" "${path}"); fi
    parent="$(dirname -- "${path}")"
    while [[ "${parent}" != / && "${parent}" != /usr && "${parent}" != /etc && "${parent}" != /dev && "${parent}" != /proc ]]; do destination_dirs["${parent}"]=1; parent="$(dirname -- "${parent}")"; done
  done
  [[ "$#" -gt 0 && "$1" == -- ]] || fail "missing confinement command delimiter ${label}"; shift; command=("$@")
  local -a directory_arguments=(); while IFS= read -r path; do directory_arguments+=(--dir "${path}"); done < <(printf '%s\n' "${!destination_dirs[@]}" | ${qualified_mawk} '{print length($0) "\t" $0}' | sort -n | cut -f2-)
  network_reconciliation_result='IN_PROGRESS'
  witness_fifo="${private_root}/.${label}.witness.fifo"; setup_fifo="${private_root}/.${label}.setup.fifo"; release_fifo="${private_root}/.${label}.release.fifo"; status_file="${private_root}/.${label}.bwrap-status.json"; witness_tmp="${private_root}/.${label}.witness.tsv"
  mkfifo -m 600 "${witness_fifo}" "${setup_fifo}" "${release_fifo}"; : > "${status_file}"; : > "${witness_tmp}"
  exec {witness_program_fd}<"${network_witness_script}"
  "${bwrap}" --die-with-parent --new-session --unshare-user --unshare-pid --unshare-net --unshare-ipc --unshare-uts --unshare-cgroup-try --uid 65534 --gid 65534 --cap-drop ALL --json-status-fd 3 --block-fd 9 \
    --ro-bind /usr /usr --symlink usr/bin /bin --symlink usr/lib /lib --symlink usr/lib64 /lib64 --dir /etc --ro-bind /etc/ssl /etc/ssl --ro-bind /etc/ca-certificates.conf /etc/ca-certificates.conf \
    --dev /dev --proc /proc --dir /run --dir /run/aqua "${directory_arguments[@]}" --ro-bind "${frozen_dotnet_root}" "${frozen_dotnet_root}" "${mount_arguments[@]}" --ro-bind-data "${witness_program_fd}" /run/aqua/network-witness --chdir "${workdir}" /bin/bash /run/aqua/network-witness 8 7 "${command[@]}" \
    3>"${status_file}" 8>"${witness_fifo}" 9<"${setup_fifo}" 7<"${release_fifo}" &
  launcher_pid=$!; active_bwrap_pid="${launcher_pid}"; launcher_start="$(process_starttime "${launcher_pid}")"; active_bwrap_starttime="${launcher_start}"
  exec {witness_read}<"${witness_fifo}"; exec {setup_write}>"${setup_fifo}"; exec {release_write}>"${release_fifo}"
  init_pid=''; for _ in $(seq 1 500); do init_pid="$(json_number child-pid "${status_file}" 2>/dev/null || true)"; [[ -n "${init_pid}" ]] && break; kill -0 "${launcher_pid}" 2>/dev/null || break; sleep 0.01; done
  [[ -n "${init_pid}" && "$(process_starttime "${launcher_pid}" 2>/dev/null || true)" == "${launcher_start}" ]] || fail "Bubblewrap launcher identity mismatch ${label}"
  init_start="$(process_starttime "${init_pid}")"; init_ppid="$(${qualified_mawk} '{print $4}' "/proc/${init_pid}/stat")"
  [[ "${init_ppid}" == "${launcher_pid}" ]] || fail "Bubblewrap namespace-init relationship mismatch ${label}"
  parent_net="$(readlink /proc/self/ns/net)"; child_net="$(readlink "/proc/${init_pid}/ns/net")"; [[ "${child_net}" != "${parent_net}" ]] || fail "Bubblewrap network namespace not isolated ${label}"
  printf 'SETUP\n' >&"${setup_write}"; exec {setup_write}>&-
  while IFS= read -r witness_line <&"${witness_read}"; do [[ "${witness_line}" == END ]] && break; printf '%s\n' "${witness_line}" >> "${witness_tmp}"; done
  [[ "${witness_line:-}" == END ]] || fail "network witness incomplete ${label}"
  witness_value() { receipt_value "$1" "${witness_tmp}"; }
  [[ "$(witness_value pid)" == 2 && "$(witness_value ppid)" == 1 && "$(witness_value uid)" == 65534 && "$(witness_value gid)" == 65534 && "$(witness_value groups)" == 65534 ]] || fail "namespace-child identity mismatch ${label}"
  [[ "$(witness_value uid_map)" == '65534 0 1' && "$(witness_value gid_map)" == '65534 0 1' && "$(witness_value network_namespace)" == "${child_net}" ]] || fail "namespace-child map/network mismatch ${label}"
  for key in CapInh CapPrm CapEff CapBnd CapAmb; do [[ "$(witness_value "${key}")" == 0000000000000000 ]] || fail "namespace-child capability mismatch ${label}"; done
  [[ "$(witness_value NoNewPrivs)" == 1 ]] || fail "namespace-child NoNewPrivs mismatch ${label}"
  child_list="$(tr ' ' '\n' < "/proc/${init_pid}/task/${init_pid}/children" | ${qualified_mawk} 'NF {print; count++} END {if(count!=1) exit 1}')" || fail "namespace-child count mismatch ${label}"
  child_pid="${child_list}"; child_start="$(process_starttime "${child_pid}")"; child_ppid="$(${qualified_mawk} '{print $4}' "/proc/${child_pid}/stat")"
  [[ "${child_ppid}" == "${init_pid}" && "${child_start}" == "$(witness_value starttime)" && "$(readlink "/proc/${child_pid}/ns/net")" == "${child_net}" ]] || fail "namespace-child PID/starttime reconciliation mismatch ${label}"
  {
    printf 'label\t%s\nlauncher_pid\t%s\nlauncher_starttime\t%s\nnamespace_init_pid\t%s\nnamespace_init_starttime\t%s\nnamespace_child_host_pid\t%s\nnamespace_child_starttime\t%s\nparent_network_namespace\t%s\n' "${label}" "${launcher_pid}" "${launcher_start}" "${init_pid}" "${init_start}" "${child_pid}" "${child_start}" "${parent_net}"
    cat "${witness_tmp}"
  } > "${evidence_dir}/network-${label}.tsv"
  printf 'RELEASE\n' >&"${release_write}"; exec {release_write}>&-; exec {witness_read}<&-; exec {witness_program_fd}<&-
  set +e; wait "${launcher_pid}"; command_status=$?; set -e
  if [[ -r "/proc/${launcher_pid}/stat" && "$(process_starttime "${launcher_pid}" 2>/dev/null || true)" == "${launcher_start}" ]]; then
    fail "Bubblewrap wait interrupted before process exit ${label}"
  fi
  active_bwrap_pid=''; active_bwrap_starttime=''
  status_exit="$(json_number exit-code "${status_file}" 2>/dev/null || true)"
  [[ -n "${status_exit}" && "${status_exit}" == "${command_status}" ]] || fail "Bubblewrap exit reconciliation mismatch ${label}"
  [[ ! -e "/proc/${init_pid}" || "$(process_starttime "${init_pid}" 2>/dev/null || true)" != "${init_start}" ]] || fail "Bubblewrap namespace cleanup mismatch ${label}"
  rm -f -- "${witness_fifo}" "${setup_fifo}" "${release_fifo}" "${status_file}" "${witness_tmp}"
  network_reconciliation_result='PASS'
  return "${command_status}"
}

dotnet_environment() {
  local home="$1" tmp="$2" packages="$3" http_cache="$4"
  printf '%s\0' PATH="${frozen_dotnet_root}:/usr/bin:/bin" HOME="${home}" TMPDIR="${tmp}" DOTNET_ROOT="${frozen_dotnet_root}" DOTNET_ROOT_X64="${frozen_dotnet_root}" DOTNET_CLI_HOME="${home}" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1 NUGET_PACKAGES="${packages}" NUGET_HTTP_CACHE_PATH="${http_cache}" DOTNET_NUGET_SIGNATURE_VERIFICATION=true NUGET_CERT_REVOCATION_MODE=offline
}

phase='GATE_Q'; gateq_root="${private_root}/gate-q"; gateq_packages="${gateq_root}/packages"; gateq_home="${gateq_root}/home"; gateq_tmp="${gateq_root}/tmp"; gateq_http="${gateq_root}/http"
gateq_artifacts="${source_q}/${backend_relative}/artifacts/aqgreen-b6-gate-q"; gateq_results="${gateq_root}/results"; mkdir -p -m 700 "${gateq_packages}" "${gateq_home}" "${gateq_tmp}" "${gateq_http}" "${gateq_artifacts}" "${gateq_results}"
mapfile -d '' gateq_env < <(dotnet_environment "${gateq_home}" "${gateq_tmp}" "${gateq_packages}" "${gateq_http}"); test_project="${source_q}/${test_relative}"
phase='TRUST_FRESHNESS_GATE_Q_PRE'; record_trust_freshness GATE_Q_RESTORE_PRE || fail '60-minute governance freshness bound failed'
phase='GATE_Q'
run_network_denied gate-q-restore "${source_q}" --ro "${source_q}" --ro "${private_u_source}" --rw "${gateq_packages}" --rw "${gateq_home}" --rw "${gateq_tmp}" --rw "${gateq_http}" --rw "${gateq_artifacts}" --rw "${gateq_results}" -- env -i "${gateq_env[@]}" "${frozen_dotnet}" restore "${test_project}" --configfile "${nuget_config}" --packages "${gateq_packages}" --locked-mode --source "${private_u_source}" --disable-build-servers -p:ArtifactsPath="${gateq_artifacts}" -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false >"${private_root}/gate-q-restore.log" 2>&1 || fail 'Gate Q restore failed'
phase='TRUST_FRESHNESS_GATE_Q_POST'; record_trust_freshness GATE_Q_RESTORE_POST || fail '60-minute governance freshness bound failed'
phase='GATE_Q'
validate_package_root "${gateq_packages}" "${tq_manifest}" "${private_root}/gate-q-tq186.tsv" 'Gate Q TQ186 cache' "${private_u_source}"
run_network_denied gate-q-build "${source_q}" --ro "${source_q}" --ro "${private_u_source}" --rw "${gateq_packages}" --rw "${gateq_home}" --rw "${gateq_tmp}" --rw "${gateq_http}" --rw "${gateq_artifacts}" --rw "${gateq_results}" -- env -i "${gateq_env[@]}" "${frozen_dotnet}" build "${test_project}" --configuration Release --no-restore --no-incremental --disable-build-servers --artifacts-path "${gateq_artifacts}" -p:ContinuousIntegrationBuild=true -p:Deterministic=true -p:DeterministicSourcePaths=true -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false >"${private_root}/gate-q-build.log" 2>&1 || fail 'Gate Q build failed'
run_network_denied gate-q-test "${source_q}" --ro "${source_q}" --ro "${private_u_source}" --rw "${gateq_packages}" --rw "${gateq_home}" --rw "${gateq_tmp}" --rw "${gateq_http}" --rw "${gateq_artifacts}" --rw "${gateq_results}" -- env -i "${gateq_env[@]}" "${frozen_dotnet}" test "${test_project}" --configuration Release --no-build --no-restore --artifacts-path "${gateq_artifacts}" --filter 'FullyQualifiedName~B6PrimitiveContractTests' --results-directory "${gateq_results}" --logger 'trx;LogFileName=gate-q.trx' >"${private_root}/gate-q-test.log" 2>&1 || fail 'Gate Q tests failed'
mapfile -t trx_files < <(find -P "${gateq_results}" -type f -name '*.trx' -print); [[ "${#trx_files[@]}" == 1 ]] || fail 'Gate Q requires one TRX'
gateq_runner="${gateq_artifacts}/bin/AqualLifeStyle.B6SnapshotRunner.Tests/release/AqualLifeStyle.B6SnapshotRunner.dll"; [[ -f "${gateq_runner}" ]] || gateq_runner="${gateq_artifacts}/bin/AqualLifeStyle.B6SnapshotRunner/release/AqualLifeStyle.B6SnapshotRunner.dll"
[[ -f "${gateq_runner}" && ! -L "${gateq_runner}" ]] || fail 'Gate Q Runner missing'
run_network_denied gate-q-verify "${source_q}" --ro "${source_q}" --ro "${private_u_source}" --rw "${gateq_packages}" --rw "${gateq_home}" --rw "${gateq_tmp}" --rw "${gateq_http}" --rw "${gateq_artifacts}" --rw "${gateq_results}" -- env -i "${gateq_env[@]}" B6_EXECUTION_EVIDENCE_CLASSIFICATION=PRECOMMIT_SYNTHETIC_VALIDATION "${frozen_dotnet}" "${gateq_runner}" verify-test-run --trx "${trx_files[0]}" --expected-count 61 >"${private_root}/gate-q-verify.log" 2>&1 || fail 'Gate Q TRX verification failed'
grep -Fqx '{"discovered":61,"executed":61,"passed":61,"failed":0,"skipped":0}' "${private_root}/gate-q-verify.log" || fail 'Gate Q result mismatch'; verify_locks "${source_q}"; verify_materialized_reviewed_tree "${source_q}"
printf 'GATE_Q=PASS classification=SOURCE_QUALIFICATION_MECHANISM discovered=61 executed=61 passed=61 failed=0 skipped=0 mechanical-network-witness=PASS\n' >&2

write_assets_manifest() { local artifact_root="$1" output="$2"; (cd "${artifact_root}" && find -P . -type f -name project.assets.json -print0 | sort -z | xargs -0 -r sha256sum --) > "${output}"; [[ "$(wc -l < "${output}")" == 3 ]] || fail 'expected three assets files'; }
b1r_restore() {
  local source="$1" label="$2"
  local root="${private_root}/lane-${label}" packages="${private_root}/lane-${label}/packages" home="${private_root}/lane-${label}/home" tmp="${private_root}/lane-${label}/tmp" http="${private_root}/lane-${label}/http" artifacts="${source}/${backend_relative}/artifacts/aqgreen-b6-authoritative"
  mkdir -p -m 700 "${packages}" "${home}" "${tmp}" "${http}" "${artifacts}"; mapfile -d '' lane_env < <(dotnet_environment "${home}" "${tmp}" "${packages}" "${http}")
  phase="TRUST_FRESHNESS_B1_R_${label^^}_PRE"; record_trust_freshness "B1_R_${label^^}_RESTORE_PRE" || fail '60-minute governance freshness bound failed'; phase='GATE_A_B1_R'
  run_network_denied "b1-r-${label}" "${source}" --ro "${source}" --ro "${private_g_source}" --rw "${packages}" --rw "${home}" --rw "${tmp}" --rw "${http}" --rw "${artifacts}" -- env -i "${lane_env[@]}" "${frozen_dotnet}" restore "${source}/${runner_relative}" --configfile "${source}/${package_relative}/qualification-nuget.config" --packages "${packages}" --locked-mode --source "${private_g_source}" --disable-build-servers -p:ArtifactsPath="${artifacts}" -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false >"${private_root}/b1-r-${label}.log" 2>&1 || fail "B1-R ${label} failed"
  phase="TRUST_FRESHNESS_B1_R_${label^^}_POST"; record_trust_freshness "B1_R_${label^^}_RESTORE_POST" || fail '60-minute governance freshness bound failed'; phase='GATE_A_B1_R'
  verify_locks "${source}"; validate_package_root "${packages}" "${source}/${package_relative}/authority/G197.tsv" "${private_root}/packages-${label}.raw.tsv" "B1-R ${label} cache" "${private_g_source}"
  cmp -s "${private_root}/packages-${label}.raw.tsv" "${private_root}/promoted-g197.tsv" || fail "B1-R ${label} bytes mismatch"; write_assets_manifest "${artifacts}" "${private_root}/assets-${label}.before"
  printf 'B1-R_%s=PASS packages=197 graph-sha256=%s network-witness=PASS\n' "${label}" "${expected_g_sha256}" >&2
}
qualify_build_log() {
  local log="$1" label="$2"
  "${qualified_mawk}" '/: warning CS0628:/ {a++} /: warning CA2255:/ {b++} /: warning [A-Z][A-Z0-9]+:/ && $0 !~ /: warning CS0628:/ && $0 !~ /: warning CA2255:/ {u++} /: error [A-Z][A-Z0-9]+:/ {e++} /Determining projects to restore|Restored |All projects are up-to-date for restore/ {r++} END {exit !(a==2 && b==1 && u==0 && e==0 && r==0)}' "${log}" || fail "B2 ${label} warning closure mismatch"
}
b2_build() {
  local source="$1" label="$2"
  local packages="${private_root}/lane-${label}/packages" home="${private_root}/lane-${label}/home" tmp="${private_root}/lane-${label}/tmp" http="${private_root}/lane-${label}/http" artifacts="${source}/${backend_relative}/artifacts/aqgreen-b6-authoritative"
  mapfile -d '' lane_env < <(dotnet_environment "${home}" "${tmp}" "${packages}" "${http}")
  run_network_denied "b2-${label}" "${source}" --ro "${source}" --ro "${private_g_source}" --rw "${packages}" --rw "${home}" --rw "${tmp}" --rw "${http}" --rw "${artifacts}" -- env -i "${lane_env[@]}" "${frozen_dotnet}" build "${source}/${runner_relative}" --configuration Release --no-restore --no-incremental --disable-build-servers --artifacts-path "${artifacts}" -p:ContinuousIntegrationBuild=true -p:Deterministic=true -p:DeterministicSourcePaths=true -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false >"${private_root}/b2-${label}.log" 2>&1 || fail "B2 ${label} failed"
  qualify_build_log "${private_root}/b2-${label}.log" "${label}"; write_assets_manifest "${artifacts}" "${private_root}/assets-${label}.after"; cmp -s "${private_root}/assets-${label}.before" "${private_root}/assets-${label}.after" || fail "B2 ${label} changed assets"; verify_locks "${source}"; verify_materialized_reviewed_tree "${source}"
  printf 'B2_%s=PASS no-restore=true no-incremental=true warnings=3 unexpected=0 errors=0\n' "${label}" >&2
}

phase='GATE_A_B1_R'; b1r_restore "${source_a}" a; b1r_restore "${source_b}" b

phase='GATE_A_B2'; b2_build "${source_a}" a; b2_build "${source_b}" b
runner_dir_relative="${backend_relative}/artifacts/aqgreen-b6-authoritative/bin/AqualLifeStyle.B6SnapshotRunner/release"; runner_dir_a="${source_a}/${runner_dir_relative}"; runner_dir_b="${source_b}/${runner_dir_relative}"
write_runtime_manifest() { local root="$1" output="$2"; [[ -d "${root}" && ! -L "${root}" && -z "$(find -P "${root}" -mindepth 1 ! -type d ! -type f -print -quit)" ]] || fail 'unsafe runtime tree'; (cd "${root}" && find -P . -type f -print0 | sort -z | while IFS= read -r -d '' path; do printf '%s\t%s\n' "${path#./}" "$(sha256_file "${path}")"; done) > "${output}"; }
write_runtime_manifest "${runner_dir_a}" "${private_root}/runtime-a.tsv"; write_runtime_manifest "${runner_dir_b}" "${private_root}/runtime-b.tsv"; cmp -s "${private_root}/runtime-a.tsv" "${private_root}/runtime-b.tsv" || fail 'complete B2 trees differ'
runner_sha="$(sha256_file "${runner_dir_a}/AqualLifeStyle.B6SnapshotRunner.dll")"; core_sha="$(sha256_file "${runner_dir_a}/AqualLifeStyle.Core.dll")"; ef_sha="$(sha256_file "${runner_dir_a}/AqualLifeStyle.EntityFrameworkCore.dll")"
for assembly in AqualLifeStyle.B6SnapshotRunner.dll AqualLifeStyle.Core.dll AqualLifeStyle.EntityFrameworkCore.dll; do cmp -s "${runner_dir_a}/${assembly}" "${runner_dir_b}/${assembly}" || fail "artifact mismatch ${assembly}"; done
write_informational_versions() {
  local source="$1" output="$2" assembly path value
  : > "${output}"
  for assembly in AqualLifeStyle.B6SnapshotRunner AqualLifeStyle.Core AqualLifeStyle.EntityFrameworkCore; do
    path="${source}/${backend_relative}/artifacts/aqgreen-b6-authoritative/obj/${assembly}/release/${assembly}.AssemblyInfo.cs"
    [[ -f "${path}" && ! -L "${path}" ]] || fail "missing generated assembly metadata ${assembly}"
    value="$(${qualified_mawk} '/AssemblyInformationalVersionAttribute\("/ {value=$0; sub(/^.*AssemblyInformationalVersionAttribute\("/, "", value); sub(/"\)\].*$/, "", value); print value; count++} END {if(count!=1) exit 1}' "${path}")" || fail "invalid generated assembly metadata ${assembly}"
    [[ "${value}" == *+"${reviewed_commit}" ]] || fail "assembly metadata does not bind reviewed commit ${assembly}"
    printf '%s\t%s\n' "${assembly}" "${value}" >> "${output}"
  done
}
write_informational_versions "${source_a}" "${private_root}/assembly-informational-versions-a.tsv"
write_informational_versions "${source_b}" "${private_root}/assembly-informational-versions-b.tsv"
cmp -s "${private_root}/assembly-informational-versions-a.tsv" "${private_root}/assembly-informational-versions-b.tsv" || fail 'B2 assembly informational versions differ'
runner_info="$(${qualified_mawk} -F '\t' '$1=="AqualLifeStyle.B6SnapshotRunner" {print $2}' "${private_root}/assembly-informational-versions-a.tsv")"
core_info="$(${qualified_mawk} -F '\t' '$1=="AqualLifeStyle.Core" {print $2}' "${private_root}/assembly-informational-versions-a.tsv")"
ef_info="$(${qualified_mawk} -F '\t' '$1=="AqualLifeStyle.EntityFrameworkCore" {print $2}' "${private_root}/assembly-informational-versions-a.tsv")"
[[ -n "${runner_info}" && -n "${core_info}" && -n "${ef_info}" ]] || fail 'incomplete B2 assembly informational-version evidence'

phase='GATE_A_SIMULATE'; simulate_source="${source_a}/${package_relative}/fixture/b2-simulate-input.json"
[[ -f "${simulate_source}" && ! -L "${simulate_source}" && "$(sha256_file "${simulate_source}")" == "${expected_simulate_input_sha256}" ]] || fail 'simulate input mismatch'
simulate_input="${private_root}/b2-simulate-input.json"; cp "${simulate_source}" "${simulate_input}"; chmod 600 "${simulate_input}"; simulate_a_dir="${private_root}/simulate-a-output"; simulate_b_dir="${private_root}/simulate-b-output"; mkdir -m 700 "${simulate_a_dir}" "${simulate_b_dir}" "${private_root}/simulate-home-a" "${private_root}/simulate-home-b" "${private_root}/simulate-tmp-a" "${private_root}/simulate-tmp-b"; simulate_a="${simulate_a_dir}/simulate.json"; simulate_b="${simulate_b_dir}/simulate.json"
simulation_environment=(PATH="${frozen_dotnet_root}:/usr/bin:/bin" DOTNET_ROOT="${frozen_dotnet_root}" DOTNET_ROOT_X64="${frozen_dotnet_root}" DOTNET_NOLOGO=1 B6_EXECUTION_EVIDENCE_CLASSIFICATION=OPERATIONAL_EVIDENCE B6_PREAUTHORITY_PROVENANCE="${provenance_contract}" B6_PREAUTHORITY_REPOSITORY_COMMIT="${reviewed_commit}" B6_PREAUTHORITY_RUNNER_SHA256="${runner_sha}" B6_PREAUTHORITY_CORE_SHA256="${core_sha}" B6_PREAUTHORITY_EF_SHA256="${ef_sha}")
run_network_denied gate-a-simulate-a "${source_a}" --ro "${source_a}" --ro "${simulate_input}" --rw "${simulate_a_dir}" --rw "${private_root}/simulate-home-a" --rw "${private_root}/simulate-tmp-a" -- env -i "${simulation_environment[@]}" HOME="${private_root}/simulate-home-a" TMPDIR="${private_root}/simulate-tmp-a" B6_VERIFIED_ARTIFACT_ROOT="${runner_dir_a}" "${frozen_dotnet}" "${runner_dir_a}/AqualLifeStyle.B6SnapshotRunner.dll" simulate --input "${simulate_input}" --output "${simulate_a}" >"${private_root}/simulate-a.log" 2>&1 || fail 'simulate A failed'
run_network_denied gate-a-simulate-b "${source_b}" --ro "${source_b}" --ro "${simulate_input}" --rw "${simulate_b_dir}" --rw "${private_root}/simulate-home-b" --rw "${private_root}/simulate-tmp-b" -- env -i "${simulation_environment[@]}" HOME="${private_root}/simulate-home-b" TMPDIR="${private_root}/simulate-tmp-b" B6_VERIFIED_ARTIFACT_ROOT="${runner_dir_b}" "${frozen_dotnet}" "${runner_dir_b}/AqualLifeStyle.B6SnapshotRunner.dll" simulate --input "${simulate_input}" --output "${simulate_b}" >"${private_root}/simulate-b.log" 2>&1 || fail 'simulate B failed'
cmp -s "${simulate_a}" "${simulate_b}" || fail 'simulation outputs differ'; simulation_sha256="$(sha256_file "${simulate_a}")"
gate_a_verifier_arguments=(verify-simulation-output --expected-repository-commit "${reviewed_commit}" --expected-runner-sha256 "${runner_sha}" --expected-runner-informational-version "${runner_info}" --expected-core-sha256 "${core_sha}" --expected-core-informational-version "${core_info}" --expected-ef-sha256 "${ef_sha}" --expected-ef-informational-version "${ef_info}")
run_network_denied gate-a-verify-a "${source_a}" --ro "${source_a}" --ro "${simulate_a}" --rw "${private_root}/simulate-home-a" --rw "${private_root}/simulate-tmp-a" -- env -i "${simulation_environment[@]}" HOME="${private_root}/simulate-home-a" TMPDIR="${private_root}/simulate-tmp-a" B6_VERIFIED_ARTIFACT_ROOT="${runner_dir_a}" "${frozen_dotnet}" "${runner_dir_a}/AqualLifeStyle.B6SnapshotRunner.dll" "${gate_a_verifier_arguments[@]}" --input "${simulate_a}" >"${private_root}/gate-a-verify-a.log" 2>&1 || fail 'Gate A output verification A failed'
run_network_denied gate-a-verify-b "${source_b}" --ro "${source_b}" --ro "${simulate_b}" --rw "${private_root}/simulate-home-b" --rw "${private_root}/simulate-tmp-b" -- env -i "${simulation_environment[@]}" HOME="${private_root}/simulate-home-b" TMPDIR="${private_root}/simulate-tmp-b" B6_VERIFIED_ARTIFACT_ROOT="${runner_dir_b}" "${frozen_dotnet}" "${runner_dir_b}/AqualLifeStyle.B6SnapshotRunner.dll" "${gate_a_verifier_arguments[@]}" --input "${simulate_b}" >"${private_root}/gate-a-verify-b.log" 2>&1 || fail 'Gate A output verification B failed'
grep -Fq '"result":"PASS"' "${private_root}/gate-a-verify-a.log" || fail 'Gate A verifier A result mismatch'
grep -Fq '"result":"PASS"' "${private_root}/gate-a-verify-b.log" || fail 'Gate A verifier B result mismatch'
grep -Fq '"semanticProjectionSha256":"'"${expected_gate_a_semantic_projection_sha256}"'"' "${private_root}/gate-a-verify-a.log" || fail 'Gate A semantic projection A mismatch'
grep -Fq '"semanticProjectionSha256":"'"${expected_gate_a_semantic_projection_sha256}"'"' "${private_root}/gate-a-verify-b.log" || fail 'Gate A semantic projection B mismatch'
printf 'GATE_A=PASS claim=BOUNDED_REPRODUCIBILITY_AND_BEHAVIOR runtime-tree-equal=true simulate-output-equal=true semantic-projection-sha256=%s current-output-sha256=%s\n' "${expected_gate_a_semantic_projection_sha256}" "${simulation_sha256}" >&2

phase='EVIDENCE_PUBLICATION'
for retained in gate-q-restore.log gate-q-build.log gate-q-test.log gate-q-verify.log b1-r-a.log b1-r-b.log b2-a.log b2-b.log simulate-a.log simulate-b.log gate-a-verify-a.log gate-a-verify-b.log runtime-a.tsv runtime-b.tsv assembly-informational-versions-a.tsv assembly-informational-versions-b.tsv assets-a.before assets-a.after assets-b.before assets-b.after; do cp "${private_root}/${retained}" "${evidence_dir}/${retained}"; done
cp "${trx_files[0]}" "${evidence_dir}/gate-q.trx"; cp "${simulate_input}" "${evidence_dir}/b2-simulate-input.json"; cp "${simulate_a}" "${evidence_dir}/simulate-a.json"; cp "${simulate_b}" "${evidence_dir}/simulate-b.json"
cp "${g_manifest}" "${evidence_dir}/G197.tsv"; cp "${tq_manifest}" "${evidence_dir}/TQ186.tsv"; cp "${u_manifest}" "${evidence_dir}/U215.tsv"; cp "${u1_manifest}" "${evidence_dir}/U1.tsv"; cp "${u2_manifest}" "${evidence_dir}/U2.tsv"
printf 'repository_commit\t%s\ntransaction_id\t%s\nrunner_sha256\t%s\ncore_sha256\t%s\nef_sha256\t%s\ng197_sha256\t%s\ntq186_sha256\t%s\nu215_sha256\t%s\nu1_sha256\t%s\nu2_sha256\t%s\ncore_lock_sha256\t%s\nef_lock_sha256\t%s\nrunner_lock_sha256\t%s\ntest_lock_sha256\t%s\nsigner_policy_sha256\t%s\nsimulation_input_sha256\t%s\ngate_a_semantic_projection_schema\t%s\ngate_a_semantic_projection_sha256\t%s\ncurrent_simulation_output_sha256\t%s\n' "${reviewed_commit}" "${transaction_id}" "${runner_sha}" "${core_sha}" "${ef_sha}" "${expected_g_sha256}" "${expected_tq_sha256}" "${expected_u_sha256}" "${expected_u1_sha256}" "${expected_u2_sha256}" "${expected_core_lock_sha256}" "${expected_ef_lock_sha256}" "${expected_runner_lock_sha256}" "${expected_test_lock_sha256}" "${signer_policy_sha256}" "${expected_simulate_input_sha256}" "${gate_a_semantic_projection_schema}" "${expected_gate_a_semantic_projection_sha256}" "${simulation_sha256}" > "${evidence_dir}/qualification-identities.tsv"
printf 'qualification_result\tPASS\nGate_Q\tPASS_SOURCE_QUALIFICATION_MECHANISM\nGate_A\tPASS_BOUNDED_REPRODUCIBILITY_AND_BEHAVIOR\nmechanical_network_witness\tPASS\n' > "${evidence_dir}/qualification-verdict.tsv"
write_checksum_manifest qualification-SHA256SUMS || fail 'qualification evidence verification failed'
qualification_result='PASS'

phase='OPERATION_RELEASE'; chmod -R a-w "${source_a}"; verified_runner="${runner_dir_a}/AqualLifeStyle.B6SnapshotRunner.dll"
[[ "$(sha256_file "${verified_runner}")" == "${runner_sha}" && "$(sha256_file "${runner_dir_a}/AqualLifeStyle.Core.dll")" == "${core_sha}" && "$(sha256_file "${runner_dir_a}/AqualLifeStyle.EntityFrameworkCore.dll")" == "${ef_sha}" ]] || fail 'selected artifacts changed'
common_environment=(PATH="${frozen_dotnet_root}:/usr/bin:/bin" HOME="${HOME}" TMPDIR="${TMPDIR:-/tmp}" DOTNET_ROOT="${frozen_dotnet_root}" DOTNET_ROOT_X64="${frozen_dotnet_root}" B6_EXECUTION_EVIDENCE_CLASSIFICATION=OPERATIONAL_EVIDENCE B6_PREAUTHORITY_PROVENANCE="${provenance_contract}" B6_PREAUTHORITY_REPOSITORY_COMMIT="${reviewed_commit}" B6_PREAUTHORITY_RUNNER_SHA256="${runner_sha}" B6_PREAUTHORITY_CORE_SHA256="${core_sha}" B6_PREAUTHORITY_EF_SHA256="${ef_sha}" B6_VERIFIED_ARTIFACT_ROOT="${runner_dir_a}")
[[ -n "${docker_host}" ]] && common_environment+=(DOCKER_HOST="${docker_host}"); [[ -n "${docker_context}" ]] && common_environment+=(DOCKER_CONTEXT="${docker_context}"); [[ -n "${xdg_runtime_dir}" ]] && common_environment+=(XDG_RUNTIME_DIR="${xdg_runtime_dir}")
set +e
operation_result='IN_PROGRESS'
case "${operation}" in
  operator-extract) env -i "${common_environment[@]}" B6_SOURCE_DSN="${source_dsn}" B6_SOURCE_SYSTEM_ID="${source_system_id}" B6_REPOSITORY_COMMIT="${reviewed_commit}" B6_RAW_ZONE2_DIR="${raw_zone2_dir}" B6_PSEUDONYM_KEY_FILE="${pseudonym_key_file}" B6_ZONE3_DIR="${zone3_dir}" B6_SNAPSHOT_ID="${snapshot_id}" B6_TENANT_ID="${tenant_id}" B6_ANALYSIS_SNAPSHOT_CUTOFF="${analysis_cutoff}" bash "${source_a}/${package_relative}/operator-extract.sh" ;;
  run-analysis) env -i "${common_environment[@]}" B6_SNAPSHOT_DIR="${snapshot_dir}" B6_BASE_MANIFEST="${base_manifest}" B6_ROOT_DISPOSITIONS="${root_dispositions}" B6_ANALYSIS_OUTPUT_DIR="${analysis_output_dir}" bash "${source_a}/${package_relative}/run-analysis.sh" ;;
  runner) env -i "${common_environment[@]}" "${frozen_dotnet}" "${verified_runner}" "$@" ;;
esac
operation_exit_code=$?
set -e
if (( operation_exit_code == 0 )); then operation_result='PASS'; overall_result='PASS'; phase='COMPLETE'; else operation_result='FAIL'; overall_result='FAIL'; phase='OPERATION_FAILED'; [[ "${first_gate_failure}" != NONE ]] || first_gate_failure='OPERATION_RELEASE'; fi
intended_exit_code="${operation_exit_code}"
exit "${intended_exit_code}"
