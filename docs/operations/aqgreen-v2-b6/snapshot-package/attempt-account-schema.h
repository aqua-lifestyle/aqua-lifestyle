#ifndef B6_ATTEMPT_ACCOUNT_SCHEMA_H
#define B6_ATTEMPT_ACCOUNT_SCHEMA_H
#include <stddef.h>
#include <stdint.h>

/* AUTH §1/§5 bounds. Syntax only: no authority, eligibility or referent proof. */
#define B6_SYNTAX_OBJECT_MAX 8192u
#define B6_SYNTAX_RECORD_MAX 64u
#define B6_SYNTAX_STORE_MAX 256u
/* Accepted account clarification §§4-5; protocol bounds, not store proof. */
#define B6_SYNTAX_REPORT_MAX 919u
#define B6_SYNTAX_ACCOUNT_STORE_BYTES 98u
/* ATTEMPT §I: exact one-frame bounds, independent of the 8192-byte objects. */
#define B6_SYNTAX_EVENT_PAYLOAD_MAX 16384u
#define B6_SYNTAX_EVENT_FRAME_MAX 16455u

enum b6_syntax_error {
    B6_SYNTAX_OK = 0, B6_SYNTAX_ARGUMENT, B6_SYNTAX_SIZE,
    B6_SYNTAX_LEXICAL, B6_SYNTAX_RECORDS, B6_SYNTAX_ORDER,
    B6_SYNTAX_SCHEMA, B6_SYNTAX_VALUE, B6_SYNTAX_IO
};
enum b6_syntax_state { B6_SYNTAX_INVALID = 0, B6_SYNTAX_GRAMMAR_ONLY };
enum b6_syntax_schema {
    B6_SYNTAX_NO_SCHEMA = 0, B6_SYNTAX_STORE_V2,
    B6_SYNTAX_CONTRACT_SET_V2, B6_SYNTAX_SETUP_V2,
    B6_SYNTAX_ASSIGNMENT_V1, /* Exact V1 literal required by the V2 contract. */
    B6_SYNTAX_PERMIT_V2, B6_SYNTAX_SCOPE_V2, B6_SYNTAX_POOL_V1,
    B6_SYNTAX_ACCOUNT_REPORT_V1, B6_SYNTAX_ACCOUNT_STORE_V1
};
struct b6_syntax_hex { char text[65]; };
struct b6_syntax_uuid { char text[37]; };
struct b6_syntax_id { char text[65]; };
/* Inherited IPC AE scalar grammar; these are not authority/custody proofs. */
struct b6_syntax_ref { char text[257]; };
struct b6_syntax_path { char text[4096]; };
/* Tokens are representation data, never account states/transition authority. */
enum b6_syntax_format_version { B6_SYNTAX_NO_VERSION = 0, B6_SYNTAX_FORMAT_V1 };
struct b6_syntax_version {
    enum b6_syntax_state state;
    enum b6_syntax_format_version value;
};
enum b6_syntax_cause_value { B6_SYNTAX_NO_CAUSE = 0,
    B6_SYNTAX_CAUSE_COMPLETION, B6_SYNTAX_CAUSE_FAILURE,
    B6_SYNTAX_CAUSE_CANCELLATION, B6_SYNTAX_CAUSE_RECOVERY };
struct b6_syntax_cause { enum b6_syntax_state state; enum b6_syntax_cause_value value; };
enum b6_syntax_closure_value { B6_SYNTAX_NO_CLOSURE = 0,
    B6_SYNTAX_CLOSURE_WITHDRAWAL, B6_SYNTAX_CLOSURE_EXPIRY };
struct b6_syntax_closure { enum b6_syntax_state state; enum b6_syntax_closure_value value; };
enum b6_syntax_status { B6_SYNTAX_NO_STATUS = 0, B6_SYNTAX_STATUS_PASS,
    B6_SYNTAX_STATUS_FAIL, B6_SYNTAX_STATUS_UNAVAILABLE, B6_SYNTAX_STATUS_NOT_APPLICABLE };
enum b6_syntax_report_result { B6_SYNTAX_NO_RESULT = 0,
    B6_SYNTAX_RESULT_ACCEPTED, B6_SYNTAX_RESULT_RECONCILED_FAILED,
    B6_SYNTAX_RESULT_INDETERMINATE };
