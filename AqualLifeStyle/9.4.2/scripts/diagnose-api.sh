#!/usr/bin/env bash
# Backend API connectivity diagnostic for AquaLifeStyle + Next.js frontend.
set -u

API_HTTPS="${API_HTTPS:-https://localhost:44311}"
API_HTTP="${API_HTTP:-http://localhost:21021}"
FRONTEND_ORIGIN="${FRONTEND_ORIGIN:-http://localhost:3000}"
FRONTEND_DIR="$(cd "$(dirname "$0")/../aqua-frontend" && pwd)"
HOST_BIN_HINT="AqualLifeStyle.Web.Host"

red() { printf '\033[0;31m%s\033[0m\n' "$*"; }
green() { printf '\033[0;32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[0;33m%s\033[0m\n' "$*"; }
section() { printf '\n=== %s ===\n' "$*"; }

pass=0
fail=0
check() {
  local name="$1"
  local ok="$2"
  if [[ "$ok" == "1" ]]; then
    green "PASS: $name"
    pass=$((pass + 1))
  else
    red "FAIL: $name"
    fail=$((fail + 1))
  fi
}

section "1. Backend process"
if pgrep -af "$HOST_BIN_HINT" | grep -v grep >/tmp/aqua-diag-procs.txt 2>/dev/null; then
  cat /tmp/aqua-diag-procs.txt
  check "Backend process running" 1
else
  yellow "Backend process not found (start: dotnet run --project src/AqualLifeStyle.Web.Host)"
  check "Backend process running" 0
fi

section "2. Listening ports"
for port in 44311 21021 3000; do
  if ss -tln 2>/dev/null | grep -q ":${port} "; then
    green "Listening on :$port"
  else
    yellow "Not listening on :$port"
  fi
done

section "3. HTTPS API health ($API_HTTPS/api/health)"
https_body="$(mktemp)"
https_code="$(curl -sk -o "$https_body" -w "%{http_code}" --connect-timeout 5 "$API_HTTPS/api/health" || true)"
echo "HTTP status: $https_code"
head -c 400 "$https_body"; echo
if [[ "$https_code" == "200" ]]; then
  check "HTTPS /api/health returns 200" 1
else
  check "HTTPS /api/health returns 200" 0
fi

section "4. HTTP API health ($API_HTTP/api/health) [dev fallback]"
http_body="$(mktemp)"
http_code="$(curl -s -o "$http_body" -w "%{http_code}" --connect-timeout 5 "$API_HTTP/api/health" || true)"
echo "HTTP status: $http_code"
head -c 400 "$http_body"; echo
if [[ "$http_code" == "200" ]]; then
  check "HTTP /api/health returns 200" 1
else
  yellow "HTTP fallback optional if HTTPS works and cert is trusted"
  check "HTTP /api/health returns 200" 0
fi

section "5. TLS certificate trust (no -k)"
using_http_fallback=0
if [[ -f "$FRONTEND_DIR/.env.local" ]] && grep -qE "^NEXT_PUBLIC_ABP_API_URL=${API_HTTP//\//\\/}/?$" "$FRONTEND_DIR/.env.local"; then
  using_http_fallback=1
fi
if curl -s -o /dev/null -w "trusted_status:%{http_code}\n" --connect-timeout 5 "$API_HTTPS/api/health" 2>/tmp/aqua-diag-tls.err; then
  check "System TLS trust for localhost HTTPS" 1
else
  cat /tmp/aqua-diag-tls.err 2>/dev/null || true
  yellow "Certificate not trusted by curl/system store. Browser may also block HTTPS fetch."
  yellow "Fix: open $API_HTTPS/swagger and accept the cert, or: dotnet dev-certs https --trust"
  yellow "Linux fallback: set NEXT_PUBLIC_ABP_API_URL=$API_HTTP"
  if [[ "$using_http_fallback" == "1" && "$http_code" == "200" ]]; then
    yellow "WARN: TLS not trusted, but HTTP fallback is configured and healthy — treating as non-blocking."
    pass=$((pass + 1))
    green "PASS: Local API reachable via HTTP fallback"
  else
    check "System TLS trust for localhost HTTPS" 0
  fi
fi

section "6. CORS preflight ($FRONTEND_ORIGIN)"
cors_headers="$(mktemp)"
curl -sk -D "$cors_headers" -o /dev/null -X OPTIONS "$API_HTTPS/api/health" \
  -H "Origin: $FRONTEND_ORIGIN" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: Content-Type, Authorization, __tenant" \
  --connect-timeout 5 || true
grep -i "access-control" "$cors_headers" || yellow "No Access-Control headers returned"
if grep -qi "access-control-allow-origin: $FRONTEND_ORIGIN" "$cors_headers"; then
  check "CORS allows $FRONTEND_ORIGIN" 1
else
  check "CORS allows $FRONTEND_ORIGIN" 0
fi

section "7. Frontend environment"
env_file="$FRONTEND_DIR/.env.local"
if [[ -f "$env_file" ]]; then
  grep -E '^NEXT_PUBLIC_ABP_API_URL=' "$env_file" || true
  if grep -qE '^NEXT_PUBLIC_ABP_API_URL=https://localhost:44311/?$' "$env_file" \
    || grep -qE '^NEXT_PUBLIC_ABP_API_URL=http://localhost:21021/?$' "$env_file"; then
    check "NEXT_PUBLIC_ABP_API_URL points at local host API" 1
  else
    yellow "Unexpected API URL (expected https://localhost:44311 or http://localhost:21021)"
    check "NEXT_PUBLIC_ABP_API_URL points at local host API" 0
  fi
else
  yellow "Missing $env_file (copy from .env.example)"
  check "Frontend .env.local exists" 0
fi

section "Summary"
echo "Passed: $pass  Failed: $fail"
if [[ "$fail" -gt 0 ]]; then
  exit 1
fi
exit 0
