#!/usr/bin/env bash
# Captures the editor Game view to Temp/shots/<name>.png
P="C:/Users/lunal/Desktop/VSCoded/Games/ChoppingTrees"
name=${1:-shot}
mkdir -p "$P/Temp/shots"
# The game view only redraws when repainted; force it so the capture is current.
bash "$(dirname "$0")/eval.sh" 'UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); return "";' >/dev/null 2>&1
sleep 0.5
unity command game_view --width ${2:-1280} --height ${3:-720} --format json --project-path $P --caller plugin --skill unity-cli --no-banner > "$P/Temp/shots/$name.json" 2>&1
python - "$P/Temp/shots/$name.json" "$P/Temp/shots/$name.png" <<'PY'
import json,sys,base64,re
raw=open(sys.argv[1],encoding='utf-8').read()
m=re.search(r'"(?:image|base64|data|png)[A-Za-z_]*"\s*:\s*"([A-Za-z0-9+/=]{200,})"',raw)
if not m:
    print(raw[:600]); sys.exit(1)
open(sys.argv[2],'wb').write(base64.b64decode(m.group(1)))
print(sys.argv[2])
PY
