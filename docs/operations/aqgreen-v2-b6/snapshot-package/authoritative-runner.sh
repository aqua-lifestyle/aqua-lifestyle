#!/usr/bin/env bash
set -euo pipefail
umask 0077

# This bootstrap is operationally authoritative only when Bash receives these
# exact bytes from `git show <reviewed-commit>:<this-path>`. The mutable live copy
# is not an operational entry point.
if [[ "$#" -lt 5 || "${1:-}" != "--repository" || "${3:-}" != "--reviewed-commit" ]]; then
  printf 'Usage: GIT_NO_REPLACE_OBJECTS=1 git show <reviewed-commit>:%s | bash -s -- --repository <root> --reviewed-commit <sha> <operator-extract|run-analysis|runner> [runner arguments]\n' \
    'docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh' >&2
  exit 2
fi

repository="$2"
reviewed_commit="$4"
operation="$5"
shift 5

if [[ ! "${reviewed_commit}" =~ ^[0-9a-f]{40}$ ]]; then
  printf 'The reviewed commit must be a full lowercase Git SHA.\n' >&2
  exit 2
fi
repository="$(realpath -- "${repository}")"
if [[ ! -d "${repository}" || -L "${repository}" ]]; then
  printf 'The repository root must be a real directory.\n' >&2
  exit 2
fi
resolved_root="$(git -C "${repository}" rev-parse --show-toplevel)"
resolved_commit="$(GIT_NO_REPLACE_OBJECTS=1 git -C "${repository}" rev-parse "${reviewed_commit}^{commit}")"
if [[ "${resolved_root}" != "${repository}" || "${resolved_commit}" != "${reviewed_commit}" ]]; then
  printf 'The repository or reviewed Git-object authority is not exact.\n' >&2
  exit 2
fi
case "${operation}" in
  operator-extract|run-analysis|runner) ;;
  *) printf 'Unsupported authoritative B6 operation: %s\n' "${operation}" >&2; exit 2 ;;
esac
if [[ "${operation}" != "runner" && "$#" -ne 0 ]]; then
  printf '%s accepts no additional arguments.\n' "${operation}" >&2
  exit 2
fi
if [[ "${operation}" == "runner" && "$#" -eq 0 ]]; then
  printf 'The runner operation requires a B6 Runner command.\n' >&2
  exit 2
fi

# Capture only the operation-specific values the reviewed adapter may receive.
# Remove every B6 operational capability before Git materialisation, restore, or
# build subprocesses start.
source_dsn="${B6_SOURCE_DSN-}"
source_system_id="${B6_SOURCE_SYSTEM_ID-}"
raw_zone2_dir="${B6_RAW_ZONE2_DIR-}"
pseudonym_key_file="${B6_PSEUDONYM_KEY_FILE-}"
zone3_dir="${B6_ZONE3_DIR-}"
snapshot_id="${B6_SNAPSHOT_ID-}"
tenant_id="${B6_TENANT_ID-}"
analysis_cutoff="${B6_ANALYSIS_SNAPSHOT_CUTOFF-}"
snapshot_dir="${B6_SNAPSHOT_DIR-}"
base_manifest="${B6_BASE_MANIFEST-}"
root_dispositions="${B6_ROOT_DISPOSITIONS-}"
analysis_output_dir="${B6_ANALYSIS_OUTPUT_DIR-}"
test_sensitive_sentinel="${B6_TEST_SENSITIVE_DSN_SENTINEL-}"
test_unverified_marker="${B6_TEST_UNVERIFIED_MARKER-}"
test_verified_marker="${B6_TEST_VERIFIED_MARKER-}"
docker_host="${DOCKER_HOST-}"
docker_context="${DOCKER_CONTEXT-}"
xdg_runtime_dir="${XDG_RUNTIME_DIR-}"
unset B6_SOURCE_DSN B6_SOURCE_SYSTEM_ID B6_REPOSITORY_COMMIT B6_RAW_ZONE2_DIR \
  B6_PSEUDONYM_KEY_FILE B6_ZONE3_DIR B6_SNAPSHOT_ID B6_TENANT_ID \
  B6_ANALYSIS_SNAPSHOT_CUTOFF B6_SNAPSHOT_DIR B6_BASE_MANIFEST \
  B6_ROOT_DISPOSITIONS B6_ANALYSIS_OUTPUT_DIR B6_EXECUTION_EVIDENCE_CLASSIFICATION \
  B6_PREAUTHORITY_PROVENANCE B6_PREAUTHORITY_REPOSITORY_COMMIT \
  B6_PREAUTHORITY_RUNNER_SHA256 B6_PREAUTHORITY_CORE_SHA256 \
  B6_PREAUTHORITY_EF_SHA256 B6_VERIFIED_ARTIFACT_ROOT \
  B6_TEST_SENSITIVE_DSN_SENTINEL B6_TEST_UNVERIFIED_MARKER B6_TEST_VERIFIED_MARKER

