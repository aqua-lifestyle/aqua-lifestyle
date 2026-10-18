# B6S read-only PB1 component and entrypoint fixture

Implements only the owner-adopted PB1 scope bound to manifest
`0711c7d483f16bddf8bb83ecacb7b4dedda978adc85295c4704516514e81fd7b`.
The five approved normative files remain unchanged. See the
[limited adoption record](../../docs/operations/aqgreen-v2-b6-successor/v1/LIMITED-ADOPTION.md)
and [reuse provenance](ORIGIN.md).

## Implemented boundary

- `record.[ch]`: owned exact descriptor/result decoding and canonical encoding,
  typed scalar/limit checks, fixed versions/realm and closed exit/result pairs.
- `pb1.[ch]`: exact argv/env checks, pure local role/byte/expected-pin comparison,
  selected canonical-schema handling, output/actual-input/descriptor/exit accounting,
  and private result writes retaining partial failure. Missing is not zero/success.
- `fixture.c`: **compile-time fixture-only** process with exact seven argv/two env
  strings and FDs0–5. The test launcher descriptor-executes a held ELF using a
  CLOEXEC FD and sets the specified CPU/address-space/stack/FD maxima and
  no-new-privileges. Runtime view/authority remains unqualified.
  Preflight rejections are silent: no diagnostic write occurs until the separate
  diagnostic FD has passed the fixture checks, including source/result aliasing.

`B6S_CHECKED` is local representation/accounting, not actual admission. The local
binding API checks given bytes/pins/roles; it does not authenticate the caller's
expected pins or validate opaque manifest assertions. No caller success flag is
accepted as a substitute. The runtime-admission function **always returns
UNDETERMINED**. An otherwise locally valid fixture request produces exit12 with
`UNDETERMINED/DEPENDENCY`; the fixture has no exit0 path. Positive representation
and success-result accounting are tested at the pure library boundary, not
misreported as a qualified actual launch.

Only the two new canonical descriptor/result schemas are compiled. Native-layout
profiles and old TQ1/S1 namespaces are unsupported; no fallback is supplied. The
fixture supports /dev/null inert FDs0/1 and fresh0600 diagnostic/result files as
its explicit subprofile. Those local metadata checks do not prove private custody.

## Explicit remaining gaps

| Requirement | Current state |
| --- | --- |
| External accepted controller/campaign and10-second wall deadline, current authority and original typed source/supply/manifest validators | NOT IMPLEMENTED; raw hash agreement cannot satisfy them; the15-second test-harness timeout is only test hygiene |
| Qualified immutable runtime/loader/dependency closure, namespace/confinement, zero supplementary groups/capability proof and FD/custody lifetime | NOT QUALIFIED; resource limits/no-new-privileges are only observed fixture mechanics |
| Independent pre/post observation/assessment ledger and cross-attempt intent-conflict/recovery controller | NOT IMPLEMENTED; independent outputs/replay component tests are not that controller |
| Full B6/TQ1/BIND/helper/adapters/representative/final/applied or D10 financial commissioning | OUTSIDE this implementation; no authority granted |

Consequently Q02 and parts of Q03–Q06/Q09 gain focused component/fixture evidence;
no complete Q01–Q09/Q21 runtime campaign is claimed. The entrypoint is not exposed
through HTTP/DI/workers, no production-capable executable is installed, and no
fixed protected stores or existing financial/history state are touched.

## Focused verification

From the repository root, supply a **new canonical external directory** whose
parent already exists:

```bash
node tools/b6-pb1/test.mjs /tmp/opencode/b6s-pb1-new-qualification
```

Requires Linux, GCC/cc, ar, OpenSSL development headers/libcrypto and Node built-ins.
No package install or production connection. The harness uses an owned new0700
directory, strict-warning native builds, new component unit tests in normal and
ASan/UBSan modes, then37 normal held-ELF process fixture cases. Sanitizer components
are not claimed to run under the normal128MiB entrypoint profile. Systematic
truncation/control-byte loops are assertion breadth, not distinct attack classes.

The result preserves all original failed/partial attempt artifacts, command output,
source/executable/test hashes and a bounded artifact manifest. Failures remain in
their original output directory; use a new directory after correcting source.
No accepted Customer11/native foundation/.NET/PG suites are rerun. Process stdout
is inert and diagnostics contain fixed category codes only. Test fixtures contain
no production records or credentials and are not registered as accepted subjects.
