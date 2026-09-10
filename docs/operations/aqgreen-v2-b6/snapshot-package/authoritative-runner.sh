#!/usr/bin/env bash
set -euo pipefail
umask 0077
export LC_ALL=C

readonly expected_selected_package_count='197'
readonly expected_canonical_graph_sha256='a32d7081f7c687c9ece1511122dd772ac17e7cfc682943e5706ac0caef6fc4a0'
readonly expected_core_lock_sha256='9f467700282dc48f8bdc172eb8f1830a3167cbc4efaa223a328b1ff00c4acddc'
readonly expected_ef_lock_sha256='2b1925d618a39d10fa57e3e8455f06c3565de75ccdc3ae4a9021e357c9d07356'
readonly expected_runner_lock_sha256='401fafbcfa35347666a4de3fcfa92be9c66399663d8f8410eca420879c3d7b7b'

# This bootstrap is operationally authoritative only when Bash receives these
# exact bytes from `git show <reviewed-commit>:<this-path>`. The mutable live copy
# is not an operational entry point.
if [[ "$#" -lt 5 || "${1:-}" != "--repository" || "${3:-}" != "--reviewed-commit" ]]; then
  printf 'Usage: GIT_NO_REPLACE_OBJECTS=1 git show <reviewed-commit>:%s | bash -s -- --repository <root> --reviewed-commit <sha> --seed-nuget-packages-from <absolute-canonical-G-directory> <operator-extract|run-analysis|runner> [runner arguments]\n' \
    'docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh' >&2
  exit 2
fi

repository="$2"
reviewed_commit="$4"
shift 4
seed_nuget_packages_from=''
seed_option_count=0
for argument in "$@"; do
  [[ "${argument}" == "--seed-nuget-packages-from" ]] && seed_option_count=$((seed_option_count + 1))
done
if (( seed_option_count > 1 )); then
  printf '%s may be supplied at most once.\n' '--seed-nuget-packages-from' >&2
  exit 2
fi
if [[ "${1:-}" == "--seed-nuget-packages-from" ]]; then
  if [[ "$#" -lt 3 ]]; then
    printf '%s requires a directory value and an operation.\n' '--seed-nuget-packages-from' >&2
    exit 2
  fi
  seed_nuget_packages_from="$2"
  shift 2
elif (( seed_option_count == 1 )); then
  printf '%s must immediately follow the reviewed commit.\n' '--seed-nuget-packages-from' >&2
  exit 2
fi
if [[ -z "${seed_nuget_packages_from}" ]]; then
  printf '%s is required with A-L content-bound G output for B1-R selected-only locked reproduction.\n' '--seed-nuget-packages-from' >&2
  exit 2
fi
operation="$1"
shift

if [[ ! "${reviewed_commit}" =~ ^[0-9a-f]{40}$ ]]; then
  printf 'The reviewed commit must be a full lowercase Git SHA.\n' >&2
  exit 2
fi
repository="$(realpath -- "${repository}")"
if [[ ! -d "${repository}" || -L "${repository}" ]]; then
  printf 'The repository root must be a real directory.\n' >&2
  exit 2
