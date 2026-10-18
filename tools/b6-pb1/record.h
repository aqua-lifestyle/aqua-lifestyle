#ifndef B6S_PB1_RECORD_H
#define B6S_PB1_RECORD_H
#include <stddef.h>
#include <stdint.h>

#define B6S_DESCRIPTOR_MAX 16384U
#define B6S_RESULT_MAX 4096U
#define B6S_INPUT_MAX 1048576U
#define B6S_CONFIGURATION_MAX 65536U
#define B6S_DESCRIPTOR_SCHEMA "B6S_PB1_INVOCATION_V1"
#define B6S_RESULT_SCHEMA "B6S_PB1_RESULT_V1"
#define B6S_PACKAGE_SHA256 "0711c7d483f16bddf8bb83ecacb7b4dedda978adc85295c4704516514e81fd7b"
#define B6S_INVOCATION_SPEC_SHA256 "e9fe2bc58b1c38d0e4f1dd39d26d81f1243970072b21f016c2ca966022ed215a"

/* CHECKED is local representation/accounting only. It never admits execution. */
enum b6s_status { B6S_CHECKED, B6S_INVALID, B6S_UNSUPPORTED, B6S_UNDETERMINED };
enum b6s_kind { B6S_DESCRIPTOR, B6S_RESULT };
struct b6s_span { const unsigned char *bytes; size_t length; };
struct b6s_record;

/* Owned copied records; borrowed fields survive until destroy. C callers provide
 * valid disjoint output-pointer storage; every supplied byte is untrusted. */
enum b6s_status b6s_decode(enum b6s_kind, const unsigned char *, size_t,
                          struct b6s_record **);
enum b6s_status b6s_encode(enum b6s_kind, const struct b6s_span *, size_t,
                          unsigned char *, size_t, size_t *);
void b6s_destroy(struct b6s_record *);
struct b6s_span b6s_value(const struct b6s_record *, size_t);
struct b6s_span b6s_bytes(const struct b6s_record *);
enum b6s_kind b6s_kind(const struct b6s_record *);
size_t b6s_field_count(enum b6s_kind);
int b6s_equal(struct b6s_span, const char *);
int b6s_uint(struct b6s_span, uint64_t *);
int b6s_hash(struct b6s_span);
int b6s_schema_supported(const char *);
int b6s_result_exit(const struct b6s_record *);
/* Overlapping input/hash-output storage is rejected without touching either. */
enum b6s_status b6s_sha256(const unsigned char *, size_t, char [65]);
#endif
