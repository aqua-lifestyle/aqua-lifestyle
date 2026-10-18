# Limited B6S PB1 implementation

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Authority and immutable baseline

The owner's “LIMITED B6S PB1 CONTRACT ADOPTION AND IMPLEMENTATION” instruction
conditionally adopts only read-only PB1 and authorizes the isolated implementation,
focused tests and separate publication. Five normative subjects and manifest were
verified before editing:0711c7d483f16bddf8bb83ecacb7b4dedda978adc85295c4704516514e81fd7b.
No normative file is modified. Broader B6S/TQ1/BIND/reconciliation/commissioning is
not adopted. An outside-normative limited-adoption record will preserve this scope.

## Source and implementation scope

Dedicated branch `b6/pb1-readonly-v1` from exact PR108ed07fbe. PR107/108/106
branches remain unchanged. Retained successor-record scanner/decimal/EVP primitives
are adapted into a PB1-only codec, without importing old schema/pin tables. The
single source reuse input is identified; no45-entry/foundation/Customer11 check or
unchanged suite is repeated.

The library implements copied canonical descriptor/result codecs, local typed-role
byte/hash/profile comparisons, exact argv/env checks and output/exit accounting.
Those are representation facts, not actual authority/custody/manifest qualification.
Runtime admission has no success path while the required controller/qualification/
confinement/custody/independent ledger remain unimplemented. The executable is
fixture-only and gives UNDETERMINED/DEPENDENCY for an otherwise locally valid case.
No caller flag, hash agreement or synthetic subject fills those missing predicates.

## Plan and evidence

Add focused native unit/mutation/fault/replay tests and actual held-ELF fixture
process cases using seven argv elements, two env strings and FDs0–5. Record compiler,
source, executables, test outcomes and partial/failure artifacts. Sanitizer evidence
is component evidence, not the normal128MiB process-profile qualification.
No product/.NET/PG suites, original searches or accepted Customer11 executions.

Initial builds retained: run1 failed strict compilation on unchecked diagnostic
write returns; corrected actual IO handling without suppressing warnings. Run2 had
31/32 process passes because read-fault injection missed glibc's fortified
__read_chk; symbol inspection established the cause and both read paths are now
wrapped without disabling fortification. Run3 passed its earlier source snapshot.

Self-review regressions then proved two new component gaps: success-result
accounting omitted package/spec/layout checks (run4) and the SHA helper could
overwrite aliased input (run5). Both were corrected; run6 passed8groups/4597
assertions per normal/ASan/UBSan and32process cases. Those results are an earlier
snapshot, not the final source's validation.

Independent review of exact run6source found two medium introduced defects:
unsafe rejection diagnostics could write through FD2alias into input, and nested
allocation failure became INVALID instead of UNDETERMINED. New focused before-fix
process regressions fail against retained run6binaries; run7's native regression
fails on the nested allocation path. Preflight now performs no writes until FD
validation, and nested resource failure is propagated with no operation result.
Expanded coverage includes initially empty aliased input and diagnostic/result
aliasing; no assertions or approved normative bytes are weakened.

Final run8 passes8 native groups/4599 assertions in both normal and ASan/UBSan
modes with identical logical stdout/empty runtime stderr;37 process cases pass,
0skip,38physical attempts (replay pair), noneexit0. Exact source/executable/test
pins and239-file manifest are recorded in the
[implementation evidence](../../verification/aqgreen-v2-b6-pb1-readonly-v1/README.md).
Independent correction review verifies those identities, closes R1/R2 and finds
no new material delta finding. It is not external runtime/business acceptance.

No runtime/B6 qualification is claimed. Next: inspect final/staged diff, verify
committed source against run8pins and approved normative-byte equality, then publish
the separate stacked PR with explicit blocked controller/custody/confinement/ledger
gaps. No broader adopted contract is inferred.
