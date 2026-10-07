#define _POSIX_C_SOURCE 200809L
#include "attempt-account-schema.h"
#include <errno.h>
#include <fcntl.h>
#include <inttypes.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static size_t cases, assertions;
static const char *active;
static int wrapped_fd = -2;
static size_t read_cap, read_calls;
static unsigned interruptions;
static size_t fail_after_reads;
ssize_t __real_read(int fd, void *buffer, size_t count);
ssize_t __real___read_chk(int fd, void *buffer, size_t count, size_t buffer_size);
ssize_t __wrap_read(int fd, void *buffer, size_t count)
{
    if (fd == wrapped_fd) {
        ++read_calls;
        if (fail_after_reads && read_calls>fail_after_reads) { errno=EIO; return -1; }
        if (interruptions) { --interruptions; errno=EINTR; return -1; }
        if (read_cap && count>read_cap) count=read_cap;
    }
    return __real_read(fd,buffer,count);
}
/* Ubuntu's fortified build may emit __read_chk instead of read. Preserve
   its bounds check and apply the same test-only short-read/EINTR injection. */
ssize_t __wrap___read_chk(int fd, void *buffer, size_t count, size_t buffer_size)
{
    if (count>buffer_size) return __real___read_chk(fd,buffer,count,buffer_size);
    if (fd == wrapped_fd) {
        ++read_calls;
        if (fail_after_reads && read_calls>fail_after_reads) { errno=EIO; return -1; }
        if (interruptions) { --interruptions; errno=EINTR; return -1; }
        if (read_cap && count>read_cap) count=read_cap;
    }
    return __real___read_chk(fd,buffer,count,buffer_size);
}
static void check(int ok, const char *reason)
{
    ++assertions;
    if (!ok) { fprintf(stderr, "FAIL case=%s assertion=%s\n", active, reason); exit(1); }
}
static void begin(const char *name) { active = name; ++cases; printf("CASE\t%s\n", name); }
#define CHECK(x) check((x), #x)
static const char hex[] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
static const char uuid[] = "01234567-89ab-cdef-0123-456789abcdef";
struct fixture { enum b6_syntax_schema schema; unsigned char bytes[8300]; size_t n; };
static void add(struct fixture *f, const char *key, const char *value)
{
    int got = snprintf((char *)f->bytes+f->n, sizeof(f->bytes)-f->n, "%s\t%s\n", key, value);
    if (got < 0 || (size_t)got >= sizeof(f->bytes)-f->n) abort();
    f->n += (size_t)got;
}
static struct fixture canonical(enum b6_syntax_schema schema)
{
    struct fixture f = {.schema = schema};
    switch (schema) {
    case B6_SYNTAX_STORE_V2:
        add(&f,"schema","B6_CUSTODY_STORE_V2"); add(&f,"store_id",uuid);
        add(&f,"execution_principal_pool_sha256",hex); break;
    case B6_SYNTAX_CONTRACT_SET_V2:
        add(&f,"schema","B6_CUSTODY_CONTRACT_SET_V2");
        add(&f,"exclusive_principal_contract_sha256",hex); add(&f,"witness_gate_contract_sha256",hex);
        add(&f,"custody_account_contract_sha256",hex); add(&f,"helper_protocol_sha256",hex);
        add(&f,"delegation_contract_sha256",hex); add(&f,"predecessor_amendments_sha256",hex); break;
    case B6_SYNTAX_SETUP_V2:
        add(&f,"schema","B6_CUSTODY_SETUP_V2"); add(&f,"generation",uuid);
        add(&f,"delegation_sha256",hex); add(&f,"commission_scope_sha256",hex);
        add(&f,"release_descriptor_sha256",hex); add(&f,"materialization_manifest_sha256",hex);
        add(&f,"contract_set_sha256",hex); add(&f,"execution_principal_assignment_sha256",hex);
        add(&f,"initial_anchor_sha256",hex);
        add(&f,"topology","FRESH_SB_RO_PRIVATE_VIEW_EXCLUSIVE_E_TRACKED_CONSUMERS_V2");
        add(&f,"source_index_sha256",hex); break;
    case B6_SYNTAX_ASSIGNMENT_V1:
        add(&f,"schema","B6_EXECUTION_PRINCIPAL_ASSIGNMENT_V1"); add(&f,"generation",uuid);
        add(&f,"custody_store_id",uuid); add(&f,"pool_sha256",hex); add(&f,"allocation_ordinal","1");
        add(&f,"execution_uid","1"); add(&f,"execution_gid","1");
        add(&f,"prior_custody_head_sha256","GENESIS"); add(&f,"reuse","NEVER"); break;
    default: abort();
    }
    return f;
}
static struct fixture replace(const struct fixture *base, const char *key, const char *value)
{
    struct fixture f = *base; size_t start = 0, key_n = strlen(key), value_n = strlen(value);
    while (start < f.n) {
        size_t end = start; while (end < f.n && f.bytes[end] != '\n') ++end;
        if (end-start > key_n && memcmp(f.bytes+start,key,key_n)==0 && f.bytes[start+key_n]=='\t') {
            size_t at = start+key_n+1;
            size_t old = end-at;
            if (f.n-old+value_n >= sizeof f.bytes) abort();
            memmove(f.bytes+at+value_n,f.bytes+end,f.n-end);
            memcpy(f.bytes+at,value,value_n); f.n=f.n-old+value_n; f.bytes[f.n]=0; return f;
        }
        start=end+1;
    }
    abort();
}
static void document_case(const char *name, struct fixture f, enum b6_syntax_error want)
{
    begin(name); struct b6_syntax_document out;
    memset(&out, 0xa5, sizeof out);
    CHECK(b6_syntax_parse(f.bytes,f.n,f.schema,&out)==want);
    CHECK(out.state==(want==B6_SYNTAX_OK ? B6_SYNTAX_GRAMMAR_ONLY : B6_SYNTAX_INVALID));
    CHECK(out.schema==(want==B6_SYNTAX_OK ? f.schema : B6_SYNTAX_NO_SCHEMA));
    if (want!=B6_SYNTAX_OK) {
        const unsigned char *p=(const unsigned char *)&out.data;
        int cleared=1; for (size_t i=0;i<sizeof out.data;++i) if (p[i]!=0) cleared=0;
        CHECK(cleared);
    }
}
static struct fixture rename_key(struct fixture f, const char *old, const char *key)
{
    size_t old_n=strlen(old), key_n=strlen(key), start=0;
    while(start<f.n) {
        if(f.n-start>old_n && memcmp(f.bytes+start,old,old_n)==0 && f.bytes[start+old_n]=='\t') {
            if(f.n-old_n+key_n>=sizeof f.bytes) abort();
            memmove(f.bytes+start+key_n,f.bytes+start+old_n,f.n-start-old_n);
            memcpy(f.bytes+start,key,key_n); f.n=f.n-old_n+key_n; f.bytes[f.n]=0; return f;
        }
        while(start<f.n && f.bytes[start]!='\n') ++start;
        ++start;
    }
    abort();
}
static void primitives(void)
{
    struct b6_syntax_hex h; struct b6_syntax_uuid u; struct b6_syntax_id id;
    begin("hex-canonical"); CHECK(b6_syntax_hex((const unsigned char *)hex,64,&h)==B6_SYNTAX_OK);
    CHECK(strcmp(h.text,hex)==0);
    const char *bad_hex[]={"", "abc", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde",
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0",
        "G123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        "A123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"};
    const char *hex_names[]={"hex-empty","hex-short","hex-63","hex-65","hex-nonhex","hex-uppercase"};
    for(size_t i=0;i<sizeof bad_hex/sizeof bad_hex[0];++i) {
        begin(hex_names[i]); memset(&h,0xa5,sizeof h);
        CHECK(b6_syntax_hex((const unsigned char *)bad_hex[i],strlen(bad_hex[i]),&h)==B6_SYNTAX_VALUE);
        CHECK(h.text[0]==0);
    }
    begin("uuid-canonical"); CHECK(b6_syntax_uuid((const unsigned char *)uuid,36,&u)==B6_SYNTAX_OK);
    CHECK(strcmp(u.text,uuid)==0);
    const char *bad_uuid[]={"", "01234567-89ab-cdef-0123-456789abcde", "01234567-89ab-cdef-0123-456789abcdef0",
        "01234567_89ab-cdef-0123-456789abcdef", "01234567-89Ab-cdef-0123-456789abcdef",
        "01234567-89gb-cdef-0123-456789abcdef"};
    const char *uuid_names[]={"uuid-empty","uuid-35","uuid-37","uuid-hyphen","uuid-uppercase","uuid-nonhex"};
    for(size_t i=0;i<sizeof bad_uuid/sizeof bad_uuid[0];++i) {
        begin(uuid_names[i]); memset(&u,0xa5,sizeof u);
        CHECK(b6_syntax_uuid((const unsigned char *)bad_uuid[i],strlen(bad_uuid[i]),&u)==B6_SYNTAX_VALUE);
        CHECK(u.text[0]==0);
    }
    begin("uuid-no-invented-variant-rule");
    CHECK(b6_syntax_uuid((const unsigned char *)"00000000-0000-0000-0000-000000000000",36,&u)==B6_SYNTAX_OK);
    char id_max[65]; memset(id_max,'A',64); id_max[64]=0;
    const char *good_id[]={"a","0","A_a-9","store",id_max};
    const char *id_good_names[]={"id-min","id-digit-first","id-punctuation-after-first","id-not-case-folded","id-64"};
    for(size_t i=0;i<sizeof good_id/sizeof good_id[0];++i) {
        begin(id_good_names[i]); CHECK(b6_syntax_id((const unsigned char *)good_id[i],strlen(good_id[i]),&id)==B6_SYNTAX_OK);
        CHECK(strcmp(id.text,good_id[i])==0);
    }
    char id_long[66]; memset(id_long,'A',65); id_long[65]=0;
    const char *bad_id[]={"","STORE","_a","-a","a.b","a b",id_long};
    const char *id_names[]={"id-empty","id-reserved","id-underscore-first","id-hyphen-first","id-dot","id-space","id-65"};
    for(size_t i=0;i<sizeof bad_id/sizeof bad_id[0];++i) {
        begin(id_names[i]); memset(&id,0xa5,sizeof id);
        CHECK(b6_syntax_id((const unsigned char *)bad_id[i],strlen(bad_id[i]),&id)==B6_SYNTAX_VALUE);
        CHECK(id.text[0]==0);
    }
    const char *valid_num[]={"1","9","10","18446744073709551615"};
    const uint64_t valid_values[]={1,9,10,UINT64_MAX};
    const char *num_good_names[]={"u64-min","u64-one-digit","u64-two-digits","u64-max"};
    for(size_t i=0;i<sizeof valid_num/sizeof valid_num[0];++i) {
        uint64_t v=0; begin(num_good_names[i]);
        CHECK(b6_syntax_u64plus((const unsigned char *)valid_num[i],strlen(valid_num[i]),&v)==B6_SYNTAX_OK);
        CHECK(v==valid_values[i]);
    }
    const char *bad_num[]={"","0","01","+1","-1","1x"," 1","1 ","18446744073709551616","999999999999999999999","1.0"};
    const char *num_names[]={"u64-empty","u64-zero","u64-leading-zero","u64-plus","u64-minus","u64-partial",
        "u64-leading-space","u64-trailing-space","u64-overflow","u64-21-digits","u64-decimal-point"};
    for(size_t i=0;i<sizeof bad_num/sizeof bad_num[0];++i) {
        uint64_t v=77; begin(num_names[i]);
        CHECK(b6_syntax_u64plus((const unsigned char *)bad_num[i],strlen(bad_num[i]),&v)==B6_SYNTAX_VALUE);
        CHECK(v==0);
    }
    begin("uid-min"); uint32_t v=0; CHECK(b6_syntax_uid((const unsigned char *)"1",1,&v)==B6_SYNTAX_OK); CHECK(v==1);
    begin("uid-max"); CHECK(b6_syntax_uid((const unsigned char *)"4294967294",10,&v)==B6_SYNTAX_OK); CHECK(v==UINT32_C(4294967294));
    const char *bad_uid[]={"0","4294967295","4294967296","18446744073709551615","01"};
    const char *uid_names[]={"uid-zero","uid-reserved-max","uid-32-bit-overflow","uid-u64-max","uid-leading-zero"};
    for(size_t i=0;i<sizeof bad_uid/sizeof bad_uid[0];++i) {
        begin(uid_names[i]); v=77;
        CHECK(b6_syntax_uid((const unsigned char *)bad_uid[i],strlen(bad_uid[i]),&v)==B6_SYNTAX_VALUE); CHECK(v==0);
    }
    begin("primitive-null-input-clears"); uint64_t wide=77;
    CHECK(b6_syntax_hex(NULL,64,&h)==B6_SYNTAX_ARGUMENT); CHECK(h.text[0]==0);
    CHECK(b6_syntax_uuid(NULL,36,&u)==B6_SYNTAX_ARGUMENT); CHECK(u.text[0]==0);
    CHECK(b6_syntax_id(NULL,1,&id)==B6_SYNTAX_ARGUMENT); CHECK(id.text[0]==0);
    CHECK(b6_syntax_u64plus(NULL,1,&wide)==B6_SYNTAX_ARGUMENT); CHECK(wide==0);
    CHECK(b6_syntax_uid(NULL,1,&v)==B6_SYNTAX_ARGUMENT); CHECK(v==0);
    begin("primitive-null-output");
    CHECK(b6_syntax_hex((const unsigned char *)hex,64,NULL)==B6_SYNTAX_ARGUMENT);
    CHECK(b6_syntax_uuid((const unsigned char *)uuid,36,NULL)==B6_SYNTAX_ARGUMENT);
    CHECK(b6_syntax_id((const unsigned char *)"a",1,NULL)==B6_SYNTAX_ARGUMENT);
    CHECK(b6_syntax_u64plus((const unsigned char *)"1",1,NULL)==B6_SYNTAX_ARGUMENT);
    CHECK(b6_syntax_uid((const unsigned char *)"1",1,NULL)==B6_SYNTAX_ARGUMENT);
    begin("primitive-embedded-nul"); unsigned char h_nul[64]; memcpy(h_nul,hex,64); h_nul[32]=0;
    CHECK(b6_syntax_hex(h_nul,sizeof h_nul,&h)==B6_SYNTAX_VALUE);
    CHECK(b6_syntax_u64plus((const unsigned char *)"1\0002",3,&wide)==B6_SYNTAX_VALUE); CHECK(wide==0);
}
static void documents(void)
{
    struct fixture s=canonical(B6_SYNTAX_STORE_V2), a=canonical(B6_SYNTAX_ASSIGNMENT_V1);
    struct fixture c=canonical(B6_SYNTAX_CONTRACT_SET_V2), t=canonical(B6_SYNTAX_SETUP_V2), f;
    document_case("store-canonical",s,B6_SYNTAX_OK);
    document_case("set-canonical",c,B6_SYNTAX_OK);
    document_case("setup-canonical",t,B6_SYNTAX_OK);
    document_case("assignment-genesis",a,B6_SYNTAX_OK);
    f=replace(&a,"prior_custody_head_sha256",hex); document_case("assignment-head-digest",f,B6_SYNTAX_OK);
    f=replace(&a,"allocation_ordinal","18446744073709551615"); f=replace(&f,"execution_uid","4294967294");
    f=replace(&f,"execution_gid","4294967294"); document_case("assignment-numeric-maxima",f,B6_SYNTAX_OK);
    begin("syntax-does-not-assert-allocation-semantics");
    f=replace(&a,"execution_gid","2"); struct b6_syntax_document out;
    CHECK(b6_syntax_parse(f.bytes,f.n,f.schema,&out)==B6_SYNTAX_OK);
    CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(out.data.assignment.execution_uid!=out.data.assignment.execution_gid);
    begin("parsed-data-copied-no-input-lifetime");
    CHECK(b6_syntax_parse(s.bytes,s.n,s.schema,&out)==B6_SYNTAX_OK); memset(s.bytes,0,sizeof s.bytes);
    CHECK(strcmp(out.data.store.store_id.text,uuid)==0); CHECK(strcmp(out.data.store.execution_principal_pool_sha256.text,hex)==0);
    s=canonical(B6_SYNTAX_STORE_V2);
    document_case("store-empty-value",replace(&s,"store_id",""),B6_SYNTAX_LEXICAL);
    f=s; f.n=0; document_case("empty-document",f,B6_SYNTAX_LEXICAL);
    f=s; --f.n; document_case("missing-final-lf",f,B6_SYNTAX_LEXICAL);
    f=s; f.bytes[f.n++]='x'; document_case("trailing-byte",f,B6_SYNTAX_LEXICAL);
    f=s; f.bytes[f.n++]='\n'; document_case("extra-blank-line",f,B6_SYNTAX_LEXICAL);
    f=s; f.bytes[10]=0; document_case("embedded-nul-document",f,B6_SYNTAX_LEXICAL);
    f=s; f.bytes[f.n-2]='\r'; document_case("embedded-cr",f,B6_SYNTAX_LEXICAL);
    f=s; f.bytes[3]=0x80; document_case("non-ascii",f,B6_SYNTAX_LEXICAL);
    f=s; f.bytes[3]=0x7f; document_case("del-byte",f,B6_SYNTAX_LEXICAL);
    f=s; f.bytes[3]=1; document_case("control-byte",f,B6_SYNTAX_LEXICAL);
    f=s; memmove(f.bytes+3,f.bytes,f.n); f.bytes[0]=0xef; f.bytes[1]=0xbb; f.bytes[2]=0xbf; f.n+=3;
    document_case("bom",f,B6_SYNTAX_LEXICAL);
    f=s; f.bytes[6]=' '; document_case("no-tab",f,B6_SYNTAX_LEXICAL);
    f=s; memcpy(f.bytes,"#comment\n",9); f.n=9; document_case("comment-record",f,B6_SYNTAX_LEXICAL);
    f=replace(&s,"store_id","x\ty"); document_case("extra-tab-column",f,B6_SYNTAX_LEXICAL);
    f=s; memmove(f.bytes+1,f.bytes,f.n); f.bytes[0]=' '; ++f.n; document_case("leading-key-space",f,B6_SYNTAX_SCHEMA);
    f=s; f.bytes[0]='\t'; document_case("empty-key",f,B6_SYNTAX_LEXICAL);
    document_case("trailing-value-space",replace(&s,"store_id","01234567-89ab-cdef-0123-456789abcdef "),B6_SYNTAX_VALUE);
    document_case("leading-value-space",replace(&s,"store_id"," 01234567-89ab-cdef-0123-456789abcdef"),B6_SYNTAX_VALUE);
    f=s; add(&f,"unknown","x"); document_case("too-many-records",f,B6_SYNTAX_RECORDS);
    f=s; f.n=(size_t)((unsigned char *)strstr((char *)f.bytes,"execution_principal_pool_sha256")-f.bytes);
    document_case("too-few-records",f,B6_SYNTAX_RECORDS);
    f=s; memcpy(f.bytes+strlen("schema\tB6_CUSTODY_STORE_V2\n"),"unknown_",8); document_case("unknown-key",f,B6_SYNTAX_ORDER);
    f=s; memcpy(f.bytes+strlen("schema\tB6_CUSTODY_STORE_V2\n"),"STORE_ID",8); document_case("case-folded-key",f,B6_SYNTAX_ORDER);
    f=s; add(&f,"store_id",uuid); document_case("duplicate-added-record",f,B6_SYNTAX_RECORDS);
    document_case("old-schema",replace(&s,"schema","B6_CUSTODY_STORE_V1"),B6_SYNTAX_SCHEMA);
    document_case("future-schema",replace(&s,"schema","B6_CUSTODY_STORE_V3"),B6_SYNTAX_SCHEMA);
    document_case("zero-padded-version",replace(&s,"schema","B6_CUSTODY_STORE_V02"),B6_SYNTAX_SCHEMA);
    document_case("mixed-case-schema",replace(&s,"schema","b6_CUSTODY_STORE_V2"),B6_SYNTAX_SCHEMA);
    document_case("schema-prefix",replace(&s,"schema","B6_CUSTODY_STORE_V2_extra"),B6_SYNTAX_SCHEMA);
    document_case("latest-schema",replace(&s,"schema","B6_CUSTODY_STORE_latest"),B6_SYNTAX_SCHEMA);
    document_case("unknown-schema",replace(&s,"schema","B6_UNKNOWN_V2"),B6_SYNTAX_SCHEMA);
    document_case("assignment-not-renamed-v2",replace(&a,"schema","B6_EXECUTION_PRINCIPAL_ASSIGNMENT_V2"),B6_SYNTAX_SCHEMA);
    f=s; f.schema=B6_SYNTAX_SETUP_V2; document_case("wrong-requested-schema",f,B6_SYNTAX_SCHEMA);
    f=s; f.schema=B6_SYNTAX_NO_SCHEMA; document_case("no-default-dispatch",f,B6_SYNTAX_ARGUMENT);
    f=s; f.schema=(enum b6_syntax_schema)999; document_case("unknown-dispatch-enum",f,B6_SYNTAX_ARGUMENT);
    const char *blocked[]={"B6_NATIVE_EXECUTION_AUTHORITY_V2","B6_COMMISSION_SCOPE_V2","B6_EXECUTION_PRINCIPAL_POOL_V1"};
    const char *blocked_names[]={"permit-inherited-gap-refused","scope-inherited-gap-refused","pool-inherited-gap-refused"};
    for(size_t i=0;i<3;++i) document_case(blocked_names[i],replace(&s,"schema",blocked[i]),B6_SYNTAX_SCHEMA);
    document_case("wrong-topology",replace(&t,"topology","PRIVATE_FRESH_COPY_SB_RO_PINNED_V1"),B6_SYNTAX_VALUE);
    document_case("wrong-reuse",replace(&a,"reuse","SOMETIMES"),B6_SYNTAX_VALUE);
    document_case("wrong-head-literal",replace(&a,"prior_custody_head_sha256","genesis"),B6_SYNTAX_VALUE);
    document_case("ordinal-overflow",replace(&a,"allocation_ordinal","18446744073709551616"),B6_SYNTAX_VALUE);
    document_case("ordinal-signed",replace(&a,"allocation_ordinal","+1"),B6_SYNTAX_VALUE);
    document_case("ordinal-leading-zero",replace(&a,"allocation_ordinal","01"),B6_SYNTAX_VALUE);
    document_case("assignment-uid-out-of-range",replace(&a,"execution_uid","4294967295"),B6_SYNTAX_VALUE);
    document_case("assignment-gid-zero",replace(&a,"execution_gid","0"),B6_SYNTAX_VALUE);
    const struct fixture all[]={s,c,t,a}; const char *names[]={"store","set","setup","assignment"};
    for(size_t j=0;j<4;++j) {
        f=all[j]; size_t start=0, row=0;
        while(start<f.n) {
            size_t end=start; while(end<f.n && f.bytes[end]!='\n') ++end;
            if(row>0) {
                struct fixture bad=f; bad.bytes[start]='X'; char name[80];
                (void)snprintf(name,sizeof name,"%s-field-%zu-unknown-key",names[j],row+1);
                document_case(name,bad,B6_SYNTAX_ORDER);
            }
            ++row; start=end+1;
        }
        f=all[j]; size_t first=0, second, third;
        while(f.bytes[first]!='\n') ++first;
        ++first; second=first;
        while(f.bytes[second]!='\n') ++second;
        ++second; third=second;
        while(f.bytes[third]!='\n') ++third;
        ++third;
        unsigned char temp[1000]; size_t second_n=second-first, third_n=third-second;
        memcpy(temp,f.bytes+first,second_n); memmove(f.bytes+first,f.bytes+second,third_n);
        memcpy(f.bytes+first+third_n,temp,second_n); char name[80];
        (void)snprintf(name,sizeof name,"%s-reordered-fields",names[j]); document_case(name,f,B6_SYNTAX_ORDER);
    }
    f=rename_key(c,"delegation_contract_sha256","helper_protocol_sha256");
    document_case("duplicate-logical-key-same-count",f,B6_SYNTAX_ORDER);
    f=s; f.n=B6_SYNTAX_STORE_MAX+1; document_case("store-contract-byte-limit",f,B6_SYNTAX_SIZE);
    f=c; f.n=B6_SYNTAX_OBJECT_MAX+1; document_case("general-contract-byte-limit",f,B6_SYNTAX_SIZE);
    f=c; f.n=SIZE_MAX; document_case("size-t-max-no-read",f,B6_SYNTAX_SIZE);
    f=(struct fixture){.schema=B6_SYNTAX_CONTRACT_SET_V2};
    for(size_t i=0;i<65;++i) add(&f,"a","b");
    document_case("contract-record-limit-65",f,B6_SYNTAX_RECORDS);
    begin("null-document-input-clears"); memset(&out,0xa5,sizeof out);
    CHECK(b6_syntax_parse(NULL,1,B6_SYNTAX_STORE_V2,&out)==B6_SYNTAX_ARGUMENT);
    CHECK(out.state==B6_SYNTAX_INVALID); CHECK(out.schema==B6_SYNTAX_NO_SCHEMA);
    begin("null-document-output"); CHECK(b6_syntax_parse(s.bytes,s.n,s.schema,NULL)==B6_SYNTAX_ARGUMENT);
    begin("success-then-failure-invalidates"); CHECK(b6_syntax_parse(s.bytes,s.n,s.schema,&out)==B6_SYNTAX_OK);
    CHECK(b6_syntax_parse(s.bytes,0,s.schema,&out)==B6_SYNTAX_LEXICAL); CHECK(out.state==B6_SYNTAX_INVALID);
    CHECK(out.schema==B6_SYNTAX_NO_SCHEMA);
    begin("assignment-typed-fields"); f=replace(&a,"allocation_ordinal","123");
    f=replace(&f,"execution_uid","456"); f=replace(&f,"execution_gid","789");
    f=replace(&f,"prior_custody_head_sha256",hex);
    CHECK(b6_syntax_parse(f.bytes,f.n,f.schema,&out)==B6_SYNTAX_OK);
    CHECK(strcmp(out.data.assignment.generation.text,uuid)==0);
    CHECK(strcmp(out.data.assignment.custody_store_id.text,uuid)==0);
    CHECK(strcmp(out.data.assignment.pool_sha256.text,hex)==0);
    CHECK(out.data.assignment.allocation_ordinal==123);
    CHECK(out.data.assignment.execution_uid==456); CHECK(out.data.assignment.execution_gid==789);
    CHECK(out.data.assignment.prior_custody_head.kind==B6_SYNTAX_HEAD_DIGEST);
    CHECK(strcmp(out.data.assignment.prior_custody_head.digest.text,hex)==0);
    CHECK(b6_syntax_parse(a.bytes,a.n,a.schema,&out)==B6_SYNTAX_OK);
    CHECK(out.data.assignment.prior_custody_head.kind==B6_SYNTAX_HEAD_GENESIS);
    CHECK(out.data.assignment.prior_custody_head.digest.text[0]==0);
    begin("contract-set-distinct-typed-fields");
    const char *set_keys[]={"exclusive_principal_contract_sha256","witness_gate_contract_sha256",
        "custody_account_contract_sha256","helper_protocol_sha256","delegation_contract_sha256","predecessor_amendments_sha256"};
    f=c;
    for(size_t i=0;i<6;++i) { char digest[65]; memset(digest,(int)('1'+i),64); digest[64]=0; f=replace(&f,set_keys[i],digest); }
    CHECK(b6_syntax_parse(f.bytes,f.n,f.schema,&out)==B6_SYNTAX_OK);
    const struct b6_syntax_hex *set_values[]={&out.data.contract_set.exclusive_principal_contract_sha256,
        &out.data.contract_set.witness_gate_contract_sha256,&out.data.contract_set.custody_account_contract_sha256,
        &out.data.contract_set.helper_protocol_sha256,&out.data.contract_set.delegation_contract_sha256,
        &out.data.contract_set.predecessor_amendments_sha256};
    for(size_t i=0;i<6;++i) { char digest[65]; memset(digest,(int)('1'+i),64); digest[64]=0; CHECK(strcmp(set_values[i]->text,digest)==0); }
    begin("setup-distinct-typed-fields");
    const char *setup_keys[]={"delegation_sha256","commission_scope_sha256","release_descriptor_sha256",
        "materialization_manifest_sha256","contract_set_sha256","execution_principal_assignment_sha256","initial_anchor_sha256","source_index_sha256"};
    f=t;
    for(size_t i=0;i<8;++i) { char digest[65]; memset(digest,(int)('1'+i),64); digest[64]=0; f=replace(&f,setup_keys[i],digest); }
    CHECK(b6_syntax_parse(f.bytes,f.n,f.schema,&out)==B6_SYNTAX_OK);
    CHECK(strcmp(out.data.setup.generation.text,uuid)==0);
    const struct b6_syntax_hex *setup_values[]={&out.data.setup.delegation_sha256,&out.data.setup.commission_scope_sha256,
        &out.data.setup.release_descriptor_sha256,&out.data.setup.materialization_manifest_sha256,
        &out.data.setup.contract_set_sha256,&out.data.setup.execution_principal_assignment_sha256,
        &out.data.setup.initial_anchor_sha256,&out.data.setup.source_index_sha256};
    for(size_t i=0;i<8;++i) { char digest[65]; memset(digest,(int)('1'+i),64); digest[64]=0; CHECK(strcmp(setup_values[i]->text,digest)==0); }
}
static void fd_case(const char *name, struct fixture f, enum b6_syntax_error want)
{
    begin(name); FILE *file=tmpfile(); CHECK(file!=NULL);
    CHECK(fwrite(f.bytes,1,f.n,file)==f.n); CHECK(fflush(file)==0); CHECK(fseek(file,0,SEEK_SET)==0);
    struct b6_syntax_document out; memset(&out,0xa5,sizeof out);
    if(strcmp(name,"fd-interrupted-short-reads")==0 || strcmp(name,"fd-partial-io-error")==0) {
        wrapped_fd=fileno(file); read_cap=7; read_calls=0; interruptions=1;
        fail_after_reads=strcmp(name,"fd-partial-io-error")==0 ? 2u : 0u;
    }
    CHECK(b6_syntax_read_fd(fileno(file),f.schema,&out)==want);
    CHECK(out.state==(want==B6_SYNTAX_OK ? B6_SYNTAX_GRAMMAR_ONLY : B6_SYNTAX_INVALID));
    if(wrapped_fd==fileno(file)) {
        CHECK(read_calls>2); CHECK(interruptions==0); wrapped_fd=-2; read_cap=0; fail_after_reads=0;
    }
    CHECK(fclose(file)==0);
}
static void fd_tests(void)
{
    struct fixture s=canonical(B6_SYNTAX_STORE_V2), f=s;
    fd_case("fd-complete-canonical",s,B6_SYNTAX_OK);
    fd_case("fd-interrupted-short-reads",s,B6_SYNTAX_OK);
    fd_case("fd-partial-io-error",s,B6_SYNTAX_IO);
    f.n=0; fd_case("fd-empty",f,B6_SYNTAX_LEXICAL);
    f=s; --f.n; fd_case("fd-short-document",f,B6_SYNTAX_LEXICAL);
    f=s; memset(f.bytes+f.n,'x',257-f.n); f.n=257; fd_case("fd-store-one-byte-lookahead",f,B6_SYNTAX_SIZE);
    f=canonical(B6_SYNTAX_SETUP_V2); memset(f.bytes+f.n,'x',8193-f.n); f.n=8193;
    fd_case("fd-general-one-byte-lookahead",f,B6_SYNTAX_SIZE);
    begin("fd-io-error-clears"); struct b6_syntax_document out;
    memset(&out,0xa5,sizeof out); int fd=open("/dev/null",O_WRONLY); CHECK(fd>=0);
    CHECK(b6_syntax_read_fd(fd,B6_SYNTAX_STORE_V2,&out)==B6_SYNTAX_IO); CHECK(out.state==B6_SYNTAX_INVALID); CHECK(close(fd)==0);
    begin("fd-invalid-argument"); CHECK(b6_syntax_read_fd(-1,B6_SYNTAX_STORE_V2,&out)==B6_SYNTAX_ARGUMENT);
    CHECK(out.state==B6_SYNTAX_INVALID); CHECK(b6_syntax_read_fd(0,B6_SYNTAX_NO_SCHEMA,&out)==B6_SYNTAX_ARGUMENT);
    CHECK(b6_syntax_read_fd(0,B6_SYNTAX_STORE_V2,NULL)==B6_SYNTAX_ARGUMENT);
    begin("fd-pipe-complete"); int pair[2]; CHECK(pipe(pair)==0);
    CHECK(write(pair[1],s.bytes,7)==7); CHECK(write(pair[1],s.bytes+7,s.n-7)==(ssize_t)(s.n-7)); CHECK(close(pair[1])==0);
    CHECK(b6_syntax_read_fd(pair[0],s.schema,&out)==B6_SYNTAX_OK); CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);
    CHECK(close(pair[0])==0);
}
int main(void)
{
    primitives(); documents(); fd_tests();
    printf("RESULT semantic_cases=%zu assertions=%zu PASS\n",cases,assertions); return 0;
}
