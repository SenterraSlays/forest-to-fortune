#!/usr/bin/env bash
# Enters play mode in the connected editor and keeps it ticking while unfocused.
D="$(dirname "$0")"
bash "$D/eval.sh" 'UnityEditor.EditorApplication.isPlaying = true; return "play";' >/dev/null
for i in $(seq 1 30); do
  sleep 2
  r=$(bash "$D/eval.sh" 'return UnityEditor.EditorApplication.isPlaying && ChoppingTrees.Game.Instance != null ? ChoppingTrees.EditorTools.PlayDriver.Enable(true, 1) : "wait";' 2>/dev/null)
  case "$r" in *driving*|*unchanged*) echo "playing"; exit 0;; esac
done
echo "failed to enter play mode"; exit 1
