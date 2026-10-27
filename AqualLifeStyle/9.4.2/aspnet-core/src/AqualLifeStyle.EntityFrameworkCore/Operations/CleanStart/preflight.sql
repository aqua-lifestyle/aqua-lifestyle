\set ON_ERROR_STOP on
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout='30s';
SET LOCAL lock_timeout='3s';
SET LOCAL idle_in_transaction_session_timeout='45s';
SELECT set_config('aqua.clean_start.tenant', '1', true),
       set_config('aqua.clean_start.boundary', :'boundary_utc', true),
       set_config('aqua.clean_start.reset_id', :'reset_id', true),
       set_config('aqua.clean_start.database', :'source_database', true),
       set_config('aqua.clean_start.expected_identity', :'expected_database_identity_digest', true);
DO $identity$
DECLARE actual text;
BEGIN
 IF (SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()) IS DISTINCT FROM true THEN
  RAISE EXCEPTION 'CLEAN_START_TLS_REQUIRED'; END IF;
 SELECT encode(sha256(convert_to(system_identifier::text||'|'||current_database()||'|'||
  (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex') INTO actual FROM pg_control_system();
 IF current_database()<>current_setting('aqua.clean_start.database') OR actual<>current_setting('aqua.clean_start.expected_identity') THEN
  RAISE EXCEPTION 'CLEAN_START_SOURCE_IDENTITY_CHANGED'; END IF;
END $identity$;
\ir state.sql
SELECT jsonb_build_object('transactionReadOnly',current_setting('transaction_read_only'),
 'tls',(SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()),
 'observedAtUtc',transaction_timestamp(),
 'mvccDigest',encode(sha256(convert_to(txid_current_snapshot()::text,'UTF8')),'hex'),
 'migrationCount',(SELECT count(*) FROM public."__EFMigrationsHistory"),
 'latestMigration',(SELECT max("MigrationId") FROM public."__EFMigrationsHistory"),
 'databaseIdentityDigest',encode(sha256(convert_to(current_database()||':'||
   (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex'),
 'nativeDatabaseIdentityDigest',(SELECT encode(sha256(convert_to(system_identifier::text||'|'||current_database()||'|'||
   (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex') FROM pg_control_system()),
 'schemaDigest',encode(sha256(convert_to((SELECT string_agg(table_name||':'||column_name||':'||data_type||':'||is_nullable,
   E'\n' ORDER BY table_name,ordinal_position) FROM information_schema.columns WHERE table_schema='public'),'UTF8')),'hex'));
ROLLBACK;
