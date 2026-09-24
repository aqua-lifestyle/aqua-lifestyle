#!/bin/bash -p
# The native direct parent is lifecycle authority. This script never supervises it.
set -euo pipefail
umask 077
export PATH=/usr/bin:/bin LC_ALL=C
fail() { printf 'B6_HARNESS_FAIL reason=%s\n' "$1" >&2; exit 2; }
[[ $EUID != 0 && $# == 4 && $1 == --output-dir && $3 == --transaction-id ]] || fail exact-argv
output=$2; transaction=$4
[[ $transaction =~ ^[0-9a-f]{64}$ && $output == /* ]] || fail transaction-format
package=$(/usr/bin/dirname -- "${BASH_SOURCE[0]}")
[[ $package == /* && ! -L ${BASH_SOURCE[0]} ]] || fail harness-path
uid=$(/usr/bin/id -u); gid=$(/usr/bin/id -g)
[[ $uid != 0 && $gid != 0 ]] || fail ordinary-user
[[ $(/usr/bin/stat -c '%u %g %a' -- "$package") == "$uid $gid 700" ]] || fail package-custody
[[ ! -L $output && $(/usr/bin/stat -c '%u %g %a' -- "$output") == "$uid $gid 700" ]] || fail output-custody
for file in native-supervisor-build-manifest.tsv native-supervisor.c native-supervisor-build.sh native-supervisor-amd64 non-root-bwrap-proof-harness.sh; do
  [[ -f $package/$file && ! -L $package/$file ]] || fail package-file
  read -r owner group mode links < <(/usr/bin/stat -c '%u %g %a %h' -- "$package/$file")
  [[ $owner == "$uid" && $group == "$gid" && $links == 1 && $((8#$mode & 0022)) == 0 ]] || fail package-file-custody
done
declare -A manifest=()
while IFS= read -r record; do
  [[ $record == *$'\t'* ]] || fail manifest-columns
  key=${record%%$'\t'*}; value=${record#*$'\t'}
  [[ $key =~ ^[a-z][a-z0-9_]*$ && -n $value && $value != *$'\t'* && ! -v manifest[$key] ]] || fail manifest-record
  manifest[$key]=$value
done < "$package/native-supervisor-build-manifest.tsv"
[[ ${manifest[protocol]:-} == B6_NATIVE_BUILD_FRESH_V1 ]] || fail manifest-protocol
hash() { /usr/bin/sha256sum -- "$1" | /usr/bin/cut -d' ' -f1; }
for binding in 'source_sha256 native-supervisor.c' 'build_script_sha256 native-supervisor-build.sh' 'elf_sha256 native-supervisor-amd64'; do
  read -r key name <<< "$binding"
  [[ ${manifest[$key]:-} =~ ^[0-9a-f]{64}$ && $(hash "$package/$name") == "${manifest[$key]}" ]] || fail manifest-identity
done
supervisor=$PPID
[[ $supervisor =~ ^[1-9][0-9]*$ && $supervisor != 1 ]] || fail parent-pid
supervisor_exe=$(/usr/bin/readlink -f "/proc/$supervisor/exe")
[[ $supervisor_exe == "$package/native-supervisor-amd64" && $(hash "/proc/$supervisor/exe") == "${manifest[elf_sha256]}" ]] || fail parent-executable
parent_stat=$(<"/proc/$supervisor/stat"); parent_fields=${parent_stat##*) }; read -ra parent_fields <<< "$parent_fields"
supervisor_start=${parent_fields[19]}
self_stat=$(<"/proc/$$/stat"); self_fields=${self_stat##*) }; read -ra self_fields <<< "$self_fields"
[[ ${self_fields[1]} == "$supervisor" && ${self_fields[2]} == "$$" ]] || fail parent-or-pgid
child_start=${self_fields[19]}
[[ $(set -o | /usr/bin/mawk '$1=="monitor"{print $2}') == off ]] || fail monitor-on
[[ $(/usr/bin/mawk '/^Uid:/{print $2,$3,$4,$5}' "/proc/$supervisor/status") == "$uid $uid $uid $uid" ]] || fail parent-uid
[[ $(/usr/bin/mawk '/^Gid:/{print $2,$3,$4,$5}' "/proc/$supervisor/status") == "$gid $gid $gid $gid" ]] || fail parent-gid
for process in "$supervisor" "$$"; do
  [[ $(/usr/bin/mawk '/^CapEff:|^CapPrm:|^CapAmb:/{print $2}' "/proc/$process/status") == $'0000000000000000\n0000000000000000\n0000000000000000' ]] || fail capabilities
done
blocked=$(/usr/bin/mawk '/^SigBlk:/{print $2}' "/proc/$supervisor/status")
ignored=$(/usr/bin/mawk '/^SigIgn:/{print $2}' "/proc/$supervisor/status")
[[ $((16#$blocked & 0x4002)) == 16386 && $((16#$ignored & 0x4002)) == 0 ]] || fail parent-signals
expected_environment=$(printf 'PATH=/usr/bin:/bin\0LC_ALL=C\0' | /usr/bin/sha256sum | /usr/bin/cut -d' ' -f1)
[[ $(hash "/proc/$supervisor/environ") == "$expected_environment" && $(hash "/proc/$$/environ") == "$expected_environment" ]] || fail initial-environment
expected_parent_argv=$(printf '%s\0--output-dir\0%s\0' "$supervisor_exe" "$output" | /usr/bin/sha256sum | /usr/bin/cut -d' ' -f1)
[[ $(hash "/proc/$supervisor/cmdline") == "$expected_parent_argv" ]] || fail parent-argv
expected_child_argv=$(printf '/bin/bash\0-p\0%s\0--output-dir\0%s\0--transaction-id\0%s\0' "${BASH_SOURCE[0]}" "$output" "$transaction" | /usr/bin/sha256sum | /usr/bin/cut -d' ' -f1)
[[ $(hash "/proc/$$/cmdline") == "$expected_child_argv" ]] || fail child-argv
[[ -S /proc/$$/fd/3 ]] || fail readiness-socket
[[ $(/usr/bin/readlink -f "/proc/$supervisor/exe") == "$supervisor_exe" ]] || fail parent-recheck
printf 'B6READY1\n' >&3
exec 3>&-

# Trust authority is compiled/reviewed configuration, never caller environment/argv.
approved_bwrap_version='UNRESOLVED'
approved_bwrap_sha256='UNRESOLVED'
approved_bwrap_provenance='UNRESOLVED'
bwrap='/usr/bin/bwrap'
[[ $approved_bwrap_version != UNRESOLVED && $approved_bwrap_sha256 != UNRESOLVED && $approved_bwrap_provenance != UNRESOLVED ]] || fail approved-bubblewrap-unresolved
[[ $approved_bwrap_version != '0.9.0-1ubuntu0.3' ]] || fail forbidden-bubblewrap
[[ $approved_bwrap_sha256 =~ ^[0-9a-f]{64}$ && $approved_bwrap_provenance =~ ^[0-9a-f]{64}$ && $(hash "$bwrap") == "$approved_bwrap_sha256" ]] || fail bubblewrap-identity
# B6_SYNTHETIC_BODY_ANCHOR: only a private source-copy fixture replaces the next line.
fail real-bubblewrap-qualification-unauthorized
