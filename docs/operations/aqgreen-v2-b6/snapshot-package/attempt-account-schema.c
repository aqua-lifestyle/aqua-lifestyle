#include "attempt-account-schema.h"
#include <errno.h>
#include <stdbool.h>
#include <string.h>
#include <unistd.h>

struct span { const unsigned char *p; size_t n; };
struct record { struct span key, value; };
enum atom_type { LITERAL, HEX, UUID, U64PLUS, UID, HEAD, REF, PATH, CS, ID,
    STATUS, RESULT, BIND_REF, ADMIT_REF };
struct field { const char *key; enum atom_type type; const char *literal; };
struct specification {
    enum b6_syntax_schema schema; const char *literal;
    const struct field *fields; size_t count, maximum;
};
union atom {
    struct b6_syntax_hex hex;
    struct b6_syntax_uuid uuid;
    struct b6_syntax_id id;
    uint64_t number;
    uint32_t uid;
    struct b6_syntax_head head;
    struct b6_syntax_ref reference;
    struct b6_syntax_path path;
    enum b6_syntax_status status;
    enum b6_syntax_report_result result;
    struct b6_syntax_event_ref event_ref;
};
static bool lower_hex(unsigned char c)
{ return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'); }
static bool ascii_alnum(unsigned char c)
{ return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'); }
static bool equal(struct span s, const char *text)
{ size_t n = strlen(text); return s.n == n && memcmp(s.p, text, n) == 0; }

enum b6_syntax_error b6_syntax_version(const unsigned char *p, size_t n,
    struct b6_syntax_version *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_version){0};
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (n != 1 || p[0] != '1') return B6_SYNTAX_VALUE;
    *out = (struct b6_syntax_version){B6_SYNTAX_GRAMMAR_ONLY, B6_SYNTAX_FORMAT_V1};
    return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_cause(const unsigned char *p, size_t n,
    struct b6_syntax_cause *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_cause){0};
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (!n || n > 12) return B6_SYNTAX_VALUE;
    static const struct { const char *text; enum b6_syntax_cause_value value; } tokens[] = {
        {"COMPLETION", B6_SYNTAX_CAUSE_COMPLETION}, {"FAILURE", B6_SYNTAX_CAUSE_FAILURE},
        {"CANCELLATION", B6_SYNTAX_CAUSE_CANCELLATION}, {"RECOVERY", B6_SYNTAX_CAUSE_RECOVERY}
    };
    for (size_t i = 0; i < sizeof(tokens)/sizeof(tokens[0]); ++i) {
        if (equal((struct span){p,n}, tokens[i].text)) {
            *out = (struct b6_syntax_cause){B6_SYNTAX_GRAMMAR_ONLY, tokens[i].value};
            return B6_SYNTAX_OK;
        }
    }
    return B6_SYNTAX_VALUE;
}
enum b6_syntax_error b6_syntax_closure(const unsigned char *p, size_t n,
    struct b6_syntax_closure *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_closure){0};
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (!n || n > 10) return B6_SYNTAX_VALUE;
    enum b6_syntax_closure_value value;
    if (equal((struct span){p,n}, "WITHDRAWAL")) value = B6_SYNTAX_CLOSURE_WITHDRAWAL;
    else if (equal((struct span){p,n}, "EXPIRY")) value = B6_SYNTAX_CLOSURE_EXPIRY;
    else return B6_SYNTAX_VALUE;
    *out = (struct b6_syntax_closure){B6_SYNTAX_GRAMMAR_ONLY, value};
    return B6_SYNTAX_OK;
}