private_root="$(mktemp -d -t aqua-b6-preauthority.XXXXXXXX)"
source_a="${private_root}/source-a"
source_b="${private_root}/source-b"
cleanup() {
  local result=$?
  trap - EXIT INT TERM
  chmod -R u+w -- "${private_root}" 2>/dev/null || true
  if [[ -d "${private_root}" && ! -L "${private_root}" ]]; then
    rm -rf -- "${private_root}"
  fi
  exit "${result}"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
chmod 700 -- "${private_root}"
shared_packages="${private_root}/packages"
git_home="${private_root}/git-home"
git_tmp="${private_root}/git-tmp"
git_template="${private_root}/empty-git-template"
mkdir -m 700 -- "${shared_packages}" "${git_home}" "${git_tmp}" "${git_template}"

private_git() {
  env -i PATH="${PATH}" HOME="${git_home}" TMPDIR="${git_tmp}" \
    GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null GIT_ATTR_NOSYSTEM=1 \
    GIT_NO_REPLACE_OBJECTS=1 git "$@"
}

# Each source is an independent clone, not a linked worktree. The source
# repository's mutable hooks, info/attributes, filter configuration, replace
# objects, and working-tree files therefore cannot enter either checkout.
materialize_reviewed_tree() {
  local source="$1"
  private_git clone --no-checkout --no-local --template="${git_template}" \
      --config core.hooksPath=/dev/null "${repository}" "${source}" >&2
  private_git -C "${source}" -c core.hooksPath=/dev/null -c core.fsmonitor=false \
      checkout --detach --force "${reviewed_commit}" >&2
  chmod 700 -- "${source}"
}

materialize_reviewed_tree "${source_a}"
materialize_reviewed_tree "${source_b}"

package_relative='docs/operations/aqgreen-v2-b6/snapshot-package'
backend_relative='AqualLifeStyle/9.4.2/aspnet-core'
runner_relative="${backend_relative}/tools/AqualLifeStyle.B6SnapshotRunner/AqualLifeStyle.B6SnapshotRunner.csproj"
for source in "${source_a}" "${source_b}"; do
  materialized_commit="$(private_git -C "${source}" rev-parse HEAD)"
  materialized_status="$(private_git -C "${source}" status --porcelain=v1 --ignored --untracked-files=all)"
  if [[ "${materialized_commit}" != "${reviewed_commit}" || -n "${materialized_status}" ]]; then
    printf 'Private reviewed Git-object materialisation is not exact and clean.\n' >&2
    exit 2
  fi
  tree_manifest="${private_root}/$(basename "${source}").ls-tree"
  private_git -C "${source}" ls-tree -rz --full-tree "${reviewed_commit}" > "${tree_manifest}"
  while IFS= read -r -d '' entry; do
    metadata="${entry%%$'\t'*}"
    relative_path="${entry#*$'\t'}"
    read -r file_mode object_type object_id <<< "${metadata}"
    if [[ "${object_type}" != "blob" ||
          ( "${file_mode}" != "100644" && "${file_mode}" != "100755" ) ||
          ! -f "${source}/${relative_path}" || -L "${source}/${relative_path}" ]]; then
      printf 'Reviewed source contains an unsupported or non-regular entry: %s\n' "${relative_path}" >&2
      exit 2
    fi
    actual_object_id="$(git hash-object --no-filters -- "${source}/${relative_path}")"
    if [[ "${actual_object_id}" != "${object_id}" ]]; then
      printf 'Materialized source bytes differ from reviewed Git object: %s\n' "${relative_path}" >&2
      exit 2
    fi
  done < "${tree_manifest}"
  for input in \
    "${source}/${package_relative}/authoritative-runner.sh" \
    "${source}/${package_relative}/operator-extract.sh" \
    "${source}/${package_relative}/run-analysis.sh" \
    "${source}/${runner_relative}" \
    "${source}/${backend_relative}/NuGet.Config"; do
    if [[ ! -f "${input}" || -L "${input}" ]]; then
      printf 'Reviewed B6 build/operation input must be a regular file: %s\n' "${input}" >&2
      exit 2
    fi
  done
done

build_reviewed_tree() {
  local source="$1"
  local label="$2"
  local backend_root="${source}/${backend_relative}"
  local runner_project="${source}/${runner_relative}"
  local nuget_config="${backend_root}/NuGet.Config"
  local artifact_root="${backend_root}/artifacts/aqgreen-b6-authoritative"
  local tool_home="${private_root}/tool-home-${label}"
  local tool_tmp="${private_root}/tool-tmp-${label}"
  mkdir -m 700 -- "${tool_home}" "${tool_tmp}"
  if [[ -L "${backend_root}/artifacts" || -L "${artifact_root}" ]] ||
    [[ -d "${artifact_root}" && -n "$(find "${artifact_root}" -type l -print -quit)" ]]; then
    printf 'Reviewed B6 artifact paths must not contain symbolic links.\n' >&2
    exit 2
  fi

  env -i PATH="${PATH}" HOME="${tool_home}" TMPDIR="${tool_tmp}" \
    DOTNET_CLI_HOME="${tool_home}" DOTNET_NOLOGO=1 \
    dotnet restore "${runner_project}" \
      --configfile "${nuget_config}" \
      --packages "${shared_packages}" \
      --force-evaluate \
      --ignore-failed-sources \
      --disable-build-servers \
      -p:ArtifactsPath="${artifact_root}" \
      -p:ImportDirectoryBuildProps=false \
      -p:ImportDirectoryBuildTargets=false >&2

  env -i PATH="${PATH}" HOME="${tool_home}" TMPDIR="${tool_tmp}" \
    DOTNET_CLI_HOME="${tool_home}" DOTNET_NOLOGO=1 \
    dotnet build "${runner_project}" \
      --configuration Release \
      --no-restore \
      --no-incremental \
      --disable-build-servers \
      --artifacts-path "${artifact_root}" \
      -p:ContinuousIntegrationBuild=true \
      -p:Deterministic=true \
      -p:DeterministicSourcePaths=true \
      -p:ImportDirectoryBuildProps=false \
      -p:ImportDirectoryBuildTargets=false >&2

  if [[ -n "$(find "${artifact_root}" -type l -print -quit)" ]]; then
    printf 'Reviewed B6 build produced symbolic-link artifact state.\n' >&2
    exit 2
  fi
}

build_reviewed_tree "${source_a}" a
build_reviewed_tree "${source_b}" b
for source in "${source_a}" "${source_b}"; do
  if [[ "$(private_git -C "${source}" rev-parse HEAD)" != "${reviewed_commit}" ||
        -n "$(private_git -C "${source}" status --porcelain=v1 --untracked-files=all)" ]]; then
    printf 'Canonical build changed the reviewed Git-object source or operation adapters.\n' >&2
    exit 2
  fi
done

runner_dir_relative="${backend_relative}/artifacts/aqgreen-b6-authoritative/bin/AqualLifeStyle.B6SnapshotRunner/release"
runner_dir_a="${source_a}/${runner_dir_relative}"
runner_dir_b="${source_b}/${runner_dir_relative}"
runner_sha=''
core_sha=''
ef_sha=''
for assembly in \
  AqualLifeStyle.B6SnapshotRunner.dll \
  AqualLifeStyle.Core.dll \
  AqualLifeStyle.EntityFrameworkCore.dll; do
  path_a="${runner_dir_a}/${assembly}"
  path_b="${runner_dir_b}/${assembly}"
  if [[ ! -f "${path_a}" || -L "${path_a}" || ! -f "${path_b}" || -L "${path_b}" ]]; then
    printf 'Canonical reviewed builds did not produce regular first-party assembly %s.\n' "${assembly}" >&2
    exit 2
  fi
  hash_a="$(sha256sum -- "${path_a}" | cut -d' ' -f1)"
  hash_b="$(sha256sum -- "${path_b}" | cut -d' ' -f1)"
  if [[ "${hash_a}" != "${hash_b}" ]]; then
    printf 'Canonical reviewed builds disagree for %s.\n' "${assembly}" >&2
    exit 2
  fi
  case "${assembly}" in
    AqualLifeStyle.B6SnapshotRunner.dll) runner_sha="${hash_a}" ;;
    AqualLifeStyle.Core.dll) core_sha="${hash_a}" ;;
    AqualLifeStyle.EntityFrameworkCore.dll) ef_sha="${hash_a}" ;;
  esac
