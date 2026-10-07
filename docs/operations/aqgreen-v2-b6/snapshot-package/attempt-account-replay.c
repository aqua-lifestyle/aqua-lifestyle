#include "attempt-account-replay.h"
#include <openssl/sha.h>
#include <string.h>

/* Branch authority: DURABLE-REPLAY-AUTHORITY-TO-CODE.tsv (R, A, S, AUTH).
   Framing below identifies a bounded span; the existing parser alone validates
   frame bytes/encoding/domain/suffix and its local SHA. No competing codec. */
static int same(const char *a,const char *b,size_t capacity)
{ for(size_t i=0;i<capacity;++i) {if(a[i]!=b[i])return 0;if(!a[i])return 1;}return 0; }
#define EQ(a,b) same((a).text,(b).text,sizeof (a).text)
static int digest(const unsigned char *p,size_t n,struct b6_syntax_hex *out)
{ unsigned char raw[SHA256_DIGEST_LENGTH];static const char digits[]="0123456789abcdef";
 if(!SHA256(p,n,raw))return 0;
 for(size_t i=0;i<sizeof raw;++i){out->text[i*2]=digits[raw[i]>>4];out->text[i*2+1]=digits[raw[i]&15u];}
 out->text[64]=0;return 1;
}
static int overlap(const void *a,size_t na,const void *b,size_t nb)
{ if(!na || !nb)return 0;uintptr_t pa=(uintptr_t)a,pb=(uintptr_t)b;
 if(na>UINTPTR_MAX-pa || nb>UINTPTR_MAX-pb)return 1;
 return pa<pb+nb && pb<pa+na;
}
static struct b6_account_replay_result empty(enum b6_replay_disposition d)
{ struct b6_account_replay_result r;memset(&r,0,sizeof r);r.disposition=d;return r; }
static void clear_workspace(struct b6_replay_workspace w)
{ if(w.record_capacity)memset(w.records,0,w.record_capacity*sizeof *w.records);
 if(w.commission_capacity)memset(w.commissions,0,w.commission_capacity*sizeof *w.commissions);
}
static struct b6_account_replay_result fail(struct b6_replay_workspace w,enum b6_replay_disposition d)
{ clear_workspace(w);return empty(d); }
static enum b6_replay_disposition required(enum b6_external_disposition d)
{ if(d==B6_EXTERNAL_VALIDATED)return B6_REPLAY_NO_RESULT;
 return d==B6_EXTERNAL_INVALID?B6_REPLAY_INVALID_HISTORY:B6_REPLAY_UNDETERMINED;
}
#define NEED(d) do {enum b6_replay_disposition need=required(d);if(need!=B6_REPLAY_NO_RESULT)return fail(w,need);}while(0)
#define INVALID_IF(c) do {if(c)return fail(w,B6_REPLAY_INVALID_HISTORY);}while(0)

