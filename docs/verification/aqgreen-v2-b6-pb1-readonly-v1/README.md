# Limited B6S PB1 implementation evidence

**IMPLEMENTED / TESTED locally:** exact PB1 descriptor/result codecs, local
representation/binding/output accounting and a deliberately blocked entrypoint
fixture. **Actual runtime admission/qualification remains BLOCKED.** This is not
TQ1 compatibility, BIND activation, final B6 acceptance or B7/D10 commissioning.

- Adopted package: `B6S/1.0.0-candidate.1`, manifest SHA-256
  `0711c7d483f16bddf8bb83ecacb7b4dedda978adc85295c4704516514e81fd7b`.
- Source baseline: PR108ed07fbe9a1ecfb885a3d727ba2fe1aff3745be36. New branch:
  `b6/pb1-readonly-v1`; separate stacked implementation PR targets PR108's branch.
- [Limited owner decision](../../operations/aqgreen-v2-b6-successor/v1/LIMITED-ADOPTION.md),
  [implementation/reproduction](../../../tools/b6-pb1/README.md),
  [retained framing provenance](../../../tools/b6-pb1/ORIGIN.md),
  [exact machine-readable evidence](evidence.json).

## Actual implementation and test scope

| Boundary | Evidence and limit |
| --- | --- |
| P02 codecs | Exact copied20-row descriptor/eight-row result, strict framing/scalars/bounds/schema/version/realm, canonical encoding and closed exit/result pairs |
| Local binding/admission rejection | Twelve role/expected-pin/actual-byte comparisons, actual input digest/count, supported canonical-schema/profile checks; missing evidence stays indeterminate. Opaque manifest truth/authority is not established |
| P03 entrypoint fixture | Actual held native ELF, seven argv/two env strings, FD access/object/offset/alias checks, CPU/AS/stack/FD maxima and no-new-privileges. No namespace/immutable-view/full-principal qualification is inferred |
| P05 output accounting | Raw descriptor/input/result/exit binding, positive local grammar/profile verification, truthful negative categories, no fabricated complete result on incomplete observation, retained partial writes |
| P06 replay | Pure completed-result equality under satisfied conditions; separate retained negative process attempts; operational failures remain failures, not replacements. Campaign concurrency/intent/recovery controller absent |
| Runtime grant | `b6s_runtime_admission()` always UNDETERMINED. Fixture-only compile guard and no exit0 path. Locally valid fixture input gives exit12/UNDETERMINED/DEPENDENCY |

Final focused command:

```bash
node tools/b6-pb1/test.mjs /tmp/opencode/b6s-pb1-implementation-20261018-8
```

The command was run against the exact source hashes in evidence.json before Git
publication. Final commit source bytes are checked against those pins; source-state
metadata honestly records an uncommitted test workspace, not an already published
or operationally qualified release. Reproduction needs a **new** output directory.

| Final run8 evidence | Actual result |
| --- | --- |
| Strict native builds | Passed; warnings remain errors, no warning suppression |
| Native normal component groups |8 groups /4,599 assertions passed; empty runtime stderr |
| Native ASan/UBSan component groups |Same8 groups /4,599 assertions passed, logical stdout identical, no sanitizer diagnostics |
| Native process fixture tests |37 passed /0 failed /0 skipped /0 cancelled |
| Physical process attempts |38, because replay uses two attempts; none exits0, all source bytes preserved |
| Raw artifact integrity |239 listed files match manifest SHA-256 `b20a2a31386ee532f88a7a782d3487fc4b487c5338f6e5bc089539fe6d50217b` |

Assertions include systematic truncation/control-byte variants and48 exit/verdict/
reason combinations. They are not4,599 independent attack classes or twice as many
unique groups because two build modes executed them. Sanitizers apply to components,
not a claim they fit the normal128MiB process profile. The native suite currently
has local execution evidence; existing application CI does not execute this suite.

## Defects and preserved failed evidence

All failed/partial artifacts remain in their original owned external directories;
no pass totals are accumulated across revisions.

| Finding | Before evidence / smallest correction |
| --- | --- |
| Unchecked diagnostics writes | Run1 strict compile failure; handle actual short/error writes, retain `-Werror` |
| Input-read fault test missed fortified path | Run2:31/32 passes; symbol inspection found `__read_chk`. Wrap both read paths without disabling fortification; final EIO case has no fabricated output |
| New success-accounting profile gap | Run4 fails added regression; enforce adopted package/spec/canonical-layout checks before accepting local exit0 accounting |
| New hash-output alias mutation | Run5 fails added regression; reject overlap before touching input/output |
| Independent R1: unsafe rejection diagnostics | Four selected alias regressions fail against retained run6fixture. Preflight now writes nothing until FD validation. Final five alias cases, including empty input, preserve all source/result objects |
| Independent R2: nested resource failure misclassified | Run7 new calloc regression fails; propagate nested UNDETERMINED, clean both records and leave NO_OPERATION. Both calloc/malloc regressions pass normally and under sanitizers |

Run3 and run6 were earlier passing source snapshots, not final execution counts.
Before-fix four-test diagnostic TAP:0 passed/4 failed/0 skipped; the fifth empty
alias vector was added for the corrected boundary. Run7 stopped at calloc failure;
it does not prove the later malloc assertion executed before the fix.

## Independent technical review

Initial separate-agent review reproduced R1 using an anonymous synthetic memfd and
R2 using targeted existing fault controls; it requested changes. Final review
verified all ten source pins, all five approved normative subjects, all239 final
artifact hashes and retained results without rerunning the passed suite. It closed
R1/R2 for these exact bytes and found no new material correction-delta findings.
The initial wider review and final correction review remain separately traceable.

| Exact reviewer report | SHA-256 |
| --- | --- |
| `/tmp/opencode/b6s-pb1-implementation-independent-review.md` | `67b56823161c7cac1c07ca1a670e78731975b5e87f498b6b84e9647a08d6895d` |
| `/tmp/opencode/b6s-pb1-implementation-final-review.md` | `a8873f01417578525c1170e6a34a901cc99f000bb98b70213fc4f1107df98d9c` |

This is separate technical correctness review, not external human examination or
source/tool/runtime/custody acceptance. No old accepted Customer11/native/.NET/PG
suite, historical recovery search, provider operation or CI dispatch was performed.

## Remaining BLOCKED requirements

- External accepted controller/campaign/current authority, original typed manifest/
  source/supply validators and actual release/platform selection.
- Immutable ELF/loader/dependency closure, namespace/confinement, full principal/
  supplementary-group/capability evidence and protected handle lifetime/custody.
- The controller's10-second wall deadline; the15-second test guard is test hygiene.
- Independent original observer/assessment ledger, current pre/post evidence,
  cross-attempt intent-conflict/concurrent/recovery controller and actual success
  launch. Unit signal-status accounting is not execution of a signalled process.
- Full Q01–Q09/Q21 runtime qualification and all broader B6/TQ1/BIND/final/
  representative/applied/D10 financial authority.

No caller-provided flag or hash agreement supplies these facts. The next bounded
engineering review can accept this local codec/fixture scope; qualification work
must first obtain actual selected typed profiles, controller/observer and protected
custody inputs without changing the adopted normative bytes. Customer11 and PR107
remain preserved and independent.
