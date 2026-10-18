# PB1 C0 selected-profile proposal

Version `B6S-PB1-C0/1.0.0-proposed.1`; all new selections below are PROPOSED.
No broader implementation or runtime-admission change is authorized here.

## 1. Exact scope and target

Target only PR109's normal compile-time fixture, SHA-256
`de4a14a5a10398a45676f866dbc1280a55172bcc4c6b210081b87945b560b9b9`.
Do not rebuild or substitute it. Do not use sanitizer or fault-injection executables
as the normal subject. The held-ELF launcher mechanics and existing local codecs
are reusable inputs, not an accepted controller or actual typed-manifest validator.

C0 is **controller-profile qualification using a test object**, not actual PB1
runtime admission. Synthetic fixture descriptors stay explicitly test-only in the
outer campaign/observation record and are never registered as actual accepted
source/supply/qualification manifests. Their wire realm remains the adopted literal;
that literal supplies no authority. Even C0 PASS cannot close the real P04 authority
and typed-manifest predicates, Q01–Q09/Q21, or permit an exit0 parser release.

Only actual configured owner/operator/examiner authority for this isolated fixture
campaign can authorize its future launch. No caller flag or self-hashed selection
file supplies that authority. The proposal's missing-role states deny execution.

## 2. Reuse inventory: mechanisms are not composed capabilities

| Retained component | Reuse without repeating old tests | Genuinely missing / unsafe substitution |
| --- | --- | --- |
| PR109 `record.[ch]`, `pb1.[ch]` | Accepted exact codecs, local bindings/accounting, partial-write and rejection behavior | Original typed-manifest validators/current authority and actual admission remain absent |
| PR109 `tests/launch.c` and pinned launcher | Held ELF/fexecve, CLOEXEC, resource/no-new-privileges mechanics | No fixed manifest-driven controller, deadline, namespace, full principal/custody or original observer |
| G `native-supervisor.c` | pidfd_send_signal, waitid(P_PIDFD), one-consuming-reap, signal/state/accounting source patterns | Its binary launches Bash with another argv/env, has no10-second deadline, and revokes force-child after READY; do not lift those old guards or claim it is the PB1 controller |
| Retained Bubblewrap/harness | Existing namespace/read-only-view witness approach; identified current bwrap bytes | Harness still has UNRESOLVED pins and explicit unauthorized refusal. Group forwarding does not prove --new-session/namespace cleanup; no current confinement qualification follows |
| U `successor-store` | Descriptor-relative object identity/open/readback, bounded IO, persistent original-process guard primitives | No protected provisioning, ancestry/mount baseline, absence-of-writers proof or complete current custody controller |
| U selected-supply/authority-snapshot/graph | Copied typed-source/capture/continuity correlations and retained read interfaces | Structure/correlation is not original recorder truth, acceptance, external owner continuity or a complete resolver |
| PR109 process observation harness | Raw attempt separation, source/result hashes and original failures | No protected independent kernel-observation/assessment ledger; its15-second test timeout is not the10-second profile |

G is published B6438852e; U is the separately preserved43-candidate worktree state,
not an integrated qualified release. These source patterns need reviewed adaptation,
not redevelopment of codecs and not transfer of their historical qualification to
a new controller. Customer11 and previous native/recovery suites remain untouched.

## 3. One proposed controller/confinement/observation arrangement

Select a **single native Linux parent controller/observer** for one owned launch,
reusing the pidfd/reap/held-ELF patterns. Its executable and source are MISSING,
not represented by a fabricated hash. It constructs exact adopted argv/env/FDs,
selects only the pinned fixture and fixed dependencies, and never accepts a caller
command, shell, executable path, provider credential or production DSN.

Use the identified Bubblewrap candidate for private user/PID/mount/network/IPC/UTS
views, under an owner-approved operator-controlled **disposable Linux x86-64
environment with no production data/credentials and no external network route**.
No such environment is assumed supplied. Privileged namespace setup requires
separate explicit operator authority; the parser remains non-root UID/GID65534 in
its final namespace, zero supplementary groups/capabilities and no-new-privileges.
The complete host/intermediate/final identity-map chain must be supplied and
approved; namespace UID0 is not silently equated with host root or qualification.

The new parent traces its owned setup chain and uses Linux ptrace syscall-entry/
exit observation/enforcement through termination. Setup execution is limited to the
pinned Bubblewrap and the pinned PR109held-ELF launcher at `/runtime/pb1-launcher`;
that tiny launcher remains a bootstrap mechanism, not the controller. Its inputs
are constructed by the parent, never a caller command. The exact setup-role native
policy/ownership chain must be examined before execution; no wildcard setup exec
or unobserved helper is allowed.

At the single **target-transition exec**, the parent observes held-FD execveat(6,
empty-path, exact target argv/env, AT_EMPTY_PATH), its exec stop and loader mapping
before releasing target userspace. FD6 is CLOEXEC and absent at target entry.
After that single approved exec, deny network, fork/clone/new exec, ptrace/process-
memory, credential/capability, namespace/mount and other unselected operations
**before kernel execution**. This mediator is MISSING implementation/qualification;
Bubblewrap alone does not supply it. No shell/process-group-only fallback is allowed.

