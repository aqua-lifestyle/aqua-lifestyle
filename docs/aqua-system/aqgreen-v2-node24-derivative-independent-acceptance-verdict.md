# AQGREEN V2 — NODE 24 DERIVATIVE INDEPENDENT ACCEPTANCE VERDICT

**Agent 2 · 12 October 2026 · Independent local engineering review**

## Decision and exact subject

The separate derivative is verified for **owner acceptance of the locally
qualified isolated MVP candidate**. No material derivative-owned defect was found.

**Derivative source-manifest SHA-256:**
`5e133be584398b2db14a5c542e27860bd6f02adae0c66c1488ef30e7ee2573e0`.

Verified bundle:
`/home/wtc/Downloads/newAqua/aqgreen-v2-node24-candidate-20261012-5e133be58439`.
Its archive SHA-256 is
`0af6babebb839c3e65cf4df4797d59822ba84700829b2f0e96e0cdbd87faf11d`.
Bare HEAD `e9bd8e96` does not identify this subject. The original accepted subject
and derivative remain separate; this review does not confer owner acceptance.

## 1. Original preservation — independently passed

| Check personally performed | Result |
| --- | --- |
| Accepted 1,739-file subject manifest | All hashes match preserved original; manifest identity `72219b17d8cd5a55f9c0b542190caa969bb5193131fa2924fdefcdff54d1e26c` |
| Preserved original source inventory and hashes | **2,101/2,101**, no unmanifested files, symlinks or special files |
| Original archive digest | Matches `784836d801cb459f1026e24805fb4c0b52b79e86ad6b4ed0838b0349d4964aa5` |
| Fresh independent extraction into reviewer-owned temporary directory | **2,101/2,101**, exact manifest inventory and hashes |
| Original snapshot manifest | Matches `a21093f4319ac454a3308ea68a3544b0eddc8d5b51852e84ff1262d3738dd384` |