done

# The selected source tree and artifact root are private and made read-only after the
# exact-byte comparison. Reviewed adapters recheck all three hashes immediately
# before every privileged Runner launch.
chmod -R a-w -- "${source_a}"
verified_runner="${runner_dir_a}/AqualLifeStyle.B6SnapshotRunner.dll"
verify_selected_artifact_set() {
  [[ -d "${runner_dir_a}" && ! -L "${runner_dir_a}" ]]
  [[ -f "${verified_runner}" && ! -L "${verified_runner}" ]]
  [[ "$(sha256sum -- "${verified_runner}" | cut -d' ' -f1)" == "${runner_sha}" ]]
  [[ "$(sha256sum -- "${runner_dir_a}/AqualLifeStyle.Core.dll" | cut -d' ' -f1)" == "${core_sha}" ]]
  [[ "$(sha256sum -- "${runner_dir_a}/AqualLifeStyle.EntityFrameworkCore.dll" | cut -d' ' -f1)" == "${ef_sha}" ]]
}
verify_selected_artifact_set

printf 'B6 pre-authority provenance: PASS\nReviewed commit: %s\nRunner SHA-256: %s\nCore SHA-256: %s\nEntityFrameworkCore SHA-256: %s\n' \
  "${reviewed_commit}" "${runner_sha}" "${core_sha}" "${ef_sha}" >&2

