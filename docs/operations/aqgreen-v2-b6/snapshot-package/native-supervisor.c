/* New B6 lineage: single-threaded lifecycle authority; no shell signal supervisor. */
#include <errno.h>
#include <fcntl.h>
#include <limits.h>
#include <poll.h>
#include <signal.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/prctl.h>
#include <sys/random.h>
#include <sys/signalfd.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/syscall.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <unistd.h>

#ifndef B6_PACKAGE_ROOT
#define B6_PACKAGE_ROOT "/tmp/aqgreen-b6-native-package"
#endif
#ifndef B6_OUTPUT_PARENT
#define B6_OUTPUT_PARENT "/tmp/aqgreen-b6-proof-output"
#endif
#ifndef B6_HARNESS
#define B6_HARNESS B6_PACKAGE_ROOT "/non-root-bwrap-proof-harness.sh"
#endif
#ifndef B6_TEST_PHASE
#define B6_TEST_PHASE ""
#endif

extern char **environ;
struct lifecycle {
    int signals, ready_fd, pidfd, output_fd;
    pid_t child;
    bool ready, reaped, group_open, acceptance_open, pre_cleanup;
    bool infrastructure_failure, handling_failure, publication_started;
    const char *cleanup_delivery;
    unsigned ints, terms, attempts, successes, esrch;
    unsigned long long parent_start, child_start;
    int child_code, raw_status, child_status, committed_status;
    char transaction[65], executable[PATH_MAX], output[PATH_MAX];
    struct stat executable_stat;
};
static struct lifecycle life = { .signals = -1, .ready_fd = -1, .pidfd = -1,
    .output_fd = -1, .child = -1, .acceptance_open = true };

static int pidfd_signal(int fd, int signal_number)
{
    return (int)syscall(SYS_pidfd_send_signal, fd, signal_number, NULL, 0);
}

static void publish(void);
static void accept_signals(void);
static void phase(const char *name);
static void fail(const char *reason) __attribute__((noreturn));

static int terminal_status(void)
{
    return life.terms ? 143 : life.ints ? 130 : life.infrastructure_failure ? 125 : life.child_status;
}

/* The only explicit force-child callsite. READY permanently revokes this path. */
static int force_before_ready(void)
{
    if (life.ready || life.reaped || life.pidfd < 0) { errno = EPERM; return -1; }
#ifdef B6_TESTING
    (void)dprintf(STDOUT_FILENO, "PRE_READY_FORCE ready=0 reaped=0\n");
#endif
    return pidfd_signal(life.pidfd, SIGKILL);
}

static void consume_child(void)
{
    if (life.reaped) fail("duplicate-reap");
    siginfo_t reaped = {0};
    int result;
    do { result = waitid(P_PIDFD, (id_t)life.pidfd, &reaped, WEXITED); } while (result < 0 && errno == EINTR);
    if (result < 0 || reaped.si_pid != life.child) fail("consume-reap");
    life.reaped = true;
    life.group_open = false; /* Irreversible, before any further callback or signal drain. */
    life.child_code = reaped.si_code;
    life.raw_status = reaped.si_status;
    if (reaped.si_code == CLD_EXITED) life.child_status = reaped.si_status;
    else if (reaped.si_code == CLD_KILLED || reaped.si_code == CLD_DUMPED) life.child_status = 128 + reaped.si_status;
    else fail("child-status");
    phase("REAPED");
}

static void fail(const char *reason)
{
    int saved = errno;
    life.infrastructure_failure = true;
    (void)dprintf(STDERR_FILENO, "B6_NATIVE_FAIL reason=%s errno=%d publication=UNCOMMITTED\n", reason, saved);
    /* Publication errors cannot be retried into a false COMMIT. Recursive errors
       never acquire a new cleanup authority or repeat a consuming reap. */
    if (life.handling_failure || life.publication_started) _exit(terminal_status());
    life.handling_failure = true;
    if (life.pidfd >= 0 && !life.reaped && !life.ready) {
        if (force_before_ready() < 0 && errno != ESRCH) _exit(terminal_status());
        consume_child();
    } else if (life.pidfd >= 0 && !life.reaped && life.ready) {
        if (!life.group_open || getpgid(life.child) != life.child) _exit(terminal_status());
        /* Cooperative cleanup is NOT synchronous lifecycle acceptance. No latch,
           accepted count or lifecycle-forward count is changed here. */
        if (kill(-life.child, SIGTERM) == 0) life.cleanup_delivery = "SENT";
        else if (errno == ESRCH) life.cleanup_delivery = "ESRCH";
        else _exit(terminal_status());
#ifdef B6_TESTING
        (void)dprintf(STDOUT_FILENO, "CLEANUP target=CHILD_GROUP pgid=%ld signal=TERM delivery=%s ints=%u terms=%u reaped=0\n",
            (long)life.child, life.cleanup_delivery, life.ints, life.terms);
#endif
        /* Only exact Bash is observed here. Known-member disappearance belongs
           to the external fixture/final observer, including after this reap. */
        consume_child();
    }
    if (life.ready && life.reaped) {
        publish();
    }
    _exit(terminal_status());
}

