# B6 successor contract candidate

**Version:** `B6S/1.0.0-candidate.1`

**Status:** PROPOSED — adoption PENDING owner approval.

**Historical TQ1 compatibility:** UNKNOWN.
**Permitted preparation:** the owner's “B6 SUCCESSOR QUALIFICATION: PRESERVE AND
COMPLETE” instruction selects Route B for preparation only. It accepts no contract,
runtime subject, qualification result, reconciliation or commissioning operation.

This is a new prospective subject, not a recovered Parent/AS1/SS1/AR1/TQ1/PB1
package. Unavailable originals remain unavailable within the recorded search
surfaces, not permanently destroyed. No historical file or receipt is replaced.

## Review subject and identity

The exact package identity is SHA-256 of `MANIFEST.sha256`. That manifest names the
five normative candidate files, in ASCII filename order, as lowercase SHA-256,
two spaces, basename, LF. It excludes itself, this index, review records and the
worklog; no self-hash cycle exists. Any normative change produces a new digest and
requires review of that exact successor. Published candidate bytes remain traceable
in Git. The final manifest digest is reported in the PR/task handoff.

| Normative subject | Purpose |
| --- | --- |
| [BASELINE.md](BASELINE.md) | Scope, prospective obligations, trust/result boundaries and owner decisions |
| [PB1-INVOCATION.md](PB1-INVOCATION.md) | Exact proposed read-only native parser invocation and admission contract |
| [OBLIGATIONS.md](OBLIGATIONS.md) | Obligation → retained source/evidence → bounded gap and reuse disposition |
| [QUALIFICATION.md](QUALIFICATION.md) | Finite22-check coverage, representative inputs, examination and D10 handoff |
| [SOURCE-EVIDENCE.json](SOURCE-EVIDENCE.json) | Immutable baseline references and limited carried-forward evidence identities |

Only PB1's first read-only invocation slice is a complete wire/entrypoint proposal
here. Broader producer/journal/root/helper/adapter obligations are bounded in the
baseline and mapping; their exact role codecs/native profiles must be selected and
reviewed before their implementation. This candidate does not invent missing
historical schemas or declare those broader subjects implementation-ready.

## Preservation baseline

- [Recovery and historical-surface closure at810ac09](https://github.com/aqua-lifestyle/aqua-lifestyle/blob/810ac0979caa0ebe9d07273a6ccfd76ae408e148/docs/verification/aqgreen-v2-b6-closure-20261017/historical-surfaces.md).
- [PR107 qualified chronology correction at8e7282a](https://github.com/aqua-lifestyle/aqua-lifestyle/pull/107).
- Published B6 foundation/customer11 source at438852e;43 separately preserved
  native candidates remain partial and unintegrated. Their existing source pins
  are evidence inputs, not actual selected runtime supply.
- Existing .NET analysis architecture, accepted bounded Customer11 reconciliation,
  and bounded B7/D10 engineering remain preserved. No unchanged suites are rerun
  for this documentation-only preparation.

The PR starts from main23f176d and contains documentation only. PR107's correction
is referenced, not duplicated; an analysis release must integrate that correction
or prove an independently reviewed equivalent before qualification.

**Next gate:** owner review of this exact candidate and its stated remaining
decisions. Neither merging documentation nor a technical review freezes/adopts it.