common_environment=(
  PATH="${PATH}"
  HOME="${HOME}"
  TMPDIR="${TMPDIR:-/tmp}"
  B6_EXECUTION_EVIDENCE_CLASSIFICATION=OPERATIONAL_EVIDENCE
  B6_PREAUTHORITY_PROVENANCE=GIT_OBJECT_DOUBLE_BUILD_EXACT_BYTES_V1
  B6_PREAUTHORITY_REPOSITORY_COMMIT="${reviewed_commit}"
  B6_PREAUTHORITY_RUNNER_SHA256="${runner_sha}"
  B6_PREAUTHORITY_CORE_SHA256="${core_sha}"
  B6_PREAUTHORITY_EF_SHA256="${ef_sha}"
  B6_VERIFIED_ARTIFACT_ROOT="${runner_dir_a}"
)
[[ -n "${docker_host}" ]] && common_environment+=(DOCKER_HOST="${docker_host}")
[[ -n "${docker_context}" ]] && common_environment+=(DOCKER_CONTEXT="${docker_context}")
[[ -n "${xdg_runtime_dir}" ]] && common_environment+=(XDG_RUNTIME_DIR="${xdg_runtime_dir}")
[[ -n "${test_sensitive_sentinel}" ]] && common_environment+=(B6_TEST_SENSITIVE_DSN_SENTINEL="${test_sensitive_sentinel}")
[[ -n "${test_unverified_marker}" ]] && common_environment+=(B6_TEST_UNVERIFIED_MARKER="${test_unverified_marker}")
[[ -n "${test_verified_marker}" ]] && common_environment+=(B6_TEST_VERIFIED_MARKER="${test_verified_marker}")

case "${operation}" in
  operator-extract)
    env -i "${common_environment[@]}" \
      B6_SOURCE_DSN="${source_dsn}" \
      B6_SOURCE_SYSTEM_ID="${source_system_id}" \
      B6_REPOSITORY_COMMIT="${reviewed_commit}" \
      B6_RAW_ZONE2_DIR="${raw_zone2_dir}" \
      B6_PSEUDONYM_KEY_FILE="${pseudonym_key_file}" \
      B6_ZONE3_DIR="${zone3_dir}" \
      B6_SNAPSHOT_ID="${snapshot_id}" \
      B6_TENANT_ID="${tenant_id}" \
      B6_ANALYSIS_SNAPSHOT_CUTOFF="${analysis_cutoff}" \
      bash "${source_a}/${package_relative}/operator-extract.sh"
    ;;
  run-analysis)
    env -i "${common_environment[@]}" \
      B6_SNAPSHOT_DIR="${snapshot_dir}" \
      B6_BASE_MANIFEST="${base_manifest}" \
      B6_ROOT_DISPOSITIONS="${root_dispositions}" \
      B6_ANALYSIS_OUTPUT_DIR="${analysis_output_dir}" \
      bash "${source_a}/${package_relative}/run-analysis.sh"
    ;;
  runner)
    verify_selected_artifact_set
    env -i "${common_environment[@]}" dotnet "${verified_runner}" "$@"
    ;;
esac