enum b6_syntax_event_ref_kind { B6_SYNTAX_NO_EVENT_REF = 0,
    B6_SYNTAX_EVENT_DIGEST, B6_SYNTAX_EVENT_NOT_BOUND, B6_SYNTAX_EVENT_NOT_ADMITTED };
struct b6_syntax_event_ref {
    enum b6_syntax_event_ref_kind kind;
    struct b6_syntax_hex digest;
};
struct b6_syntax_report {
    struct b6_syntax_uuid store_id;
    struct b6_syntax_id commission_id;
    uint64_t attempt_number;
    struct b6_syntax_event_ref native_binding_event, admission_event;
    struct b6_syntax_hex procedure_sha256, verifier_materialization_sha256;
    enum b6_syntax_status execution_evidence_class, native_wait_result;
    enum b6_syntax_status native_terminal_check, workload_check;
    enum b6_syntax_status process_reconciliation, output_reconciliation, host_continuity;
    enum b6_syntax_report_result result;
    struct b6_syntax_hex evidence_manifest_sha256;
};
struct b6_syntax_account_store {
    struct b6_syntax_uuid store_id;
    enum b6_syntax_format_version format_version;
};
/* Logical value and canonical byte object are deliberately distinct types. */
struct b6_syntax_account_store_bytes {
    enum b6_syntax_state state;
    size_t length;
    unsigned char bytes[B6_SYNTAX_ACCOUNT_STORE_BYTES];
};
struct b6_syntax_permit {
    struct b6_syntax_ref authority_reference;
    struct b6_syntax_id qualification_id;
    uint32_t authority_uid, authority_gid;
    struct b6_syntax_hex execution_principal_pool_sha256;
    struct b6_syntax_path output_path;
    struct b6_syntax_uuid boot_id;
    uint64_t not_before_boot_cs, not_after_boot_cs;
    struct b6_syntax_hex materialization_manifest_sha256, qualification_procedure_sha256;
    struct b6_syntax_hex host_contract_sha256, host_observation_sha256;
};
struct b6_syntax_scope {
    struct b6_syntax_id commission_id;
    struct b6_syntax_ref grant_reference;
    struct b6_syntax_uuid grant_boot_id;
    uint32_t authority_uid, authority_gid;
    struct b6_syntax_hex execution_principal_pool_sha256;
    struct b6_syntax_path output_path;
    struct b6_syntax_hex materialization_manifest_sha256, qualification_procedure_sha256;
    struct b6_syntax_hex host_contract_sha256;
    uint64_t not_before_boot_cs, not_after_boot_cs;
};
struct b6_syntax_pool {
    struct b6_syntax_uuid pool_id;
    struct b6_syntax_ref authority_reference;
    uint32_t first_id, last_id;
    struct b6_syntax_uuid custody_store_id;
};
enum b6_syntax_head_kind { B6_SYNTAX_NO_HEAD = 0,
    B6_SYNTAX_HEAD_GENESIS, B6_SYNTAX_HEAD_DIGEST };