static unsigned long long starttime(pid_t pid)
{
    char path[64], buffer[4096];
    (void)snprintf(path, sizeof path, "/proc/%ld/stat", (long)pid);
    int fd = open(path, O_RDONLY | O_CLOEXEC);
    if (fd < 0) fail("proc-open");
    ssize_t size = read(fd, buffer, sizeof buffer - 1);
    (void)close(fd);
    if (size <= 0 || size == (ssize_t)sizeof buffer - 1) fail("proc-size");
    buffer[size] = '\0';
    char *at = strrchr(buffer, ')');
    if (!at || at[1] != ' ') fail("proc-format");
    at += 2;
    for (int field = 3; field < 22; ++field) {
        at = strchr(at, ' ');
        if (!at) fail("proc-fields");
        ++at;
    }
    errno = 0;
    char *end;
    unsigned long long value = strtoull(at, &end, 10);
    if (errno || !value || end == at || (*end != ' ' && *end != '\n')) fail("proc-starttime");
    return value;
}

static void path_custody(const char *path, bool directory, bool private_mode)
{
    if (path[0] != '/' || strlen(path) >= PATH_MAX || strstr(path, "//") ||
        strstr(path, "/../") || strstr(path, "/./")) fail("path-grammar");
    char copy[PATH_MAX];
    (void)snprintf(copy, sizeof copy, "%s", path);
    for (char *at = copy + 1; ; ++at) {
        if (*at != '/' && *at != '\0') continue;
        bool final = *at == '\0';
        char saved = *at;
        *at = '\0';
        struct stat st;
        if (lstat(copy, &st) < 0 || S_ISLNK(st.st_mode)) fail("path-symlink-or-missing");
        if (st.st_uid != 0 && st.st_uid != getuid()) fail("path-owner");
        if ((st.st_mode & 0022) && !(S_ISDIR(st.st_mode) && st.st_uid == 0 && (st.st_mode & S_ISVTX)))
            fail("path-writable");
        if (!final && !S_ISDIR(st.st_mode)) fail("path-component");
        if (final) {
            if (st.st_uid != getuid() || st.st_gid != getgid()) fail("leaf-owner");
            if (directory ? !S_ISDIR(st.st_mode) : (!S_ISREG(st.st_mode) || st.st_nlink != 1)) fail("leaf-type-links");
            if (private_mode && (st.st_mode & 0777) != 0700) fail("directory-mode");
            break;
        }
        *at = saved;
    }
}

static void increment(unsigned *value)
{
    if (*value == UINT_MAX) fail("signal-count-overflow");
    ++*value;
}

static void accept_signals(void)
{
    if (!life.acceptance_open) fail("closed-acceptance-call");
    for (;;) {
        struct signalfd_siginfo event;
        ssize_t size = read(life.signals, &event, sizeof event);
        if (size < 0 && errno == EAGAIN) return;
        if (size < 0 && errno == EINTR) continue;
        if (size != sizeof event || (event.ssi_signo != SIGINT && event.ssi_signo != SIGTERM)) fail("signalfd-read");
        if (event.ssi_signo == SIGTERM) increment(&life.terms); else increment(&life.ints);
#ifdef B6_TESTING
        (void)dprintf(STDOUT_FILENO, "ACCEPT signal=%u ready=%d reaped=%d\n", event.ssi_signo, life.ready, life.reaped);
#endif
        if (life.reaped) continue; /* Arbitration only; no numeric PID/PGID operation. */
        if (!life.ready) {
            if (force_before_ready() < 0 && errno != ESRCH) fail("pre-ready-pidfd");
            life.pre_cleanup = true;
            /* An unconsumed queued datagram cannot retroactively grant READY. */
            if (life.ready_fd >= 0) { (void)close(life.ready_fd); life.ready_fd = -1; }
        } else if (life.group_open) {
            /* pidfd + direct unreaped child anchor this exact numeric group. */
            if (getpgid(life.child) != life.child) fail("group-identity-changed");
            increment(&life.attempts);
            if (kill(-life.child, (int)event.ssi_signo) == 0) increment(&life.successes);
            else if (errno == ESRCH) increment(&life.esrch);
            else fail("group-forward");
        }
    }
}

