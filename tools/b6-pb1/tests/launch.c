/* Test-only held-ELF launcher. It establishes argv/env/FD/resource observations,
 * NOT a qualified loader closure, namespace, custody or controller authority. */
#include <fcntl.h>
#include <sys/prctl.h>
#include <sys/resource.h>
#include <unistd.h>
extern char **environ;
static int limit(int resource, rlim_t value) {
    const struct rlimit bounds = {value, value};
    return setrlimit(resource, &bounds);
}
int main(int argc, char **argv) {
    if (argc < 2 || fcntl(6, F_SETFD, FD_CLOEXEC) < 0 ||
        prctl(PR_SET_NO_NEW_PRIVS, 1, 0, 0, 0) < 0 ||
        limit(RLIMIT_CPU, 5) || limit(RLIMIT_AS, 128U * 1024U * 1024U) ||
        limit(RLIMIT_STACK, 8U * 1024U * 1024U) || limit(RLIMIT_NOFILE, 16)) _exit(125);
    fexecve(6, &argv[1], environ);
    _exit(125);
}
