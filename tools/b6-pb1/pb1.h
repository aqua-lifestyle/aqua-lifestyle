#ifndef B6S_PB1_H
#define B6S_PB1_H
#include "record.h"

enum b6s_role {
    B6S_PACKAGE, B6S_SPEC, B6S_PLAN, B6S_RELEASE, B6S_ENTRYPOINT, B6S_EXECUTABLE,
    B6S_PARSER, B6S_GRAMMAR, B6S_SCHEMA, B6S_CONFIGURATION, B6S_PLATFORM,
    B6S_SUPPLY, B6S_ROLE_COUNT
};
struct b6s_subject {
    enum b6s_role role;
    struct b6s_span bytes;
    char expected_sha256[65];
};
enum b6s_operation {
    B6S_NO_OPERATION, B6S_REPRESENTATION, B6S_OPERATION_INVALID,
    B6S_OPERATION_UNSUPPORTED, B6S_OPERATION_UNDETERMINED
};

enum b6s_status b6s_check_argv_env(int, char *const [], char *const []);
/* Pure local byte/role equality only. The caller's expected pins and bytes are
 * NOT authenticated or qualified by this API. It cannot admit a process. */
enum b6s_status b6s_check_bindings(const struct b6s_record *, const struct b6s_subject *,
                                  size_t, const char *, const char *, struct b6s_span);
/* Original input bytes are required, not a caller success/completeness flag.
 * CHECKED means local output accounting matched. operation preserves negatives. */
enum b6s_status b6s_check_output(const struct b6s_record *, struct b6s_span,
                                struct b6s_span, int, enum b6s_operation *);
/* Always blocked: external controller/typed-manifest qualification, authority,
 * independent custody/confinement and observation ledger are NOT IMPLEMENTED. */
enum b6s_status b6s_runtime_admission(void);
enum b6s_status b6s_write_result(int, const unsigned char *, size_t);
#endif
