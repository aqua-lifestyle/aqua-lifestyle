/* Linked only into the explicitly named fault-injection fixture executables. */
#include <errno.h>
#include <stddef.h>
#include <unistd.h>
ssize_t __real_read(int, void *, size_t);
ssize_t __real_write(int, const void *, size_t);
#ifdef B6S_FAULT_INPUT_READ
ssize_t __real___read_chk(int, void *, size_t, size_t);
ssize_t __wrap_read(int fd, void *bytes, size_t count) {
    if (fd == 4) { errno = EIO; return -1; }
    return __real_read(fd, bytes, count);
}
ssize_t __wrap___read_chk(int fd, void *bytes, size_t count, size_t capacity) {
    if (fd == 4) { errno = EIO; return -1; }
    return __real___read_chk(fd, bytes, count, capacity);
}
#endif
#ifdef B6S_FAULT_PARTIAL_WRITE
ssize_t __wrap_write(int fd, const void *bytes, size_t count) {
    static int partial;
    if (fd == 5) {
        if (!partial++) return __real_write(fd, bytes, count > 17 ? 17 : count);
        errno = EIO; return -1;
    }
    return __real_write(fd, bytes, count);
}
#endif
