#define _POSIX_C_SOURCE 200809L
#include "attempt-account-schema.h"
#include <fcntl.h>
#include <inttypes.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static size_t cases, assertions;
static char active[160];
static void begin(const char *family, size_t ordinal, const char *kind)
{
    int n = snprintf(active, sizeof active, "%s-%zu-%s", family, ordinal, kind);
    if (n < 0 || (size_t)n >= sizeof active) abort();
    ++cases; printf("CASE\t%s\n", active);
}
static void check(int valid, const char *expression)
{
    ++assertions;
    if (!valid) { fprintf(stderr,"FAIL %s: %s\n",active,expression); exit(1); }
}
#define CHECK(x) check((x), #x)
static const char uuid[] = "01234567-89ab-cdef-0123-456789abcdef";
static const char other_uuid[] = "fedcba98-7654-3210-fedc-ba9876543210";
static const char pool_digest[] = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
static const char m_digest[] = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
static const char p_digest[] = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
static const char h_digest[] = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
static const char o_digest[] = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
static const char path[] = "/tmp/aqgreen-b6-proof-output/fixture-A_1.tsv";
static const char reference[] = "Owner:2026/Decision_02-v1.0";
struct row { const char *key, *value; };
struct fixture { enum b6_syntax_schema schema; const char *name; struct row rows[16]; size_t count; };
static struct fixture fixtures[] = {
    {B6_SYNTAX_PERMIT_V2,"permit",{
        {"schema","B6_NATIVE_EXECUTION_AUTHORITY_V2"}, {"purpose","BUBBLEWRAP_NONROOT_QUALIFICATION"},
        {"lifecycle","NATIVE_MACHINE_SERIALIZED_RETRY_V2"}, {"authority_reference",reference},
        {"qualification_id","Commission_02"}, {"authority_uid","123"}, {"authority_gid","456"},
        {"execution_principal_pool_sha256",pool_digest}, {"output_path",path}, {"boot_id",uuid},
        {"not_before_boot_cs","0"}, {"not_after_boot_cs","9223372036854775807"},
        {"materialization_manifest_sha256",m_digest}, {"qualification_procedure_sha256",p_digest},
        {"host_contract_sha256",h_digest}, {"host_observation_sha256",o_digest}},16},
    {B6_SYNTAX_SCOPE_V2,"scope",{
        {"schema","B6_COMMISSION_SCOPE_V2"}, {"commission_id","Commission_02"},
        {"grant_reference",reference}, {"grant_boot_id",uuid}, {"authority_uid","123"}, {"authority_gid","456"},
        {"execution_principal_pool_sha256",pool_digest}, {"output_path",path},
        {"materialization_manifest_sha256",m_digest}, {"qualification_procedure_sha256",p_digest},
        {"host_contract_sha256",h_digest}, {"not_before_boot_cs","0"}, {"not_after_boot_cs","9223372036854775807"},
        {"lifecycle","NATIVE_MACHINE_SERIALIZED_RETRY_V2"}},14},
    {B6_SYNTAX_POOL_V1,"pool",{
        {"schema","B6_EXECUTION_PRINCIPAL_POOL_V1"}, {"pool_id",uuid}, {"authority_reference",reference},
        {"first_id","1"}, {"last_id","4294967294"}, {"mapping","SAME_NUMERIC_UID_GID"},
        {"allocation","LOWEST_UNUSED_NEVER_REUSE"}, {"user_namespace","INITIAL"},
        {"subordinate_id_delegation","FORBIDDEN"}, {"login_and_service_use","FORBIDDEN"},
        {"launcher","ONE_ACCEPTED_B6_CUSTODY_STORE"}, {"custody_store_id",other_uuid}},12}
};
static size_t encode(const struct fixture *f, unsigned char *bytes, size_t capacity)
{
    size_t used = 0;
    for (size_t i = 0; i < f->count; ++i) {
        int n = snprintf((char *)bytes+used, capacity-used, "%s\t%s\n", f->rows[i].key, f->rows[i].value);
        if (n < 0 || (size_t)n >= capacity-used) abort();
        used += (size_t)n;
    }
    return used;
}
static void reject_bytes(const unsigned char *bytes, size_t n, enum b6_syntax_schema schema)
{
    struct b6_syntax_document out;
    memset(&out,0xa5,sizeof out);
    CHECK(b6_syntax_parse(bytes,n,schema,&out)!=B6_SYNTAX_OK);
    CHECK(out.state==B6_SYNTAX_INVALID);
    CHECK(out.schema==B6_SYNTAX_NO_SCHEMA);
    struct b6_syntax_document zero = {0};
    CHECK(memcmp(&out,&zero,sizeof out)==0);
}
static void reject(const struct fixture *f)
{
    unsigned char bytes[9000]; size_t n=encode(f,bytes,sizeof bytes);
    reject_bytes(bytes,n,f->schema);
}
static void valid_fields(const struct fixture *f, const struct b6_syntax_document *out)
{
    CHECK(out->state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(out->schema==f->schema);
    switch(f->schema) {
    case B6_SYNTAX_PERMIT_V2: {
        const struct b6_syntax_permit *p=&out->data.permit;
        CHECK(strcmp(p->authority_reference.text,reference)==0);
        CHECK(strcmp(p->qualification_id.text,"Commission_02")==0);
        CHECK(p->authority_uid==123 && p->authority_gid==456);
        CHECK(strcmp(p->execution_principal_pool_sha256.text,pool_digest)==0);
        CHECK(strcmp(p->output_path.text,path)==0); CHECK(strcmp(p->boot_id.text,uuid)==0);
        CHECK(p->not_before_boot_cs==0 && p->not_after_boot_cs==UINT64_C(9223372036854775807));
        CHECK(strcmp(p->materialization_manifest_sha256.text,m_digest)==0);
        CHECK(strcmp(p->qualification_procedure_sha256.text,p_digest)==0);
        CHECK(strcmp(p->host_contract_sha256.text,h_digest)==0); CHECK(strcmp(p->host_observation_sha256.text,o_digest)==0); break;
    }
    case B6_SYNTAX_SCOPE_V2: {
        const struct b6_syntax_scope *p=&out->data.scope;
        CHECK(strcmp(p->commission_id.text,"Commission_02")==0); CHECK(strcmp(p->grant_reference.text,reference)==0);
        CHECK(strcmp(p->grant_boot_id.text,uuid)==0); CHECK(p->authority_uid==123 && p->authority_gid==456);
        CHECK(strcmp(p->execution_principal_pool_sha256.text,pool_digest)==0); CHECK(strcmp(p->output_path.text,path)==0);
        CHECK(strcmp(p->materialization_manifest_sha256.text,m_digest)==0); CHECK(strcmp(p->qualification_procedure_sha256.text,p_digest)==0);
        CHECK(strcmp(p->host_contract_sha256.text,h_digest)==0);
        CHECK(p->not_before_boot_cs==0 && p->not_after_boot_cs==UINT64_C(9223372036854775807)); break;
    }
    case B6_SYNTAX_POOL_V1:
        CHECK(strcmp(out->data.pool.pool_id.text,uuid)==0); CHECK(strcmp(out->data.pool.authority_reference.text,reference)==0);
        CHECK(out->data.pool.first_id==1 && out->data.pool.last_id==UINT32_C(4294967294));
        CHECK(strcmp(out->data.pool.custody_store_id.text,other_uuid)==0); break;
    default: abort();
    }
}
static void scalars(void)
{
    const char *valid_refs[]={"A", reference,"0", "a..//::--__"};
    struct b6_syntax_ref ref;
    for(size_t i=0;i<sizeof valid_refs/sizeof valid_refs[0];++i) {
        begin("REF",i,"valid"); const char *s=valid_refs[i];
        CHECK(b6_syntax_ref((const unsigned char *)s,strlen(s),&ref)==B6_SYNTAX_OK); CHECK(strcmp(ref.text,s)==0);
    }
    const char *bad_refs[]={"", "_a", ".a", ":a", "/a", "-a", "a b", "a\tb", "a\nb", "a\rb", "a?b", "a%b", "a=b", "a@b", "a\\b"};
    for(size_t i=0;i<sizeof bad_refs/sizeof bad_refs[0];++i) {
        begin("REF",i,"invalid"); memset(&ref,0xa5,sizeof ref); const char *s=bad_refs[i];
        CHECK(b6_syntax_ref((const unsigned char *)s,strlen(s),&ref)==B6_SYNTAX_VALUE); CHECK(ref.text[0]==0);
    }
    unsigned char r[257]; memset(r,'a',sizeof r);
    begin("REF",0,"maximum"); CHECK(b6_syntax_ref(r,256,&ref)==B6_SYNTAX_OK); CHECK(strlen(ref.text)==256);
    begin("REF",0,"over-limit"); CHECK(b6_syntax_ref(r,257,&ref)==B6_SYNTAX_VALUE); CHECK(ref.text[0]==0);
    begin("REF",0,"embedded-NUL"); r[100]=0; CHECK(b6_syntax_ref(r,256,&ref)==B6_SYNTAX_VALUE);
    begin("REF",0,"non-ASCII"); r[100]=0x80; CHECK(b6_syntax_ref(r,256,&ref)==B6_SYNTAX_VALUE);
    begin("REF",0,"null-input"); CHECK(b6_syntax_ref(NULL,1,&ref)==B6_SYNTAX_ARGUMENT); CHECK(ref.text[0]==0);
    begin("REF",0,"null-output"); CHECK(b6_syntax_ref(r,1,NULL)==B6_SYNTAX_ARGUMENT);
    struct b6_syntax_path p;
    const char *valid_paths[]={"/a",path,"/elsewhere/.hidden/.../a_-9"};
    for(size_t i=0;i<sizeof valid_paths/sizeof valid_paths[0];++i) {
        begin("PATH",i,"valid"); const char *s=valid_paths[i];
        CHECK(b6_syntax_path((const unsigned char *)s,strlen(s),&p)==B6_SYNTAX_OK); CHECK(strcmp(p.text,s)==0);
    }
    const char *bad_paths[]={"","/","a","a/b","//a","/a//b","/a/","/./a","/a/.","/../a","/a/..","/a b","/a:b","/a\\b","/a\tb","/a\nb","/a\rb"};
    for(size_t i=0;i<sizeof bad_paths/sizeof bad_paths[0];++i) {
        begin("PATH",i,"invalid"); const char *s=bad_paths[i]; memset(&p,0xa5,sizeof p);
        CHECK(b6_syntax_path((const unsigned char *)s,strlen(s),&p)==B6_SYNTAX_VALUE); CHECK(p.text[0]==0);
    }
    unsigned char long_path[4096]; long_path[0]='/'; memset(long_path+1,'a',sizeof long_path-1);
    begin("PATH",0,"maximum-text-not-filesystem-claim"); CHECK(b6_syntax_path(long_path,4095,&p)==B6_SYNTAX_OK); CHECK(strlen(p.text)==4095);
    begin("PATH",0,"over-limit"); CHECK(b6_syntax_path(long_path,4096,&p)==B6_SYNTAX_VALUE); CHECK(p.text[0]==0);
    begin("PATH",0,"embedded-NUL"); long_path[100]=0; CHECK(b6_syntax_path(long_path,4095,&p)==B6_SYNTAX_VALUE);
    begin("PATH",0,"non-ASCII"); long_path[100]=0x80; CHECK(b6_syntax_path(long_path,4095,&p)==B6_SYNTAX_VALUE);
    begin("PATH",0,"null-input"); CHECK(b6_syntax_path(NULL,1,&p)==B6_SYNTAX_ARGUMENT); CHECK(p.text[0]==0);
    begin("PATH",0,"null-output"); CHECK(b6_syntax_path(long_path,1,NULL)==B6_SYNTAX_ARGUMENT);
    const char *valid_cs[]={"0","1","9223372036854775807"};
    const uint64_t expected[]={0,1,UINT64_C(9223372036854775807)};
    uint64_t cs;
    for(size_t i=0;i<sizeof valid_cs/sizeof valid_cs[0];++i) {
        begin("CS",i,"valid"); CHECK(b6_syntax_cs((const unsigned char *)valid_cs[i],strlen(valid_cs[i]),&cs)==B6_SYNTAX_OK); CHECK(cs==expected[i]);
    }
    const char *bad_cs[]={"","00","01","+0","-0","-1"," 1","1 ","1.00","1e2","0x1","1\n","9223372036854775808","18446744073709551615","99999999999999999999999999999999999999999999999"};
    for(size_t i=0;i<sizeof bad_cs/sizeof bad_cs[0];++i) {
        begin("CS",i,"invalid"); cs=99; CHECK(b6_syntax_cs((const unsigned char *)bad_cs[i],strlen(bad_cs[i]),&cs)==B6_SYNTAX_VALUE); CHECK(cs==0);
    }
    begin("CS",0,"embedded-NUL"); CHECK(b6_syntax_cs((const unsigned char *)"1\0",2,&cs)==B6_SYNTAX_VALUE); CHECK(cs==0);
    begin("CS",0,"null-input"); CHECK(b6_syntax_cs(NULL,1,&cs)==B6_SYNTAX_ARGUMENT); CHECK(cs==0);
    begin("CS",0,"null-output"); CHECK(b6_syntax_cs((const unsigned char *)"0",1,NULL)==B6_SYNTAX_ARGUMENT);
}
static void documents(void)
{
    for(size_t s=0;s<sizeof fixtures/sizeof fixtures[0];++s) {
        const struct fixture *base=&fixtures[s]; unsigned char bytes[9000]; struct b6_syntax_document out;
        size_t n=encode(base,bytes,sizeof bytes);
        begin(base->name,0,"canonical-all-output-fields"); CHECK(b6_syntax_parse(bytes,n,base->schema,&out)==B6_SYNTAX_OK); valid_fields(base,&out);
        begin(base->name,0,"discard-input-independent-output"); memset(bytes,0,n); valid_fields(base,&out);
        n=encode(base,bytes,sizeof bytes);
        for(size_t i=0;i<base->count;++i) {
            struct fixture f=*base;
            begin(base->name,i,"missing-row"); memmove(f.rows+i,f.rows+i+1,(f.count-i-1)*sizeof f.rows[0]); --f.count; reject(&f);
            f=*base; begin(base->name,i,"unknown-key"); f.rows[i].key="unrecognized"; reject(&f);
            f=*base; begin(base->name,i,"empty-value"); f.rows[i].value=""; reject(&f);
            f=*base; begin(base->name,i,"invalid-value-domain"); f.rows[i].value="?"; reject(&f);
            if(i>0) { f=*base; begin(base->name,i,"duplicate-key"); f.rows[i].key=f.rows[i-1].key; reject(&f); }
            if(i+1<base->count) {
                f=*base; begin(base->name,i,"reordered-row"); struct row tmp=f.rows[i]; f.rows[i]=f.rows[i+1]; f.rows[i+1]=tmp; reject(&f);
            }
        }
        for(size_t i=0;i<sizeof fixtures/sizeof fixtures[0];++i) if(i!=s) {
            begin(base->name,i,"other-schema-selected"); reject_bytes(bytes,n,fixtures[i].schema);
        }
        struct fixture f=*base;
        char schema_literal[96]; size_t schema_n=strlen(base->rows[0].value);
        if(schema_n>=sizeof schema_literal) abort();
        memcpy(schema_literal,base->rows[0].value,schema_n+1);
        begin(base->name,0,"older-schema"); schema_literal[schema_n-1]=(char)(schema_literal[schema_n-1]-1);
        f.rows[0].value=schema_literal; reject(&f);
        f=*base; memcpy(schema_literal,base->rows[0].value,schema_n+1);
        begin(base->name,0,"future-schema"); schema_literal[schema_n-1]=(char)(schema_literal[schema_n-1]+1);
        f.rows[0].value=schema_literal; reject(&f);
        f=*base; memcpy(schema_literal,base->rows[0].value,schema_n+1);
        begin(base->name,0,"casefolded-schema"); schema_literal[0]='b'; f.rows[0].value=schema_literal; reject(&f);
        begin(base->name,0,"missing-LF"); reject_bytes(bytes,n-1,base->schema);
        begin(base->name,0,"trailing-blank"); bytes[n]='\n'; reject_bytes(bytes,n+1,base->schema);
        begin(base->name,0,"second-object"); memcpy(bytes+n,bytes,n); reject_bytes(bytes,n*2,base->schema);
        begin(base->name,0,"unknown-extra-record"); memcpy(bytes+n,"unknown\tvalue\n",14); reject_bytes(bytes,n+14,base->schema);
        const unsigned char bad[]={0,'\r','\t',0x80,0x7f};
        for(size_t i=0;i<sizeof bad;++i) {
            begin(base->name,i,"envelope-byte"); n=encode(base,bytes,sizeof bytes); bytes[8]=bad[i]; reject_bytes(bytes,n,base->schema);
        }
        begin(base->name,0,"BOM"); n=encode(base,bytes,sizeof bytes); memmove(bytes+3,bytes,n); bytes[0]=0xef; bytes[1]=0xbb; bytes[2]=0xbf; reject_bytes(bytes,n+3,base->schema);
        begin(base->name,0,"oversized"); memset(bytes,'x',8193); bytes[8192]='\n'; reject_bytes(bytes,8193,base->schema);
        begin(base->name,0,"unknown-dispatch"); n=encode(base,bytes,sizeof bytes); reject_bytes(bytes,n,B6_SYNTAX_NO_SCHEMA);
        begin(base->name,0,"FD-reader"); char name[]="/tmp/b6-inherited-fixture-XXXXXX"; int fd=mkstemp(name); CHECK(fd>=0); CHECK(unlink(name)==0);
        CHECK(write(fd,bytes,n)==(ssize_t)n); CHECK(lseek(fd,0,SEEK_SET)==0);
        CHECK(b6_syntax_read_fd(fd,base->schema,&out)==B6_SYNTAX_OK); valid_fields(base,&out); CHECK(close(fd)==0);
        if(s<2) {
            size_t ref_at=s==0?3u:2u, path_at=s==0?8u:7u, before=s==0?10u:11u, after=s==0?11u:12u;
            const char *bad_ref[]={"-bad", "a?b"};
            for(size_t i=0;i<2;++i) { f=*base; begin(base->name,i,"invalid-REF-field"); f.rows[ref_at].value=bad_ref[i]; reject(&f); }
            const char *bad_path[]={"relative/path", "/a/../b", "/a//b"};
            for(size_t i=0;i<3;++i) { f=*base; begin(base->name,i,"invalid-PATH-field"); f.rows[path_at].value=bad_path[i]; reject(&f); }
            f=*base; begin(base->name,0,"CS-overflow-field"); f.rows[after].value="9223372036854775808"; reject(&f);
            f=*base; begin(base->name,0,"semantic-window-and-parent-not-validated");
            f.rows[before].value="2"; f.rows[after].value="0"; f.rows[path_at].value="/other/parent/leaf";
            n=encode(&f,bytes,sizeof bytes); CHECK(b6_syntax_parse(bytes,n,f.schema,&out)==B6_SYNTAX_OK); CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);
            begin(base->name,0,"wrong-lifecycle-version"); f=*base; f.rows[s==0?2u:13u].value="NATIVE_MACHINE_SERIALIZED_RETRY_V1"; reject(&f);
            char max_ref[257], max_path[4096]; memset(max_ref,'a',256); max_ref[256]=0;
            max_path[0]='/'; memset(max_path+1,'a',4094); max_path[4095]=0;
            f=*base; begin(base->name,0,"maximum-REF-PATH-field"); f.rows[ref_at].value=max_ref; f.rows[path_at].value=max_path;
            n=encode(&f,bytes,sizeof bytes); CHECK(b6_syntax_parse(bytes,n,f.schema,&out)==B6_SYNTAX_OK); CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);
        } else {
            begin(base->name,0,"invalid-REF-field"); f=*base; f.rows[2].value="/not-ref"; reject(&f);
            begin(base->name,0,"UID-sentinel"); f=*base; f.rows[4].value="4294967295"; reject(&f);
            begin(base->name,0,"pool-order-is-semantic"); f=*base; f.rows[3].value="9"; f.rows[4].value="1";
            n=encode(&f,bytes,sizeof bytes); CHECK(b6_syntax_parse(bytes,n,f.schema,&out)==B6_SYNTAX_OK); CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);
        }
    }
}
int main(void)
{
    scalars(); documents();
    printf("SUMMARY\tsemantic_cases=%zu\tassertions=%zu\tresult=PASS\n",cases,assertions);
    return 0;
}
