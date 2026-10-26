#!/usr/bin/env bash
# Evaluates C# in the connected editor and prints the result string.
P="C:/Users/lunal/Desktop/VSCoded/Games/ChoppingTrees"
unity command eval --code "$1" --timeout ${2:-300} --project-path $P --caller plugin --skill unity-cli --no-banner --format json 2>&1 | python -c "
import json,sys
raw=sys.stdin.read()
try:
    d=json.loads(raw)
    r=d.get('data') or {}
    r=(r.get('result') or {})
    print(r.get('result', r) if isinstance(r,dict) else r)
    if d.get('errors'): print('ERRORS', d['errors'])
except Exception:
    print(raw[:2000])
"