enum b6_syntax_error b6_syntax_hex(const unsigned char *p, size_t n,
    struct b6_syntax_hex *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_hex){0};
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (n != 64) return B6_SYNTAX_VALUE;
    for (size_t i = 0; i < n; ++i) if (!lower_hex(p[i])) return B6_SYNTAX_VALUE;
    memcpy(out->text, p, n); return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_uuid(const unsigned char *p, size_t n,
    struct b6_syntax_uuid *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_uuid){0};
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (n != 36) return B6_SYNTAX_VALUE;
    for (size_t i = 0; i < n; ++i) {
        if (i == 8 || i == 13 || i == 18 || i == 23) {
            if (p[i] != '-') return B6_SYNTAX_VALUE;
        } else if (!lower_hex(p[i])) return B6_SYNTAX_VALUE;
    }
    memcpy(out->text, p, n); return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_id(const unsigned char *p, size_t n,
    struct b6_syntax_id *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_id){0};
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (!n || n > 64 || (n == 5 && memcmp(p, "STORE", 5) == 0)) return B6_SYNTAX_VALUE;
    for (size_t i = 0; i < n; ++i) {
        bool alnum = (p[i] >= 'a' && p[i] <= 'z') || (p[i] >= 'A' && p[i] <= 'Z') ||
            (p[i] >= '0' && p[i] <= '9');
        if (!alnum && (i == 0 || (p[i] != '_' && p[i] != '-'))) return B6_SYNTAX_VALUE;
    }
    memcpy(out->text, p, n); return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_u64plus(const unsigned char *p, size_t n, uint64_t *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = 0;
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (!n || n > 20 || p[0] < '1' || p[0] > '9') return B6_SYNTAX_VALUE;
    uint64_t value = 0;
    for (size_t i = 0; i < n; ++i) {
        if (p[i] < '0' || p[i] > '9') return B6_SYNTAX_VALUE;
        uint64_t digit = (uint64_t)(p[i] - '0');
        if (value > (UINT64_MAX - digit) / 10) return B6_SYNTAX_VALUE;
        value = value * 10 + digit;
    }
    *out = value; return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_uid(const unsigned char *p, size_t n, uint32_t *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = 0;
    uint64_t value = 0;
    enum b6_syntax_error error = b6_syntax_u64plus(p, n, &value);
    if (error != B6_SYNTAX_OK) return error;
    if (value > UINT64_C(4294967294)) return B6_SYNTAX_VALUE;
    *out = (uint32_t)value; return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_ref(const unsigned char *p, size_t n, struct b6_syntax_ref *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_ref){0};
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (!n || n > 256 || !ascii_alnum(p[0])) return B6_SYNTAX_VALUE;
    for (size_t i = 1; i < n; ++i)
        if (!ascii_alnum(p[i]) && p[i] != '_' && p[i] != '.' && p[i] != ':' &&
            p[i] != '/' && p[i] != '-') return B6_SYNTAX_VALUE;
    memcpy(out->text, p, n); return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_path(const unsigned char *p, size_t n, struct b6_syntax_path *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_path){0};
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (n < 2 || n > 4095 || p[0] != '/') return B6_SYNTAX_VALUE;
    size_t start = 1;
    for (size_t i = 1; i <= n; ++i) {
        if (i == n || p[i] == '/') {
            size_t length = i-start;
            if (!length || (length == 1 && p[start] == '.') ||
                (length == 2 && p[start] == '.' && p[start+1] == '.')) return B6_SYNTAX_VALUE;
            start = i+1;
        } else if (!ascii_alnum(p[i]) && p[i] != '.' && p[i] != '_' && p[i] != '-')
            return B6_SYNTAX_VALUE;
    }
    memcpy(out->text, p, n); return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_cs(const unsigned char *p, size_t n, uint64_t *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = 0;
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (n == 1 && p[0] == '0') return B6_SYNTAX_OK;
    if (n > 19) return B6_SYNTAX_VALUE;
    uint64_t value = 0;
    enum b6_syntax_error error = b6_syntax_u64plus(p, n, &value);
    if (error != B6_SYNTAX_OK) return error;
    if (value > UINT64_C(9223372036854775807)) return B6_SYNTAX_VALUE;
    *out = value; return B6_SYNTAX_OK;
}

static const struct field store_fields[] = {
    {"schema", LITERAL, "B6_CUSTODY_STORE_V2"}, {"store_id", UUID, NULL},
    {"execution_principal_pool_sha256", HEX, NULL}
};
static const struct field set_fields[] = {
    {"schema", LITERAL, "B6_CUSTODY_CONTRACT_SET_V2"},
    {"exclusive_principal_contract_sha256", HEX, NULL},
    {"witness_gate_contract_sha256", HEX, NULL}, {"custody_account_contract_sha256", HEX, NULL},
    {"helper_protocol_sha256", HEX, NULL}, {"delegation_contract_sha256", HEX, NULL},
    {"predecessor_amendments_sha256", HEX, NULL}
};
static const struct field setup_fields[] = {
    {"schema", LITERAL, "B6_CUSTODY_SETUP_V2"}, {"generation", UUID, NULL},
    {"delegation_sha256", HEX, NULL}, {"commission_scope_sha256", HEX, NULL},
    {"release_descriptor_sha256", HEX, NULL}, {"materialization_manifest_sha256", HEX, NULL},
    {"contract_set_sha256", HEX, NULL}, {"execution_principal_assignment_sha256", HEX, NULL},
    {"initial_anchor_sha256", HEX, NULL},
    {"topology", LITERAL, "FRESH_SB_RO_PRIVATE_VIEW_EXCLUSIVE_E_TRACKED_CONSUMERS_V2"},
    {"source_index_sha256", HEX, NULL}
};
static const struct field assignment_fields[] = {
    {"schema", LITERAL, "B6_EXECUTION_PRINCIPAL_ASSIGNMENT_V1"},
    {"generation", UUID, NULL}, {"custody_store_id", UUID, NULL}, {"pool_sha256", HEX, NULL},
    {"allocation_ordinal", U64PLUS, NULL}, {"execution_uid", UID, NULL},
    {"execution_gid", UID, NULL}, {"prior_custody_head_sha256", HEAD, NULL},
    {"reuse", LITERAL, "NEVER"}
};
static const struct field permit_fields[] = {
    {"schema", LITERAL, "B6_NATIVE_EXECUTION_AUTHORITY_V2"},
    {"purpose", LITERAL, "BUBBLEWRAP_NONROOT_QUALIFICATION"},
    {"lifecycle", LITERAL, "NATIVE_MACHINE_SERIALIZED_RETRY_V2"},
    {"authority_reference", REF, NULL}, {"qualification_id", ID, NULL},
    {"authority_uid", UID, NULL}, {"authority_gid", UID, NULL},
    {"execution_principal_pool_sha256", HEX, NULL}, {"output_path", PATH, NULL},
    {"boot_id", UUID, NULL}, {"not_before_boot_cs", CS, NULL}, {"not_after_boot_cs", CS, NULL},
    {"materialization_manifest_sha256", HEX, NULL}, {"qualification_procedure_sha256", HEX, NULL},
    {"host_contract_sha256", HEX, NULL}, {"host_observation_sha256", HEX, NULL}
};
static const struct field scope_fields[] = {
    {"schema", LITERAL, "B6_COMMISSION_SCOPE_V2"}, {"commission_id", ID, NULL},
    {"grant_reference", REF, NULL}, {"grant_boot_id", UUID, NULL},
    {"authority_uid", UID, NULL}, {"authority_gid", UID, NULL},
    {"execution_principal_pool_sha256", HEX, NULL}, {"output_path", PATH, NULL},
    {"materialization_manifest_sha256", HEX, NULL}, {"qualification_procedure_sha256", HEX, NULL},
    {"host_contract_sha256", HEX, NULL}, {"not_before_boot_cs", CS, NULL},
    {"not_after_boot_cs", CS, NULL}, {"lifecycle", LITERAL, "NATIVE_MACHINE_SERIALIZED_RETRY_V2"}
};
static const struct field pool_fields[] = {
    {"schema", LITERAL, "B6_EXECUTION_PRINCIPAL_POOL_V1"}, {"pool_id", UUID, NULL},
    {"authority_reference", REF, NULL}, {"first_id", UID, NULL}, {"last_id", UID, NULL},
    {"mapping", LITERAL, "SAME_NUMERIC_UID_GID"}, {"allocation", LITERAL, "LOWEST_UNUSED_NEVER_REUSE"},
    {"user_namespace", LITERAL, "INITIAL"}, {"subordinate_id_delegation", LITERAL, "FORBIDDEN"},
    {"login_and_service_use", LITERAL, "FORBIDDEN"}, {"launcher", LITERAL, "ONE_ACCEPTED_B6_CUSTODY_STORE"},
    {"custody_store_id", UUID, NULL}
};
/* Accepted clarification §§2/4/5; exact report and attempt-store grammar only. */
static const struct field report_fields[] = {
    {"schema", LITERAL, "B6_ACCOUNT_RECONCILIATION_V1"}, {"store_id", UUID, NULL},
    {"commission_id", ID, NULL}, {"attempt_number", U64PLUS, NULL},
    {"native_binding_event_sha256", BIND_REF, "NOT_BOUND"},
    {"admission_event_sha256", ADMIT_REF, "NOT_ADMITTED"},
    {"procedure_sha256", HEX, NULL}, {"verifier_materialization_sha256", HEX, NULL},
    {"execution_evidence_class", STATUS, NULL}, {"native_wait_result", STATUS, NULL},
    {"native_terminal_check", STATUS, NULL}, {"workload_check", STATUS, NULL},
    {"process_reconciliation", STATUS, NULL}, {"output_reconciliation", STATUS, NULL},
    {"host_continuity", STATUS, NULL}, {"result", RESULT, NULL},
    {"evidence_manifest_sha256", HEX, NULL}
};
static const struct field account_store_fields[] = {
    {"schema", LITERAL, "B6_ATTEMPT_ACCOUNT_STORE_V1"}, {"store_id", UUID, NULL},
    {"format_version", LITERAL, "1"}
};
#define SPEC(id, name, rows, max) {id, name, rows, sizeof(rows)/sizeof(rows[0]), max}
static const struct specification specifications[] = {
    SPEC(B6_SYNTAX_STORE_V2, "B6_CUSTODY_STORE_V2", store_fields, B6_SYNTAX_STORE_MAX),
    SPEC(B6_SYNTAX_CONTRACT_SET_V2, "B6_CUSTODY_CONTRACT_SET_V2", set_fields, B6_SYNTAX_OBJECT_MAX),
    SPEC(B6_SYNTAX_SETUP_V2, "B6_CUSTODY_SETUP_V2", setup_fields, B6_SYNTAX_OBJECT_MAX),
    SPEC(B6_SYNTAX_ASSIGNMENT_V1, "B6_EXECUTION_PRINCIPAL_ASSIGNMENT_V1", assignment_fields, B6_SYNTAX_OBJECT_MAX),
    SPEC(B6_SYNTAX_PERMIT_V2, "B6_NATIVE_EXECUTION_AUTHORITY_V2", permit_fields, B6_SYNTAX_OBJECT_MAX),
    SPEC(B6_SYNTAX_SCOPE_V2, "B6_COMMISSION_SCOPE_V2", scope_fields, B6_SYNTAX_OBJECT_MAX),
    SPEC(B6_SYNTAX_POOL_V1, "B6_EXECUTION_PRINCIPAL_POOL_V1", pool_fields, B6_SYNTAX_OBJECT_MAX),
    SPEC(B6_SYNTAX_ACCOUNT_REPORT_V1, "B6_ACCOUNT_RECONCILIATION_V1", report_fields, B6_SYNTAX_REPORT_MAX),
    SPEC(B6_SYNTAX_ACCOUNT_STORE_V1, "B6_ATTEMPT_ACCOUNT_STORE_V1", account_store_fields, B6_SYNTAX_ACCOUNT_STORE_BYTES)
};
static const struct specification *specification(enum b6_syntax_schema expected)
{
    for (size_t i = 0; i < sizeof(specifications)/sizeof(specifications[0]); ++i)
        if (specifications[i].schema == expected) return &specifications[i];
    return NULL;
}
static enum b6_syntax_error records(const unsigned char *p, size_t n,
    struct record *rows, size_t *count)
{
    if (!n || p[n-1] != '\n') return B6_SYNTAX_LEXICAL;
    size_t start = 0, tab = 0; bool have_tab = false;
    *count = 0;
    for (size_t i = 0; i < n; ++i) {
        unsigned char c = p[i];
        if (c == '\t') {
            if (have_tab || i == start) return B6_SYNTAX_LEXICAL;
            tab = i; have_tab = true;
        } else if (c == '\n') {
            if (!have_tab || i == tab+1) return B6_SYNTAX_LEXICAL;
            if (*count == B6_SYNTAX_RECORD_MAX) return B6_SYNTAX_RECORDS;
            rows[*count] = (struct record){{p+start, tab-start}, {p+tab+1, i-tab-1}};
            ++*count; start = i+1; have_tab = false;
        } else if (c < 32 || c >= 127) return B6_SYNTAX_LEXICAL;
    }
    return B6_SYNTAX_OK;
}
static enum b6_syntax_error atom(struct span value, const struct field *field, union atom *out)
{
    switch (field->type) {
    case LITERAL: return equal(value, field->literal) ? B6_SYNTAX_OK : B6_SYNTAX_VALUE;
    case HEX: return b6_syntax_hex(value.p, value.n, &out->hex);
    case UUID: return b6_syntax_uuid(value.p, value.n, &out->uuid);
    case ID: return b6_syntax_id(value.p, value.n, &out->id);
    case U64PLUS: return b6_syntax_u64plus(value.p, value.n, &out->number);
    case UID: return b6_syntax_uid(value.p, value.n, &out->uid);
    case REF: return b6_syntax_ref(value.p, value.n, &out->reference);
    case PATH: return b6_syntax_path(value.p, value.n, &out->path);
    case CS: return b6_syntax_cs(value.p, value.n, &out->number);
    case STATUS:
        if (equal(value, "PASS")) out->status = B6_SYNTAX_STATUS_PASS;
        else if (equal(value, "FAIL")) out->status = B6_SYNTAX_STATUS_FAIL;
        else if (equal(value, "UNAVAILABLE")) out->status = B6_SYNTAX_STATUS_UNAVAILABLE;
        else if (equal(value, "NOT_APPLICABLE")) out->status = B6_SYNTAX_STATUS_NOT_APPLICABLE;
        else return B6_SYNTAX_VALUE;
        return B6_SYNTAX_OK;
    case RESULT:
        if (equal(value, "ACCEPTED")) out->result = B6_SYNTAX_RESULT_ACCEPTED;
        else if (equal(value, "RECONCILED_FAILED")) out->result = B6_SYNTAX_RESULT_RECONCILED_FAILED;
        else if (equal(value, "INDETERMINATE")) out->result = B6_SYNTAX_RESULT_INDETERMINATE;
        else return B6_SYNTAX_VALUE;
        return B6_SYNTAX_OK;
    case BIND_REF: case ADMIT_REF:
        if (equal(value, field->literal)) {
            out->event_ref.kind = field->type == BIND_REF ?
                B6_SYNTAX_EVENT_NOT_BOUND : B6_SYNTAX_EVENT_NOT_ADMITTED;
        } else {
            if (b6_syntax_hex(value.p, value.n, &out->event_ref.digest) != B6_SYNTAX_OK)
                return B6_SYNTAX_VALUE;
            out->event_ref.kind = B6_SYNTAX_EVENT_DIGEST;
        }
        return B6_SYNTAX_OK;
    case HEAD:
        if (equal(value, "GENESIS")) { out->head.kind = B6_SYNTAX_HEAD_GENESIS; return B6_SYNTAX_OK; }
        if (b6_syntax_hex(value.p, value.n, &out->head.digest) != B6_SYNTAX_OK) return B6_SYNTAX_VALUE;
        out->head.kind = B6_SYNTAX_HEAD_DIGEST; return B6_SYNTAX_OK;
    }
    return B6_SYNTAX_ARGUMENT;
}
enum b6_syntax_error b6_syntax_parse(const unsigned char *p, size_t n,
    enum b6_syntax_schema expected, struct b6_syntax_document *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_document){0};
    const struct specification *spec = specification(expected);
    if (!spec || !p) return B6_SYNTAX_ARGUMENT;
    if (n > spec->maximum) return B6_SYNTAX_SIZE;
    struct record rows[B6_SYNTAX_RECORD_MAX]; size_t count = 0;
    enum b6_syntax_error error = records(p, n, rows, &count);
    if (error != B6_SYNTAX_OK) return error;
    if (!equal(rows[0].key, "schema") || !equal(rows[0].value, spec->literal)) return B6_SYNTAX_SCHEMA;
    if (count != spec->count) return B6_SYNTAX_RECORDS;
    /* Largest accepted schema is the 17-record report; no unbounded allocation. */
    union atom values[17] = {0};
    if (count > sizeof(values)/sizeof(values[0])) return B6_SYNTAX_RECORDS;
    for (size_t i = 0; i < count; ++i) {
        if (!equal(rows[i].key, spec->fields[i].key)) return B6_SYNTAX_ORDER;
        error = atom(rows[i].value, &spec->fields[i], &values[i]);
        if (error != B6_SYNTAX_OK) return error;
    }
    struct b6_syntax_document parsed = {0};
    parsed.schema = expected;
    switch (expected) {
    case B6_SYNTAX_STORE_V2:
        parsed.data.store = (struct b6_syntax_store){values[1].uuid, values[2].hex}; break;
    case B6_SYNTAX_CONTRACT_SET_V2:
        parsed.data.contract_set = (struct b6_syntax_contract_set){values[1].hex, values[2].hex,
            values[3].hex, values[4].hex, values[5].hex, values[6].hex}; break;
    case B6_SYNTAX_SETUP_V2:
        parsed.data.setup = (struct b6_syntax_setup){values[1].uuid, values[2].hex, values[3].hex,
            values[4].hex, values[5].hex, values[6].hex, values[7].hex, values[8].hex, values[10].hex}; break;
    case B6_SYNTAX_ASSIGNMENT_V1:
        parsed.data.assignment = (struct b6_syntax_assignment){values[1].uuid, values[2].uuid,
            values[3].hex, values[4].number, values[5].uid, values[6].uid, values[7].head}; break;
    case B6_SYNTAX_PERMIT_V2:
        parsed.data.permit = (struct b6_syntax_permit){values[3].reference, values[4].id,
            values[5].uid, values[6].uid, values[7].hex, values[8].path, values[9].uuid,
            values[10].number, values[11].number, values[12].hex, values[13].hex,
            values[14].hex, values[15].hex}; break;
    case B6_SYNTAX_SCOPE_V2:
        parsed.data.scope = (struct b6_syntax_scope){values[1].id, values[2].reference,
            values[3].uuid, values[4].uid, values[5].uid, values[6].hex, values[7].path,
            values[8].hex, values[9].hex, values[10].hex, values[11].number, values[12].number}; break;
    case B6_SYNTAX_POOL_V1:
        parsed.data.pool = (struct b6_syntax_pool){values[1].uuid, values[2].reference,
            values[3].uid, values[4].uid, values[11].uuid}; break;
    case B6_SYNTAX_ACCOUNT_REPORT_V1:
        parsed.data.report = (struct b6_syntax_report){values[1].uuid, values[2].id,
            values[3].number, values[4].event_ref, values[5].event_ref,
            values[6].hex, values[7].hex, values[8].status, values[9].status,
            values[10].status, values[11].status, values[12].status, values[13].status,
            values[14].status, values[15].result, values[16].hex}; break;
    case B6_SYNTAX_ACCOUNT_STORE_V1:
        parsed.data.account_store = (struct b6_syntax_account_store){values[1].uuid,
            B6_SYNTAX_FORMAT_V1}; break;
    default: return B6_SYNTAX_ARGUMENT;
    }
    parsed.state = B6_SYNTAX_GRAMMAR_ONLY;
    *out = parsed; return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_serialize_account_store(
    const struct b6_syntax_account_store *value, struct b6_syntax_account_store_bytes *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_account_store_bytes){0};
    if (!value) return B6_SYNTAX_ARGUMENT;
    struct b6_syntax_uuid uuid;
    if (value->format_version != B6_SYNTAX_FORMAT_V1 || value->store_id.text[36] != '\0' ||
        b6_syntax_uuid((const unsigned char *)value->store_id.text, 36, &uuid) != B6_SYNTAX_OK)
        return B6_SYNTAX_VALUE;
    static const unsigned char prefix[] = "schema\tB6_ATTEMPT_ACCOUNT_STORE_V1\nstore_id\t";
    static const unsigned char suffix[] = "\nformat_version\t1\n";
    _Static_assert(sizeof(prefix)-1+36+sizeof(suffix)-1 == B6_SYNTAX_ACCOUNT_STORE_BYTES,
        "accepted identity must encode exactly 98 bytes");
    struct b6_syntax_account_store_bytes encoded = {0};
    memcpy(encoded.bytes, prefix, sizeof(prefix)-1);
    memcpy(encoded.bytes+sizeof(prefix)-1, uuid.text, 36);
    memcpy(encoded.bytes+sizeof(prefix)-1+36, suffix, sizeof(suffix)-1);
    encoded.length = B6_SYNTAX_ACCOUNT_STORE_BYTES;
    encoded.state = B6_SYNTAX_GRAMMAR_ONLY;
    *out = encoded; return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_read_fd(int fd, enum b6_syntax_schema expected,
    struct b6_syntax_document *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    *out = (struct b6_syntax_document){0};
    const struct specification *spec = specification(expected);
    if (!spec || fd < 0) return B6_SYNTAX_ARGUMENT;
    unsigned char bytes[B6_SYNTAX_OBJECT_MAX+1]; size_t used = 0;
    /* maximum+1 is safe: every specification uses one of the fixed bounds above. */
    while (used < spec->maximum+1) {
        ssize_t got = read(fd, bytes+used, spec->maximum+1-used);
        if (got < 0) { if (errno == EINTR) continue; return B6_SYNTAX_IO; }
        if (got == 0) return b6_syntax_parse(bytes, used, expected, out);
        used += (size_t)got;
        if (used > spec->maximum) return B6_SYNTAX_SIZE;
    }
    return B6_SYNTAX_SIZE;
}

/* Continuation05D: ATTEMPT §I plus exact accepted scalar-domain successors.
   Pure one-frame grammar. OpenSSL computes only the local raw payload digest. */
#include <inttypes.h>
#include <stdio.h>
#include <openssl/sha.h>

struct event_shape {
    const char *token;
    enum b6_syntax_event_type type;
    size_t count;
    const char *suffix[7];
};
static const struct event_shape event_shapes[] = {
    {"GENESIS", B6_SYNTAX_GENESIS, 1, {"format_version"}},
    {"REGISTER_COMMISSION", B6_SYNTAX_REGISTER_COMMISSION, 4,
        {"grant_reference", "enrollment_permit_sha256", "commission_scope_sha256", "resource_key"}},
    {"RESERVE_ATTEMPT", B6_SYNTAX_RESERVE_ATTEMPT, 3,
        {"permit_snapshot_sha256", "observation_snapshot_sha256", "native_preparation_descriptor_sha256"}},
    {"PREPARATION_INTENT", B6_SYNTAX_PREPARATION_INTENT, 1,
        {"supervisor_executable_sha256"}},
    {"BIND_NATIVE", B6_SYNTAX_BIND_NATIVE, 7,
        {"native_transaction_id", "supervisor_pid", "supervisor_starttime", "bash_pid",
         "bash_starttime", "output_device", "output_inode"}},
    {"ADMIT_EXECUTION", B6_SYNTAX_ADMIT_EXECUTION, 3,
        {"admission_permit_sha256", "admission_observation_sha256", "admission_host_evidence_sha256"}},
    {"BEGIN_RECONCILIATION", B6_SYNTAX_BEGIN_RECONCILIATION, 1, {"cause"}},
    {"RECONCILED_FAILED", B6_SYNTAX_RECONCILED_FAILED, 1,
        {"reconciliation_report_sha256"}},
    {"QUALIFICATION_ACCEPTED", B6_SYNTAX_QUALIFICATION_ACCEPTED, 1,
        {"reconciliation_report_sha256"}},
    {"CLOSE_UNUSED_COMMISSION", B6_SYNTAX_CLOSE_UNUSED_COMMISSION, 1,
        {"closure_reason"}},
    {"RETIRE_INDETERMINATE", B6_SYNTAX_RETIRE_INDETERMINATE, 2,
        {"recovery_authority_reference", "recovery_report_sha256"}}
};
static const char *const event_common_keys[] = {
    "schema", "store_id", "event_seq", "previous_event_sha256", "event_type",
    "commission_id", "attempt_number", "writer_boot_id", "writer_pid",
    "writer_starttime", "recorded_boot_cs", "evidence_sha256"
};
static const struct event_shape *event_shape(enum b6_syntax_event_type type)
{
    for (size_t i = 0; i < sizeof(event_shapes)/sizeof(event_shapes[0]); ++i)
        if (type == event_shapes[i].type) return &event_shapes[i];
    return NULL;
}
enum b6_syntax_error b6_syntax_event_token(const unsigned char *p, size_t n,
    struct b6_syntax_event_token *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    memset(out, 0, sizeof(*out));
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (!n || n > 23) return B6_SYNTAX_VALUE;
    for (size_t i = 0; i < sizeof(event_shapes)/sizeof(event_shapes[0]); ++i) {
        if (equal((struct span){p,n}, event_shapes[i].token)) {
            out->value = event_shapes[i].type;
            out->state = B6_SYNTAX_GRAMMAR_ONLY;
            return B6_SYNTAX_OK;
        }
    }
    return B6_SYNTAX_VALUE;
}
static enum b6_syntax_error event_u64(struct span s, uint64_t *out)
{
    *out = 0;
    if (s.n == 1 && s.p[0] == '0') return B6_SYNTAX_OK;
    return b6_syntax_u64plus(s.p, s.n, out);
}
static enum b6_syntax_error event_pid(struct span s, uint32_t *out)
{
    uint64_t value = 0;
    *out = 0;
    enum b6_syntax_error error = b6_syntax_u64plus(s.p, s.n, &value);
    if (error != B6_SYNTAX_OK) return error;
    if (value < 2 || value > UINT64_C(2147483647)) return B6_SYNTAX_VALUE;
    *out = (uint32_t)value;
    return B6_SYNTAX_OK;
}
static enum b6_syntax_error event_digest(const unsigned char *p, size_t n,
    struct b6_syntax_hex *out)
{
    unsigned char digest[SHA256_DIGEST_LENGTH];
    static const char digits[] = "0123456789abcdef";
    memset(out, 0, sizeof(*out));
    if (!SHA256(p, n, digest)) return B6_SYNTAX_VALUE;
    for (size_t i = 0; i < sizeof(digest); ++i) {
        out->text[2*i] = digits[digest[i] >> 4];
        out->text[2*i+1] = digits[digest[i] & 15u];
    }
    return B6_SYNTAX_OK;
}
static enum b6_syntax_error event_payload(const unsigned char *p, size_t n,
    struct b6_syntax_event_frame *parsed)
{
    struct record rows[B6_SYNTAX_RECORD_MAX];
    size_t count = 0;
    enum b6_syntax_error error = records(p, n, rows, &count);
    if (error != B6_SYNTAX_OK) return error;
    if (count < 13 || count > 19) return B6_SYNTAX_RECORDS;
    for (size_t i = 0; i < 12; ++i)
        if (!equal(rows[i].key, event_common_keys[i])) return B6_SYNTAX_ORDER;
    if (!equal(rows[0].value, "B6_ATTEMPT_ACCOUNT_EVENT_V1")) return B6_SYNTAX_SCHEMA;
    struct b6_syntax_event_token token;
    error = b6_syntax_event_token(rows[4].value.p, rows[4].value.n, &token);
    if (error != B6_SYNTAX_OK) return error;
    const struct event_shape *shape = event_shape(token.value);
    if (!shape || count != 12+shape->count) return B6_SYNTAX_RECORDS;
    for (size_t i = 0; i < shape->count; ++i)
        if (!equal(rows[12+i].key, shape->suffix[i])) return B6_SYNTAX_ORDER;
    struct b6_syntax_event_common *common = &parsed->common;
#define EVENT_PARSE(index, function, target) do { \
    error = function(rows[index].value.p, rows[index].value.n, target); \
    if (error != B6_SYNTAX_OK) return error; \
} while (0)
    EVENT_PARSE(1, b6_syntax_uuid, &common->store_id);
    EVENT_PARSE(2, b6_syntax_u64plus, &common->event_seq);
    if (token.value == B6_SYNTAX_GENESIS) {
        if (!equal(rows[3].value, "GENESIS") || !equal(rows[5].value, "STORE"))
            return B6_SYNTAX_VALUE;
        common->previous_event.kind = B6_SYNTAX_HEAD_GENESIS;
        memcpy(common->commission_id.text, "STORE", 5);
    } else {
        EVENT_PARSE(3, b6_syntax_hex, &common->previous_event.digest);
        common->previous_event.kind = B6_SYNTAX_HEAD_DIGEST;
        EVENT_PARSE(5, b6_syntax_id, &common->commission_id);
    }
    /* §I defines a conditional nonattempt zero representation in the common
       field, not an enumerated event-to-zero map. This preserves its bytes;
       it does not determine whether the event genuinely is commission/store
       with no individual attempt. That predicate requires later semantics. */
    error = event_u64(rows[6].value, &common->attempt_number);
    if (error != B6_SYNTAX_OK) return error;
    EVENT_PARSE(7, b6_syntax_uuid, &common->writer_boot_id);
    error = event_pid(rows[8].value, &common->writer_pid);
    if (error != B6_SYNTAX_OK) return error;
    EVENT_PARSE(9, b6_syntax_u64plus, &common->writer_starttime);
    EVENT_PARSE(10, b6_syntax_cs, &common->recorded_boot_cs);
    if (token.value == B6_SYNTAX_GENESIS && equal(rows[11].value, "NOT_APPLICABLE"))
        common->evidence.kind = B6_SYNTAX_EVIDENCE_NOT_APPLICABLE;
    else {
        EVENT_PARSE(11, b6_syntax_hex, &common->evidence.digest);
        common->evidence.kind = B6_SYNTAX_EVIDENCE_DIGEST;
    }
    switch (token.value) {
    case B6_SYNTAX_GENESIS: {
        struct b6_syntax_version version;
        EVENT_PARSE(12, b6_syntax_version, &version);
        parsed->data.genesis.format_version = version.value;
        break;
    }
    case B6_SYNTAX_REGISTER_COMMISSION:
        EVENT_PARSE(12, b6_syntax_ref, &parsed->data.registration.grant_reference);
        EVENT_PARSE(13, b6_syntax_hex, &parsed->data.registration.enrollment_permit_sha256);
        EVENT_PARSE(14, b6_syntax_hex, &parsed->data.registration.commission_scope_sha256);
        EVENT_PARSE(15, b6_syntax_hex, &parsed->data.registration.resource_key);
        break;
    case B6_SYNTAX_RESERVE_ATTEMPT:
        EVENT_PARSE(12, b6_syntax_hex, &parsed->data.reservation.permit_snapshot_sha256);
        EVENT_PARSE(13, b6_syntax_hex, &parsed->data.reservation.observation_snapshot_sha256);
        EVENT_PARSE(14, b6_syntax_hex, &parsed->data.reservation.native_preparation_descriptor_sha256);
        break;
    case B6_SYNTAX_PREPARATION_INTENT:
        EVENT_PARSE(12, b6_syntax_hex, &parsed->data.preparation.supervisor_executable_sha256);
        break;
    case B6_SYNTAX_BIND_NATIVE:
        EVENT_PARSE(12, b6_syntax_hex, &parsed->data.binding.native_transaction_id);
        error = event_pid(rows[13].value, &parsed->data.binding.supervisor_pid);
        if (error != B6_SYNTAX_OK) return error;
        EVENT_PARSE(14, b6_syntax_u64plus, &parsed->data.binding.supervisor_starttime);
        error = event_pid(rows[15].value, &parsed->data.binding.bash_pid);
        if (error != B6_SYNTAX_OK) return error;
        EVENT_PARSE(16, b6_syntax_u64plus, &parsed->data.binding.bash_starttime);
        error = event_u64(rows[17].value, &parsed->data.binding.output_device);
        if (error != B6_SYNTAX_OK) return error;
        EVENT_PARSE(18, b6_syntax_u64plus, &parsed->data.binding.output_inode);
        break;
    case B6_SYNTAX_ADMIT_EXECUTION:
        EVENT_PARSE(12, b6_syntax_hex, &parsed->data.admission.admission_permit_sha256);
        EVENT_PARSE(13, b6_syntax_hex, &parsed->data.admission.admission_observation_sha256);
        EVENT_PARSE(14, b6_syntax_hex, &parsed->data.admission.admission_host_evidence_sha256);
        break;
    case B6_SYNTAX_BEGIN_RECONCILIATION: {
        struct b6_syntax_cause cause;
        EVENT_PARSE(12, b6_syntax_cause, &cause);
        parsed->data.reconciliation_begin.cause = cause.value;
        break;
    }
    case B6_SYNTAX_RECONCILED_FAILED:
        EVENT_PARSE(12, b6_syntax_hex, &parsed->data.reconciled_failed.reconciliation_report_sha256);
        break;
    case B6_SYNTAX_QUALIFICATION_ACCEPTED:
        EVENT_PARSE(12, b6_syntax_hex, &parsed->data.qualification_accepted.reconciliation_report_sha256);
        break;
    case B6_SYNTAX_CLOSE_UNUSED_COMMISSION: {
        struct b6_syntax_closure closure;
        EVENT_PARSE(12, b6_syntax_closure, &closure);
        parsed->data.unused_closure.closure_reason = closure.value;
        break;
    }
    case B6_SYNTAX_RETIRE_INDETERMINATE:
        EVENT_PARSE(12, b6_syntax_ref, &parsed->data.retirement.recovery_authority_reference);
        EVENT_PARSE(13, b6_syntax_hex, &parsed->data.retirement.recovery_report_sha256);
        break;
    default: return B6_SYNTAX_VALUE;
    }
#undef EVENT_PARSE
    parsed->event_type = token.value;
    parsed->state = B6_SYNTAX_GRAMMAR_ONLY;
    return B6_SYNTAX_OK;
}
enum b6_syntax_error b6_syntax_parse_event_frame(const unsigned char *p, size_t n,
    struct b6_syntax_event_frame *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    memset(out, 0, sizeof(*out));
    if (!p) return B6_SYNTAX_ARGUMENT;
    if (!n || n > B6_SYNTAX_EVENT_FRAME_MAX) return B6_SYNTAX_SIZE;
    size_t tab = 0;
    while (tab < n && tab < 6 && p[tab] != '\t') ++tab;
    if (!tab || tab > 5 || tab == n || p[tab] != '\t') return B6_SYNTAX_LEXICAL;
    uint64_t length = 0;
    enum b6_syntax_error error = b6_syntax_u64plus(p, tab, &length);
    if (error != B6_SYNTAX_OK) return error;
    if (length > B6_SYNTAX_EVENT_PAYLOAD_MAX) return B6_SYNTAX_SIZE;
    size_t header = tab+66;
    if (n < header || p[header-1] != '\n') return B6_SYNTAX_LEXICAL;
    if (n-header != (size_t)length) return B6_SYNTAX_SIZE;
    struct b6_syntax_hex stated, actual;
    error = b6_syntax_hex(p+tab+1, 64, &stated);
    if (error != B6_SYNTAX_OK) return error;
    error = event_digest(p+header, (size_t)length, &actual);
    if (error != B6_SYNTAX_OK) return error;
    if (memcmp(stated.text, actual.text, 64) != 0) return B6_SYNTAX_VALUE;
    struct b6_syntax_event_frame parsed;
    memset(&parsed, 0, sizeof(parsed));
    error = event_payload(p+header, (size_t)length, &parsed);
    if (error != B6_SYNTAX_OK) return error;
    memcpy(out, &parsed, sizeof(parsed));
    return B6_SYNTAX_OK;
}
struct event_builder { unsigned char bytes[B6_SYNTAX_EVENT_PAYLOAD_MAX]; size_t n; };
static bool event_put(struct event_builder *b, const char *key, const char *value, size_t cap)
{
    const char *end = memchr(value, '\0', cap);
    if (!end) return false;
    size_t k = strlen(key), v = (size_t)(end-value);
    size_t remaining = sizeof(b->bytes)-b->n;
    if (k > remaining || remaining-k < 2 || v > remaining-k-2) return false;
    memcpy(b->bytes+b->n, key, k); b->n += k; b->bytes[b->n++] = '\t';
    memcpy(b->bytes+b->n, value, v); b->n += v; b->bytes[b->n++] = '\n';
    return true;
}
static bool event_number(struct event_builder *b, const char *key, uint64_t value)
{
    char text[21];
    int count = snprintf(text, sizeof(text), "%" PRIu64, value);
    return count > 0 && (size_t)count < sizeof(text) && event_put(b, key, text, sizeof(text));
}
static const char *event_cause(enum b6_syntax_cause_value value)
{
    switch (value) {
    case B6_SYNTAX_CAUSE_COMPLETION: return "COMPLETION";
    case B6_SYNTAX_CAUSE_FAILURE: return "FAILURE";
    case B6_SYNTAX_CAUSE_CANCELLATION: return "CANCELLATION";
    case B6_SYNTAX_CAUSE_RECOVERY: return "RECOVERY";
    default: return NULL;
    }
}
enum b6_syntax_error b6_syntax_serialize_event_frame(
    const struct b6_syntax_event_frame *value, struct b6_syntax_event_bytes *out)
{
    if (!out) return B6_SYNTAX_ARGUMENT;
    memset(out, 0, sizeof(*out));
    if (!value) return B6_SYNTAX_ARGUMENT;
    const struct event_shape *shape = event_shape(value->event_type);
    if (!shape || value->state != B6_SYNTAX_GRAMMAR_ONLY) return B6_SYNTAX_VALUE;
    struct event_builder payload = {{0},0};
    const struct b6_syntax_event_common *common = &value->common;
#define EVENT_TEXT(key, text) do { if (!event_put(&payload, key, text, sizeof(text))) return B6_SYNTAX_VALUE; } while (0)
#define EVENT_NUM(key, number) do { if (!event_number(&payload, key, number)) return B6_SYNTAX_VALUE; } while (0)
    EVENT_TEXT("schema", "B6_ATTEMPT_ACCOUNT_EVENT_V1");
    EVENT_TEXT("store_id", common->store_id.text);
    EVENT_NUM("event_seq", common->event_seq);
    if (common->previous_event.kind == B6_SYNTAX_HEAD_GENESIS) {
        EVENT_TEXT("previous_event_sha256", "GENESIS");
    } else if (common->previous_event.kind == B6_SYNTAX_HEAD_DIGEST) {
        EVENT_TEXT("previous_event_sha256", common->previous_event.digest.text);
    } else return B6_SYNTAX_VALUE;
    if (!event_put(&payload, "event_type", shape->token, strlen(shape->token)+1)) return B6_SYNTAX_VALUE;
    EVENT_TEXT("commission_id", common->commission_id.text);
    EVENT_NUM("attempt_number", common->attempt_number);
    EVENT_TEXT("writer_boot_id", common->writer_boot_id.text);
    EVENT_NUM("writer_pid", common->writer_pid);
    EVENT_NUM("writer_starttime", common->writer_starttime);
    EVENT_NUM("recorded_boot_cs", common->recorded_boot_cs);
    if (common->evidence.kind == B6_SYNTAX_EVIDENCE_NOT_APPLICABLE) {
        EVENT_TEXT("evidence_sha256", "NOT_APPLICABLE");
    } else if (common->evidence.kind == B6_SYNTAX_EVIDENCE_DIGEST) {
        EVENT_TEXT("evidence_sha256", common->evidence.digest.text);
    } else return B6_SYNTAX_VALUE;
    switch (value->event_type) {
    case B6_SYNTAX_GENESIS:
        if (value->data.genesis.format_version != B6_SYNTAX_FORMAT_V1) return B6_SYNTAX_VALUE;
        EVENT_TEXT("format_version", "1"); break;
    case B6_SYNTAX_REGISTER_COMMISSION:
        EVENT_TEXT("grant_reference", value->data.registration.grant_reference.text);
        EVENT_TEXT("enrollment_permit_sha256", value->data.registration.enrollment_permit_sha256.text);
        EVENT_TEXT("commission_scope_sha256", value->data.registration.commission_scope_sha256.text);
        EVENT_TEXT("resource_key", value->data.registration.resource_key.text); break;
    case B6_SYNTAX_RESERVE_ATTEMPT:
        EVENT_TEXT("permit_snapshot_sha256", value->data.reservation.permit_snapshot_sha256.text);
        EVENT_TEXT("observation_snapshot_sha256", value->data.reservation.observation_snapshot_sha256.text);
        EVENT_TEXT("native_preparation_descriptor_sha256", value->data.reservation.native_preparation_descriptor_sha256.text); break;
    case B6_SYNTAX_PREPARATION_INTENT:
        EVENT_TEXT("supervisor_executable_sha256", value->data.preparation.supervisor_executable_sha256.text); break;
    case B6_SYNTAX_BIND_NATIVE:
        EVENT_TEXT("native_transaction_id", value->data.binding.native_transaction_id.text);
        EVENT_NUM("supervisor_pid", value->data.binding.supervisor_pid);
        EVENT_NUM("supervisor_starttime", value->data.binding.supervisor_starttime);
        EVENT_NUM("bash_pid", value->data.binding.bash_pid);
        EVENT_NUM("bash_starttime", value->data.binding.bash_starttime);
        EVENT_NUM("output_device", value->data.binding.output_device);
        EVENT_NUM("output_inode", value->data.binding.output_inode); break;
    case B6_SYNTAX_ADMIT_EXECUTION:
        EVENT_TEXT("admission_permit_sha256", value->data.admission.admission_permit_sha256.text);
        EVENT_TEXT("admission_observation_sha256", value->data.admission.admission_observation_sha256.text);
        EVENT_TEXT("admission_host_evidence_sha256", value->data.admission.admission_host_evidence_sha256.text); break;
    case B6_SYNTAX_BEGIN_RECONCILIATION: {
        const char *cause = event_cause(value->data.reconciliation_begin.cause);
        if (!cause || !event_put(&payload, "cause", cause, strlen(cause)+1)) return B6_SYNTAX_VALUE;
        break;
    }
    case B6_SYNTAX_RECONCILED_FAILED:
        EVENT_TEXT("reconciliation_report_sha256", value->data.reconciled_failed.reconciliation_report_sha256.text); break;
    case B6_SYNTAX_QUALIFICATION_ACCEPTED:
        EVENT_TEXT("reconciliation_report_sha256", value->data.qualification_accepted.reconciliation_report_sha256.text); break;
    case B6_SYNTAX_CLOSE_UNUSED_COMMISSION: {
        const char *reason;
        if (value->data.unused_closure.closure_reason == B6_SYNTAX_CLOSURE_WITHDRAWAL) reason = "WITHDRAWAL";
        else if (value->data.unused_closure.closure_reason == B6_SYNTAX_CLOSURE_EXPIRY) reason = "EXPIRY";
        else return B6_SYNTAX_VALUE;
        if (!event_put(&payload, "closure_reason", reason, strlen(reason)+1)) return B6_SYNTAX_VALUE;
        break;
    }
    case B6_SYNTAX_RETIRE_INDETERMINATE:
        EVENT_TEXT("recovery_authority_reference", value->data.retirement.recovery_authority_reference.text);
        EVENT_TEXT("recovery_report_sha256", value->data.retirement.recovery_report_sha256.text); break;
    default: return B6_SYNTAX_VALUE;
    }
#undef EVENT_TEXT
#undef EVENT_NUM
    struct b6_syntax_event_bytes encoded;
    memset(&encoded, 0, sizeof(encoded));
    struct b6_syntax_hex digest;
    enum b6_syntax_error error = event_digest(payload.bytes, payload.n, &digest);
    if (error != B6_SYNTAX_OK) return error;
    char header[72];
    int count = snprintf(header, sizeof(header), "%zu\t%s\n", payload.n, digest.text);
    if (count <= 0 || (size_t)count >= sizeof(header)) return B6_SYNTAX_SIZE;
    size_t header_length = (size_t)count;
    if (payload.n > sizeof(encoded.bytes)-header_length) return B6_SYNTAX_SIZE;
    memcpy(encoded.bytes, header, header_length);
    memcpy(encoded.bytes+header_length, payload.bytes, payload.n);
    encoded.length = header_length+payload.n;
    /* Reject malformed typed fields, enum/sentinel mismatches or domains before
       publishing anything. Reparse is representation validation, not policy. */
    struct b6_syntax_event_frame checked;
    error = b6_syntax_parse_event_frame(encoded.bytes, encoded.length, &checked);
    if (error != B6_SYNTAX_OK) return error;
    encoded.state = B6_SYNTAX_GRAMMAR_ONLY;
    memcpy(out, &encoded, sizeof(encoded));
    return B6_SYNTAX_OK;
}
