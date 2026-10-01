#include "attempt-account-schema.h"
#include <inttypes.h>
#include <openssl/sha.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static size_t cases, assertions;
static const char *active;
#define CHECK(x) do { ++assertions; if (!(x)) { fprintf(stderr,"FAIL %s line %d: %s\n",active,__LINE__,#x); exit(1); } } while (0)
static void begin(size_t event, size_t field, const char *property)
{ active=property; ++cases; printf("CASE\t%zu\t%zu\t%zu\t%s\n",cases,event,field,property); }
static int zero(const void *p,size_t n)
{ const unsigned char *b=p; for(size_t i=0;i<n;++i) if(b[i]) return 0; return 1; }
static const char uuid[]="01234567-89ab-cdef-0123-456789abcdef";
static const char hex[]="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
static const char *const tokens[]={"GENESIS","REGISTER_COMMISSION","RESERVE_ATTEMPT","PREPARATION_INTENT","BIND_NATIVE","ADMIT_EXECUTION","BEGIN_RECONCILIATION","RECONCILED_FAILED","QUALIFICATION_ACCEPTED","CLOSE_UNUSED_COMMISSION","RETIRE_INDETERMINATE"};
static const enum b6_syntax_event_type types[]={B6_SYNTAX_GENESIS,B6_SYNTAX_REGISTER_COMMISSION,B6_SYNTAX_RESERVE_ATTEMPT,B6_SYNTAX_PREPARATION_INTENT,B6_SYNTAX_BIND_NATIVE,B6_SYNTAX_ADMIT_EXECUTION,B6_SYNTAX_BEGIN_RECONCILIATION,B6_SYNTAX_RECONCILED_FAILED,B6_SYNTAX_QUALIFICATION_ACCEPTED,B6_SYNTAX_CLOSE_UNUSED_COMMISSION,B6_SYNTAX_RETIRE_INDETERMINATE};
struct row { const char *key,*value; };
struct form { size_t count; struct row rows[22]; };
static struct form form(size_t e)
{
 struct form f={12,{{"schema","B6_ATTEMPT_ACCOUNT_EVENT_V1"},{"store_id",uuid},
  {"event_seq","1"},{"previous_event_sha256",e?hex:"GENESIS"},{"event_type",tokens[e]},
  {"commission_id",e?"commission_1":"STORE"},{"attempt_number",e==0||e==1||e==9?"0":"1"},
  {"writer_boot_id",uuid},{"writer_pid","2"},{"writer_starttime","1"},
  {"recorded_boot_cs","0"},{"evidence_sha256",e?hex:"NOT_APPLICABLE"}}};
 const struct row suffixes[11][7]={
  {{"format_version","1"}},
  {{"grant_reference","grant_1"},{"enrollment_permit_sha256",hex},{"commission_scope_sha256",hex},{"resource_key",hex}},
  {{"permit_snapshot_sha256",hex},{"observation_snapshot_sha256",hex},{"native_preparation_descriptor_sha256",hex}},
  {{"supervisor_executable_sha256",hex}},
  {{"native_transaction_id",hex},{"supervisor_pid","2"},{"supervisor_starttime","1"},{"bash_pid","2"},{"bash_starttime","1"},{"output_device","0"},{"output_inode","1"}},
  {{"admission_permit_sha256",hex},{"admission_observation_sha256",hex},{"admission_host_evidence_sha256",hex}},
  {{"cause","COMPLETION"}},{{"reconciliation_report_sha256",hex}},{{"reconciliation_report_sha256",hex}},
  {{"closure_reason","WITHDRAWAL"}},{{"recovery_authority_reference","recovery_1"},{"recovery_report_sha256",hex}}};
 const size_t counts[]={1,4,3,1,7,3,1,1,1,1,2};
 for(size_t i=0;i<counts[e];++i) f.rows[f.count++]=suffixes[e][i];
 return f;
}
static size_t payload(const struct form *f,unsigned char *bytes)
{
 size_t n=0;
 for(size_t i=0;i<f->count;++i) {
  size_t k=strlen(f->rows[i].key),v=strlen(f->rows[i].value);
  if(n+k+v+2>20000) abort();
  memcpy(bytes+n,f->rows[i].key,k); n+=k; bytes[n++]='\t';
  memcpy(bytes+n,f->rows[i].value,v); n+=v; bytes[n++]='\n';
 }
 return n;
}
static size_t frame(const unsigned char *p,size_t n,unsigned char *bytes)
{
 unsigned char hash[SHA256_DIGEST_LENGTH]; char digest[65];
 if(!SHA256(p,n,hash)) abort();
 for(size_t i=0;i<sizeof hash;++i) {
  static const char digits[]="0123456789abcdef";
  digest[2*i]=digits[hash[i]>>4]; digest[2*i+1]=digits[hash[i]&15u];
 }
 digest[64]=0;
 int header=snprintf((char *)bytes,100,"%zu\t%s\n",n,digest);
 if(header<=0||header>=100) abort();
 memcpy(bytes+(size_t)header,p,n); return (size_t)header+n;
}
static size_t encode(const struct form *f,unsigned char *bytes)
{ unsigned char p[20000];size_t n=payload(f,p);return frame(p,n,bytes); }
static struct b6_syntax_event_frame accept(const struct form *f)
{
 unsigned char bytes[21000]; size_t n=encode(f,bytes);
 struct b6_syntax_event_frame out;
 memset(&out,0xa5,sizeof out);
 CHECK(b6_syntax_parse_event_frame(bytes,n,&out)==B6_SYNTAX_OK);
 CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);
 struct b6_syntax_event_bytes encoded;
 memset(&encoded,0xa5,sizeof encoded);
 CHECK(b6_syntax_serialize_event_frame(&out,&encoded)==B6_SYNTAX_OK);
 CHECK(encoded.state==B6_SYNTAX_GRAMMAR_ONLY); CHECK(encoded.length==n);
 CHECK(memcmp(encoded.bytes,bytes,n)==0);
 CHECK(zero(encoded.bytes+n,sizeof(encoded.bytes)-n));
 struct b6_syntax_event_frame again;
 CHECK(b6_syntax_parse_event_frame(encoded.bytes,encoded.length,&again)==B6_SYNTAX_OK);
 CHECK(memcmp(&out,&again,sizeof out)==0);
 /* Output owns storage after both caller byte buffers have been overwritten. */
 memset(bytes,0x5a,n); memset(&encoded,0x5a,sizeof encoded);
 CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);
 return out;
}
static void reject_bytes(const unsigned char *p,size_t n)
{
 struct b6_syntax_event_frame out;
 unsigned char valid[21000]; struct form f=form(4);size_t length=encode(&f,valid);
 CHECK(b6_syntax_parse_event_frame(valid,length,&out)==B6_SYNTAX_OK);
 CHECK(out.event_type==B6_SYNTAX_BIND_NATIVE);
 CHECK(b6_syntax_parse_event_frame(p,n,&out)!=B6_SYNTAX_OK);
 CHECK(out.state==B6_SYNTAX_INVALID);CHECK(out.event_type==B6_SYNTAX_NO_EVENT);
 CHECK(zero(&out,sizeof out));
}
static void reject(const struct form *f)
{ unsigned char bytes[21000];size_t n=encode(f,bytes);reject_bytes(bytes,n); }
static void reject_serial(const struct b6_syntax_event_frame *f)
{
 struct b6_syntax_event_bytes out;memset(&out,0xa5,sizeof out);
 CHECK(b6_syntax_serialize_event_frame(f,&out)!=B6_SYNTAX_OK);
 CHECK(out.state==B6_SYNTAX_INVALID);CHECK(out.length==0);CHECK(zero(&out,sizeof out));
}
static void vocabulary(void)
{
 for(size_t e=0;e<11;++e) {
  begin(e,4,"exact-token-grammar-only");struct b6_syntax_event_token t;
  CHECK(b6_syntax_event_token((const unsigned char *)tokens[e],strlen(tokens[e]),&t)==B6_SYNTAX_OK);
  CHECK(t.state==B6_SYNTAX_GRAMMAR_ONLY);CHECK(t.value==types[e]);
  char changed[64];strcpy(changed,tokens[e]);changed[0]=(char)(changed[0]+('a'-'A'));
  begin(e,4,"wrong-case-fails-and-clears-token");
  CHECK(b6_syntax_event_token((const unsigned char *)changed,strlen(changed),&t)!=B6_SYNTAX_OK);CHECK(zero(&t,sizeof t));
  begin(e,4,"prefix-fails");
  CHECK(b6_syntax_event_token((const unsigned char *)tokens[e],strlen(tokens[e])-1,&t)!=B6_SYNTAX_OK);CHECK(zero(&t,sizeof t));
  strcpy(changed,tokens[e]);strcat(changed,"X");
  begin(e,4,"suffix-alias-fails");
  CHECK(b6_syntax_event_token((const unsigned char *)changed,strlen(changed),&t)!=B6_SYNTAX_OK);CHECK(zero(&t,sizeof t));
 }
 const char *bad[]={"","UNKNOWN","BIND","REGISTER","EXECUTION_ADMITTED","GENESIS "," GENESIS","GENESIS\n","GENESIS\t","GENESIS\r"};
 for(size_t i=0;i<sizeof bad/sizeof bad[0];++i) {
  begin(11,i,"no-unknown-alias-padding-passthrough");struct b6_syntax_event_token t;memset(&t,0xa5,sizeof t);
  CHECK(b6_syntax_event_token((const unsigned char *)bad[i],strlen(bad[i]),&t)!=B6_SYNTAX_OK);CHECK(zero(&t,sizeof t));
 }
 begin(11,0,"token-null-and-SIZE_MAX-before-read");struct b6_syntax_event_token t;
 CHECK(b6_syntax_event_token(NULL,1,&t)==B6_SYNTAX_ARGUMENT);CHECK(zero(&t,sizeof t));
 CHECK(b6_syntax_event_token((const unsigned char *)"G",SIZE_MAX,&t)!=B6_SYNTAX_OK);CHECK(zero(&t,sizeof t));
 CHECK(b6_syntax_event_token((const unsigned char *)"GENESIS",7,NULL)==B6_SYNTAX_ARGUMENT);
}
static void positives(void)
{
 for(size_t e=0;e<11;++e) {
  struct form f=form(e);begin(e,0,"all-eleven-canonical-shapes-round-trip");
  struct b6_syntax_event_frame out=accept(&f);
  CHECK(out.event_type==types[e]);CHECK(strcmp(out.common.store_id.text,uuid)==0);
  CHECK(out.common.event_seq==1);CHECK(out.common.previous_event.kind==(e?B6_SYNTAX_HEAD_DIGEST:B6_SYNTAX_HEAD_GENESIS));
  CHECK(strcmp(out.common.commission_id.text,e?"commission_1":"STORE")==0);
  CHECK(out.common.attempt_number==(uint64_t)(e==0||e==1||e==9?0:1));
  CHECK(strcmp(out.common.writer_boot_id.text,uuid)==0);CHECK(out.common.writer_pid==2);
  CHECK(out.common.writer_starttime==1);CHECK(out.common.recorded_boot_cs==0);
  CHECK(out.common.evidence.kind==(e?B6_SYNTAX_EVIDENCE_DIGEST:B6_SYNTAX_EVIDENCE_NOT_APPLICABLE));
  if(e) {CHECK(strcmp(out.common.previous_event.digest.text,hex)==0);CHECK(strcmp(out.common.evidence.digest.text,hex)==0);}
  switch(e) {
  case 0:CHECK(out.data.genesis.format_version==B6_SYNTAX_FORMAT_V1);break;
  case 1:CHECK(strcmp(out.data.registration.grant_reference.text,"grant_1")==0);
   CHECK(strcmp(out.data.registration.enrollment_permit_sha256.text,hex)==0);CHECK(strcmp(out.data.registration.commission_scope_sha256.text,hex)==0);CHECK(strcmp(out.data.registration.resource_key.text,hex)==0);break;
  case 2:CHECK(strcmp(out.data.reservation.permit_snapshot_sha256.text,hex)==0);CHECK(strcmp(out.data.reservation.observation_snapshot_sha256.text,hex)==0);CHECK(strcmp(out.data.reservation.native_preparation_descriptor_sha256.text,hex)==0);break;
  case 3:CHECK(strcmp(out.data.preparation.supervisor_executable_sha256.text,hex)==0);break;
  case 4:CHECK(strcmp(out.data.binding.native_transaction_id.text,hex)==0);CHECK(out.data.binding.supervisor_pid==2);CHECK(out.data.binding.supervisor_starttime==1);CHECK(out.data.binding.bash_pid==2);CHECK(out.data.binding.bash_starttime==1);CHECK(out.data.binding.output_device==0);CHECK(out.data.binding.output_inode==1);break;
  case 5:CHECK(strcmp(out.data.admission.admission_permit_sha256.text,hex)==0);CHECK(strcmp(out.data.admission.admission_observation_sha256.text,hex)==0);CHECK(strcmp(out.data.admission.admission_host_evidence_sha256.text,hex)==0);break;
  case 6:CHECK(out.data.reconciliation_begin.cause==B6_SYNTAX_CAUSE_COMPLETION);break;
  case 7:CHECK(strcmp(out.data.reconciled_failed.reconciliation_report_sha256.text,hex)==0);break;
  case 8:CHECK(strcmp(out.data.qualification_accepted.reconciliation_report_sha256.text,hex)==0);break;
  case 9:CHECK(out.data.unused_closure.closure_reason==B6_SYNTAX_CLOSURE_WITHDRAWAL);break;
  case 10:CHECK(strcmp(out.data.retirement.recovery_authority_reference.text,"recovery_1")==0);CHECK(strcmp(out.data.retirement.recovery_report_sha256.text,hex)==0);break;
  default:abort();
  }
 }
 const char *causes[]={"COMPLETION","FAILURE","CANCELLATION","RECOVERY"};
 for(size_t i=0;i<4;++i) {struct form f=form(6);f.rows[12].value=causes[i];begin(6,12,"closed-cause-round-trip");(void)accept(&f);}
 struct form f=form(9);f.rows[12].value="EXPIRY";begin(9,12,"closed-expiry-round-trip");(void)accept(&f);
 f=form(0);f.rows[11].value=hex;begin(0,11,"GENESIS-real-digest-alternative");(void)accept(&f);
}
struct numeric {size_t event,field;const char *minimum,*maximum,*overflow;int allow_zero;};
static void numbers(void)
{
 struct numeric domains[42];size_t count=0;
 for(size_t e=0;e<11;++e) {
  domains[count++]=(struct numeric){e,8,"2","2147483647","2147483648",0};
  domains[count++]=(struct numeric){e,9,"1","18446744073709551615","18446744073709551616",0};
  domains[count++]=(struct numeric){e,10,"0","9223372036854775807","9223372036854775808",1};
 }
 domains[count++]=(struct numeric){4,13,"2","2147483647","2147483648",0};
 domains[count++]=(struct numeric){4,14,"1","18446744073709551615","18446744073709551616",0};
 domains[count++]=(struct numeric){4,15,"2","2147483647","2147483648",0};
 domains[count++]=(struct numeric){4,16,"1","18446744073709551615","18446744073709551616",0};
 domains[count++]=(struct numeric){4,17,"0","18446744073709551615","18446744073709551616",1};
 domains[count++]=(struct numeric){4,18,"1","18446744073709551615","18446744073709551616",0};
 const char *bad[]={"","00","01","02","+2","-2"," 2","2 ","2.0","2e0","0x2","2x","2\t","2\n","2\r","184467440737095516150","99999999999999999999"};
 for(size_t d=0;d<count;++d) {
  struct numeric x=domains[d];struct form f=form(x.event);
  if(!x.allow_zero) {f.rows[x.field].value=x.minimum;begin(x.event,x.field,"numeric-minimum-round-trip");(void)accept(&f);}
  f.rows[x.field].value=x.maximum;begin(x.event,x.field,"numeric-maximum-round-trip");(void)accept(&f);
  f.rows[x.field].value="0";begin(x.event,x.field,x.allow_zero?"zero-DOMAIN-VALID-no-runtime-proof":"zero-domain-excluded");
  if(x.allow_zero) (void)accept(&f);else reject(&f);
  f.rows[x.field].value=x.overflow;begin(x.event,x.field,"exact-domain-overflow-clears-all-output");reject(&f);
  if(x.field==8||x.field==13||x.field==15) {f.rows[x.field].value="1";begin(x.event,x.field,"PID-one-excluded");reject(&f);}
  for(size_t b=0;b<sizeof bad/sizeof bad[0];++b) {
   f.rows[x.field].value=bad[b];begin(x.event,x.field,"noncanonical-number-no-normalization");reject(&f);
  }
 }
 for(size_t e=0;e<11;++e) for(size_t field=2;field<=6;field+=4) {
  struct form f=form(e);f.rows[field].value="18446744073709551615";begin(e,field,"sequence-or-attempt-U64-max-grammar-only");(void)accept(&f);
  f.rows[field].value="0";begin(e,field,"sequence-zero-or-conditional-nonattempt-zero");
  if(field==6) {struct b6_syntax_event_frame out=accept(&f);CHECK(out.common.attempt_number==0);CHECK(out.state==B6_SYNTAX_GRAMMAR_ONLY);}
  else reject(&f);
  for(size_t b=0;b<sizeof bad/sizeof bad[0];++b) {f.rows[field].value=bad[b];begin(e,field,"sequence-attempt-noncanonical");reject(&f);}
  f.rows[field].value="18446744073709551616";begin(e,field,"sequence-attempt-overflow");reject(&f);
 }
}
static void references(void)
{
 const char *valid[]={"R","0","01","a/../b","ref:unknown-or-expired",hex,uuid};
 const char *bad[]={"","/r",".r","_r",":r","r r","r%1","r?1","r#1","r+1","r\t","r\n","r\r"};
 char maximum[258];memset(maximum,'a',257);maximum[257]=0;
 for(size_t which=0;which<2;++which) {
  size_t e=which?10:1;struct form f=form(e);
  for(size_t i=0;i<sizeof valid/sizeof valid[0];++i) {f.rows[12].value=valid[i];begin(e,12,"REF-lexical-overlap-is-not-authority");(void)accept(&f);}
  maximum[256]=0;f.rows[12].value=maximum;begin(e,12,"REF-256-byte-boundary");(void)accept(&f);
  maximum[256]='a';f.rows[12].value=maximum;begin(e,12,"REF-257-byte-rejected");reject(&f);
  for(size_t i=0;i<sizeof bad/sizeof bad[0];++i) {f.rows[12].value=bad[i];begin(e,12,"REF-malformed-no-dereference");reject(&f);}
 }
}
static const char *digest_at(const struct b6_syntax_event_frame *f,size_t e,size_t i)
{
 if(i==3) return f->common.previous_event.digest.text;
 if(i==11) return f->common.evidence.digest.text;
 switch(e) {
 case 1:if(i==13)return f->data.registration.enrollment_permit_sha256.text;
  if(i==14)return f->data.registration.commission_scope_sha256.text;
  if(i==15)return f->data.registration.resource_key.text;
  break;
 case 2:if(i==12)return f->data.reservation.permit_snapshot_sha256.text;
  if(i==13)return f->data.reservation.observation_snapshot_sha256.text;
  if(i==14)return f->data.reservation.native_preparation_descriptor_sha256.text;
  break;
 case 3:return f->data.preparation.supervisor_executable_sha256.text;
 case 4:return f->data.binding.native_transaction_id.text;
 case 5:if(i==12)return f->data.admission.admission_permit_sha256.text;
  if(i==13)return f->data.admission.admission_observation_sha256.text;
  if(i==14)return f->data.admission.admission_host_evidence_sha256.text;
  break;
 case 7:return f->data.reconciled_failed.reconciliation_report_sha256.text;
 case 8:return f->data.qualification_accepted.reconciliation_report_sha256.text;
 case 10:return f->data.retirement.recovery_report_sha256.text;
 default:break;
 }
 return NULL;
}
static void field_retention(void)
{
 for(size_t e=0;e<11;++e) {
  struct form original=form(e);
  for(size_t i=0;i<original.count;++i) if(original.rows[i].value==hex) {
   char distinct[65];memset(distinct,'0',64);distinct[64]=0;
   static const char digits[]="0123456789abcdef";
   distinct[62]=digits[(e+i)/16];distinct[63]=digits[(e+i)%16];
   struct form f=original;f.rows[i].value=distinct;
   begin(e,i,"distinct-digest-retained-in-exact-typed-slot");
   struct b6_syntax_event_frame out=accept(&f);const char *retained=digest_at(&out,e,i);
   CHECK(retained!=NULL);CHECK(strcmp(retained,distinct)==0);
  }
 }
 begin(4,0,"golden-header-agrees-with-native-sha256sum");
 static const char expected[]="617\t99f22b3ff2163dc37cd7bc4dcca746e1d33f103624a3fbc9018b679d9aad2b31\n";
 struct form f=form(4);unsigned char bytes[21000];size_t n=encode(&f,bytes);
 CHECK(n==sizeof expected-1+617);CHECK(memcmp(bytes,expected,sizeof expected-1)==0);
 (void)accept(&f);
}
static void structure(void)
{
 const char *bad_events[]={"","UNKNOWN","bind_native","BIND","BIND_NATIVE_EXTRA","EXECUTION_ADMITTED"};
 const char *bad_schema[]={"B6_ATTEMPT_ACCOUNT_EVENT_V2","B6_ATTEMPT_ACCOUNT_STORE_V1","B6_ACCOUNT_RECONCILIATION_V1","b6_attempt_account_event_v1","B6_ATTEMPT_ACCOUNT_EVENT_V1 ",""};
 for(size_t e=0;e<11;++e) {
  for(size_t i=0;i<sizeof bad_events/sizeof bad_events[0];++i) {struct form f=form(e);f.rows[4].value=bad_events[i];begin(e,4,"frame-unknown-event-no-fallback");reject(&f);}
  for(size_t i=0;i<sizeof bad_schema/sizeof bad_schema[0];++i) {struct form f=form(e);f.rows[0].value=bad_schema[i];begin(e,0,"frame-schema-exact");reject(&f);}
  struct form original=form(e);
  for(size_t i=0;i<original.count;++i) {
   struct form f=original;for(size_t j=i;j+1<f.count;++j)f.rows[j]=f.rows[j+1];--f.count;
   begin(e,i,"each-mandatory-record-missing");reject(&f);
   f=original;f.rows[i].key="unknown_field";begin(e,i,"each-unknown-field");reject(&f);
   f=original;f.rows[f.count++]=f.rows[i];begin(e,i,"each-duplicate-record");reject(&f);
   f=original;f.rows[i].value="";begin(e,i,"each-empty-mandatory-value");reject(&f);
   if(i+1<original.count) {f=original;struct row r=f.rows[i];f.rows[i]=f.rows[i+1];f.rows[i+1]=r;begin(e,i,"every-adjacent-record-order");reject(&f);}
  }
  for(size_t other=0;other<11;++other) if(other!=e) {
   struct form f=original,alt=form(other);f.count=12;
   for(size_t i=12;i<alt.count;++i)f.rows[f.count++]=alt.rows[i];
   begin(e,other,"other-event-suffix-cannot-cross-shape");
   /* Failure/success report events deliberately share one exact suffix. */
   if((e==7&&other==8)||(e==8&&other==7)) (void)accept(&f);else reject(&f);
  }
  struct form f=original;f.rows[f.count++]=(struct row){"unknown_record","x"};begin(e,0,"unknown-extra-record");reject(&f);
  f=original;f.rows[3].value=e?"GENESIS":hex;begin(e,3,"GENESIS-head-exception-scope");reject(&f);
  f=original;f.rows[5].value=e?"STORE":"commission_1";begin(e,5,"STORE-reserved-exception-scope");reject(&f);
  if(e) {f=original;f.rows[11].value="NOT_APPLICABLE";begin(e,11,"GENESIS-only-evidence-sentinel");reject(&f);}
  unsigned char p[20000],bytes[21000];size_t n=payload(&original,p);
  const unsigned char bad[]={0,'\r',0x80,0x7f,'\t',' '};
  for(size_t field=0;field<original.count;++field) for(size_t i=0;i<sizeof bad;++i) {
   size_t at=0;for(size_t j=0;j<field;++j)at+=strlen(original.rows[j].key)+strlen(original.rows[j].value)+2;
   at+=strlen(original.rows[field].key)+1;
   (void)payload(&original,p);p[at]=bad[i];begin(e,field,"NUL-CR-nonASCII-extraTAB-space-with-valid-digest");reject_bytes(bytes,frame(p,n,bytes));
  }
  (void)payload(&original,p);begin(e,0,"missing-final-LF-with-valid-digest");reject_bytes(bytes,frame(p,n-1,bytes));
  (void)payload(&original,p);p[n++]='\n';begin(e,0,"extra-blank-record-valid-digest");reject_bytes(bytes,frame(p,n,bytes));
  f=original;f.rows[f.count-1].value="invalid-late-value";begin(e,f.count-1,"late-failure-after-common-and-prefix-suffix");reject(&f);
 }
}
static void headers(void)
{
 unsigned char p[20000],bytes[42000],valid[21000];struct form f=form(4);
 size_t plen=payload(&f,p),n=frame(p,plen,valid),header=n-plen;
 for(size_t cut=0;cut<n;++cut) {begin(4,cut,"every-prefix-truncation-invalidates-output");reject_bytes(valid,cut);}
 const char *bad[]={"0","00","01","+1","-1"," 1","1 ","1.0","1e0","0x1","18446744073709551616","16385","99999"};
 for(size_t i=0;i<sizeof bad/sizeof bad[0];++i) {
  size_t tab=0;while(valid[tab]!='\t')++tab;
  size_t k=strlen(bad[i]);memcpy(bytes,bad[i],k);memcpy(bytes+k,valid+tab,n-tab);
  begin(4,i,"noncanonical-zero-overflow-size-header");reject_bytes(bytes,k+n-tab);
 }
 for(size_t i=0;i<header;++i) {
  memcpy(bytes,valid,n);bytes[i]=0;begin(4,i,"header-NUL-at-every-position");reject_bytes(bytes,n);
 }
 memcpy(bytes,valid,n);bytes[n-2]^=1;begin(4,0,"local-payload-digest-mismatch");reject_bytes(bytes,n);
 memcpy(bytes,valid,n);bytes[n]='x';begin(4,0,"extra-trailing-byte");reject_bytes(bytes,n+1);
 memcpy(bytes,valid,n);memcpy(bytes+n,valid,n);begin(4,0,"two-frames-not-a-one-frame-object");reject_bytes(bytes,2*n);
 memset(p,'a',16384);p[16383]='\n';begin(4,0,"payload-at-container-bound-invalid-shape");reject_bytes(bytes,frame(p,16384,bytes));
 p[16384]='\n';begin(4,0,"payload-one-over-container-bound");reject_bytes(bytes,frame(p,16385,bytes));
 begin(4,0,"SIZE_MAX-before-read");reject_bytes((const unsigned char *)"x",SIZE_MAX);
 begin(4,0,"frame-maximum-one-over-before-read");reject_bytes((const unsigned char *)"x",B6_SYNTAX_EVENT_FRAME_MAX+1u);
 begin(4,0,"null-input");reject_bytes(NULL,n);
 begin(4,0,"null-output");CHECK(b6_syntax_parse_event_frame(valid,n,NULL)==B6_SYNTAX_ARGUMENT);
}
static void serialization(void)
{
 for(size_t e=0;e<11;++e) {
  struct form f=form(e);begin(e,0,"typed-suffix-grammar-only");struct b6_syntax_event_frame valid=accept(&f),out;
  out=valid;out.state=B6_SYNTAX_INVALID;begin(e,0,"serializer-invalid-state-clears-byte-object");reject_serial(&out);
  out=valid;out.event_type=(enum b6_syntax_event_type)999;begin(e,0,"serializer-unknown-event");reject_serial(&out);
  out=valid;out.event_type=B6_SYNTAX_NO_EVENT;begin(e,0,"serializer-empty-event");reject_serial(&out);
  out=valid;out.common.event_seq=0;begin(e,2,"serializer-zero-sequence");reject_serial(&out);
  out=valid;out.common.writer_pid=0;begin(e,8,"serializer-PID-zero");reject_serial(&out);
  out=valid;out.common.writer_pid=UINT32_MAX;begin(e,8,"serializer-PID-overflow");reject_serial(&out);
  out=valid;out.common.writer_starttime=0;begin(e,9,"serializer-starttime-zero");reject_serial(&out);
  out=valid;out.common.recorded_boot_cs=UINT64_MAX;begin(e,10,"serializer-CS-overflow");reject_serial(&out);
  out=valid;memset(out.common.store_id.text,'a',sizeof out.common.store_id.text);begin(e,1,"unterminated-UUID-storage");reject_serial(&out);
  out=valid;memset(out.common.commission_id.text,'a',sizeof out.common.commission_id.text);begin(e,5,"unterminated-ID-storage");reject_serial(&out);
  out=valid;out.common.previous_event.kind=B6_SYNTAX_NO_HEAD;begin(e,3,"no-default-head-kind");reject_serial(&out);
  out=valid;out.common.evidence.kind=B6_SYNTAX_NO_EVIDENCE;begin(e,11,"no-default-evidence-kind");reject_serial(&out);
  out=valid;memset(&out.data,0,sizeof out.data);begin(e,12,"partial-union-has-no-mandatory-default");reject_serial(&out);
  if(e) {out=valid;memset(out.common.evidence.digest.text,'a',sizeof out.common.evidence.digest.text);begin(e,11,"unterminated-HEX-storage");reject_serial(&out);}
 }
 struct form f=form(4);begin(4,0,"binding-for-typed-negative-fixtures");struct b6_syntax_event_frame valid=accept(&f),out;
 for(size_t field=0;field<6;++field) {
  out=valid;switch(field) {case 0:out.data.binding.supervisor_pid=0;break;case 1:out.data.binding.supervisor_starttime=0;break;case 2:out.data.binding.bash_pid=0;break;case 3:out.data.binding.bash_starttime=0;break;case 4:out.data.binding.output_inode=0;break;case 5:memset(out.data.binding.native_transaction_id.text,'a',65);break;default:abort();}
  begin(4,field,"late-binding-typed-zero-or-unterminated");reject_serial(&out);
 }
 f=form(10);begin(10,0,"retirement-for-typed-negative-fixtures");valid=accept(&f);out=valid;
 memset(out.data.retirement.recovery_authority_reference.text,'a',257);begin(10,12,"unterminated-recovery-reference");reject_serial(&out);
 out=valid;memset(out.data.retirement.recovery_report_sha256.text,'a',65);begin(10,13,"late-unterminated-recovery-report");reject_serial(&out);
 begin(11,0,"null-serialization-arguments");struct b6_syntax_event_bytes bytes;memset(&bytes,0xa5,sizeof bytes);
 CHECK(b6_syntax_serialize_event_frame(NULL,&bytes)==B6_SYNTAX_ARGUMENT);CHECK(zero(&bytes,sizeof bytes));
 CHECK(b6_syntax_serialize_event_frame(&valid,NULL)==B6_SYNTAX_ARGUMENT);
}
int main(int argc,char **argv)
{
 if(argc==2) {
  unsigned char bytes[21000];struct form f=form(4);size_t n;
  if(strcmp(argv[1],"--emit-golden-payload")==0) n=payload(&f,bytes);
  else if(strcmp(argv[1],"--emit-golden-frame")==0) n=encode(&f,bytes);
  else return 2;
  return fwrite(bytes,1,n,stdout)==n?0:1;
 }
 if(argc!=1) return 2;
 vocabulary();positives();numbers();references();field_retention();structure();headers();serialization();
 printf("SUMMARY\tsemantic_cases=%zu\tassertions=%zu\tresult=PASS\n",cases,assertions);return 0;
}