struct b6_syntax_head { enum b6_syntax_head_kind kind; struct b6_syntax_hex digest; };
struct b6_syntax_store {
    struct b6_syntax_uuid store_id;
    struct b6_syntax_hex execution_principal_pool_sha256;
};
struct b6_syntax_contract_set {
    struct b6_syntax_hex exclusive_principal_contract_sha256;
    struct b6_syntax_hex witness_gate_contract_sha256;
    struct b6_syntax_hex custody_account_contract_sha256;
    struct b6_syntax_hex helper_protocol_sha256;
    struct b6_syntax_hex delegation_contract_sha256;
    struct b6_syntax_hex predecessor_amendments_sha256;
};
struct b6_syntax_setup {
    struct b6_syntax_uuid generation;
    struct b6_syntax_hex delegation_sha256, commission_scope_sha256;
    struct b6_syntax_hex release_descriptor_sha256, materialization_manifest_sha256;
    struct b6_syntax_hex contract_set_sha256, execution_principal_assignment_sha256;
    struct b6_syntax_hex initial_anchor_sha256, source_index_sha256;
    /* topology is checked as the sole literal; no alternate value can escape. */
};
struct b6_syntax_assignment {
    struct b6_syntax_uuid generation, custody_store_id;
    struct b6_syntax_hex pool_sha256;
    uint64_t allocation_ordinal;
    uint32_t execution_uid, execution_gid;
    struct b6_syntax_head prior_custody_head;
    /* reuse is checked as NEVER, not inferred from a default. */
};
struct b6_syntax_document {
    enum b6_syntax_state state;
    enum b6_syntax_schema schema;
    union {
        struct b6_syntax_store store;
        struct b6_syntax_contract_set contract_set;
        struct b6_syntax_setup setup;
        struct b6_syntax_assignment assignment;
        struct b6_syntax_permit permit;
        struct b6_syntax_scope scope;
        struct b6_syntax_pool pool;
        struct b6_syntax_report report;
        struct b6_syntax_account_store account_store;
    } data;
};
/* Closed wire vocabulary. A tag denotes grammatical shape, never legality. */
enum b6_syntax_event_type {
    B6_SYNTAX_NO_EVENT = 0, B6_SYNTAX_GENESIS, B6_SYNTAX_REGISTER_COMMISSION,
    B6_SYNTAX_RESERVE_ATTEMPT, B6_SYNTAX_PREPARATION_INTENT, B6_SYNTAX_BIND_NATIVE,
    B6_SYNTAX_ADMIT_EXECUTION, B6_SYNTAX_BEGIN_RECONCILIATION,
    B6_SYNTAX_RECONCILED_FAILED, B6_SYNTAX_QUALIFICATION_ACCEPTED,
    B6_SYNTAX_CLOSE_UNUSED_COMMISSION, B6_SYNTAX_RETIRE_INDETERMINATE
};
struct b6_syntax_event_token {
    enum b6_syntax_state state;
    enum b6_syntax_event_type value;
};
enum b6_syntax_evidence_kind {
    B6_SYNTAX_NO_EVIDENCE = 0, B6_SYNTAX_EVIDENCE_DIGEST,
    B6_SYNTAX_EVIDENCE_NOT_APPLICABLE
};
struct b6_syntax_event_evidence {
    enum b6_syntax_evidence_kind kind;
    struct b6_syntax_hex digest;
};
struct b6_syntax_event_common {
    struct b6_syntax_uuid store_id;
    uint64_t event_seq;
    struct b6_syntax_head previous_event;
    /* GENESIS STORE is the sole reserved-ID grammatical exception. */
    struct b6_syntax_id commission_id;
    /* §I conditional zero is represented, not licensed: genuine nonattempt
       commission/store use requires semantic evidence beyond this codec. */
    uint64_t attempt_number;
    struct b6_syntax_uuid writer_boot_id;
    uint32_t writer_pid;
    uint64_t writer_starttime, recorded_boot_cs;
    struct b6_syntax_event_evidence evidence;
};
struct b6_syntax_genesis { enum b6_syntax_format_version format_version; };
struct b6_syntax_registration {
    struct b6_syntax_ref grant_reference;
    struct b6_syntax_hex enrollment_permit_sha256, commission_scope_sha256, resource_key;
};
struct b6_syntax_reservation {
    struct b6_syntax_hex permit_snapshot_sha256, observation_snapshot_sha256;
    struct b6_syntax_hex native_preparation_descriptor_sha256;
};
struct b6_syntax_preparation { struct b6_syntax_hex supervisor_executable_sha256; };
struct b6_syntax_native_binding {
    struct b6_syntax_hex native_transaction_id;
    uint32_t supervisor_pid;
    uint64_t supervisor_starttime;
    uint32_t bash_pid;
    uint64_t bash_starttime, output_device, output_inode;
};
struct b6_syntax_admission {
    struct b6_syntax_hex admission_permit_sha256, admission_observation_sha256;
    struct b6_syntax_hex admission_host_evidence_sha256;
};
struct b6_syntax_reconciliation_begin { enum b6_syntax_cause_value cause; };
struct b6_syntax_terminal_report { struct b6_syntax_hex reconciliation_report_sha256; };
struct b6_syntax_unused_closure { enum b6_syntax_closure_value closure_reason; };
struct b6_syntax_retirement {
    struct b6_syntax_ref recovery_authority_reference;
    struct b6_syntax_hex recovery_report_sha256;
};
/* Owns all storage; no allocation, handles, runtime proofs or borrowed spans.
   Schema is always B6_ATTEMPT_ACCOUNT_EVENT_V1. No journal/state API exists here. */
