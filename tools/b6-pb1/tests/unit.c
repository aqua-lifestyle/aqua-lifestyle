#include "pb1.h"
#include <errno.h>
#include <fcntl.h>
#include <openssl/evp.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

static unsigned checks, groups;
#define CHECK(c) do { checks++; if (!(c)) { fprintf(stderr, "FAIL unit.c:%d\n", __LINE__); exit(1); } } while (0)
static int fail_malloc, fail_calloc, fail_digest, write_mode, write_step, write_fd = -1;
void *__real_malloc(size_t);
void *__real_calloc(size_t, size_t);
ssize_t __real_write(int, const void *, size_t);
int __real_EVP_Digest(const void *, size_t, unsigned char *, unsigned int *, const EVP_MD *, ENGINE *);
void *__wrap_malloc(size_t n) { if (fail_malloc && !--fail_malloc) return NULL; return __real_malloc(n); }
void *__wrap_calloc(size_t n, size_t m) { if (fail_calloc && !--fail_calloc) return NULL; return __real_calloc(n, m); }
int __wrap_EVP_Digest(const void *p, size_t n, unsigned char *out, unsigned int *length, const EVP_MD *md, ENGINE *engine) {
    if (fail_digest) { fail_digest = 0; return 0; }
    return __real_EVP_Digest(p, n, out, length, md, engine);
}
ssize_t __wrap_write(int fd, const void *p, size_t n) {
    if (fd != write_fd || !write_mode) return __real_write(fd, p, n);
    int step = write_step++;
    if (write_mode == 1 && step) { errno = EIO; return -1; }
    if (write_mode == 2) return 0;
    if (write_mode == 3 && !step) { errno = EINTR; return -1; }
    if ((write_mode == 1 || write_mode == 4) && !step) return __real_write(fd, p, n > 17 ? 17 : n);
    return __real_write(fd, p, n);
}
static const unsigned char sample[] =
    "schema\tB6S_PB1_RESULT_V1\nversion\tB6S1\n"
    "descriptor_sha256\taaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n"
    "input_sha256\tbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
    "input_bytes\t0\nschema_id\tB6S_TEST_PAYLOAD\noutcome\tREPRESENTATION_CHECKED\nreason\tNONE\n";
