#include "pb1.h"
#include <errno.h>
#include <fcntl.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

static struct b6s_span text(const char *s) {
    return s ? (struct b6s_span){(const unsigned char *)s, strlen(s)} : (struct b6s_span){NULL, 0};
}
static int same(struct b6s_span a, struct b6s_span b) {
    return a.bytes && b.bytes && a.length == b.length && !memcmp(a.bytes, b.bytes, a.length);
}
enum b6s_status b6s_check_argv_env(int argc, char *const argv[], char *const env[]) {
    static const char *const expected[] = {
        "b6-pb1-v1", "--request-fd", "3", "--input-fd", "4", "--result-fd", "5"
    };
    if (argc != 7 || !argv || !env) return B6S_INVALID;
    for (size_t i = 0; i < 7; i++) if (!argv[i] || strcmp(argv[i], expected[i])) return B6S_INVALID;
    if (argv[7] || !env[0] || strcmp(env[0], "LC_ALL=C") || !env[1] ||
        strcmp(env[1], "TZ=UTC") || env[2]) return B6S_INVALID;
    return B6S_CHECKED;
}
enum b6s_status b6s_check_bindings(const struct b6s_record *d,
                                  const struct b6s_subject *subjects, size_t count,
                                  const char *schema, const char *layout, struct b6s_span input) {
    static const size_t fields[B6S_ROLE_COUNT] = {3,4,5,6,7,8,9,10,12,14,15,16};
    if (!d || b6s_kind(d) != B6S_DESCRIPTOR) return B6S_INVALID;
    if (!b6s_equal(b6s_value(d, 3), B6S_PACKAGE_SHA256) ||
        !b6s_equal(b6s_value(d, 4), B6S_INVOCATION_SPEC_SHA256)) return B6S_INVALID;
    if (!schema || !layout || !input.bytes || !subjects) return B6S_UNDETERMINED;
    if (!b6s_schema_supported(schema) || !b6s_equal(b6s_value(d, 13), "CANONICAL_TEXT") ||
        strcmp(layout, "CANONICAL_TEXT")) return B6S_UNSUPPORTED;
    char requested[65]; struct b6s_span selected = b6s_value(d, 11);
    memcpy(requested, selected.bytes, selected.length); requested[selected.length] = 0;
    if (!b6s_schema_supported(requested)) return B6S_UNSUPPORTED;
    if (strcmp(requested, schema)) return B6S_INVALID;
    if (count < B6S_ROLE_COUNT) return B6S_UNDETERMINED;
    if (count != B6S_ROLE_COUNT) return B6S_INVALID;
    unsigned seen = 0;
    for (size_t i = 0; i < count; i++) {
        const struct b6s_subject *s = subjects + i;
        if ((unsigned)s->role >= B6S_ROLE_COUNT || (seen & (1U << (unsigned)s->role))) return B6S_INVALID;
        seen |= 1U << (unsigned)s->role;
        if (!s->bytes.bytes) return B6S_UNDETERMINED;
        if (strnlen(s->expected_sha256, 65) != 64 || !b6s_hash(text(s->expected_sha256))) return B6S_INVALID;
        if (s->role == B6S_CONFIGURATION && s->bytes.length > B6S_CONFIGURATION_MAX) return B6S_INVALID;
        char actual[65];
        if (b6s_sha256(s->bytes.bytes, s->bytes.length, actual) != B6S_CHECKED) return B6S_UNDETERMINED;
        if (strcmp(actual, s->expected_sha256) || !b6s_equal(b6s_value(d, fields[s->role]), actual)) return B6S_INVALID;
    }
    char input_hash[65]; uint64_t input_count;
    if (b6s_sha256(input.bytes, input.length, input_hash) != B6S_CHECKED) return B6S_UNDETERMINED;
    if (!b6s_uint(b6s_value(d, 18), &input_count) || input_count != input.length ||
        !b6s_equal(b6s_value(d, 17), input_hash)) return B6S_INVALID;
    return B6S_CHECKED;
}
enum b6s_status b6s_check_output(const struct b6s_record *d, struct b6s_span input,
                                struct b6s_span output, int exit_code, enum b6s_operation *operation) {
    if (!operation) return B6S_INVALID;
    *operation = B6S_NO_OPERATION;
    if (!d || b6s_kind(d) != B6S_DESCRIPTOR) return B6S_INVALID;
    if (!input.bytes || !output.bytes || !output.length ||
        (exit_code != 0 && exit_code != 10 && exit_code != 11 && exit_code != 12)) return B6S_UNDETERMINED;
    if (exit_code == 0 && (!b6s_equal(b6s_value(d, 3), B6S_PACKAGE_SHA256) ||
        !b6s_equal(b6s_value(d, 4), B6S_INVOCATION_SPEC_SHA256) ||
        !b6s_equal(b6s_value(d, 13), "CANONICAL_TEXT"))) return B6S_INVALID;
    struct b6s_record *r = NULL;
    enum b6s_status status = b6s_decode(B6S_RESULT, output.bytes, output.length, &r);
    if (status != B6S_CHECKED) return status;
    char dh[65], ih[65]; uint64_t observed;
    struct b6s_span descriptor = b6s_bytes(d);
    if (b6s_sha256(descriptor.bytes, descriptor.length, dh) != B6S_CHECKED ||
        b6s_sha256(input.bytes, input.length, ih) != B6S_CHECKED) { b6s_destroy(r); return B6S_UNDETERMINED; }
    int valid = b6s_result_exit(r) == exit_code && b6s_equal(b6s_value(r, 2), dh) &&
        b6s_equal(b6s_value(r, 3), ih) && b6s_uint(b6s_value(r, 4), &observed) && observed == input.length &&
        same(b6s_value(r, 5), b6s_value(d, 11));
    uint64_t expected_count;
    int digest_matches = b6s_uint(b6s_value(d, 18), &expected_count) && expected_count == input.length &&
        b6s_equal(b6s_value(d, 17), ih);
    if (exit_code == 0 && !digest_matches) valid = 0;
    if (b6s_equal(b6s_value(r, 7), "INPUT_DIGEST") && digest_matches) valid = 0;
    if (exit_code == 0) {
        char schema[65]; struct b6s_span name = b6s_value(d, 11);
        memcpy(schema, name.bytes, name.length); schema[name.length] = 0;
        struct b6s_record *parsed = NULL;
        enum b6s_status parsed_status = !strcmp(schema, B6S_DESCRIPTOR_SCHEMA)
            ? b6s_decode(B6S_DESCRIPTOR, input.bytes, input.length, &parsed)
            : !strcmp(schema, B6S_RESULT_SCHEMA) ? b6s_decode(B6S_RESULT, input.bytes, input.length, &parsed) : B6S_UNSUPPORTED;
        if (parsed_status == B6S_UNDETERMINED) {
            b6s_destroy(parsed); b6s_destroy(r); return B6S_UNDETERMINED;
        }
        if (parsed_status != B6S_CHECKED) valid = 0;
        b6s_destroy(parsed);
    }
    if (valid) *operation = exit_code == 0 ? B6S_REPRESENTATION : exit_code == 10 ? B6S_OPERATION_INVALID :
        exit_code == 11 ? B6S_OPERATION_UNSUPPORTED : B6S_OPERATION_UNDETERMINED;
    b6s_destroy(r);
    return valid ? B6S_CHECKED : B6S_INVALID;
}
enum b6s_status b6s_runtime_admission(void) { return B6S_UNDETERMINED; }
enum b6s_status b6s_write_result(int fd, const unsigned char *bytes, size_t length) {
    struct b6s_record *r = NULL;
    enum b6s_status status = b6s_decode(B6S_RESULT, bytes, length, &r);
    b6s_destroy(r);
    if (status != B6S_CHECKED) return status;
    struct stat st; int flags = fcntl(fd, F_GETFL);
    if (flags < 0 || fstat(fd, &st) < 0) return B6S_UNDETERMINED;
    if ((flags & O_ACCMODE) != O_WRONLY || !S_ISREG(st.st_mode) || st.st_size != 0 || lseek(fd, 0, SEEK_CUR) != 0)
        return B6S_INVALID;
    size_t written = 0;
    while (written < length) {
        ssize_t n = write(fd, bytes + written, length - written);
        if (n < 0 && errno == EINTR) continue;
        if (n <= 0 || (size_t)n > length - written) return B6S_UNDETERMINED;
        written += (size_t)n;
    }
    return B6S_CHECKED;
}