struct b6_syntax_event_frame {
    enum b6_syntax_state state;
    enum b6_syntax_event_type event_type;
    struct b6_syntax_event_common common;
    union {
        struct b6_syntax_genesis genesis;
        struct b6_syntax_registration registration;
        struct b6_syntax_reservation reservation;
        struct b6_syntax_preparation preparation;
        struct b6_syntax_native_binding binding;
        struct b6_syntax_admission admission;
        struct b6_syntax_reconciliation_begin reconciliation_begin;
        struct b6_syntax_terminal_report reconciled_failed, qualification_accepted;
        struct b6_syntax_unused_closure unused_closure;
        struct b6_syntax_retirement retirement;
    } data;
};
struct b6_syntax_event_bytes {
    enum b6_syntax_state state;
    size_t length;
    unsigned char bytes[B6_SYNTAX_EVENT_FRAME_MAX];
};
enum b6_syntax_error b6_syntax_event_token(const unsigned char *, size_t,
    struct b6_syntax_event_token *);
/* Exactly one complete frame. Checks its local payload SHA, never a chain.
   Input must be readable for length bytes and must not alias output.
   All failures clear the entire output, including tag and suffix union. */
enum b6_syntax_error b6_syntax_parse_event_frame(const unsigned char *, size_t,
    struct b6_syntax_event_frame *);
/* Validates the selected typed shape before publishing canonical bytes.
   No input/output aliasing. Every failure invalidates the whole byte object. */
enum b6_syntax_error b6_syntax_serialize_event_frame(
    const struct b6_syntax_event_frame *, struct b6_syntax_event_bytes *);
/* Input spans must be readable for length bytes and must not alias outputs.
   Each failure clears the output; primitive success is grammar only too. */
enum b6_syntax_error b6_syntax_hex(const unsigned char *, size_t, struct b6_syntax_hex *);
enum b6_syntax_error b6_syntax_uuid(const unsigned char *, size_t, struct b6_syntax_uuid *);
enum b6_syntax_error b6_syntax_id(const unsigned char *, size_t, struct b6_syntax_id *);
enum b6_syntax_error b6_syntax_u64plus(const unsigned char *, size_t, uint64_t *);
enum b6_syntax_error b6_syntax_uid(const unsigned char *, size_t, uint32_t *);
enum b6_syntax_error b6_syntax_ref(const unsigned char *, size_t, struct b6_syntax_ref *);
/* Text syntax only; never resolves a path or checks the frozen output parent. */
enum b6_syntax_error b6_syntax_path(const unsigned char *, size_t, struct b6_syntax_path *);
enum b6_syntax_error b6_syntax_cs(const unsigned char *, size_t, uint64_t *);
/* Value spans only (not complete events); LF/TAB/empty/null are not tokens.
   Successful typed tokens are GRAMMAR_ONLY, without a transition predicate. */
enum b6_syntax_error b6_syntax_version(const unsigned char *, size_t, struct b6_syntax_version *);
enum b6_syntax_error b6_syntax_cause(const unsigned char *, size_t, struct b6_syntax_cause *);
enum b6_syntax_error b6_syntax_closure(const unsigned char *, size_t, struct b6_syntax_closure *);
/* Pure in-memory codec; no filesystem, provisioning, agreement or eligibility
   checks. Input must not alias output. Failure clears the entire byte object. */
enum b6_syntax_error b6_syntax_serialize_account_store(
    const struct b6_syntax_account_store *, struct b6_syntax_account_store_bytes *);
/* Caller selects one exact schema; NO_SCHEMA is not an automatic/default mode. */
enum b6_syntax_error b6_syntax_parse(const unsigned char *, size_t,
    enum b6_syntax_schema, struct b6_syntax_document *);
/* Bounded complete read of an already supplied FD, at its current offset.
   Does not close, rewind, authenticate, qualify custody, or accept paths. */
enum b6_syntax_error b6_syntax_read_fd(int, enum b6_syntax_schema,
    struct b6_syntax_document *);
#endif