Read the [owner's original isolated technical acceptance record](aqgreen-v2-isolated-mvp-owner-acceptance-20261012.md),
preserved candidate metadata and original 17-case report/evidence references.
Their subject identities agree. The expanded 2,101-file preservation snapshot
includes additional source/evidence beyond the 1,739-file accepted-subject manifest;
those two manifest identities are correctly distinguished, not interchangeable.
Compiled original binaries are excluded from source preservation; their recorded
identifiers are not claimed as independently rehashed executable artifacts here.

## 2. Exact derivative changes — independently passed

Verified **2,105/2,105** derivative source files and **2,105/2,105** freshly extracted
archive files, including exact inventories and declared archive/manifest digests.
Compared original and derivative path/hash inventories, not merely Agent 1's diff.
Exactly these five paths differ under `AqualLifeStyle/9.4.2/aqua-frontend/app/`:

- **Modified:** `layout.tsx`.
- **Added:** `fonts/geist-latin-variable.woff2`,
  `fonts/geist-mono-latin-variable.woff2`, `fonts/OFL.txt`, `fonts/README.md`.

All **2,100 other original paths are unchanged**. Derivative metadata's changed
paths, old/new hashes and parent identities match the independent comparison.
`package.json` and the dependency lock are unchanged; lock SHA-256 remains
`1a9e2d3ef91b972c50af82baa9dadf9707d1e978f211b734e157892d8dc64704`.

The actual layout changes only Google-font loader calls to `next/font/local`,
adding the two local source paths and explicit normal/100–900/swap options.
CSS variables, root classes, providers, navigation, demo banner, metadata and
child rendering are retained. No unrelated functional change was found.

## 3. Fonts, provenance and licensing

- Independently checked font binary hashes and `fc-scan`: **Geist** and **Geist
  Mono**, with normal variable-weight faces and Thin–Black instances. Layout and
  unchanged global CSS preserve the existing font variables and family usage.
- Provenance is documented as the prior local Next font output. I verified the
  actual family/variable metadata and source-to-served-body hashes. The historical
  build cache was excluded from preservation, so this is not an independently
  reproduced acquisition chain from that original cache.
- Retrieved [upstream Geist OFL.txt](https://raw.githubusercontent.com/vercel/geist-font/main/OFL.txt).
  It matches the bundled notice **byte-for-byte**, SHA-256
  `c683bfbcc7e087f5d37a54ef628f10387c451a83ddc459b151403a164ac46c90`.
  The SIL OFL 1.1 permits embedding/bundling subject to its conditions; the
  preserved source bundle retains the copyright and complete license. Keep these
  notices with any redistributed font copies; no production distribution is
  authorized by this review.
- Scoped application-code search found no remaining Google font import or Google
  font URL in the derivative. Retained denied-network build and browser asset
  evidence show no external font dependency on the exercised build/runtime paths.
  This is not a claim that every possible application external integration is gone.

**Font correction/licensing verdict:** supported for this preserved local candidate;
no licensing mismatch or font-related accepted-workflow regression demonstrated.

## 4. Retained build/runtime evidence — independently examined, not rerun

Verified all **49** qualification evidence checksum entries. Read the actual install,
build, focused-test, startup and network records and parsed all **56** journal rows.
Chronology is ordered; all **seven** stopped operator attempts remain recorded
alongside successful resumed checks, rather than being relabeled as passes.

| Qualification claim | Retained evidence supports |
| --- | --- |
| Locked Node 24 installation | Logs identify **Node 24.21.0 / npm 11.19.0**, fresh offline install of 566 packages after the disclosed initial cache miss. Source/build lock checks agree. This is not a clean vulnerability-audit claim. |
| Derivative production build | Actual Next **16.3.6** log: compilation, TypeScript, 66-route static generation and finalization succeed. The original denied-network Google-font failure is retained; original source remains unchanged. |
| Production frontend runtime | `runtime-node.json` records Node 24.21.0 and `NODE_ENV=production`; frontend startup and operator setup identify actual standalone output. Browser driver uses Node 26.6.0 separately; it is not the application runtime. |
| **23 frontend tests** | Retained Vitest output: four auth/session files, **23/23 passed**; layout ESLint command present with no reported errors. This focused suite is not all frontend coverage. |
| **13 isolation tests** | Actual xUnit output names **13/13 passed**, including real-gateway rejection before HTTP, accidental gateway registration, invalid demo classifications and target checks. These are mock/component isolation checks, not provider acceptance or production security verification. |
| Production-mode browser smoke | Journal supports signup/verification, R1,200 and distinct R600 payments, wrong amount/replay/member denial, pending versus Active, Area approval, sponsor/parent distinction, progress, authorized Area/central sales decisions and scope denials. |
| Versioned calculation / no payout | Persisted/readback R400 = R150 + R250, PlacementV2 and named terms/decision versions; repeat creates zero rows, post-cutoff newcomers excluded. Both Release and RecordPayment are denied with explicit demo details, with unchanged ledger/readback and null release/payment times. Four retained denial events cover two runs. |
| Runtime font delivery | Both successful font-readback events contain HTTP 200 bodies whose hashes equal independently hashed candidate assets; document fonts loaded, no JS/server errors or external requests recorded in those route checks. |
| Production-backend synthetic opt-in | Actual negative startup log refuses demo-in-Production before services start. Application backend runtime otherwise remains `AQGreenV2Demo`. A production frontend build does not imply production backend/use. |

The root R400 record is **Earned/unreleased**, with null financial hold reason.
Positive monthly-obligation Held behavior and the accepted **17/17** cases are
explicitly **carried forward**, not freshly rerun here or under Node 24 as seventeen
new acceptance cases. The examined regression evidence supports the report within
those stated boundaries.

## 5. Regression and focused independent-check result

Business orchestration, joining/approval/placement, role/sales review, commission
logic, persistence, mocks, API contracts and other UI component files are
byte-identical between original and derivative. No unknown-to-zero conversion,
V2 fallback, historical reinterpretation, authorization weakening, admin money
override or payout enablement is introduced by the font-only diff. Retained
production smoke exercises the affected shared layout around the main MVP paths.
No derivative-owned material regression was identified; this is not exhaustive
proof against every inherited or unexercised defect.

**Checks personally executed:** full source/manifest hashes, archive digests,
fresh independent extraction/inventory verification, exact five-path comparison,
49 evidence hashes, journal/ledger/font-readback assertions, fontconfig metadata,
upstream license hash and byte comparison, and scoped font-dependency search.
No build, application test suite, browser journey, database or payment operation
was rerun because no remaining material gap justified repeating that work.

Review-attempt corrections are disclosed in the [independent check record](../verification/aqgreen-v2-node24-independent-review-20261012.json):
archive-sidecar relative paths were initially checked from the wrong directory;
one audit assertion initially expected one payout-denial run instead of the two
retained runs; a guessed upstream LICENSE URL returned 404. Corrected checks passed
without changing source/evidence or accepting a financial fallback.

## 6. Remaining acceptance conditions and limits

1. Owner acceptance must name this exact derivative identity; the original's
   acceptance is neither transferred nor overwritten by the review.
2. Preserve both source archives, manifests and license notices. Requalify relevant
   behavior if the candidate changes; do not use mutable bare HEAD as its identity.
3. Local independent review establishes **engineering evidence, not a separately
   isolated production-security boundary**. No deployment, real Yoco/payout,
   production data/use, B6/B7, human competence, commercial or legal acceptance
   follows from this verdict. No clean security/dependency audit is claimed.

Proverbs 18:17 is applied through independent examination; Proverbs 11:1 through
exact identities, counts and truthful failure/evidence reporting; 1 Corinthians
4:1–5 through faithful stewardship of the accepted subject. Those are governing
obligations; technical conclusions rest on the checks and records above.

**A. VERIFIED — READY FOR OWNER ACCEPTANCE**