The initial closed syscall selection is in SELECTION.json. Even listed syscalls
require argument/object constraints: opens are read-only against the explicit
private runtime/proc objects; writes only the established diagnostic/result objects;
fixed FDs0–5 cannot close/reassign, fcntl is query-only and writable shared source
mapping is denied; prlimit64 queries only; fcntl only F_GETFL/F_GETFD and arch_prctl
only ARCH_SET_FS/ARCH_GET_FS. Unknown syscalls/ABI, unexplained register changes,
lost trace stops or failed original observation deny continuation and acceptance.
The separately approved C05 register probe is explicit test instrumentation with
original register/denial evidence, not an exception available to ordinary requests.
The examiner must approve actual ABI/native decoding and enforcement before a run;
the list is not evidence of observed completeness. Never silently widen it to make
a campaign pass.

## 4. Immutable executable/loader/input custody

SELECTION.json records current exact candidates, including the parser's actual
PT_INTERP `/lib64/ld-linux-x86-64.so.2`, libcrypto.so.3, libc.so.6 and loader. Setup-
tool dependencies additionally include libcap, libselinux and libpcre2. Static
DT_NEEDED closure is not dynamic configuration/dlopen/kernel/provenance proof and
does not retroactively identify run8's loaded objects.

Proposed custody strategy: materialize each selected runtime/input object into an
owned sealed memfd, compare original bytes/size/hash, apply
F_SEAL_WRITE|F_SEAL_GROW|F_SEAL_SHRINK|F_SEAL_SEAL, reopen read-only and prove identical
object/seals. Bind the **same sealed inodes** at the declared private runtime paths;
do not substitute a new unsealed copy through a convenience bind-data operation.
Support for these exact mounts/objects is a required platform/custody proof, not an
assumption. If unsupported, BLOCK the selection; do not fall back to chmod alone.

Keep runtime root/empty cwd read-only. The target view contains only the pinned
fixture, pinned immutable bootstrap launcher, three parser dependencies, required
private proc view, inert /dev/null and exact inherited channels. The launcher file
cannot be re-executed after target entry. Host paths, /etc/ld.so.preload, loader cache, hwcaps
variants, external OpenSSL config/modules and unselected files are absent; an
unexpected lookup/mapping fails instead of reaching the host. The setup-tool TCB
and any actual configuration it consumes need their own examined exact closure.

FD3/4 are sealed, read-only, offset0; FD2/5 are fresh separate0600 private write-only
objects with4,096-byte bounds. All extra target FDs are closed. The parent retains
separate readback/source handles. Full original object/mount/seal/FD observations
must establish writer absence and ownership/lifetime; passing read-only FDs or a
local hash comparison is insufficient. No hostile-host-root guarantee is claimed:
the independently approved disposable host/kernel/operator is the explicit TCB.

## 5. Exact10-second deadline and terminal ownership

Use timerfd on **CLOCK_BOOTTIME**, absolute deadline T0+10,000,000,000ns. This counts
suspension, not just CPU time. T0 is after sealed inputs are verified and immediately
before creating any owned execution-capable child; launcher/setup/exec/stop time is
inside the budget. Unsupported clock/pidfd/native observation prevents launch.
Record the explicit clock ID/boot context under the new profile's observation
schema; do not relabel historical monotonic fields as BOOTTIME evidence.

The parent obtains stable pidfds for owned launcher/namespace-init/target before
releasing their controlled gates. No bare-PID reuse, process-group-only containment
or reset-on-progress timeout is accepted. CPU5s/AS128MiB/stack8MiB/FD16 remain hard
maxima as adopted; unknown/larger actual limits reject the profile.

Expiry irreversibly closes result acceptance at T0+10s and initiates pidfd SIGKILL
of the owned namespace-init/target plus termination/reaping of all owned helpers.
Record actual expiry/delivery/exit timestamps, errors and any overrun; general
Linux scheduling is not a hard-real-time termination proof. No late output is
accepted. A further2s is only a cleanup-observation budget, never extra permitted
parser execution. If disappearance/reaping/completeness cannot be proved, classify
UNDETERMINED/RECONCILIATION_REQUIRED and quarantine; do not report successful cleanup
or reuse the scope. Source/partial/evidence objects remain preserved.

Controller death must close the same owned scope through a verified kernel parent-
death/namespace chain, rearmed after any credential transitions and checked before
gate release. Retained pre-READY force and cooperative group cleanup are only source
patterns, not that proof. No new signal policy overwrites the accepted old supervisor.

## 6. Observer and examiner responsibilities

The parent records original raw kernel syscall/exec/exit data, namespace/principal/
map/FD/object/seal/limit facts, boot+BOOTTIME readings, exact source/release/view/
descriptor/input/output identities and owned-process inventory. Each attempt has
a new UUID/output/evidence object; static plans cannot hash future descriptors or
results. Preserve original failed/partial evidence and do not normalize it into a
success. Every unavailable observation is explicitly UNDETERMINED, never an
invented empty process set, zero time, accepted flag or complete custody proof.
Synthesized storage diagnostic codes are not original kernel errno observations.
The exact closed observation record/native layout/decoder contract is still a
required selection before implementing that recording role; the required-fact list
here is not a fabricated historical codec or an implementation-ready wire schema.

Before traces become readable facts, the independent examiner qualifies the exact
observer/native ABI parser against independently encoded byte/layout/known-answer
inputs. The examiner controls an original capture/readback route and independently
checks scope completeness, timestamps/cleanup, derivations and report identity.
The implementation's own ledger and a second local agent are not independent
acceptance. Parent observation and examiner acceptance are separate roles even if
one executable implements launch and raw capture.

Campaign PASS means only the explicitly observed C0 control predicate. The child
operation retains exit12/DEPENDENCY and actual runtime admission stays BLOCKED.
No producer/journal/root/BIND, representative/financial or D10 authority is supplied.
