#!/usr/bin/env bash
set -euo pipefail

# Compile and execute only the nonprivileged foundation fixtures. No runner,
# Bubblewrap, helper, journal writer, or live evidence adapter is invoked.
if (( $# != 1 )); then
  printf 'Usage: bash %s <external-output-directory>\n' "$0" >&2
  exit 2
fi
b6_pkg=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
b6_repo=$(git -C "$b6_pkg" rev-parse --show-toplevel)
b6_out=$(realpath -m -- "$1")
case "$b6_out/" in "$b6_repo/"*)
  printf 'Fixture outputs must be outside the repository.\n' >&2
  exit 2
esac
mkdir -p -- "$b6_out"
: > "$b6_out/commands.log"
for b6_suite in prior inherited account-grammar event-frame transition replay; do
  b6_sources=("$b6_pkg/attempt-account-schema.c")
  b6_wrap=()
  case "$b6_suite" in
    prior) b6_fixture=b6-schema-fixture.c; b6_wrap=(-Wl,--wrap=read -Wl,--wrap=__read_chk);;
    inherited) b6_fixture=b6-inherited-schema-fixture.c;;
    account-grammar) b6_fixture=b6-account-grammar-fixture.c;;
    event-frame) b6_fixture=b6-event-frame-fixture.c;;
    transition) b6_fixture=b6-transition-fixture.c; b6_sources+=("$b6_pkg/attempt-account-transition.c");;
    replay) b6_fixture=b6-account-replay-fixture.c; b6_sources+=("$b6_pkg/attempt-account-transition.c" "$b6_pkg/attempt-account-replay.c");;
  esac
  b6_sources+=("$b6_pkg/$b6_fixture")
  for b6_mode in normal sanitizer; do
    b6_flags=(-std=c11 -g -Wall -Wextra -Werror -Wconversion -Wshadow -Wpedantic)
    if [[ $b6_mode == normal ]]; then
      b6_flags+=(-O2)
    else
      b6_flags+=(-O1 -fsanitize=address,undefined -fno-omit-frame-pointer -fno-pie -no-pie)
    fi
    b6_build=(cc "${b6_flags[@]}" "${b6_sources[@]}" "${b6_wrap[@]}" -lcrypto -o "$b6_out/$b6_suite-$b6_mode")
    printf '%q ' "${b6_build[@]}" >> "$b6_out/commands.log"
    printf '\n' >> "$b6_out/commands.log"
    "${b6_build[@]}" > "$b6_out/$b6_suite-build-$b6_mode.stdout" 2> "$b6_out/$b6_suite-build-$b6_mode.stderr"
    b6_run=("$b6_out/$b6_suite-$b6_mode")
    if [[ $b6_mode == sanitizer ]]; then
      b6_run=(env ASAN_OPTIONS=detect_leaks=1:halt_on_error=1 UBSAN_OPTIONS=halt_on_error=1:print_stacktrace=1 "${b6_run[@]}")
    fi
    printf '%q ' "${b6_run[@]}" >> "$b6_out/commands.log"
    printf '\n' >> "$b6_out/commands.log"
    "${b6_run[@]}" > "$b6_out/$b6_suite-$b6_mode.stdout" 2> "$b6_out/$b6_suite-$b6_mode.stderr"
    test ! -s "$b6_out/$b6_suite-build-$b6_mode.stderr"
    test ! -s "$b6_out/$b6_suite-$b6_mode.stderr"
  done
  cmp -- "$b6_out/$b6_suite-normal.stdout" "$b6_out/$b6_suite-sanitizer.stdout"
  tail -n 1 "$b6_out/$b6_suite-normal.stdout"
done
printf 'All six suites: normal PASS; ASan/UBSan PASS; sanitizer diagnostics 0.\n'
