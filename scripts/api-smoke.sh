#!/usr/bin/env bash
# Smoke-tests the API against a freshly seeded dev database (dotnet run -- reset-demo).
# Usage: API=http://localhost:5080 PASSWORD=Rahoon-Demo-2026! bash scripts/api-smoke.sh
set -euo pipefail
API=${API:-http://localhost:5080}
PASSWORD=${PASSWORD:-Rahoon-Demo-2026!}
ORIGIN=http://localhost:3000
WORK=$(mktemp -d)

jar() { echo "$WORK/$1.jar"; }
csrf() { awk '$6=="rahoon_csrf"{print $7}' "$(jar "$1")"; }
req() { # who method path [json] [idempotency-key]
  local who=$1 method=$2 path=$3 body=${4:-} key=${5:-}
  local args=(-sS -b "$(jar "$who")" -c "$(jar "$who")" -X "$method" -H "Origin: $ORIGIN" -H "Content-Type: application/json")
  [[ -n "$(csrf "$who" 2>/dev/null)" ]] && args+=(-H "X-CSRF-Token: $(csrf "$who")")
  [[ -n "$key" ]] && args+=(-H "Idempotency-Key: $key")
  [[ -n "$body" ]] && args+=(--data "$body")
  curl "${args[@]}" -w '\n%{http_code}' "$API$path"
}
login() { # who email
  touch "$(jar "$1")"
  local out code
  out=$(req "$1" POST /api/auth/login "{\"email\":\"$2\",\"password\":\"$PASSWORD\"}")
  code=$(echo "$out" | head -1 | python -c "import sys,json; print(json.load(sys.stdin)['sandboxCode'])")
  req "$1" POST /api/auth/mfa/verify "{\"code\":\"$code\"}" | head -1
}
check() { local label=$1 expected=$2 actual=$3; if [[ "$actual" == "$expected" ]]; then echo "PASS $label"; else echo "FAIL $label (expected $expected got $actual)"; FAILED=1; fi; }
status() { echo "$1" | tail -1; }
FAILED=0

echo "── سارة (multi-org) ──"
next=$(login sara s.alqahtani@alufuq.example)
echo "$next"
mid=$(req sara GET /api/auth/me | head -1 | python -c "import sys,json; m=json.load(sys.stdin)['memberships']; print([x for x in m if x['organization']=='مصرف الأفق'][0]['id'])")
req sara POST /api/auth/context "{\"membershipId\":\"$mid\"}" | head -1

out=$(req sara GET /api/portfolio); check "portfolio 200" 200 "$(status "$out")"
echo "$out" | head -1 | python -c "import sys,json; d=json.load(sys.stdin); k=d['kpis']; print('  active',k['active'],'overdue',k['overdue'],'tasks',k['myTasks'],'outstanding',k['outstanding'])"
out=$(req sara GET "/api/cases?view=mine&pageSize=10"); check "case list 200" 200 "$(status "$out")"
echo "$out" | head -1 | python -c "import sys,json; d=json.load(sys.stdin); print('  counts',d['counts']); [print('  ',i['reference'],i['statusLabel'],'|',i['nextAction'],'|',i['slaText']) for i in d['items'][:4]]"
out=$(req sara GET /api/cases/RH-2026-004172); check "workspace 200" 200 "$(status "$out")"
echo "$out" | head -1 | python -c "import sys,json; d=json.load(sys.stdin); print('  ',d['header']['title'],'|',d['nextAction']['title'],'| enabled:',d['nextAction']['primary']['enabled'])"
out=$(req sara GET /api/cases/RH-2026-004172/solutions/2/submission); check "submission view 200" 200 "$(status "$out")"
echo "$out" | head -1 | python -c "import sys,json; d=json.load(sys.stdin); print('  approver',d['approver'],'blockers',d['blockers'])"

echo "── tenant isolation ──"
out=$(req sara GET /api/cases/RH-2026-005101); check "other tenant case hidden" 403 "$(status "$out")"
out=$(req sara GET /api/cases/RH-2099-999999); check "unknown case same refusal" 403 "$(status "$out")"

echo "── CSRF / origin ──"
code=$(curl -sS -o /dev/null -w '%{http_code}' -b "$(jar sara)" -X POST -H "Origin: $ORIGIN" -H "Content-Type: application/json" -H "Idempotency-Key: x1" --data '{}' "$API/api/cases/drafts")
check "mutation without CSRF header rejected" 403 "$code"
code=$(curl -sS -o /dev/null -w '%{http_code}' -b "$(jar sara)" -X POST -H "Origin: https://evil.example" -H "X-CSRF-Token: $(csrf sara)" -H "Content-Type: application/json" --data '{}' "$API/api/cases/drafts")
check "foreign origin rejected" 403 "$code"

echo "── فهد (preparer) cannot submit own version ──"
login fahad f.alotaibi@alufuq.example >/dev/null
out=$(req fahad POST /api/cases/RH-2026-004172/solutions/2/submit '{"note":"أرسل الحل للموافقة الآن","attested":true}' "k-fahad-1")
check "preparer submit forbidden" 403 "$(status "$out")"

echo "── submit v2 for approval (idempotent) ──"
body='{"note":"راجعت الحل مع كشف الراتب v2. القسط ضمن حد الاستقطاع.","attested":true,"expectedStatus":"proposed_solution"}'
out1=$(req sara POST /api/cases/RH-2026-004172/solutions/2/submit "$body" "k-submit-v2"); check "submit 200" 200 "$(status "$out1")"
echo "  $(echo "$out1" | head -1)"
out2=$(req sara POST /api/cases/RH-2026-004172/solutions/2/submit "$body" "k-submit-v2"); check "replay returns same 200" 200 "$(status "$out2")"
[[ "$(echo "$out1" | head -1)" == "$(echo "$out2" | head -1)" ]] && echo "PASS replay identical body" || { echo "FAIL replay body differs"; FAILED=1; }
out3=$(req sara POST /api/cases/RH-2026-004172/solutions/2/submit "$body" "k-submit-v2-again"); check "second logical submit refused" 409 "$(status "$out3")"
out=$(req sara GET /api/cases/RH-2026-004172); echo "$out" | head -1 | python -c "import sys,json; d=json.load(sys.stdin); print('  status now:',d['header']['statusLabel'],'| next:',d['nextAction']['title'],'| for you:',d['nextAction']['forYou'])"

echo "── audit chain ──"
out=$(req sara GET "/api/cases/RH-2026-004172/activity?take=3"); echo "$out" | head -1 | python -c "import sys,json; [print('  ',e['seq'],e['title'],'|',e['actorLabel']) for e in json.load(sys.stdin)]"

rm -rf "$WORK"
exit $FAILED