static void phase(const char *name)
{
#ifdef B6_TESTING
    (void)dprintf(STDOUT_FILENO, "PHASE %s\n", name);
    if (strcmp(name, B6_TEST_PHASE) == 0) {
        (void)dprintf(STDOUT_FILENO, "HOLD %s\n", name);
        char command[8]; size_t used = 0;
        while (used < sizeof command) {
            ssize_t size = read(STDIN_FILENO, command + used, 1);
            if (size < 0 && errno == EINTR) continue;
            if (size != 1) _exit(125);
            if (command[used++] == '\n') break;
        }
        if (used == 5 && memcmp(command, "FAIL\n", 5) == 0 && life.acceptance_open) {
            accept_signals();
            fail("synthetic-generic-failure");
        }
        if (used != 8 || memcmp(command, "RELEASE\n", 8) != 0) _exit(125);
        (void)dprintf(STDOUT_FILENO, "RELEASE %s\n", name);
    }
#else
    (void)name;
#endif
}

static void readiness(void)
{
    char message[10];
    union { struct cmsghdr align; char bytes[CMSG_SPACE(sizeof(struct ucred))]; } control;
    struct iovec vector = { .iov_base = message, .iov_len = sizeof message };
    struct msghdr packet = { .msg_iov = &vector, .msg_iovlen = 1,
        .msg_control = control.bytes, .msg_controllen = sizeof control.bytes };
    ssize_t size = recvmsg(life.ready_fd, &packet, MSG_DONTWAIT);
    if (size < 0 && (errno == EAGAIN || errno == EINTR)) return;
    if (size == 0) {
        if (!life.ready && !life.pre_cleanup) fail("ready-eof");
        (void)close(life.ready_fd); life.ready_fd = -1;
        return;
    }
    if (life.ready || life.pre_cleanup || size != 9 || memcmp(message, "B6READY1\n", 9) != 0 ||
        (packet.msg_flags & (MSG_TRUNC | MSG_CTRUNC))) fail("ready-message");
    struct cmsghdr *header = CMSG_FIRSTHDR(&packet);
    if (!header || header->cmsg_level != SOL_SOCKET || header->cmsg_type != SCM_CREDENTIALS ||
        header->cmsg_len != CMSG_LEN(sizeof(struct ucred)) || CMSG_NXTHDR(&packet, header)) fail("ready-credentials-record");
    struct ucred credential;
    memcpy(&credential, CMSG_DATA(header), sizeof credential);
    if (credential.pid != life.child || credential.uid != getuid() || credential.gid != getgid()) fail("ready-credentials");
    if (getpgid(life.child) != life.child) fail("ready-pgid");
    life.ready = true;
    life.group_open = true;
    phase("RUNNING");
}

