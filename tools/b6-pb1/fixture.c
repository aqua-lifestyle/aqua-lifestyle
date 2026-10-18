#ifndef B6S_TEST_FIXTURE
#error "This entrypoint is fixture-only; no runtime controller is implemented."
#endif
#include "pb1.h"
#include <dirent.h>
#include <errno.h>
#include <fcntl.h>
#include <openssl/evp.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <sys/sysmacros.h>
#include <unistd.h>

extern char **environ;
static int diagnostic(const char *code) {
    /* Fixed category literals only. No input, config, path or errno text. */
    size_t length = strlen(code);
    if (length > 64) return 0;
    char bytes[65]; memcpy(bytes, code, length); bytes[length++] = '\n';
    size_t done = 0;
    while (done < length) {
        ssize_t n = write(2, bytes + done, length - done);
        if (n < 0 && errno == EINTR) continue;
        if (n <= 0 || (size_t)n > length - done) return 0;
        done += (size_t)n;
    }
    return 1;
}
static int stop(int exit_code, const char *reason) { (void)diagnostic(reason); return exit_code; }
static struct b6s_span text(const char *s) { return (struct b6s_span){(const unsigned char *)s, strlen(s)}; }
static int same_object(const struct stat *a, const struct stat *b) {
    return a->st_dev == b->st_dev && a->st_ino == b->st_ino;
}
static int unchanged(const struct stat *a, const struct stat *b) {
    return same_object(a, b) && a->st_mode == b->st_mode && a->st_uid == b->st_uid &&
        a->st_gid == b->st_gid && a->st_size == b->st_size &&
        a->st_mtim.tv_sec == b->st_mtim.tv_sec && a->st_mtim.tv_nsec == b->st_mtim.tv_nsec &&
        a->st_ctim.tv_sec == b->st_ctim.tv_sec && a->st_ctim.tv_nsec == b->st_ctim.tv_nsec;
}
static enum b6s_status check_fds(void) {
    struct stat st[6];
    for (int i = 0; i < 6; i++) if (fstat(i, &st[i]) < 0) return B6S_UNDETERMINED;
    /* The fixture supports the explicit /dev/null inert-endpoint subprofile. */
    for (int i = 0; i < 2; i++)
        if (!S_ISCHR(st[i].st_mode) || major(st[i].st_rdev) != 1 || minor(st[i].st_rdev) != 3) return B6S_UNSUPPORTED;
    for (int i = 2; i < 6; i++) {
        int flags = fcntl(i, F_GETFL);
        if (flags < 0) return B6S_UNDETERMINED;
        if (!S_ISREG(st[i].st_mode) || (flags & O_ACCMODE) != (i == 3 || i == 4 ? O_RDONLY : O_WRONLY)) return B6S_INVALID;
        if ((i == 3 || i == 4 || i == 5) && lseek(i, 0, SEEK_CUR) != 0) return B6S_INVALID;
    }
    if (st[2].st_size != 0 || lseek(2, 0, SEEK_CUR) != 0 ||
        (st[2].st_mode & 077) || (st[5].st_mode & 077) ||
        st[2].st_uid != geteuid() || st[5].st_uid != geteuid()) return B6S_INVALID;
    if (st[5].st_size != 0 || same_object(&st[5], &st[3]) || same_object(&st[5], &st[4]) ||
        same_object(&st[2], &st[3]) || same_object(&st[2], &st[4]) || same_object(&st[2], &st[5])) return B6S_INVALID;
    DIR *directory = opendir("/proc/self/fd");
    if (!directory) return B6S_UNDETERMINED;
    int own = dirfd(directory), extra = 0, saved_error = 0;
    errno = 0;
    struct dirent *entry;
    while ((entry = readdir(directory))) {
        char *end; long number = strtol(entry->d_name, &end, 10);
        if (*entry->d_name && !*end && number >= 6 && number != own) extra = 1;
        errno = 0;
    }
    saved_error = errno;
    if (closedir(directory) < 0 || saved_error) return B6S_UNDETERMINED;
    return extra ? B6S_INVALID : B6S_CHECKED;
}
static enum b6s_status read_bounded(int fd, size_t limit, unsigned char **out,
                                   size_t *length, const char **reason) {
    *out = NULL; *length = 0;
    struct stat before, after;
    if (fstat(fd, &before) < 0) { *reason = "IO"; return B6S_UNDETERMINED; }
    if (before.st_size < 0 || (uint64_t)before.st_size > limit) { *reason = "LIMIT"; return B6S_INVALID; }
    unsigned char *bytes = malloc(limit + 1);
    if (!bytes) { *reason = "RESOURCE"; return B6S_UNDETERMINED; }
    size_t used = 0;
    for (;;) {
        ssize_t n = read(fd, bytes + used, limit + 1 - used);
        if (n < 0 && errno == EINTR) continue;
        if (n < 0) { free(bytes); *reason = "IO"; return B6S_UNDETERMINED; }
        if (!n) break;
        used += (size_t)n;
        if (used > limit) { free(bytes); *reason = "LIMIT"; return B6S_INVALID; }
    }
    if (fstat(fd, &after) < 0) { free(bytes); *reason = "IO"; return B6S_UNDETERMINED; }
    if (!unchanged(&before, &after) || (uint64_t)after.st_size != used) {
        free(bytes); *reason = "CONTEXT"; return B6S_UNDETERMINED;
    }
    *out = bytes; *length = used;
    return B6S_CHECKED;
}
static enum b6s_status self_hash(char out[65]) {
    int fd = open("/proc/self/exe", O_RDONLY | O_CLOEXEC);
    if (fd < 0) return B6S_UNDETERMINED;
    EVP_MD_CTX *ctx = EVP_MD_CTX_new();
    unsigned char buffer[8192], digest[32]; unsigned count = 0;
    int ok = ctx && EVP_DigestInit_ex(ctx, EVP_sha256(), NULL) == 1;
    while (ok) {
        ssize_t n = read(fd, buffer, sizeof(buffer));
        if (n < 0 && errno == EINTR) continue;
        if (n < 0) { ok = 0; break; }
        if (!n) break;
        ok = EVP_DigestUpdate(ctx, buffer, (size_t)n) == 1;
    }
    if (ok) ok = EVP_DigestFinal_ex(ctx, digest, &count) == 1 && count == 32;
    EVP_MD_CTX_free(ctx);
    if (close(fd) < 0) ok = 0;
    if (!ok) { out[0] = 0; return B6S_UNDETERMINED; }
    for (size_t i = 0; i < 32; i++) (void)snprintf(out + 2 * i, 3, "%02x", digest[i]);
    return B6S_CHECKED;
}
static int emit(const struct b6s_record *d, const unsigned char *input, size_t length,
                 const char *outcome, const char *reason) {
    char descriptor_hash[65], input_hash[65], count[32];
    struct b6s_span descriptor = b6s_bytes(d);
    if (b6s_sha256(descriptor.bytes, descriptor.length, descriptor_hash) != B6S_CHECKED ||
        b6s_sha256(input, length, input_hash) != B6S_CHECKED) return stop(12, "RESOURCE");
    int n = snprintf(count, sizeof(count), "%zu", length);
    if (n <= 0 || (size_t)n >= sizeof(count)) return stop(12, "LIMIT");
    struct b6s_span fields[] = {text(B6S_RESULT_SCHEMA), text("B6S1"), text(descriptor_hash),
        text(input_hash), text(count), b6s_value(d, 11), text(outcome), text(reason)};
    unsigned char result[B6S_RESULT_MAX]; size_t bytes = 0;
    if (b6s_encode(B6S_RESULT, fields, 8, result, sizeof(result), &bytes) != B6S_CHECKED) return stop(12, "RESOURCE");
    if (b6s_write_result(5, result, bytes) != B6S_CHECKED) return stop(12, "IO");
    return !strcmp(outcome, "INVALID") ? 10 : !strcmp(outcome, "UNSUPPORTED") ? 11 : 12;
}
int main(int argc, char **argv) {
    /* No writes, including diagnostics, until the FD2 endpoint is established
     * as the fixture's separate private channel. A rejected FD2 can alias input. */
    if (b6s_check_argv_env(argc, argv, environ) != B6S_CHECKED) return 10;
    enum b6s_status status = check_fds();
    if (status != B6S_CHECKED) return status == B6S_INVALID ? 10 : 12;
    if (!diagnostic("FIXTURE_ONLY")) return 12;
    unsigned char *descriptor = NULL, *input = NULL;
    size_t descriptor_length = 0, input_length = 0;
    const char *reason = "IO";
    status = read_bounded(3, B6S_DESCRIPTOR_MAX, &descriptor, &descriptor_length, &reason);
    if (status != B6S_CHECKED) return stop(status == B6S_INVALID ? 10 : 12, reason);
    struct b6s_record *d = NULL;
    status = b6s_decode(B6S_DESCRIPTOR, descriptor, descriptor_length, &d);
    free(descriptor);
    if (status != B6S_CHECKED) return stop(status == B6S_INVALID ? 10 : status == B6S_UNSUPPORTED ? 11 : 12,
                                           status == B6S_UNSUPPORTED ? "SCHEMA" : "DESCRIPTOR");
    status = read_bounded(4, B6S_INPUT_MAX, &input, &input_length, &reason);
    if (status != B6S_CHECKED) { b6s_destroy(d); return stop(status == B6S_INVALID ? 10 : 12, reason); }
    char actual_hash[65], executable_hash[65], schema[65]; uint64_t expected;
    int exit_code;
    if (b6s_sha256(input, input_length, actual_hash) != B6S_CHECKED || self_hash(executable_hash) != B6S_CHECKED)
        exit_code = stop(12, "RESOURCE");
    else if (!b6s_uint(b6s_value(d, 18), &expected) || expected != input_length ||
             !b6s_equal(b6s_value(d, 17), actual_hash)) exit_code = emit(d, input, input_length, "INVALID", "INPUT_DIGEST");
    else if (!b6s_equal(b6s_value(d, 3), B6S_PACKAGE_SHA256) ||
             !b6s_equal(b6s_value(d, 4), B6S_INVOCATION_SPEC_SHA256) ||
             !b6s_equal(b6s_value(d, 8), executable_hash)) exit_code = emit(d, input, input_length, "INVALID", "CONTEXT");
    else {
        struct b6s_span name = b6s_value(d, 11);
        memcpy(schema, name.bytes, name.length); schema[name.length] = 0;
        struct b6s_record *parsed = NULL;
        status = !b6s_equal(b6s_value(d, 13), "CANONICAL_TEXT") || !b6s_schema_supported(schema) ? B6S_UNSUPPORTED :
            b6s_decode(!strcmp(schema, B6S_DESCRIPTOR_SCHEMA) ? B6S_DESCRIPTOR : B6S_RESULT, input, input_length, &parsed);
        b6s_destroy(parsed);
        if (status == B6S_INVALID) exit_code = emit(d, input, input_length, "INVALID", "GRAMMAR");
        else if (status == B6S_UNSUPPORTED) exit_code = emit(d, input, input_length, "UNSUPPORTED", "SCHEMA");
        else if (status == B6S_UNDETERMINED) exit_code = emit(d, input, input_length, "UNDETERMINED", "RESOURCE");
        else {
            /* No modeled flags, matching hashes or fixture success can replace
             * actual controller/typed-manifest/custody/confinement admission. */
            if (b6s_runtime_admission() != B6S_UNDETERMINED) exit_code = stop(12, "CONTEXT");
            else exit_code = emit(d, input, input_length, "UNDETERMINED", "DEPENDENCY");
        }
    }
    free(input); b6s_destroy(d);
    return exit_code;
}
