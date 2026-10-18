#include "record.h"
#include <openssl/evp.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* Narrow adaptation of retained successor-record.c's copied TAB/LF scanner,
 * decimal overflow check and EVP hashing. See ORIGIN.md. No legacy schema table,
 * authority disposition, repeat/tuple grammar or historical pin is imported. */
struct row { struct b6s_span key, value; };
struct b6s_record {
    unsigned char *bytes;
    size_t length, count;
    enum b6s_kind kind;
    struct row rows[20];
};
static const char *const descriptor_keys[] = {
    "schema", "version", "realm", "successor_manifest_sha256",
    "invocation_spec_sha256", "qualification_plan_sha256", "release_manifest_sha256",
    "entrypoint_source_sha256", "executable_sha256", "parser_manifest_sha256",
    "grammar_manifest_sha256", "schema_id", "schema_manifest_sha256", "layout",
    "configuration_sha256", "platform_manifest_sha256", "selected_supply_sha256",
    "input_sha256", "input_bytes", "invocation_id"
};
static const char *const result_keys[] = {
    "schema", "version", "descriptor_sha256", "input_sha256", "input_bytes",
    "schema_id", "outcome", "reason"
};
static const char *const outcomes[] = {
    "REPRESENTATION_CHECKED", "INVALID", "UNSUPPORTED", "UNDETERMINED"
};
static const char *const reasons[] = {
    "NONE", "DESCRIPTOR", "INPUT_DIGEST", "GRAMMAR", "SCHEMA", "CONTEXT",
    "LIMIT", "DEPENDENCY", "RESOURCE", "IO", "TIMEOUT", "CANCELLED"
};
int b6s_equal(struct b6s_span a, const char *b) {
    return a.bytes && b && a.length == strlen(b) && !memcmp(a.bytes, b, a.length);
}
int b6s_uint(struct b6s_span s, uint64_t *out) {
    uint64_t n = 0;
    if (!out) return 0;
    *out = 0;
    if (!s.bytes || !s.length || (s.length > 1 && s.bytes[0] == '0')) return 0;
    for (size_t i = 0; i < s.length; i++) {
        unsigned d = (unsigned)s.bytes[i] - (unsigned)'0';
        if (d > 9 || n > (UINT64_MAX - d) / 10) return 0;
        n = n * 10 + d;
    }
    *out = n;
    return 1;
}
static int hex(struct b6s_span s, size_t count) {
    if (!s.bytes || s.length != count) return 0;
    for (size_t i = 0; i < count; i++)
        if (!((s.bytes[i] >= '0' && s.bytes[i] <= '9') ||
              (s.bytes[i] >= 'a' && s.bytes[i] <= 'f'))) return 0;
    return 1;
}
int b6s_hash(struct b6s_span s) {
    int nonzero = 0;
    if (!hex(s, 64)) return 0;
    for (size_t i = 0; i < s.length; i++) nonzero |= s.bytes[i] != '0';
    return nonzero;
}
static int uuid(struct b6s_span s) {
    int nonzero = 0;
    if (!s.bytes || s.length != 36) return 0;
    for (size_t i = 0; i < s.length; i++) {
        if (i == 8 || i == 13 || i == 18 || i == 23) {
            if (s.bytes[i] != '-') return 0;
        } else {
            if (!hex((struct b6s_span){s.bytes + i, 1}, 1)) return 0;
            nonzero |= s.bytes[i] != '0';
        }
    }
    return nonzero;
}
static int identifier(struct b6s_span s) {
    if (!s.bytes || !s.length || s.length > 64 || s.bytes[0] < 'A' || s.bytes[0] > 'Z') return 0;
    for (size_t i = 1; i < s.length; i++)
        if (!((s.bytes[i] >= 'A' && s.bytes[i] <= 'Z') ||
              (s.bytes[i] >= '0' && s.bytes[i] <= '9') || s.bytes[i] == '_')) return 0;
    return 1;
}
static int member(struct b6s_span s, const char *const *values, size_t count) {
    for (size_t i = 0; i < count; i++) if (b6s_equal(s, values[i])) return (int)i;
    return -1;
}
size_t b6s_field_count(enum b6s_kind kind) {
    return kind == B6S_DESCRIPTOR ? 20U : kind == B6S_RESULT ? 8U : 0U;
}
int b6s_schema_supported(const char *schema) {
    return schema && (!strcmp(schema, B6S_DESCRIPTOR_SCHEMA) || !strcmp(schema, B6S_RESULT_SCHEMA));
}
void b6s_destroy(struct b6s_record *r) {
    if (r) { free(r->bytes); memset(r, 0, sizeof(*r)); free(r); }
}
struct b6s_span b6s_value(const struct b6s_record *r, size_t i) {
    return r && i < r->count ? r->rows[i].value : (struct b6s_span){NULL, 0};
}
struct b6s_span b6s_bytes(const struct b6s_record *r) {
    return r ? (struct b6s_span){r->bytes, r->length} : (struct b6s_span){NULL, 0};
}
enum b6s_kind b6s_kind(const struct b6s_record *r) { return r ? r->kind : (enum b6s_kind)-1; }
int b6s_result_exit(const struct b6s_record *r) {
    if (!r || r->kind != B6S_RESULT) return -1;
    int outcome = member(b6s_value(r, 6), outcomes, 4);
    int reason = member(b6s_value(r, 7), reasons, 12);
    if (outcome == 0 && reason == 0) return 0;
    if (outcome == 1 && (reason == 1 || reason == 2 || reason == 3 || reason == 5 || reason == 6)) return 10;
    if (outcome == 2 && reason == 4) return 11;
    if (outcome == 3 && (reason == 5 || (reason >= 7 && reason <= 11))) return 12;
    return -1;
}
enum b6s_status b6s_decode(enum b6s_kind kind, const unsigned char *bytes,
                          size_t length, struct b6s_record **out) {
    if (!out) return B6S_INVALID;
    *out = NULL;
    size_t expected = b6s_field_count(kind), start = 0, tab = 0;
    if (!expected) return B6S_UNSUPPORTED;
    size_t limit = kind == B6S_DESCRIPTOR ? B6S_DESCRIPTOR_MAX : B6S_RESULT_MAX;
    if (!bytes || !length || length > limit || bytes[length - 1] != '\n') return B6S_INVALID;
    struct b6s_record *r = calloc(1, sizeof(*r));
    if (!r) return B6S_UNDETERMINED;
    r->bytes = malloc(length);
    if (!r->bytes) { b6s_destroy(r); return B6S_UNDETERMINED; }
    memcpy(r->bytes, bytes, length);
    r->length = length;
    r->kind = kind;
    for (size_t i = 0; i < length; i++) {
        unsigned char c = r->bytes[i];
        if (c == '\t') { if (tab || i == start) goto invalid; tab = i; }
        else if (c == '\n') {
            if (!tab || i == tab + 1 || r->count == expected) goto invalid;
            r->rows[r->count++] = (struct row){{r->bytes + start, tab - start},
                {r->bytes + tab + 1, i - tab - 1}};
            start = i + 1; tab = 0;
        } else if (c < 33 || c > 126) goto invalid;
    }
    if (r->count != expected) goto invalid;
    const char *const *keys = kind == B6S_DESCRIPTOR ? descriptor_keys : result_keys;
    for (size_t i = 0; i < expected; i++) if (!b6s_equal(r->rows[i].key, keys[i])) goto invalid;
    if (!identifier(b6s_value(r, 0)) || !identifier(b6s_value(r, 1))) goto invalid;
    if (!b6s_equal(b6s_value(r, 0), kind == B6S_DESCRIPTOR ? B6S_DESCRIPTOR_SCHEMA : B6S_RESULT_SCHEMA) ||
        !b6s_equal(b6s_value(r, 1), "B6S1")) goto unsupported;
    uint64_t count;
    if (kind == B6S_DESCRIPTOR) {
        if (!identifier(b6s_value(r, 2))) goto invalid;
        if (!b6s_equal(b6s_value(r, 2), "ISOLATED_QUALIFICATION")) goto unsupported;
        for (size_t i = 3; i <= 17; i++) {
            if (i == 11 || i == 13) continue;
            if (!b6s_hash(b6s_value(r, i))) goto invalid;
        }
        if (!identifier(b6s_value(r, 11)) ||
            !(b6s_hash(b6s_value(r, 13)) || b6s_equal(b6s_value(r, 13), "CANONICAL_TEXT")) ||
            !b6s_uint(b6s_value(r, 18), &count) || count > B6S_INPUT_MAX || !uuid(b6s_value(r, 19))) goto invalid;
    } else if (!b6s_hash(b6s_value(r, 2)) || !b6s_hash(b6s_value(r, 3)) ||
               !b6s_uint(b6s_value(r, 4), &count) || !identifier(b6s_value(r, 5)) || b6s_result_exit(r) < 0) goto invalid;
    *out = r;
    return B6S_CHECKED;
unsupported:
    b6s_destroy(r); return B6S_UNSUPPORTED;
invalid:
    b6s_destroy(r); return B6S_INVALID;
}
enum b6s_status b6s_encode(enum b6s_kind kind, const struct b6s_span *values,
                          size_t count, unsigned char *out, size_t capacity, size_t *out_length) {
    if (!out_length) return B6S_INVALID;
    *out_length = 0;
    size_t expected = b6s_field_count(kind), offset = 0;
    if (!expected) return B6S_UNSUPPORTED;
    if (!values || !out || count != expected) return B6S_INVALID;
    unsigned char buffer[B6S_DESCRIPTOR_MAX];
    size_t limit = kind == B6S_DESCRIPTOR ? B6S_DESCRIPTOR_MAX : B6S_RESULT_MAX;
    const char *const *keys = kind == B6S_DESCRIPTOR ? descriptor_keys : result_keys;
    for (size_t i = 0; i < count; i++) {
        size_t key_length = strlen(keys[i]);
        if (!values[i].bytes || !values[i].length || key_length + 2 > limit - offset ||
            values[i].length > limit - offset - key_length - 2) return B6S_INVALID;
        memcpy(buffer + offset, keys[i], key_length); offset += key_length;
        buffer[offset++] = '\t';
        memcpy(buffer + offset, values[i].bytes, values[i].length); offset += values[i].length;
        buffer[offset++] = '\n';
    }
    struct b6s_record *checked = NULL;
    enum b6s_status status = b6s_decode(kind, buffer, offset, &checked);
    b6s_destroy(checked);
    if (status != B6S_CHECKED) return status;
    if (capacity < offset) return B6S_UNDETERMINED;
    memcpy(out, buffer, offset);
    *out_length = offset;
    return B6S_CHECKED;
}
enum b6s_status b6s_sha256(const unsigned char *bytes, size_t count, char out[65]) {
    unsigned char digest[32]; unsigned length = 0;
    if (!out) return B6S_INVALID;
    if (bytes && count) {
        uintptr_t input = (uintptr_t)bytes, output = (uintptr_t)out;
        if ((output >= input && output - input < count) ||
            (output < input && input - output < 65)) return B6S_INVALID;
    }
    memset(out, 0, 65);
    if ((!bytes && count) || OPENSSL_init_crypto(OPENSSL_INIT_NO_LOAD_CONFIG, NULL) != 1 ||
        EVP_Digest(bytes, count, digest, &length, EVP_sha256(), NULL) != 1 || length != 32)
        return B6S_UNDETERMINED;
    for (size_t i = 0; i < 32; i++) (void)snprintf(out + 2 * i, 3, "%02x", digest[i]);
    return B6S_CHECKED;
}