static void publish(void)
{
    phase("PRECOMMIT");
    accept_signals();
    const char *source = life.terms ? "TERM" : life.ints ? "INT" : life.infrastructure_failure ? "INFRASTRUCTURE" : "CHILD";
    const char *latch = life.terms ? "TERM" : life.ints ? "INT" : "NONE";
    life.committed_status = terminal_status();
    life.publication_started = true;
    char pgid[32], ready_pid[32], uid[32], gid[32];
    (void)snprintf(pgid, sizeof pgid, "%ld", (long)life.child);
    (void)snprintf(ready_pid, sizeof ready_pid, "%ld", (long)life.child);
    (void)snprintf(uid, sizeof uid, "%lu", (unsigned long)getuid());
    (void)snprintf(gid, sizeof gid, "%lu", (unsigned long)getgid());
    int fd = openat(life.output_fd, ".terminal-stage", O_WRONLY | O_CREAT | O_EXCL | O_NOFOLLOW | O_CLOEXEC, 0600);
    if (fd < 0) fail("terminal-stage-open");
    int written = dprintf(fd,
        "protocol\tB6_NATIVE_TERMINAL_FRESH_V1\ntransaction_id\t%s\noutput_dir\t%s\n"
        "supervisor_pid\t%ld\nsupervisor_starttime\t%llu\nsupervisor_executable\t%s\n"
        "supervisor_device\t%llu\nsupervisor_inode\t%llu\nchild_pid\t%ld\nchild_starttime\t%llu\n"
        "child_pgid\t%s\nready_ack\t%s\nready_pid\t%s\nready_uid\t%s\nready_gid\t%s\n"
        "child_code\t%d\nchild_raw_status\t%d\nchild_status\t%d\nchild_reaped_count\t1\n"
        "accepted_int_count\t%u\naccepted_term_count\t%u\ngroup_attempts\t%u\ngroup_successes\t%u\ngroup_esrch\t%u\n"
        "group_authority\tCLOSED_AFTER_REAP\npre_ready_cleanup\t%s\nlatch\t%s\nterminal_source\t%s\n"
        "infrastructure_failure\t%s\ncleanup_target\t%s\ncleanup_signal\t%s\ncleanup_delivery\t%s\n"
        "committed_status\t%d\nlifecycle_acceptance\tCLOSED\ncommit_state\tCOMMITTED\ncommit_mechanism\tRENAME_NOREPLACE\n",
        life.transaction, life.output, (long)getpid(), life.parent_start, life.executable,
        (unsigned long long)life.executable_stat.st_dev, (unsigned long long)life.executable_stat.st_ino,
        (long)life.child, life.child_start, life.ready ? pgid : "UNAVAILABLE", life.ready ? "B6READY1" : "NOT_RECEIVED",
        life.ready ? ready_pid : "UNAVAILABLE", life.ready ? uid : "UNAVAILABLE", life.ready ? gid : "UNAVAILABLE",
        life.child_code, life.raw_status, life.child_status, life.ints, life.terms, life.attempts, life.successes, life.esrch,
        life.pre_cleanup ? "PIDFD_EXACT_CHILD" : "NOT_USED", latch, source,
        life.infrastructure_failure ? "YES" : "NO", life.cleanup_delivery ? "CHILD_GROUP" : "NONE",
        life.cleanup_delivery ? "TERM" : "NONE", life.cleanup_delivery ? life.cleanup_delivery : "NOT_REQUESTED",
        life.committed_status);
    if (written < 0 || fsync(fd) < 0 || close(fd) < 0) fail("terminal-stage-write");
    if (syscall(SYS_renameat2, life.output_fd, ".terminal-stage", life.output_fd,
        "native-supervisor-terminal.tsv", RENAME_NOREPLACE) < 0) fail("terminal-publication");
    life.acceptance_open = false; /* The successful rename is the COMMIT linearization point. */
    phase("COMMITTED");
    _exit(life.committed_status);
}