static struct b6s_span values[20];
static struct b6s_subject subjects[B6S_ROLE_COUNT];
static char role_hashes[B6S_ROLE_COUNT][65], input_hash[65], count_text[32];
static unsigned char descriptor[B6S_DESCRIPTOR_MAX];
static size_t descriptor_length;
static unsigned char *package_bytes, *spec_bytes;
static const char *output_directory;
static const size_t role_fields[B6S_ROLE_COUNT] = {3,4,5,6,7,8,9,10,12,14,15,16};
static struct b6s_span text(const char *p) { return (struct b6s_span){(const unsigned char *)p, strlen(p)}; }
static void group(const char *name) { groups++; printf("PASS group/%s\n", name); }
static unsigned char *file_bytes(const char *directory, const char *name, size_t *count) {
    char path[4096]; CHECK(snprintf(path, sizeof(path), "%s/%s", directory, name) > 0);
    FILE *f = fopen(path, "rb"); CHECK(f != NULL);
    CHECK(fseek(f, 0, SEEK_END) == 0); long n = ftell(f); CHECK(n >= 0 && n < 65536);
    CHECK(fseek(f, 0, SEEK_SET) == 0);
    unsigned char *bytes = malloc((size_t)n + 1); CHECK(bytes != NULL);
    CHECK(fread(bytes, 1, (size_t)n, f) == (size_t)n && fclose(f) == 0);
    *count = (size_t)n; return bytes;
}
static struct b6s_record *decode(enum b6s_kind kind, const unsigned char *bytes, size_t length) {
    struct b6s_record *r = NULL; CHECK(b6s_decode(kind, bytes, length, &r) == B6S_CHECKED && r); return r;
}
static void initialize(const char *package) {
    static const char *const labels[B6S_ROLE_COUNT] = {
        "", "", "TEST_ONLY_PLAN", "TEST_ONLY_RELEASE", "TEST_ONLY_ENTRYPOINT", "TEST_ONLY_EXECUTABLE",
        "TEST_ONLY_PARSER", "TEST_ONLY_GRAMMAR", "TEST_ONLY_SCHEMA", "TEST_ONLY_CONFIGURATION",
        "TEST_ONLY_PLATFORM", "TEST_ONLY_SUPPLY"
    };
    memset(values, 0, sizeof(values));
    values[0] = text(B6S_DESCRIPTOR_SCHEMA); values[1] = text("B6S1");
    values[2] = text("ISOLATED_QUALIFICATION");
    values[11] = text(B6S_RESULT_SCHEMA); values[13] = text("CANONICAL_TEXT");
    values[19] = text("11111111-1111-4111-8111-111111111111");
    size_t package_length, spec_length;
    package_bytes = file_bytes(package, "MANIFEST.sha256", &package_length);
    spec_bytes = file_bytes(package, "PB1-INVOCATION.md", &spec_length);
    for (size_t i = 0; i < B6S_ROLE_COUNT; i++) {
        struct b6s_span bytes = i == 0 ? (struct b6s_span){package_bytes, package_length} :
            i == 1 ? (struct b6s_span){spec_bytes, spec_length} : text(labels[i]);
        CHECK(b6s_sha256(bytes.bytes, bytes.length, role_hashes[i]) == B6S_CHECKED);
        subjects[i].role = (enum b6s_role)i; subjects[i].bytes = bytes;
        memcpy(subjects[i].expected_sha256, role_hashes[i], 65);
        values[role_fields[i]] = text(role_hashes[i]);
    }
    CHECK(!strcmp(role_hashes[0], B6S_PACKAGE_SHA256));
    CHECK(!strcmp(role_hashes[1], B6S_INVOCATION_SPEC_SHA256));
    CHECK(b6s_sha256(sample, sizeof(sample) - 1, input_hash) == B6S_CHECKED);
    CHECK(snprintf(count_text, sizeof(count_text), "%zu", sizeof(sample) - 1) > 0);
    values[17] = text(input_hash); values[18] = text(count_text);
    CHECK(b6s_encode(B6S_DESCRIPTOR, values, 20, descriptor, sizeof(descriptor), &descriptor_length) == B6S_CHECKED);
}
static size_t edit(const unsigned char *bytes, size_t length, const char *key,
                    const char *replacement, unsigned char *out) {
    size_t k = strlen(key), n = strlen(replacement);
    for (size_t start = 0; start < length;) {
        size_t end = start; while (end < length && bytes[end] != '\n') end++;
        if (end > start + k && bytes[start + k] == '\t' && !memcmp(bytes + start, key, k)) {
            size_t prefix = start + k + 1;
            memcpy(out, bytes, prefix); memcpy(out + prefix, replacement, n);
            memcpy(out + prefix + n, bytes + end, length - end); return prefix + n + length - end;
        }
        start = end + 1;
    }
    CHECK(0); return 0;
}
static void reject(enum b6s_kind kind, const unsigned char *bytes, size_t length, enum b6s_status expected) {
    struct b6s_record *out = (struct b6s_record *)1;
    CHECK(b6s_decode(kind, bytes, length, &out) == expected && out == NULL);
}
static void codec(void) {
    struct b6s_record *r = decode(B6S_DESCRIPTOR, descriptor, descriptor_length);
    CHECK(b6s_kind(r) == B6S_DESCRIPTOR && b6s_equal(b6s_value(r, 19), "11111111-1111-4111-8111-111111111111"));
    CHECK(b6s_value(r, 20).bytes == NULL && b6s_bytes(NULL).bytes == NULL);
    struct b6s_span copied[20]; for (size_t i = 0; i < 20; i++) copied[i] = b6s_value(r, i);
    unsigned char out[B6S_DESCRIPTOR_MAX]; size_t length = 0;
    CHECK(b6s_encode(B6S_DESCRIPTOR, copied, 20, out, sizeof(out), &length) == B6S_CHECKED);
    CHECK(length == descriptor_length && !memcmp(out, descriptor, length));
    out[0] = 'X'; CHECK(b6s_bytes(r).bytes[0] == 's');
    memset(out, 0xcc, sizeof(out)); length = 99;
    CHECK(b6s_encode(B6S_DESCRIPTOR, copied, 20, out, 1, &length) == B6S_UNDETERMINED && length == 0 && out[0] == 0xcc);
    CHECK(b6s_encode(B6S_DESCRIPTOR, copied, 19, out, sizeof(out), &length) == B6S_INVALID && length == 0);
    b6s_destroy(r);
    r = decode(B6S_RESULT, sample, sizeof(sample) - 1);
    for (size_t i = 0; i < 8; i++) copied[i] = b6s_value(r, i);
    CHECK(b6s_encode(B6S_RESULT, copied, 8, out, sizeof(out), &length) == B6S_CHECKED);
    CHECK(length == sizeof(sample) - 1 && !memcmp(out, sample, length));
    CHECK(b6s_result_exit(r) == 0); b6s_destroy(r);
    group("exact_codecs_roundtrip_and_owned_copy");
}
static void mutations(void) {
    unsigned char changed[B6S_DESCRIPTOR_MAX + 2];
    for (size_t i = 0; i < descriptor_length; i++) {
        reject(B6S_DESCRIPTOR, descriptor, i, B6S_INVALID);
        memcpy(changed, descriptor, descriptor_length); changed[i] = '\r';
        reject(B6S_DESCRIPTOR, changed, descriptor_length, B6S_INVALID);
        changed[i] = 0; reject(B6S_DESCRIPTOR, changed, descriptor_length, B6S_INVALID);
    }
    for (size_t i = 0; i < sizeof(sample) - 1; i++) reject(B6S_RESULT, sample, i, B6S_INVALID);
    memcpy(changed, descriptor, descriptor_length); changed[descriptor_length] = '\n';
    reject(B6S_DESCRIPTOR, changed, descriptor_length + 1, B6S_INVALID);
    changed[0] = 'X'; reject(B6S_DESCRIPTOR, changed, descriptor_length, B6S_INVALID);
    size_t first = (size_t)((const unsigned char *)strchr((const char *)descriptor, '\n') - descriptor) + 1;
    memcpy(changed, descriptor + first, descriptor_length - first); memcpy(changed + descriptor_length - first, descriptor, first);
    reject(B6S_DESCRIPTOR, changed, descriptor_length, B6S_INVALID);
    size_t n = edit(descriptor, descriptor_length, "version", "B6S0", changed);
    reject(B6S_DESCRIPTOR, changed, n, B6S_UNSUPPORTED);
    n = edit(descriptor, descriptor_length, "schema", "B6_TQ_PARSER_BINDING_TQ1", changed);
    reject(B6S_DESCRIPTOR, changed, n, B6S_UNSUPPORTED);
    n = edit(descriptor, descriptor_length, "realm", "OPERATIONAL_READONLY", changed);
    reject(B6S_DESCRIPTOR, changed, n, B6S_UNSUPPORTED);
    const char *const bad_counts[] = {"00", "01", "+1", "-1", "1048577", "18446744073709551616"};
    for (size_t i = 0; i < sizeof(bad_counts) / sizeof(bad_counts[0]); i++) {
        n = edit(descriptor, descriptor_length, "input_bytes", bad_counts[i], changed);
        reject(B6S_DESCRIPTOR, changed, n, B6S_INVALID);
    }
    const char *const bad_hashes[] = {"", "AA", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        "0000000000000000000000000000000000000000000000000000000000000000", "aabbcc"};
    for (size_t i = 0; i < sizeof(bad_hashes) / sizeof(bad_hashes[0]); i++) {
        n = edit(descriptor, descriptor_length, "input_sha256", bad_hashes[i], changed);
        reject(B6S_DESCRIPTOR, changed, n, B6S_INVALID);
    }
    n = edit(descriptor, descriptor_length, "invocation_id", "00000000-0000-0000-0000-000000000000", changed);
    reject(B6S_DESCRIPTOR, changed, n, B6S_INVALID);
    n = edit(descriptor, descriptor_length, "schema_id", "lower-case", changed);
    reject(B6S_DESCRIPTOR, changed, n, B6S_INVALID);
    reject((enum b6s_kind)44, descriptor, descriptor_length, B6S_UNSUPPORTED);
    group("strict_truncation_control_byte_order_version_scalar_mutations");
}
static void result_pairs(void) {
    const char *const outcomes[] = {"REPRESENTATION_CHECKED", "INVALID", "UNSUPPORTED", "UNDETERMINED"};
    const char *const reasons[] = {"NONE", "DESCRIPTOR", "INPUT_DIGEST", "GRAMMAR", "SCHEMA", "CONTEXT", "LIMIT", "DEPENDENCY", "RESOURCE", "IO", "TIMEOUT", "CANCELLED"};
    struct b6s_record *r = decode(B6S_RESULT, sample, sizeof(sample) - 1);
    struct b6s_span fields[8]; for (size_t i = 0; i < 8; i++) fields[i] = b6s_value(r, i);
    unsigned char out[B6S_RESULT_MAX]; size_t length;
    unsigned valid = 0;
    for (size_t i = 0; i < 4; i++) for (size_t j = 0; j < 12; j++) {
        fields[6] = text(outcomes[i]); fields[7] = text(reasons[j]);
        int expected = (i == 0 && j == 0) || (i == 1 && (j == 1 || j == 2 || j == 3 || j == 5 || j == 6)) ||
            (i == 2 && j == 4) || (i == 3 && (j == 5 || j >= 7));
        enum b6s_status status = b6s_encode(B6S_RESULT, fields, 8, out, sizeof(out), &length);
        CHECK(status == (expected ? B6S_CHECKED : B6S_INVALID));
        if (expected) { struct b6s_record *parsed = decode(B6S_RESULT, out, length);
            CHECK(b6s_result_exit(parsed) == (i == 0 ? 0 : i == 1 ? 10 : i == 2 ? 11 : 12));
            b6s_destroy(parsed); valid++; }
    }
    CHECK(valid == 13); b6s_destroy(r);
    group("all_48_exit_outcome_reason_combinations");
}
static void bindings(void) {
    struct b6s_record *d = decode(B6S_DESCRIPTOR, descriptor, descriptor_length);
    struct b6s_span input = {sample, sizeof(sample) - 1};
    CHECK(b6s_check_bindings(d, subjects, B6S_ROLE_COUNT, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", input) == B6S_CHECKED);
    CHECK(b6s_runtime_admission() == B6S_UNDETERMINED);
    for (size_t i = 0; i < B6S_ROLE_COUNT; i++) {
        char old = subjects[i].expected_sha256[0]; subjects[i].expected_sha256[0] = old == 'a' ? 'b' : 'a';
        CHECK(b6s_check_bindings(d, subjects, B6S_ROLE_COUNT, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", input) == B6S_INVALID);
        subjects[i].expected_sha256[0] = old;
        struct b6s_span saved = subjects[i].bytes; subjects[i].bytes = (struct b6s_span){NULL, 0};
        CHECK(b6s_check_bindings(d, subjects, B6S_ROLE_COUNT, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", input) == B6S_UNDETERMINED);
        subjects[i].bytes = text("TEST_ONLY_SUBSTITUTION");
        CHECK(b6s_check_bindings(d, subjects, B6S_ROLE_COUNT, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", input) == B6S_INVALID);
        subjects[i].bytes = saved; CHECK(b6s_runtime_admission() == B6S_UNDETERMINED);
    }
    CHECK(b6s_check_bindings(d, subjects, 11, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", input) == B6S_UNDETERMINED);
    enum b6s_role saved_role = subjects[1].role; subjects[1].role = B6S_PACKAGE;
    CHECK(b6s_check_bindings(d, subjects, 12, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", input) == B6S_INVALID);
    subjects[1].role = saved_role;
    CHECK(b6s_check_bindings(d, subjects, 12, "UNKNOWN", "CANONICAL_TEXT", input) == B6S_UNSUPPORTED);
    CHECK(b6s_check_bindings(d, subjects, 12, B6S_RESULT_SCHEMA, "UNKNOWN", input) == B6S_UNSUPPORTED);
    CHECK(b6s_check_bindings(d, subjects, 12, B6S_DESCRIPTOR_SCHEMA, "CANONICAL_TEXT", input) == B6S_INVALID);
    CHECK(b6s_check_bindings(d, subjects, 12, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", (struct b6s_span){sample, 1}) == B6S_INVALID);
    unsigned char changed[B6S_DESCRIPTOR_MAX]; size_t n = edit(descriptor, descriptor_length, "successor_manifest_sha256", role_hashes[2], changed);
    struct b6s_record *wrong = decode(B6S_DESCRIPTOR, changed, n);
    CHECK(b6s_check_bindings(wrong, subjects, 12, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", input) == B6S_INVALID); b6s_destroy(wrong);
    n = edit(descriptor, descriptor_length, "layout", role_hashes[2], changed); wrong = decode(B6S_DESCRIPTOR, changed, n);
    CHECK(b6s_check_bindings(wrong, subjects, 12, B6S_RESULT_SCHEMA, "CANONICAL_TEXT", input) == B6S_UNSUPPORTED); b6s_destroy(wrong);
    b6s_destroy(d); group("12role_identity_missing_substitution_and_runtime_block");
}
static size_t result_for(const struct b6s_record *d, const char *outcome, const char *reason, unsigned char *out) {
    char dh[65]; struct b6s_span raw = b6s_bytes(d);
    CHECK(b6s_sha256(raw.bytes, raw.length, dh) == B6S_CHECKED);
    struct b6s_span fields[] = {text(B6S_RESULT_SCHEMA), text("B6S1"), text(dh), text(input_hash), text(count_text),
        text(B6S_RESULT_SCHEMA), text(outcome), text(reason)};
    size_t length; CHECK(b6s_encode(B6S_RESULT, fields, 8, out, B6S_RESULT_MAX, &length) == B6S_CHECKED); return length;
}
static void accounting(void) {
    struct b6s_record *d = decode(B6S_DESCRIPTOR, descriptor, descriptor_length);
    unsigned char result[B6S_RESULT_MAX], repeated[B6S_RESULT_MAX];
    struct b6s_span input = {sample, sizeof(sample) - 1}; enum b6s_operation operation;
    size_t length = result_for(d, "REPRESENTATION_CHECKED", "NONE", result);
    CHECK(b6s_check_output(d, input, (struct b6s_span){result,length}, 0, &operation) == B6S_CHECKED && operation == B6S_REPRESENTATION);
    for (size_t i = 0; i < 12; i++) { size_t n = result_for(d, "REPRESENTATION_CHECKED", "NONE", repeated);
        CHECK(n == length && !memcmp(result, repeated, n)); }
    CHECK(b6s_check_output(d, input, (struct b6s_span){result,length}, 12, &operation) == B6S_INVALID && operation == B6S_NO_OPERATION);
    CHECK(b6s_check_output(d, input, (struct b6s_span){result,length-1}, 0, &operation) != B6S_CHECKED && operation == B6S_NO_OPERATION);
    CHECK(b6s_check_output(d, input, (struct b6s_span){result,0}, 12, &operation) == B6S_UNDETERMINED);
    CHECK(b6s_check_output(d, input, (struct b6s_span){result,length}, 137, &operation) == B6S_UNDETERMINED);
    length = result_for(d, "UNDETERMINED", "IO", result);
    CHECK(b6s_check_output(d, input, (struct b6s_span){result,length}, 12, &operation) == B6S_CHECKED && operation == B6S_OPERATION_UNDETERMINED);
    length = result_for(d, "REPRESENTATION_CHECKED", "NONE", repeated);
    CHECK(b6s_check_output(d, input, (struct b6s_span){repeated,length}, 0, &operation) == B6S_CHECKED && operation == B6S_REPRESENTATION);
    fail_calloc = 2;
    CHECK(b6s_check_output(d, input, (struct b6s_span){repeated,length}, 0, &operation) == B6S_UNDETERMINED && operation == B6S_NO_OPERATION);
    fail_malloc = 2;
    CHECK(b6s_check_output(d, input, (struct b6s_span){repeated,length}, 0, &operation) == B6S_UNDETERMINED && operation == B6S_NO_OPERATION);
    unsigned char changed[B6S_DESCRIPTOR_MAX];
    const char *const keys[] = {"successor_manifest_sha256", "invocation_spec_sha256", "layout"};
    for (size_t i = 0; i < 3; i++) {
        size_t n = edit(descriptor, descriptor_length, keys[i], role_hashes[2], changed);
        struct b6s_record *wrong = decode(B6S_DESCRIPTOR, changed, n);
        n = result_for(wrong, "REPRESENTATION_CHECKED", "NONE", result);
        CHECK(b6s_check_output(wrong, input, (struct b6s_span){result,n}, 0, &operation) == B6S_INVALID && operation == B6S_NO_OPERATION);
        b6s_destroy(wrong);
    }
    length = result_for(d, "REPRESENTATION_CHECKED", "NONE", result);
    size_t n = edit(result, length, "descriptor_sha256", role_hashes[2], changed);
    CHECK(b6s_check_output(d, input, (struct b6s_span){changed,n}, 0, &operation) == B6S_INVALID);
    n = edit(result, length, "input_sha256", role_hashes[2], changed);
    CHECK(b6s_check_output(d, input, (struct b6s_span){changed,n}, 0, &operation) == B6S_INVALID);
    n = edit(result, length, "input_bytes", "0", changed);
    CHECK(b6s_check_output(d, input, (struct b6s_span){changed,n}, 0, &operation) == B6S_INVALID);
    memcpy(changed, result, length); changed[length] = '\n';
    CHECK(b6s_check_output(d, input, (struct b6s_span){changed,length+1}, 0, &operation) == B6S_INVALID);
    CHECK(b6s_runtime_admission() == B6S_UNDETERMINED);
    b6s_destroy(d); group("success_negative_incomplete_signal_and_operational_replay_accounting");
}
static void resources(void) {
    struct b6s_record *r = (struct b6s_record *)1;
    fail_calloc = 1; CHECK(b6s_decode(B6S_DESCRIPTOR, descriptor, descriptor_length, &r) == B6S_UNDETERMINED && !r);
    fail_malloc = 1; CHECK(b6s_decode(B6S_DESCRIPTOR, descriptor, descriptor_length, &r) == B6S_UNDETERMINED && !r);
    char out[65]; memset(out, 'x', sizeof(out)); fail_digest = 1;
    CHECK(b6s_sha256(sample, sizeof(sample) - 1, out) == B6S_UNDETERMINED && out[0] == 0);
    unsigned char aliased[128], before[128]; memset(aliased, 'a', sizeof(aliased)); memcpy(before, aliased, sizeof(before));
    CHECK(b6s_sha256(aliased, sizeof(aliased), (char *)aliased + 32) == B6S_INVALID);
    CHECK(!memcmp(aliased, before, sizeof(aliased)));
    group("allocation_and_crypto_failure_no_published_record");
}
static void output_io(void) {
    struct b6s_record *d = decode(B6S_DESCRIPTOR, descriptor, descriptor_length);
    unsigned char result[B6S_RESULT_MAX]; size_t length = result_for(d, "UNDETERMINED", "DEPENDENCY", result);
    for (int mode = 0; mode <= 4; mode++) {
        char file[4096]; CHECK(snprintf(file, sizeof(file), "%s/unit-output-%d", output_directory, mode) > 0);
        int fd = open(file, O_WRONLY | O_CREAT | O_EXCL, 0600); CHECK(fd >= 0);
        write_fd = fd; write_mode = mode; write_step = 0;
        CHECK(b6s_write_result(fd, result, length) == (mode == 1 || mode == 2 ? B6S_UNDETERMINED : B6S_CHECKED));
        struct stat st; CHECK(fstat(fd, &st) == 0);
        CHECK(st.st_size == (off_t)(mode == 1 ? 17 : mode == 2 ? 0 : length));
        write_mode = 0; write_fd = -1; CHECK(close(fd) == 0);
        fd = open(file, O_RDONLY); CHECK(fd >= 0);
        CHECK(b6s_write_result(fd, result, length) == B6S_INVALID); CHECK(close(fd) == 0);
    }
    b6s_destroy(d); group("real_output_short_write_eintr_zero_and_partial_failure");
}
static void argv_env(void) {
    char *argv[] = {"b6-pb1-v1","--request-fd","3","--input-fd","4","--result-fd","5",NULL};
    char *env[] = {"LC_ALL=C","TZ=UTC",NULL};
    CHECK(b6s_check_argv_env(7, argv, env) == B6S_CHECKED);
    for (size_t i = 0; i < 7; i++) { char *save = argv[i]; argv[i] = "WRONG";
        CHECK(b6s_check_argv_env(7, argv, env) == B6S_INVALID); argv[i] = save; }
    CHECK(b6s_check_argv_env(6, argv, env) == B6S_INVALID);
    env[0] = "TZ=UTC"; env[1] = "LC_ALL=C"; CHECK(b6s_check_argv_env(7, argv, env) == B6S_INVALID);
    group("exact_argv_and_environment");
}
int main(int argc, char **argv) {
    CHECK(argc == 3); output_directory = argv[2]; initialize(argv[1]);
    codec(); mutations(); result_pairs(); bindings(); accounting(); resources(); output_io(); argv_env();
    free(package_bytes); free(spec_bytes);
    printf("B6S_UNIT groups=%u assertions=%u scope=LOCAL_COMPONENT_NOT_RUNTIME_QUALIFICATION\n", groups, checks);
    return 0;
}