struct b6_account_replay_result b6_account_replay(const struct b6_replay_input *in,
 struct b6_replay_workspace w)
{
 /* API span/arithmetic/alias guards. No semantic state is invented on failure. */
 if(!in || w.record_capacity>SIZE_MAX/sizeof *w.records
  || w.commission_capacity>SIZE_MAX/sizeof *w.commissions
  || (w.record_capacity && !w.records) || (w.commission_capacity && !w.commissions))
  return empty(B6_REPLAY_UNDETERMINED);
 size_t nr=w.record_capacity*sizeof *w.records,nc=w.commission_capacity*sizeof *w.commissions;
 if(in->record_count>SIZE_MAX/sizeof *in->records || in->journal_length>(size_t)PTRDIFF_MAX
  || in->store_identity_length>(size_t)PTRDIFF_MAX)return empty(B6_REPLAY_UNDETERMINED);
 size_t ne=in->record_count*sizeof *in->records;
 if(overlap(w.records,nr,w.commissions,nc)
  || overlap(w.records,nr,in,sizeof *in) || overlap(w.commissions,nc,in,sizeof *in)
  || overlap(w.records,nr,in->journal,in->journal_length) || overlap(w.commissions,nc,in->journal,in->journal_length)
  || overlap(w.records,nr,in->store_identity,in->store_identity_length) || overlap(w.commissions,nc,in->store_identity,in->store_identity_length)
  || overlap(w.records,nr,in->records,ne) || overlap(w.commissions,nc,in->records,ne))
  return empty(B6_REPLAY_UNDETERMINED); /* Do not clear memory aliasing immutable input. */
 clear_workspace(w);
 if(!in->journal || !in->store_identity || (in->record_count && !in->records)
  || !in->journal_length || !in->record_count || in->record_count>w.record_capacity
  || in->record_count>w.commission_capacity)return fail(w,B6_REPLAY_UNDETERMINED);
 if(in->scope!=B6_REPLAY_HISTORICAL_PREFIX && in->scope!=B6_REPLAY_COMPLETE_CURRENT_CANDIDATE)
  return fail(w,B6_REPLAY_UNDETERMINED);
 /* A H/I/L; R2/3/5. Equality/codec is not ownership or trusted durable proof. */
 NEED(in->protected_journal_store_custody);NEED(in->historical_identity_continuity);
 NEED(in->required_custody_history);
 struct b6_syntax_document store;struct b6_syntax_account_store_bytes canonical;
 INVALID_IF(b6_syntax_parse(in->store_identity,in->store_identity_length,B6_SYNTAX_ACCOUNT_STORE_V1,&store)!=B6_SYNTAX_OK
  || store.schema!=B6_SYNTAX_ACCOUNT_STORE_V1);
 INVALID_IF(b6_syntax_serialize_account_store(&in->validated_store_identity,&canonical)!=B6_SYNTAX_OK);
 INVALID_IF(canonical.length!=in->store_identity_length
  || memcmp(canonical.bytes,in->store_identity,canonical.length)!=0);
 struct b6_account_replay_result r=empty(B6_REPLAY_NO_RESULT);
 if(!digest(in->journal,in->journal_length,&r.journal_sha256))return fail(w,B6_REPLAY_UNDETERMINED);
 INVALID_IF(!EQ(r.journal_sha256,in->validated_journal_sha256));
 r.store_context=in->original_provisioning;
 INVALID_IF(!EQ(r.store_context.store_id,store.data.account_store.store_id));
 size_t offset=0,count=0,commissions=0;uint64_t last_seq=0;
 struct b6_syntax_hex previous;memset(&previous,0,sizeof previous);
 while(offset<in->journal_length) {
  /* A I exact bounded positive length; no scan/resynchronization after error. */
  INVALID_IF(count>=in->record_count);
  size_t remaining=in->journal_length-offset,tab=0;
  while(tab<remaining && tab<6 && in->journal[offset+tab]!='\t')++tab;
  INVALID_IF(!tab || tab>5 || tab==remaining || in->journal[offset+tab]!='\t');
  uint64_t payload_length=0;
  INVALID_IF(b6_syntax_u64plus(in->journal+offset,tab,&payload_length)!=B6_SYNTAX_OK
   || payload_length>B6_SYNTAX_EVENT_PAYLOAD_MAX);
  size_t header=tab+66;
  INVALID_IF(header>remaining || payload_length>(uint64_t)(remaining-header));
  size_t frame_length=header+(size_t)payload_length;
  struct b6_syntax_event_frame f;
  INVALID_IF(b6_syntax_parse_event_frame(in->journal+offset,frame_length,&f)!=B6_SYNTAX_OK);
  INVALID_IF(!EQ(f.common.store_id,store.data.account_store.store_id)
   || last_seq==UINT64_MAX || f.common.event_seq!=last_seq+1u);
  if(!count) {
   INVALID_IF(f.event_type!=B6_SYNTAX_GENESIS || f.common.previous_event.kind!=B6_SYNTAX_HEAD_GENESIS);
  } else {
   INVALID_IF(f.event_type==B6_SYNTAX_GENESIS || f.common.previous_event.kind!=B6_SYNTAX_HEAD_DIGEST
    || !EQ(f.common.previous_event.digest,previous));
  }
  struct b6_syntax_hex payload,prefix;
  if(!digest(in->journal+offset+header,(size_t)payload_length,&payload)
   || !digest(in->journal,offset,&prefix))return fail(w,B6_REPLAY_UNDETERMINED);
  const struct b6_replay_original_evidence *proof=&in->records[count];
  /* R2/3/4 named original facts; retained role sources and writer proven externally. */
  NEED(proof->original_preceding_projection);NEED(proof->original_writer_provenance);
  NEED(proof->retained_original_sources);NEED(proof->original_durable_commit);
  INVALID_IF(!EQ(prefix,proof->preceding_prefix_sha256));
  struct b6_account_evidence e=proof->original_transition;
  INVALID_IF(e.operation!=B6_ACCOUNT_APPLY_FRESH
   || e.dispositions.complete_current_history!=B6_EXTERNAL_UNVALIDATED);
  INVALID_IF(!EQ(payload,e.event_payload_sha256));
  /* R3: complete VALIDATED preceding projection AT original point satisfies S's
     history prerequisite for this invocation. Never maps today's completeness
     or a raw prefix/hash to permission, and never writes this into current proof. */
  e.dispositions.complete_current_history=proof->original_preceding_projection;
  struct b6_account_context c=r.store_context;
  size_t slot=0;
  if(f.event_type!=B6_SYNTAX_GENESIS) {
   for(slot=0;slot<commissions;++slot)
    if(EQ(w.commissions[slot].context.commission_id,f.common.commission_id))break;
   if(slot==commissions) {
    /* R3: only original independently established nonenrollment permits creation
       of this projection from proven GENESIS; no default for absent/damaged store. */
    INVALID_IF(f.event_type!=B6_SYNTAX_REGISTER_COMMISSION);
    c.commission_id=f.common.commission_id;
   } else c=w.commissions[slot].context;
  }
  if(c.kind==B6_ACCOUNT_NORMAL_CONTEXT)c.retained_history_sha256=prefix;
  /* A J/R3: historical conflict disposition must agree with ALL preceding
     commission projections. Other terminal histories remain retained; an
     unresolved same governed resource cannot disappear behind a producer flag. */
  if(f.event_type==B6_SYNTAX_RESERVE_ATTEMPT) {
   for(size_t other=0;other<commissions;++other) {
    if(other==slot)continue;
    const struct b6_account_context *oc=&w.commissions[other].context;
    int unresolved=oc->phase==B6_ATTEMPT_RESERVED_UNRECONCILED
     || oc->phase==B6_ATTEMPT_ADMITTED_UNRECONCILED || oc->phase==B6_ATTEMPT_RECONCILING;
    if(unresolved && EQ(oc->resource_key,c.resource_key))
     e.dispositions.resource_conflict_disposition=B6_EXTERNAL_INVALID;
   }
  }
  struct b6_account_transition_result transition=b6_account_transition_validate(&c,&f,&e);
  if(transition.disposition==B6_TRANSITION_UNDETERMINED)return fail(w,B6_REPLAY_UNDETERMINED);
  INVALID_IF(transition.disposition!=B6_TRANSITION_ALLOWED
   || transition.next_context_present!=B6_ACCOUNT_FACT_YES);
  /* AUTH6/R3/5: retain and check all linked committed reservations, not just latest.
     Full independent custody history remains a separate required input. */
  if(f.event_type==B6_SYNTAX_RESERVE_ATTEMPT) {
   for(size_t old=0;old<count;++old) {
    if(w.records[old].event.event_type!=B6_SYNTAX_RESERVE_ATTEMPT)continue;
    const struct b6_syntax_assignment *a=&w.records[old].transition.next.assignment;
    INVALID_IF(EQ(a->generation,transition.next.assignment.generation)
     || a->execution_uid==transition.next.assignment.execution_uid);
   }
  }
  w.records[count].event=f;w.records[count].payload_sha256=payload;
  w.records[count].transition=transition;
  if(f.event_type==B6_SYNTAX_GENESIS)r.store_context=transition.next;
  else {w.commissions[slot].context=transition.next;if(slot==commissions)++commissions;}
  previous=payload;last_seq=f.common.event_seq;++count;offset+=frame_length;
 }
 INVALID_IF(count!=in->record_count);
 /* R3: ONLY entire validated view PLUS separate current completeness proof may
    claim current. Missing proof still allows bounded explicitly historical result. */
 r.disposition=(in->scope==B6_REPLAY_COMPLETE_CURRENT_CANDIDATE
  && in->complete_current_identity_continuity==B6_EXTERNAL_VALIDATED)
  ?B6_REPLAY_COMPLETE_CURRENT_STATE:B6_REPLAY_HISTORICAL_STATE;
 r.historical_context_present=B6_ACCOUNT_FACT_YES;
 r.current_context_present=r.disposition==B6_REPLAY_COMPLETE_CURRENT_STATE?B6_ACCOUNT_FACT_YES:B6_ACCOUNT_FACT_NO;
 r.last_payload_sha256=previous;r.last_event_seq=last_seq;r.record_count=count;r.commission_count=commissions;
 r.store_context.retained_history_sha256=r.journal_sha256;
 for(size_t i=0;i<commissions;++i)w.commissions[i].context.retained_history_sha256=r.journal_sha256;
 r.records=w.records;r.commissions=w.commissions;return r;
}
