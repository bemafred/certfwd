#!/usr/bin/env bash
ZIP_NAME="certfwd-src.zip"

echo "Zipping certfwd source on Linux/macOS..."

zip -r "$ZIP_NAME" -@ < .zipinclude

echo "Created $ZIP_NAME"
#xdg-open "$ZIP_NAME" 2>/dev/null || open "$ZIP_NAME" 2>/dev/null
