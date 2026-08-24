#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: $0 MERGED_MODEL_DIR OUTPUT_GGUF" >&2
  exit 2
fi

: "${LLAMA_CPP_REPO:?LLAMA_CPP_REPO must point to a llama.cpp checkout}"
merged_dir=$1
output_gguf=$2
f16_gguf="${output_gguf%.gguf}-f16.tmp.gguf"

mkdir -p "$(dirname "$output_gguf")"
python "$LLAMA_CPP_REPO/convert_hf_to_gguf.py" "$merged_dir" --outfile "$f16_gguf" --outtype f16
"$LLAMA_CPP_REPO/build/bin/llama-quantize" "$f16_gguf" "$output_gguf" Q4_K_M
rm -f "$f16_gguf"
sha256sum "$output_gguf"
printf 'output=%s\n' "$output_gguf"
printf 'quantization=Q4_K_M\n'
printf 'behavior_gate=required-before-release\n'
