#!/usr/bin/env bash
# Recompiles scripts in the connected editor and prints the compiler errors.
P="C:/Users/lunal/Desktop/VSCoded/Games/ChoppingTrees"
U="unity command --project-path $P --caller plugin --skill unity-cli --no-banner"
$U recompile >/dev/null 2>&1
for i in $(seq 1 150); do
  out=$($U recompile_status --format json 2>/dev/null)
  st=$(echo "$out" | grep -o '"status": *"[a-z_]*"' | head -1)
  case "$st" in *completed*|*up_to_date*) break;; esac
  sleep 2
done
echo "$st"
echo "$out" | python -c "
import json,sys
d=json.load(sys.stdin)['data']['result']
print('failed:',d.get('failed'))
for e in d.get('errors',[])[:80]: print(e if isinstance(e,str) else json.dumps(e))
"
if echo "$out" | grep -q '"failed": true'; then
  $U console get --level error --tail 40 --format json 2>/dev/null | grep -o '"message": *"[^"]*' | sed 's/"message": *"//' | sort -u | head -40
fi
for i in $(seq 1 60); do
  unity command recompile_status --project-path $P --caller plugin --skill unity-cli --no-banner --format json >/dev/null 2>&1 && break
  sleep 2
done
