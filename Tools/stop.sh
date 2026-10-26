#!/usr/bin/env bash
D="$(dirname "$0")"
bash "$D/eval.sh" 'UnityEditor.EditorApplication.isPlaying = false; return "stopped";'