int main(int argc, char **argv)
{
    sigset_t lifecycle_set;
    if (sigemptyset(&lifecycle_set) < 0 || sigaddset(&lifecycle_set, SIGINT) < 0 ||
        sigaddset(&lifecycle_set, SIGTERM) < 0 || sigprocmask(SIG_BLOCK, &lifecycle_set, NULL) < 0) fail("signal-mask");
    struct sigaction disposition = { .sa_handler = SIG_DFL };
    (void)sigemptyset(&disposition.sa_mask);
    if (sigaction(SIGINT, &disposition, NULL) < 0 || sigaction(SIGTERM, &disposition, NULL) < 0) fail("signal-disposition");
    if (sigaction(SIGCHLD, &disposition, NULL) < 0 || sigaction(SIGPIPE, &disposition, NULL) < 0) fail("child-disposition");
    if (getuid() == 0 || getgid() == 0 || getuid() != geteuid() || getgid() != getegid()) fail("ordinary-user");
    if (argc != 3 || strcmp(argv[1], "--output-dir") != 0) fail("exact-argv");
    if (!environ[0] || !environ[1] || environ[2] || strcmp(environ[0], "PATH=/usr/bin:/bin") != 0 ||
        strcmp(environ[1], "LC_ALL=C") != 0) fail("exact-environment");
    if (prctl(PR_SET_NO_NEW_PRIVS, 1, 0, 0, 0) < 0) fail("no-new-privileges");
    if (syscall(SYS_close_range, 3U, UINT_MAX, 0) < 0) fail("close-inherited-descriptors");
    umask(0077);
    path_custody(B6_PACKAGE_ROOT, true, true);
    path_custody(B6_HARNESS, false, false);
    path_custody(B6_OUTPUT_PARENT, true, true);
    ssize_t executable_length = readlink("/proc/self/exe", life.executable, sizeof life.executable - 1);
    if (executable_length <= 0 || executable_length == sizeof life.executable - 1) fail("self-executable");
    life.executable[executable_length] = '\0';
    path_custody(life.executable, false, false);
    if (stat("/proc/self/exe", &life.executable_stat) < 0) fail("self-stat");
    size_t parent_length = strlen(B6_OUTPUT_PARENT);
    if (strncmp(argv[2], B6_OUTPUT_PARENT, parent_length) || argv[2][parent_length] != '/' ||
        !argv[2][parent_length + 1] || strchr(argv[2] + parent_length + 1, '/') ||
        strcmp(argv[2] + parent_length + 1, ".") == 0 || strcmp(argv[2] + parent_length + 1, "..") == 0 ||
        strlen(argv[2]) >= sizeof life.output) fail("output-path");
    for (const char *at = argv[2]; *at; ++at) if (*at < 33 || *at > 126) fail("output-characters");
    (void)snprintf(life.output, sizeof life.output, "%s", argv[2]);
    int parent = open(B6_OUTPUT_PARENT, O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC);
    if (parent < 0 || mkdirat(parent, argv[2] + parent_length + 1, 0700) < 0) fail("new-output-leaf");
    life.output_fd = openat(parent, argv[2] + parent_length + 1, O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC);
    (void)close(parent);
    if (life.output_fd < 0) fail("output-open");
    unsigned char random[32];
    if (getrandom(random, sizeof random, 0) != sizeof random) fail("transaction-random");
    for (size_t i = 0; i < sizeof random; ++i) (void)snprintf(life.transaction + i * 2, 3, "%02x", random[i]);
    life.parent_start = starttime(getpid());
    life.signals = signalfd(-1, &lifecycle_set, SFD_CLOEXEC | SFD_NONBLOCK);
    if (life.signals < 0) fail("signalfd-open");
    int ready[2], gate[2], credentials = 1;
    if (socketpair(AF_UNIX, SOCK_SEQPACKET | SOCK_CLOEXEC | SOCK_NONBLOCK, 0, ready) < 0 ||
        setsockopt(ready[0], SOL_SOCKET, SO_PASSCRED, &credentials, sizeof credentials) < 0 ||
        pipe2(gate, O_CLOEXEC) < 0) fail("child-channels");
    pid_t supervisor = getpid();
    life.child = fork();
    if (life.child < 0) fail("fork");
    if (life.child == 0) {
        (void)close(gate[1]); (void)close(ready[0]);
        if (prctl(PR_SET_PDEATHSIG, SIGKILL) < 0 || getppid() != supervisor || setpgid(0, 0) < 0) _exit(125);
        char release;
        if (read(gate[0], &release, 1) != 1 || release != 'R') _exit(125);
        (void)close(gate[0]);
        if (dup2(ready[1], 3) < 0 || fcntl(3, F_SETFD, 0) < 0) _exit(125);
        if (ready[1] != 3) (void)close(ready[1]);
        sigset_t empty;
        (void)sigemptyset(&empty);
        for (int signal_number = 1; signal_number < NSIG; ++signal_number)
            if (signal_number != SIGKILL && signal_number != SIGSTOP) (void)sigaction(signal_number, &disposition, NULL);
        if (sigprocmask(SIG_SETMASK, &empty, NULL) < 0) _exit(125);
        char *const child_environment[] = { "PATH=/usr/bin:/bin", "LC_ALL=C", NULL };
        char *const child_argv[] = { "/bin/bash", "-p", B6_HARNESS, "--output-dir", life.output,
            "--transaction-id", life.transaction, NULL };
        execve("/bin/bash", child_argv, child_environment);
        _exit(125);
    }
    (void)close(gate[0]); (void)close(ready[1]);
    life.ready_fd = ready[0];
    life.pidfd = (int)syscall(SYS_pidfd_open, life.child, 0);
    if (life.pidfd < 0) fail("pidfd-open");
    life.child_start = starttime(life.child);
    phase("STARTING");
    accept_signals();
    if (!life.pre_cleanup && write(gate[1], "R", 1) != 1) fail("child-release");
    (void)close(gate[1]);
    for (;;) {
        accept_signals();
        if (life.ready_fd >= 0) readiness();
        siginfo_t observation = {0};
        if (waitid(P_PIDFD, (id_t)life.pidfd, &observation, WEXITED | WNOHANG | WNOWAIT) < 0) fail("observe-child");
        if (observation.si_pid) {
            phase("OBSERVED");
            accept_signals();
            /* Drain queued READY/duplicates before consuming the only reap. */
            if (life.ready_fd >= 0) readiness();
            if (life.ready_fd >= 0) readiness();
            if (!life.ready && !life.pre_cleanup) fail("child-without-ready");
            consume_child();
            publish();
        }
        struct pollfd descriptors[3] = { {life.signals, POLLIN, 0}, {life.ready_fd, POLLIN, 0}, {life.pidfd, POLLIN, 0} };
        if (poll(descriptors, 3, -1) < 0 && errno != EINTR) fail("poll");
    }
}
