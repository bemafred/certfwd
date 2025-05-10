@echo off
setlocal
set ZIP_NAME=certfwd-src.zip

echo Zipping certfwd source on Windows...

powershell -Command ^
  "$files = Get-Content .zipinclude; Compress-Archive -Path $files -DestinationPath '%ZIP_NAME%'"

echo Created %ZIP_NAME%
rem explorer.exe %ZIP_NAME%
