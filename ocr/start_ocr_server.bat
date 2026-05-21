@echo off
setlocal
cd /d %~dp0

if not exist ".venv\Scripts\python.exe" (
    echo OCR environment not found.
    echo Please run install_ocr_env.bat first.
    pause
    exit /b 1
)

if not exist "paddle_ocr_server.py" (
    echo File not found: paddle_ocr_server.py
    pause
    exit /b 1
)

call .venv\Scripts\activate
python paddle_ocr_server.py

endlocal
exit /b 0
