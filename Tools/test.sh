#!/usr/bin/env bash
# Runs the EditMode suite (or a filter) and prints a summary.
P="C:/Users/lunal/Desktop/VSCoded/Games/ChoppingTrees"
filter=${1:-ChoppingTrees.Tests.EditMode}
type=${2:-assembly}
for i in 1 2 3; do
  unity command test run --mode EditMode --filter "$filter" --filter_type $type --format json --project-path $P --caller plugin --skill unity-cli --no-banner > "$P/Temp/editmode.json" 2>&1
  grep -q -E "PIPELINE_UNREACHABLE|Network error" "$P/Temp/editmode.json" || break
  sleep 10
done
python "$P/Tools/test_summary.py" "$P/Temp/editmode.json"
