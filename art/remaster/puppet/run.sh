#!/bin/zsh
# Runs a puppet pipeline script with the pinned Python and image dependencies (via uv), like art/audio/run.sh.
UV=${UV:-$(command -v uv 2>/dev/null)}
if [[ -z "$UV" || "$UV" == *shims* ]]; then UV=$(ls -d ~/.asdf/installs/uv/*/bin/uv 2>/dev/null | tail -1); fi
exec "$UV" run --no-project --python 3.13 --with numpy --with scipy --with pillow --with opencv-python-headless python "$@"