fi
paths_overlap() {
  [[ "$1" == "$2" || "$1" == "$2"/* || "$2" == "$1"/* ]]
}
validate_nuget_package_seed() {
  local source="$1"
  local unsafe_entry
  unsafe_entry="$(find -P "${source}" -mindepth 1 -type l -print -quit)"
  if [[ -n "${unsafe_entry}" ]]; then
    printf 'The NuGet global-packages seed must not contain symbolic links: %s\n' "${unsafe_entry}" >&2
    return 2
  fi
  unsafe_entry="$(find -P "${source}" -mindepth 1 ! -type d ! -type f -print -quit)"
  if [[ -n "${unsafe_entry}" ]]; then
    printf 'The NuGet global-packages seed must contain only directories and regular files: %s\n' "${unsafe_entry}" >&2
    return 2
  fi
}
write_nuget_tree_manifest() {
  local source="$1"
  local manifest_prefix="$2"
  (
    cd "${source}"
    find -P . -printf '%y\t%P\0' | sort -z
  ) > "${manifest_prefix}.entries"
  (
    cd "${source}"
    find -P . -type f -print0 | sort -z | xargs -0 -r sha256sum --
  ) > "${manifest_prefix}.files"
}
is_canonical_nuget_version() {
  local version="$1"
  local release prerelease identifier
  local -a release_parts prerelease_parts

  [[ "${version}" != *+* ]] || return 1
  release="${version%%-*}"
  prerelease=''
  [[ "${version}" == *-* ]] && prerelease="${version#*-}"
  IFS=. read -r -a release_parts <<< "${release}"
  (( ${#release_parts[@]} == 3 || ${#release_parts[@]} == 4 )) || return 1
  for identifier in "${release_parts[@]}"; do
    [[ "${identifier}" =~ ^(0|[1-9][0-9]*)$ ]] || return 1
  done
  if (( ${#release_parts[@]} == 4 )) && [[ "${release_parts[3]}" == 0 ]]; then
    return 1
  fi
  if [[ "${version}" == *-* ]]; then
    [[ -n "${prerelease}" ]] || return 1
    IFS=. read -r -a prerelease_parts <<< "${prerelease}"
    for identifier in "${prerelease_parts[@]}"; do
      [[ "${identifier}" =~ ^[a-z0-9-]+$ ]] || return 1
      [[ ! "${identifier}" =~ ^0[0-9]+$ ]] || return 1
    done
  fi
}
write_selected_package_manifest() {
  local source="$1"
  local manifest="$2"
  local unsafe_entry package_id_directory package_id version_directory version
  local archive sha512 metadata nuspec package_hash
  local package_count=0

  validate_nuget_package_seed "${source}"
  unsafe_entry="$(find -P "${source}" -mindepth 1 -maxdepth 1 ! -type d -print -quit)"
  if [[ -n "${unsafe_entry}" ]]; then
    printf 'The selected NuGet seed root must contain only package-ID directories: %s\n' "${unsafe_entry}" >&2
    return 2
  fi
  : > "${manifest}"
  while IFS= read -r -d '' package_id_directory; do
    package_id="$(basename -- "${package_id_directory}")"
    if [[ ! "${package_id}" =~ ^[a-z0-9][a-z0-9_.-]*$ || "${package_id}" != "${package_id,,}" ]]; then
      printf 'The selected NuGet seed contains an unsafe or non-canonical package ID: %s\n' "${package_id}" >&2
      return 2
    fi
    unsafe_entry="$(find -P "${package_id_directory}" -mindepth 1 -maxdepth 1 ! -type d -print -quit)"
    if [[ -n "${unsafe_entry}" ]]; then
      printf 'A selected NuGet package-ID directory must contain only version directories: %s\n' "${unsafe_entry}" >&2
      return 2
    fi
    if [[ -z "$(find -P "${package_id_directory}" -mindepth 1 -maxdepth 1 -type d -print -quit)" ]]; then
      printf 'A selected NuGet package-ID directory contains no version: %s\n' "${package_id}" >&2
      return 2
    fi
    while IFS= read -r -d '' version_directory; do
      version="$(basename -- "${version_directory}")"
      if [[ "${version}" != "${version,,}" ]] || ! is_canonical_nuget_version "${version}"; then
        printf 'The selected NuGet seed contains an unsafe or non-canonical package version: %s/%s\n' \
          "${package_id}" "${version}" >&2
        return 2
      fi
      archive="${version_directory}/${package_id}.${version}.nupkg"
      sha512="${archive}.sha512"
      metadata="${version_directory}/.nupkg.metadata"
      nuspec="${version_directory}/${package_id}.nuspec"
      for required_file in "${archive}" "${sha512}" "${metadata}" "${nuspec}"; do
        if [[ ! -f "${required_file}" || -L "${required_file}" ]]; then
          printf 'A selected NuGet package directory is incomplete: %s\n' "${required_file}" >&2
          return 2
        fi
      done
      package_hash="$(sha256sum -- "${archive}" | cut -d' ' -f1)"
      printf '%s\t%s\t%s\n' "${package_id}" "${version}" "${package_hash}" >> "${manifest}"
      package_count=$((package_count + 1))
    done < <(find -P "${package_id_directory}" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)
  done < <(find -P "${source}" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)
  if (( package_count == 0 )); then
    printf 'The selected NuGet seed contains no package versions.\n' >&2
    return 2
  fi
  sort -o "${manifest}" -- "${manifest}"
  SELECTED_PACKAGE_COUNT="${package_count}"
}
write_local_source_manifest() {
  local source="$1"
  local manifest="$2"
  write_selected_package_manifest "${source}" "${manifest}"
  LOCAL_SOURCE_PACKAGE_COUNT="${SELECTED_PACKAGE_COUNT}"
}
if [[ "${seed_nuget_packages_from}" != /* || ! -d "${seed_nuget_packages_from}" || -L "${seed_nuget_packages_from}" ]]; then
  printf 'The NuGet global-packages seed must be an existing absolute directory.\n' >&2
  exit 2
fi
resolved_seed_nuget_packages_from="$(realpath -e -- "${seed_nuget_packages_from}")"
if [[ "${resolved_seed_nuget_packages_from}" != "${seed_nuget_packages_from}" ]]; then
  printf 'The NuGet global-packages seed path must already be canonical and contain no symbolic traversal.\n' >&2
  exit 2
fi
seed_nuget_packages_from="${resolved_seed_nuget_packages_from}"
if paths_overlap "${seed_nuget_packages_from}" "${repository}"; then
  printf 'The NuGet global-packages seed must not overlap the live repository.\n' >&2
  exit 2
fi
resolved_temp_parent="$(realpath -e -- "${TMPDIR:-/tmp}")"
if [[ "${resolved_temp_parent}" == "${seed_nuget_packages_from}" ||
      "${resolved_temp_parent}" == "${seed_nuget_packages_from}"/* ]]; then
  printf 'The NuGet global-packages seed must not contain the runner temporary root.\n' >&2
  exit 2
fi
validate_nuget_package_seed "${seed_nuget_packages_from}"
# This bootstrap deliberately performs no routine network acquisition. Its seed is
# the content-bound exact-G output of the separately governed A-L locked-acquisition
# responsibility; B1-R only reproduces that already-approved decision.
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
private_package_source="${private_root}/package-source"
packages_a="${private_root}/packages-a"
packages_b="${private_root}/packages-b"
git_home="${private_root}/git-home"
git_tmp="${private_root}/git-tmp"
git_template="${private_root}/empty-git-template"
selected_package_manifest="${private_root}/selected-packages.manifest"
seed_tree_before="${private_root}/seed-tree-before"
mkdir -m 700 -- "${packages_a}" "${packages_b}" "${git_home}" "${git_tmp}" "${git_template}"
if paths_overlap "${seed_nuget_packages_from}" "${private_root}" ||
   paths_overlap "${seed_nuget_packages_from}" "${packages_a}" ||
   paths_overlap "${seed_nuget_packages_from}" "${packages_b}"; then
  printf 'The NuGet package seed and runner-owned package state must not overlap.\n' >&2
  exit 2
fi
write_selected_package_manifest "${seed_nuget_packages_from}" "${selected_package_manifest}"
selected_package_count="${SELECTED_PACKAGE_COUNT}"
if [[ "${selected_package_count}" != "${expected_selected_package_count}" ]]; then
  printf 'The selected NuGet seed is not exact canonical G: expected=%s actual=%s.\n' \
    "${expected_selected_package_count}" "${selected_package_count}" >&2
  exit 2
fi
write_nuget_tree_manifest "${seed_nuget_packages_from}" "${seed_tree_before}"
validate_nuget_package_seed "${seed_nuget_packages_from}"

mkdir -m 700 -- "${private_package_source}"
while IFS=$'\t' read -r package_id version expected_hash; do
    source_directory="${seed_nuget_packages_from}/${package_id}/${version}"
    destination_directory="${private_package_source}/${package_id}/${version}"
    source_archive="${source_directory}/${package_id}.${version}.nupkg"
    destination_archive="${destination_directory}/${package_id}.${version}.nupkg"
    if [[ -e "${destination_directory}" || -L "${destination_directory}" ]]; then
      printf 'The private local NuGet source package destination already exists: %s\n' \
        "${destination_directory}" >&2
      exit 2
    fi
    mkdir -p -m 700 -- "${private_package_source}/${package_id}"
    cp -R --no-dereference -- "${source_directory}" "${destination_directory}"
    destination_hash="$(sha256sum -- "${destination_archive}" | cut -d' ' -f1)"
    if [[ "${destination_hash}" != "${expected_hash}" ]] ||
       ! cmp -s -- "${source_archive}" "${destination_archive}"; then
      printf 'A private local NuGet source archive differs from the selected seed: %s/%s\n' \
        "${package_id}" "${version}" >&2
      exit 2
    fi
done < "${selected_package_manifest}"
validate_nuget_package_seed "${private_package_source}"
write_local_source_manifest "${private_package_source}" "${private_root}/local-source.manifest"
if [[ "${LOCAL_SOURCE_PACKAGE_COUNT}" != "${selected_package_count}" ]] ||
   ! cmp -s -- "${selected_package_manifest}" "${private_root}/local-source.manifest"; then
  printf 'The private local NuGet source differs from the selected NuGet seed.\n' >&2
  exit 2
fi
write_nuget_tree_manifest "${private_package_source}" "${private_root}/local-source-tree"
if ! cmp -s -- "${seed_tree_before}.entries" "${private_root}/local-source-tree.entries" ||
   ! cmp -s -- "${seed_tree_before}.files" "${private_root}/local-source-tree.files"; then
  printf 'The private local NuGet source was not derived exactly from the selected seed.\n' >&2
  exit 2
fi
chmod -R a-w -- "${private_package_source}"
selected_manifest_sha256="$(sha256sum -- "${selected_package_manifest}" | cut -d' ' -f1)"
printf 'B6 selected NuGet seed validated: packages=%s archive-manifest-sha256=%s canonical-graph-sha256=%s.\n' \
  "${selected_package_count}" "${selected_manifest_sha256}" "${expected_canonical_graph_sha256}" >&2
printf 'B6 private local NuGet source derived from selected seed: %s complete package versions, exact archive bytes, configured sources overridden.\n' \
  "${LOCAL_SOURCE_PACKAGE_COUNT}" >&2
printf 'B6 B1-R package caches are independently fresh and empty before locked replay.\n' >&2

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
core_lock_relative="${backend_relative}/src/AqualLifeStyle.Core/packages.lock.json"
ef_lock_relative="${backend_relative}/src/AqualLifeStyle.EntityFrameworkCore/packages.lock.json"
runner_lock_relative="${backend_relative}/tools/AqualLifeStyle.B6SnapshotRunner/packages.lock.json"
verify_lock_set() {
  local source="$1"
  local relative expected actual
  while IFS=$'\t' read -r relative expected; do
    if [[ ! -f "${source}/${relative}" || -L "${source}/${relative}" ]]; then
      printf 'An approved B6 packages.lock.json is absent or not a regular file: %s\n' "${relative}" >&2
      return 2
    fi
    actual="$(sha256sum -- "${source}/${relative}" | cut -d' ' -f1)"
    if [[ "${actual}" != "${expected}" ]]; then
      printf 'An approved B6 packages.lock.json differs from its reviewed hash: %s expected=%s actual=%s\n' \
        "${relative}" "${expected}" "${actual}" >&2
      return 2
    fi
  done <<LOCKS
${core_lock_relative}	${expected_core_lock_sha256}
${ef_lock_relative}	${expected_ef_lock_sha256}
${runner_lock_relative}	${expected_runner_lock_sha256}
LOCKS
}
write_assets_manifest() {
  local artifact_root="$1"
  local output="$2"
  (
    cd "${artifact_root}"
    find -P . -type f -name project.assets.json -print0 | sort -z | xargs -0 -r sha256sum --
  ) > "${output}"
  if [[ "$(wc -l < "${output}")" != 3 ]]; then
    printf 'B1-R did not generate exactly three project.assets.json files.\n' >&2
    return 2
  fi
}
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
    "${source}/${core_lock_relative}" \
    "${source}/${ef_lock_relative}" \
    "${source}/${runner_lock_relative}" \
    "${source}/${backend_relative}/NuGet.Config"; do
    if [[ ! -f "${input}" || -L "${input}" ]]; then
      printf 'Reviewed B6 build/operation input must be a regular file: %s\n' "${input}" >&2
      exit 2
    fi
  done
  verify_lock_set "${source}"
done

b1r_restore_reviewed_tree() {
  local source="$1"
  local label="$2"
  local backend_root="${source}/${backend_relative}"
  local runner_project="${source}/${runner_relative}"
  local nuget_config="${backend_root}/NuGet.Config"
  local artifact_root="${backend_root}/artifacts/aqgreen-b6-authoritative"
  local package_cache="${private_root}/packages-${label}"
  local tool_home="${private_root}/tool-home-${label}"
  local tool_tmp="${private_root}/tool-tmp-${label}"
  local http_cache="${private_root}/http-cache-${label}"
  local restore_log="${private_root}/b1-r-${label}.restore.log"
  mkdir -m 700 -- "${tool_home}" "${tool_tmp}" "${http_cache}"
  if [[ -n "$(find "${package_cache}" -mindepth 1 -print -quit)" ]]; then
    printf 'B1-R %s package cache was not fresh and empty.\n' "${label}" >&2
    return 2
  fi
  if [[ -L "${backend_root}/artifacts" || -L "${artifact_root}" ]] ||
    [[ -d "${artifact_root}" && -n "$(find "${artifact_root}" -type l -print -quit)" ]]; then
    printf 'Reviewed B6 artifact paths must not contain symbolic links.\n' >&2
    exit 2
  fi

  verify_lock_set "${source}"
  printf 'B1-R_%s_START fresh-cache=true selected-only-source=true locked-mode=true network-boundary=REQUIRED_EXTERNAL_WITNESS\n' \
    "${label}" >&2
  # The future qualification harness must launch this stage inside the already
  # established Bubblewrap network namespace. Environment/configuration controls
  # below are deterministic hygiene; they are not evidence of network absence.
  if ! env -i PATH="${PATH}" HOME="${tool_home}" TMPDIR="${tool_tmp}" \
    DOTNET_CLI_HOME="${tool_home}" DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1 NUGET_PACKAGES="${package_cache}" \
    NUGET_HTTP_CACHE_PATH="${http_cache}" \
    dotnet restore "${runner_project}" \
      --configfile "${nuget_config}" \
      --packages "${package_cache}" \
      --locked-mode \
      --source "${private_package_source}" \
      --disable-build-servers \
      -p:ArtifactsPath="${artifact_root}" \
      -p:ImportDirectoryBuildProps=false \
      -p:ImportDirectoryBuildTargets=false >"${restore_log}" 2>&1; then
    cat "${restore_log}" >&2
    printf 'B1-R %s locked restore failed.\n' "${label}" >&2
    return 2
  fi
  cat "${restore_log}" >&2
  if grep -Eiq '(^|[ :])(warning|error)([ :]| [A-Z]{2,}[0-9]+)' "${restore_log}" ||
     grep -q 'NU1603' "${restore_log}"; then
    printf 'B1-R %s emitted a warning or error and failed closed.\n' "${label}" >&2
    return 2
  fi
  verify_lock_set "${source}"
  validate_nuget_package_seed "${package_cache}"
  write_selected_package_manifest "${package_cache}" "${private_root}/packages-${label}.manifest"
  if [[ "${SELECTED_PACKAGE_COUNT}" != "${expected_selected_package_count}" ]] ||
     ! cmp -s -- "${selected_package_manifest}" "${private_root}/packages-${label}.manifest"; then
    printf 'B1-R %s materialized package inventory differs from exact G.\n' "${label}" >&2
    return 2
  fi
  write_assets_manifest "${artifact_root}" "${private_root}/assets-${label}.before-build"
  printf 'B1-R_%s=PASS packages=%s warnings=0 errors=0 NU1603=0 lock-mutation=NO assets=3 graph-sha256=%s\n' \
    "${label}" "${SELECTED_PACKAGE_COUNT}" "${expected_canonical_graph_sha256}" >&2
}

b2_build_reviewed_tree() {
  local source="$1"
  local label="$2"
  local backend_root="${source}/${backend_relative}"
  local runner_project="${source}/${runner_relative}"
  local artifact_root="${backend_root}/artifacts/aqgreen-b6-authoritative"
  local package_cache="${private_root}/packages-${label}"
  local tool_home="${private_root}/tool-home-${label}"
  local tool_tmp="${private_root}/tool-tmp-${label}"

  verify_lock_set "${source}"
  env -i PATH="${PATH}" HOME="${tool_home}" TMPDIR="${tool_tmp}" \
    DOTNET_CLI_HOME="${tool_home}" DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1 NUGET_PACKAGES="${package_cache}" \
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

  verify_lock_set "${source}"
  write_assets_manifest "${artifact_root}" "${private_root}/assets-${label}.after-build"
  if ! cmp -s -- "${private_root}/assets-${label}.before-build" "${private_root}/assets-${label}.after-build"; then
    printf 'B2 %s changed project.assets.json despite --no-restore.\n' "${label}" >&2
    return 2
  fi
  validate_nuget_package_seed "${package_cache}"
  write_selected_package_manifest "${package_cache}" "${private_root}/packages-${label}.after-build.manifest"
  if ! cmp -s -- "${selected_package_manifest}" "${private_root}/packages-${label}.after-build.manifest"; then
    printf 'B2 %s changed the exact-G package inventory.\n' "${label}" >&2
    return 2
  fi
  if [[ -n "$(find "${artifact_root}" -type l -print -quit)" ]]; then
    printf 'Reviewed B6 build produced symbolic-link artifact state.\n' >&2
    exit 2
  fi
  printf 'B2_%s=PASS no-restore=true lock-mutation=NO assets-mutation=NO packages=%s\n' \
    "${label}" "${SELECTED_PACKAGE_COUNT}" >&2
}

b1r_restore_reviewed_tree "${source_a}" a
b1r_restore_reviewed_tree "${source_b}" b
if ! cmp -s -- "${private_root}/packages-a.manifest" "${private_root}/packages-b.manifest"; then
  printf 'The two independent B1-R package inventories disagree.\n' >&2
  exit 2
fi
printf 'B1-R_DETERMINISM=PASS semantic-package-inventory-equal=true canonical-graph-sha256=%s raw-assets-path-bound=true\n' \
  "${expected_canonical_graph_sha256}" >&2

b2_build_reviewed_tree "${source_a}" a
b2_build_reviewed_tree "${source_b}" b
validate_nuget_package_seed "${seed_nuget_packages_from}"
validate_nuget_package_seed "${private_package_source}"
write_nuget_tree_manifest "${seed_nuget_packages_from}" "${private_root}/seed-tree-after-build"
write_nuget_tree_manifest "${private_package_source}" "${private_root}/local-source-tree-after-build"
write_local_source_manifest "${private_package_source}" "${private_root}/local-source-after-build.manifest"
if ! cmp -s -- "${seed_tree_before}.entries" "${private_root}/seed-tree-after-build.entries" ||
   ! cmp -s -- "${seed_tree_before}.files" "${private_root}/seed-tree-after-build.files" ||
   ! cmp -s -- "${seed_tree_before}.entries" "${private_root}/local-source-tree-after-build.entries" ||
   ! cmp -s -- "${seed_tree_before}.files" "${private_root}/local-source-tree-after-build.files" ||
   ! cmp -s -- "${selected_package_manifest}" "${private_root}/local-source-after-build.manifest"; then
  printf 'The selected NuGet seed or private source changed during B1-R/B2.\n' >&2
  exit 2
fi
printf 'B6 selected NuGet seed and private source remained unchanged through B1-R/B2.\n' >&2
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
