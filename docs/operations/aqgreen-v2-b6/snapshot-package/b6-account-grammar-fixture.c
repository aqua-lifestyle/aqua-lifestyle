#define _POSIX_C_SOURCE 200809L
#include "attempt-account-schema.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static size_t cases, assertions;
static const char *active;
#define CHECK(x) do { ++assertions; if (!(x)) { fprintf(stderr,"FAIL %s line %d: %s\n",active,__LINE__,#x); exit(1); } } while (0)
static void begin(const char *domain, size_t index, const char *property)
{ active=property; ++cases; printf("CASE\t%zu\t%s\t%zu\t%s\n",cases,domain,index,property); }
static int zero(const void *p, size_t n)
{ const unsigned char *b=p; for(size_t i=0;i<n;++i) if(b[i]) return 0; return 1; }
static const char uuid[]="01234567-89ab-cdef-0123-456789abcdef";
static const char hex[]="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
struct row { const char *key, *value; };
struct form { enum b6_syntax_schema schema; size_t count; struct row rows[18]; };
static const struct form report={B6_SYNTAX_ACCOUNT_REPORT_V1,17,{
 {"schema","B6_ACCOUNT_RECONCILIATION_V1"},{"store_id",uuid},{"commission_id","commission_1"},
 {"attempt_number","1"},{"native_binding_event_sha256",hex},{"admission_event_sha256",hex},
 {"procedure_sha256",hex},{"verifier_materialization_sha256",hex},
 {"execution_evidence_class","PASS"},{"native_wait_result","FAIL"},
 {"native_terminal_check","UNAVAILABLE"},{"workload_check","NOT_APPLICABLE"},
 {"process_reconciliation","PASS"},{"output_reconciliation","FAIL"},
 {"host_continuity","UNAVAILABLE"},{"result","ACCEPTED"},{"evidence_manifest_sha256",hex}}};
static const struct form identity={B6_SYNTAX_ACCOUNT_STORE_V1,3,{
 {"schema","B6_ATTEMPT_ACCOUNT_STORE_V1"},{"store_id",uuid},{"format_version","1"}}};
