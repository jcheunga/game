#!/bin/zsh
# Runs a pipeline script with the pinned Python and DSP dependencies (via uv).
UV=${UV:-$(command -v uv 2>/dev/null)}
if [[ -z "$UV" || "$UV" == *shims* ]]; then UV=$(ls -d ~/.asdf/installs/uv/*/bin/uv 2>/dev/null | tail -1); fi
cd "${0:A:h}" && exec "$UV" run --no-project --python 3.13 --with numpy --with scipy --with soundfile --with matplotlib python "$@"
