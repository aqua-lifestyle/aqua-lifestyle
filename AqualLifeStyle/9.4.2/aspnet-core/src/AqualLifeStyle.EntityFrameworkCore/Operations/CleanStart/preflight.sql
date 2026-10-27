-- AQGreenCleanStartScreenV2. Use only through the reviewed status-only launcher.
-- This contract supersedes the raw-count/native-pin preflight only after approval.
\set ON_ERROR_STOP on
\set QUIET on
\pset pager off
\pset format unaligned
\pset tuples_only on
\o /dev/null
\getenv expected_session_user PGUSER
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout='30s';
SET LOCAL lock_timeout='3s';
SET LOCAL idle_in_transaction_session_timeout='45s';
SET LOCAL search_path=pg_catalog;
SELECT set_config('aqua.clean_start.tenant','1',true),
       set_config('aqua.clean_start.boundary',:'boundary_utc',true),
       set_config('aqua.clean_start.reset_id',:'reset_id',true),
       set_config('aqua.clean_start.expected_user',:'expected_session_user',true);
DO $identity$
BEGIN
 IF current_database()<>'aqualifestyle' OR
    session_user<>current_setting('aqua.clean_start.expected_user') OR
    current_user<>session_user OR
    (SELECT ssl FROM pg_catalog.pg_stat_ssl WHERE pid=pg_backend_pid()) IS DISTINCT FROM true OR
    current_setting('transaction_read_only')<>'on' OR
    current_setting('transaction_isolation')<>'repeatable read' THEN
  RAISE EXCEPTION 'CLEAN_START_SOURCE_OR_READ_ONLY_SESSION_UNVERIFIED';
 END IF;
END $identity$;
\ir preflight-checks.sql
SELECT current_setting('aqua.clean_start.preflight_receipt') AS receipt \gset
ROLLBACK;
-- Nothing is emitted until the read-only transaction has rolled back successfully.
\o
\echo :receipt