static size_t encode(const struct form *f, unsigned char *bytes, size_t cap)
{
 size_t n=0;
 for(size_t i=0;i<f->count;++i) {
  size_t k=strlen(f->rows[i].key), v=strlen(f->rows[i].value);
  if(n+k+v+2>=cap) abort();
  memcpy(bytes+n,f->rows[i].key,k); n+=k; bytes[n++]='\t';
  memcpy(bytes+n,f->rows[i].value,v); n+=v; bytes[n++]='\n';
 }
 return n;
}
static struct b6_syntax_document parse(const struct form *f)
{
 unsigned char bytes[2048]; size_t n=encode(f,bytes,sizeof bytes);
 struct b6_syntax_document out;
 memset(&out,0xa5,sizeof out);
 CHECK(b6_syntax_parse(bytes,n,f->schema,&out)==B6_SYNTAX_OK);
 CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(out.schema==f->schema);
 return out;
}
static void reject_bytes(const unsigned char *p, size_t n, enum b6_syntax_schema schema)
{
 struct b6_syntax_document out;
 memset(&out,0xa5,sizeof out);
 CHECK(b6_syntax_parse(p,n,schema,&out)!=B6_SYNTAX_OK);
 CHECK(out.state==B6_SYNTAX_INVALID); CHECK(out.schema==B6_SYNTAX_NO_SCHEMA);
 CHECK(zero(&out,sizeof out));
}
static void reject(const struct form *f)
{ unsigned char bytes[2048]; size_t n=encode(f,bytes,sizeof bytes); reject_bytes(bytes,n,f->schema); }
static void tokens(void)
{
 const char *versions[]={"1","","0","2","01","+1","-1","1.0","V1","V2","latest"," 1","1 ","1\t","1\n","1\r"};
 for(size_t i=0;i<sizeof versions/sizeof versions[0];++i) {
  begin("version",i,i?"noncanonical-or-unknown":"canonical-V1");
  struct b6_syntax_version out; memset(&out,0xa5,sizeof out);
  enum b6_syntax_error e=b6_syntax_version((const unsigned char *)versions[i],strlen(versions[i]),&out);
  CHECK((e==B6_SYNTAX_OK)==(i==0));
  if(i==0) { CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(out.value==B6_SYNTAX_FORMAT_V1); }
  else { CHECK(out.state==B6_SYNTAX_INVALID); CHECK(out.value==B6_SYNTAX_NO_VERSION); }
 }
 const char *causes[]={"COMPLETION","FAILURE","CANCELLATION","RECOVERY"};
 const enum b6_syntax_cause_value cv[]={B6_SYNTAX_CAUSE_COMPLETION,B6_SYNTAX_CAUSE_FAILURE,B6_SYNTAX_CAUSE_CANCELLATION,B6_SYNTAX_CAUSE_RECOVERY};
 const char *closures[]={"WITHDRAWAL","EXPIRY"};
 const enum b6_syntax_closure_value cl[]={B6_SYNTAX_CLOSURE_WITHDRAWAL,B6_SYNTAX_CLOSURE_EXPIRY};
 for(size_t i=0;i<4;++i) {
  begin("cause",i,"recognized-label-is-grammar-only"); struct b6_syntax_cause out;
  CHECK(b6_syntax_cause((const unsigned char *)causes[i],strlen(causes[i]),&out)==B6_SYNTAX_OK);
  CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(out.value==cv[i]);
 }
 for(size_t i=0;i<2;++i) {
  begin("closure",i,"recognized-label-is-grammar-only"); struct b6_syntax_closure out;
  CHECK(b6_syntax_closure((const unsigned char *)closures[i],strlen(closures[i]),&out)==B6_SYNTAX_OK);
  CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(out.value==cl[i]);
 }
 const char *bad_c[]={"","failure","COMPLETED","FAIL","RESERVED","COMPLETION "," RECOVERY","FAILURE\t","FAILURE\n","FAILURE\r","CANCELLATIONX"};
 const char *bad_cl[]={"","withdrawal","EXPIRED","RECOVERY","CLOSE_UNUSED_COMMISSION"," EXPIRY","EXPIRY ","EXPIRY\t","EXPIRY\n","EXPIRY\r","WITHDRAWALX"};
 for(size_t i=0;i<sizeof bad_c/sizeof bad_c[0];++i) {
  begin("cause",i,"invalid-token-clears-previous-success"); struct b6_syntax_cause out;
  CHECK(b6_syntax_cause((const unsigned char *)"RECOVERY",8,&out)==B6_SYNTAX_OK);
  CHECK(b6_syntax_cause((const unsigned char *)bad_c[i],strlen(bad_c[i]),&out)!=B6_SYNTAX_OK);
  CHECK(zero(&out,sizeof out));
 }
 for(size_t i=0;i<sizeof bad_cl/sizeof bad_cl[0];++i) {
  begin("closure",i,"invalid-token-clears-previous-success"); struct b6_syntax_closure out;
  CHECK(b6_syntax_closure((const unsigned char *)"EXPIRY",6,&out)==B6_SYNTAX_OK);
  CHECK(b6_syntax_closure((const unsigned char *)bad_cl[i],strlen(bad_cl[i]),&out)!=B6_SYNTAX_OK);
  CHECK(zero(&out,sizeof out));
 }
 const unsigned char bad[]={0,0x80,0x7f};
 for(size_t i=0;i<sizeof bad;++i) {
  unsigned char c[]="FAILURE",clo[]="EXPIRY",v[]="1"; c[2]=clo[2]=v[0]=bad[i];
  begin("cause",i,"NUL-or-non-ASCII"); struct b6_syntax_cause co;
  CHECK(b6_syntax_cause(c,7,&co)!=B6_SYNTAX_OK); CHECK(zero(&co,sizeof co));
  begin("closure",i,"NUL-or-non-ASCII"); struct b6_syntax_closure cr;
  CHECK(b6_syntax_closure(clo,6,&cr)!=B6_SYNTAX_OK); CHECK(zero(&cr,sizeof cr));
  begin("version",i,"NUL-or-non-ASCII"); struct b6_syntax_version ve;
  CHECK(b6_syntax_version(v,1,&ve)!=B6_SYNTAX_OK); CHECK(zero(&ve,sizeof ve));
 }
 begin("tokens",0,"null-input-output-and-size-overflow");
 struct b6_syntax_version v; struct b6_syntax_cause c; struct b6_syntax_closure clout;
 CHECK(b6_syntax_version(NULL,1,&v)==B6_SYNTAX_ARGUMENT); CHECK(zero(&v,sizeof v));
 CHECK(b6_syntax_cause(NULL,8,&c)==B6_SYNTAX_ARGUMENT); CHECK(zero(&c,sizeof c));
 CHECK(b6_syntax_closure(NULL,6,&clout)==B6_SYNTAX_ARGUMENT); CHECK(zero(&clout,sizeof clout));
 CHECK(b6_syntax_version((const unsigned char *)"1",1,NULL)==B6_SYNTAX_ARGUMENT);
 CHECK(b6_syntax_cause((const unsigned char *)"RECOVERY",8,NULL)==B6_SYNTAX_ARGUMENT);
 CHECK(b6_syntax_closure((const unsigned char *)"EXPIRY",6,NULL)==B6_SYNTAX_ARGUMENT);
 CHECK(b6_syntax_version((const unsigned char *)"1",SIZE_MAX,&v)!=B6_SYNTAX_OK); CHECK(zero(&v,sizeof v));
 CHECK(b6_syntax_cause((const unsigned char *)"R",SIZE_MAX,&c)!=B6_SYNTAX_OK); CHECK(zero(&c,sizeof c));
 CHECK(b6_syntax_closure((const unsigned char *)"E",SIZE_MAX,&clout)!=B6_SYNTAX_OK); CHECK(zero(&clout,sizeof clout));
}
static enum b6_syntax_status status_at(const struct b6_syntax_report *r,size_t i)
{
 switch(i) {
 case 8:return r->execution_evidence_class; case 9:return r->native_wait_result;
 case 10:return r->native_terminal_check; case 11:return r->workload_check;
 case 12:return r->process_reconciliation; case 13:return r->output_reconciliation;
 case 14:return r->host_continuity; default:abort();
 }
}
static void reports(void)
{
 begin("report",0,"canonical-all-fields-retained"); struct b6_syntax_document out=parse(&report);
 CHECK(strcmp(out.data.report.store_id.text,uuid)==0); CHECK(strcmp(out.data.report.commission_id.text,"commission_1")==0);
 CHECK(out.data.report.attempt_number==1); CHECK(out.data.report.native_binding_event.kind==B6_SYNTAX_EVENT_DIGEST);
 CHECK(out.data.report.admission_event.kind==B6_SYNTAX_EVENT_DIGEST);
 CHECK(strcmp(out.data.report.native_binding_event.digest.text,hex)==0); CHECK(strcmp(out.data.report.admission_event.digest.text,hex)==0);
 CHECK(strcmp(out.data.report.procedure_sha256.text,hex)==0); CHECK(strcmp(out.data.report.verifier_materialization_sha256.text,hex)==0);
 CHECK(strcmp(out.data.report.evidence_manifest_sha256.text,hex)==0); CHECK(out.data.report.result==B6_SYNTAX_RESULT_ACCEPTED);
 const char *statuses[]={"PASS","FAIL","UNAVAILABLE","NOT_APPLICABLE"};
 const enum b6_syntax_status sv[]={B6_SYNTAX_STATUS_PASS,B6_SYNTAX_STATUS_FAIL,B6_SYNTAX_STATUS_UNAVAILABLE,B6_SYNTAX_STATUS_NOT_APPLICABLE};
 for(size_t i=8;i<=14;++i) for(size_t s=0;s<4;++s) {
  begin("report-status",i*4+s,"each-field-each-status"); struct form f=report; f.rows[i].value=statuses[s];
  out=parse(&f); CHECK(status_at(&out.data.report,i)==sv[s]);
 }
 const char *results[]={"ACCEPTED","RECONCILED_FAILED","INDETERMINATE"};
 const enum b6_syntax_report_result rv[]={B6_SYNTAX_RESULT_ACCEPTED,B6_SYNTAX_RESULT_RECONCILED_FAILED,B6_SYNTAX_RESULT_INDETERMINATE};
 for(size_t i=0;i<3;++i) {
  begin("report-result",i,"result-is-data-only"); struct form f=report; f.rows[15].value=results[i]; out=parse(&f); CHECK(out.data.report.result==rv[i]);
 }
 for(size_t mask=1;mask<4;++mask) {
  begin("report-reference",mask,"independent-absence-sentinels"); struct form f=report;
  if(mask&1u) f.rows[4].value="NOT_BOUND";
  if(mask&2u) f.rows[5].value="NOT_ADMITTED";
  out=parse(&f);
  CHECK(out.data.report.native_binding_event.kind==((mask&1u)?B6_SYNTAX_EVENT_NOT_BOUND:B6_SYNTAX_EVENT_DIGEST));
  CHECK(out.data.report.admission_event.kind==((mask&2u)?B6_SYNTAX_EVENT_NOT_ADMITTED:B6_SYNTAX_EVENT_DIGEST));
  if(mask&1u) CHECK(zero(&out.data.report.native_binding_event.digest,sizeof out.data.report.native_binding_event.digest));
  if(mask&2u) CHECK(zero(&out.data.report.admission_event.digest,sizeof out.data.report.admission_event.digest));
 }
 begin("report",0,"maximum-919-byte-instance"); struct form f=report; char maxid[65]; memset(maxid,'a',64); maxid[64]=0;
 f.rows[2].value=maxid; f.rows[3].value="18446744073709551615"; f.rows[15].value="RECONCILED_FAILED";
 for(size_t i=8;i<=14;++i) f.rows[i].value="NOT_APPLICABLE";
 unsigned char bytes[2048]; size_t n=encode(&f,bytes,sizeof bytes); CHECK(n==B6_SYNTAX_REPORT_MAX);
 out=parse(&f); CHECK(out.data.report.attempt_number==UINT64_MAX); CHECK(strlen(out.data.report.commission_id.text)==64);
 begin("report",0,"all-PASS-ACCEPTED-is-not-evidence"); f=report; for(size_t i=8;i<=14;++i) f.rows[i].value="PASS";
 f.rows[4].value="NOT_BOUND"; f.rows[5].value="NOT_ADMITTED"; out=parse(&f); CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);
 begin("report",0,"zero-UUID-is-lexically-valid-only"); f=report; f.rows[1].value="00000000-0000-0000-0000-000000000000"; out=parse(&f); CHECK(strcmp(out.data.report.store_id.text,f.rows[1].value)==0);
 /* Different digests expose accidental slot reuse in the typed mapping. */
 begin("report",0,"each-digest-keeps-own-slot"); f=report; char digests[5][65]; const size_t fields[]={4,5,6,7,16};
 for(size_t i=0;i<5;++i) { memset(digests[i],(int)('a'+(char)i),64); digests[i][64]=0; f.rows[fields[i]].value=digests[i]; }
 out=parse(&f); CHECK(strcmp(out.data.report.native_binding_event.digest.text,digests[0])==0);
 CHECK(strcmp(out.data.report.admission_event.digest.text,digests[1])==0); CHECK(strcmp(out.data.report.procedure_sha256.text,digests[2])==0);
 CHECK(strcmp(out.data.report.verifier_materialization_sha256.text,digests[3])==0); CHECK(strcmp(out.data.report.evidence_manifest_sha256.text,digests[4])==0);
 const struct {size_t field; const char *bad;} invalid[]={
 {0,"B6_ACCOUNT_RECONCILIATION_V2"},{0,"b6_ACCOUNT_RECONCILIATION_V1"},{0,"B6_ACCOUNT_RECONCILIATION_V1x"},
 {1,"01234567-89AB-cdef-0123-456789abcdef"},{1,"{01234567-89ab-cdef-0123-456789abcdef}"},{1,"/store/path"},
 {2,"STORE"},{2,"-commission"},{2,"a.b"},{2,"commission name"},
 {3,"0"},{3,"01"},{3,"+1"},{3,"1x"},{3,"18446744073709551616"},
 {4,"NOT_ADMITTED"},{5,"NOT_BOUND"},{4,"not_bound"},{5,"NOT_ADMITTEDx"},
 {6,"0123"},{7,"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdef0123456789abcdef01234567"},
 {15,"PASS"},{15,"accepted"},{15,"ACCEPTED "},{16,"NOT_BOUND"}};
 for(size_t i=0;i<sizeof invalid/sizeof invalid[0];++i) { begin("report-domain",i,"invalid-field-value"); f=report; f.rows[invalid[i].field].value=invalid[i].bad; reject(&f); }
 const char *badstatus[]={"UNKNOWN","pass","0","EXIT_0","PASS ","PASS\tPASS"};
 for(size_t i=8;i<=14;++i) for(size_t s=0;s<sizeof badstatus/sizeof badstatus[0];++s) {
  begin("report-status",i*6+s,"invalid-status-field"); f=report; f.rows[i].value=badstatus[s]; reject(&f);
 }
 begin("report",0,"commission-65-bytes"); char overid[66]; memset(overid,'a',65); overid[65]=0; f=report; f.rows[2].value=overid; reject(&f);
}
static void containers(const struct form *base,const char *name,size_t limit)
{
 unsigned char bytes[2048]; struct form f;
 for(size_t i=0;i<base->count;++i) {
  begin(name,i,"missing-record"); f=*base; for(size_t j=i;j+1<f.count;++j) f.rows[j]=f.rows[j+1]; --f.count; reject(&f);
  begin(name,i,"duplicate-record"); f=*base; f.rows[f.count++]=f.rows[i]; reject(&f);
  begin(name,i,"unknown-record"); f=*base; f.rows[i].key="unknown"; reject(&f);
  begin(name,i,"empty-value"); f=*base; f.rows[i].value=""; reject(&f);
  begin(name,i,"extra-tab"); f=*base; f.rows[i].value="value\tvalue"; reject(&f);
  if(i+1<base->count) { begin(name,i,"adjacent-order-swap"); f=*base; struct row r=f.rows[i]; f.rows[i]=f.rows[i+1]; f.rows[i+1]=r; reject(&f); }
 }
 size_t n=encode(base,bytes,sizeof bytes);
 begin(name,0,"missing-final-LF"); reject_bytes(bytes,n-1,base->schema);
 begin(name,0,"extra-blank-line"); bytes[n]='\n'; reject_bytes(bytes,n+1,base->schema);
 begin(name,0,"second-object"); memcpy(bytes+n,bytes,n); reject_bytes(bytes,n*2,base->schema);
 begin(name,0,"trailing-printable-byte"); bytes[n]='x'; reject_bytes(bytes,n+1,base->schema);
 const unsigned char forbidden[]={0,'\r',0x7f,0x80};
 for(size_t i=0;i<sizeof forbidden;++i) {
  begin(name,i,"forbidden-key-byte"); n=encode(base,bytes,sizeof bytes); bytes[2]=forbidden[i]; reject_bytes(bytes,n,base->schema);
  begin(name,i,"forbidden-final-value-byte"); n=encode(base,bytes,sizeof bytes); bytes[n-2]=forbidden[i]; reject_bytes(bytes,n,base->schema);
 }
 begin(name,0,"BOM"); n=encode(base,bytes,sizeof bytes); memmove(bytes+3,bytes,n); bytes[0]=0xef; bytes[1]=0xbb; bytes[2]=0xbf; reject_bytes(bytes,n+3,base->schema);
 begin(name,0,"leading-whitespace"); n=encode(base,bytes,sizeof bytes); bytes[0]=' '; reject_bytes(bytes,n,base->schema);
 begin(name,0,"oversized-input"); memset(bytes,'x',limit+1); bytes[limit]='\n'; reject_bytes(bytes,limit+1,base->schema);
 begin(name,0,"size_t-overflow-bound"); reject_bytes((const unsigned char *)"x",SIZE_MAX,base->schema);
 begin(name,0,"no-default-schema"); n=encode(base,bytes,sizeof bytes); reject_bytes(bytes,n,B6_SYNTAX_NO_SCHEMA);
 begin(name,0,"wrong-recognized-dispatch"); reject_bytes(bytes,n,base->schema==B6_SYNTAX_ACCOUNT_REPORT_V1?B6_SYNTAX_ACCOUNT_STORE_V1:B6_SYNTAX_ACCOUNT_REPORT_V1);
 begin(name,0,"unknown-enum-dispatch"); reject_bytes(bytes,n,(enum b6_syntax_schema)999);
 begin(name,0,"null-input"); reject_bytes(NULL,n,base->schema);
 begin(name,0,"null-output"); CHECK(b6_syntax_parse(bytes,n,base->schema,NULL)==B6_SYNTAX_ARGUMENT);
 begin(name,0,"late-failure-clears-previous-success"); struct b6_syntax_document out=parse(base); bytes[n-2]='?';
 CHECK(b6_syntax_parse(bytes,n,base->schema,&out)!=B6_SYNTAX_OK); CHECK(zero(&out,sizeof out));
}
static void identities(void)
{
 begin("identity",0,"canonical-98-byte-roundtrip-and-guards"); struct b6_syntax_document out=parse(&identity);
 CHECK(strcmp(out.data.account_store.store_id.text,uuid)==0); CHECK(out.data.account_store.format_version==B6_SYNTAX_FORMAT_V1);
 struct {unsigned char before[16];struct b6_syntax_account_store_bytes out;unsigned char after[16];} guard;
 memset(&guard,0xa5,sizeof guard);
 CHECK(b6_syntax_serialize_account_store(&out.data.account_store,&guard.out)==B6_SYNTAX_OK);
 CHECK(guard.out.state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(guard.out.length==98);
 unsigned char bytes[2048]; size_t n=encode(&identity,bytes,sizeof bytes); CHECK(n==98); CHECK(memcmp(bytes,guard.out.bytes,98)==0);
 for(size_t i=0;i<16;++i) { CHECK(guard.before[i]==0xa5); CHECK(guard.after[i]==0xa5); }
 CHECK(b6_syntax_parse(guard.out.bytes,guard.out.length,B6_SYNTAX_ACCOUNT_STORE_V1,&out)==B6_SYNTAX_OK);
 CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(strcmp(out.data.account_store.store_id.text,uuid)==0);
 begin("identity",0,"all-zero-UUID-no-custody-claim"); struct form f=identity; f.rows[1].value="00000000-0000-0000-0000-000000000000"; out=parse(&f);
 CHECK(b6_syntax_serialize_account_store(&out.data.account_store,&guard.out)==B6_SYNTAX_OK); n=encode(&f,bytes,sizeof bytes); CHECK(n==98); CHECK(memcmp(bytes,guard.out.bytes,98)==0);
 const struct {size_t field;const char *bad;} invalid[]={
 {0,"B6_CUSTODY_STORE_V2"},{0,"B6_CUSTODY_STORE_V1"},{0,"B6_ATTEMPT_ACCOUNT_STORE_V2"},
 {0,"b6_ATTEMPT_ACCOUNT_STORE_V1"},{0,"B6_ATTEMPT_ACCOUNT_STORE_V1x"},
 {1,"0123456789abcdef0123456789abcdef"},{1,"01234567-89ab-cdef-0123-456789abcdeF"},
 {1,"{01234567-89ab-cdef-0123-456789abcdef}"},{1,"/var/lib/aqua-b6/attempt-account"},
 {1,"device=1,inode=2"},{1,hex},{1,"01234567-89ab-cdef-0123-456789abcdef "},
 {2,"0"},{2,"2"},{2,"01"},{2,"+1"},{2,"1.0"},{2,"V1"},{2,"latest"},{2,"1 "}};
 for(size_t i=0;i<sizeof invalid/sizeof invalid[0];++i) { begin("identity-domain",i,"noncanonical-value"); f=identity; f.rows[invalid[i].field].value=invalid[i].bad; reject(&f); }
 begin("identity",0,"UUID-only-is-not-store-object"); reject_bytes((const unsigned char *)uuid,36,B6_SYNTAX_ACCOUNT_STORE_V1);
 begin("identity",0,"raw-logical-struct-is-not-serialization"); out=parse(&identity); reject_bytes((const unsigned char *)&out.data.account_store,sizeof out.data.account_store,B6_SYNTAX_ACCOUNT_STORE_V1);
 for(size_t i=0;i<5;++i) {
  begin("serializer",i,"invalid-value-clears-byte-output"); out=parse(&identity); struct b6_syntax_account_store value=out.data.account_store;
  if(i==0) value.format_version=B6_SYNTAX_NO_VERSION;
  if(i==1) value.format_version=(enum b6_syntax_format_version)2;
  if(i==2) value.store_id.text[2]='G';
  if(i==3) value.store_id.text[10]=0;
  if(i==4) value.store_id.text[36]='x';
  CHECK(b6_syntax_serialize_account_store(&out.data.account_store,&guard.out)==B6_SYNTAX_OK);
  CHECK(b6_syntax_serialize_account_store(&value,&guard.out)!=B6_SYNTAX_OK); CHECK(zero(&guard.out,sizeof guard.out));
 }
 begin("serializer",0,"null-arguments"); memset(&guard.out,0xa5,sizeof guard.out);
 CHECK(b6_syntax_serialize_account_store(NULL,&guard.out)==B6_SYNTAX_ARGUMENT); CHECK(zero(&guard.out,sizeof guard.out));
 CHECK(b6_syntax_serialize_account_store(&out.data.account_store,NULL)==B6_SYNTAX_ARGUMENT);
}
static void readers(const struct form *base,const char *name)
{
 unsigned char bytes[2048]; size_t n=encode(base,bytes,sizeof bytes);
 for(size_t i=0;i<4;++i) {
  begin(name,i,"bounded-FD-complete-short-extra-offset"); int fd[2]; CHECK(pipe(fd)==0);
  size_t length=n; if(i==1) length=n-1; if(i==2) {bytes[n]='\n';length=n+1;}
  CHECK(write(fd[1],bytes,length)==(ssize_t)length); CHECK(close(fd[1])==0);
  if(i==3) {unsigned char one;CHECK(read(fd[0],&one,1)==1);}
  struct b6_syntax_document out; memset(&out,0xa5,sizeof out);
  enum b6_syntax_error e=b6_syntax_read_fd(fd[0],base->schema,&out);
  CHECK((e==B6_SYNTAX_OK)==(i==0));
  if(i==0) {CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);CHECK(out.schema==base->schema);}
  else CHECK(zero(&out,sizeof out));
  CHECK(close(fd[0])==0);
 }
 begin(name,0,"FD-I/O-error-clears-output"); int fd[2]; CHECK(pipe(fd)==0); int closed=fd[0];CHECK(close(fd[0])==0);CHECK(close(fd[1])==0);
 struct b6_syntax_document out; memset(&out,0xa5,sizeof out); CHECK(b6_syntax_read_fd(closed,base->schema,&out)==B6_SYNTAX_IO);CHECK(zero(&out,sizeof out));
}
int main(void)
{
 tokens(); reports(); containers(&report,"report-container",B6_SYNTAX_REPORT_MAX);
 identities(); containers(&identity,"identity-container",B6_SYNTAX_ACCOUNT_STORE_BYTES);
 readers(&report,"report-reader");readers(&identity,"identity-reader");
 printf("SUMMARY\tsemantic_cases=%zu\tassertions=%zu\tresult=PASS\n",cases,assertions);
 return 0;
}
