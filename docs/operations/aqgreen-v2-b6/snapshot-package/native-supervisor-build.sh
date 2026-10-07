#!/bin/bash -p
set -euo pipefail
umask 077
export PATH=/usr/bin:/bin LC_ALL=C SOURCE_DATE_EPOCH=0 TZ=UTC
[[ $# == 6 && $1 == --output-a && $3 == --output-b && $5 == --manifest ]] || {
  printf 'usage: static-env bash -p native-supervisor-build.sh --output-a NEW --output-b NEW --manifest NEW\n' >&2; exit 2;
}
output_a=$2; output_b=$4; manifest=$6
[[ $output_a == /* && $output_b == /* && $manifest == /* && $output_a != "$output_b" ]] || exit 2
for destination in "$output_a" "$output_b" "$manifest"; do [[ ! -e $destination && ! -L $destination ]] || exit 2; done
package=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
compiler=/usr/bin/x86_64-linux-gnu-gcc-13
[[ $($compiler -dumpfullversion) == 13.3.0 && $($compiler -dumpmachine) == x86_64-linux-gnu ]] || exit 2
[[ $(/usr/bin/ld --version | head -n 1) == *'2.42'* ]] || exit 2
[[ $(/usr/bin/getconf GNU_LIBC_VERSION) == 'glibc 2.39' ]] || exit 2
[[ $(/usr/bin/sha256sum /bin/busybox | cut -d' ' -f1) == dbac288c29ba568459550a2da9e7ae0ded6b1fc728ee9fad3044c44e62d6ac14 ]] || exit 2
flags=(-std=c17 -D_GNU_SOURCE -O2 -Wall -Wextra -Werror -pedantic -fstack-protector-strong
       -D_FORTIFY_SOURCE=3 -fPIE -pie -Wl,-z,relro,-z,now -Wl,--build-id=sha1)
for destination in "$output_a" "$output_b"; do
  /bin/busybox env -i PATH=/usr/bin:/bin LC_ALL=C SOURCE_DATE_EPOCH=0 TZ=UTC "$compiler" \
    "${flags[@]}" -o "$destination" "$package/native-supervisor.c"
  chmod 755 "$destination"
done
/usr/bin/cmp -- "$output_a" "$output_b"
[[ $(/usr/bin/readelf -dW "$output_a" | /usr/bin/mawk '/NEEDED/{print $5}') == '[libc.so.6]' ]] || exit 2
/usr/bin/readelf -lW "$output_a" | /usr/bin/grep -q '/lib64/ld-linux-x86-64.so.2' || exit 2
/usr/bin/readelf -lW "$output_a" | /usr/bin/grep -q GNU_RELRO || exit 2
/usr/bin/readelf -dW "$output_a" | /usr/bin/grep -q BIND_NOW || exit 2
hash() { /usr/bin/sha256sum -- "$1" | /usr/bin/cut -d' ' -f1; }
{
  printf 'protocol\tB6_NATIVE_BUILD_FRESH_V1\n'
  printf 'source_sha256\t%s\n' "$(hash "$package/native-supervisor.c")"
  printf 'build_script_sha256\t%s\n' "$(hash "$package/native-supervisor-build.sh")"
  printf 'elf_sha256\t%s\n' "$(hash "$output_a")"
  printf 'compiler_version\t%s\ntarget\tx86_64-linux-gnu\n' "$($compiler -dumpfullversion)"
  printf 'flags\t%s\nenvironment\tPATH=/usr/bin:/bin LC_ALL=C SOURCE_DATE_EPOCH=0 TZ=UTC\n' "${flags[*]}"
  printf 'package_root\t/tmp/aqgreen-b6-native-package\noutput_parent\t/tmp/aqgreen-b6-proof-output\n'
  for item in compiler cc1 collect2 assembler linker loader libc bash busybox; do
    case $item in
      compiler) path=$compiler;;
      cc1|collect2) path=$($compiler -print-prog-name="$item");;
      assembler) path=/usr/bin/as;; linker) path=/usr/bin/ld;;
      loader) path=/lib64/ld-linux-x86-64.so.2;; libc) path=/lib/x86_64-linux-gnu/libc.so.6;;
      bash) path=/bin/bash;; busybox) path=/bin/busybox;;
    esac
    path=$(/usr/bin/readlink -f "$path")
    printf '%s_path\t%s\n%s_sha256\t%s\n' "$item" "$path" "$item" "$(hash "$path")"
  done
} > "$manifest"
chmod 644 "$manifest"
printf 'NATIVE_BUILD=PASS deterministic=YES elf_sha256=%s\n' "$(hash "$output_a")"
