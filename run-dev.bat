@echo off
setlocal
chcp 65001 >nul
title NX-MTD launcher
cd /d "%~dp0"

echo [NX-MTD] Dang khoi dong backend (http://localhost:5080) va frontend (http://localhost:5173)...

if not exist "fe\node_modules" (
  echo [NX-MTD] Cai dat package frontend lan dau...
  pushd fe
  call npm install
  popd
)
if not exist "fe\.env" copy /y "fe\.env.example" "fe\.env" >nul

start "NX-MTD Backend" cmd /k "cd /d %~dp0be && dotnet run --launch-profile http"
start "NX-MTD Frontend" cmd /k "cd /d %~dp0fe && npm run dev"

echo [NX-MTD] Cho server san sang...
powershell -NoProfile -Command "$ok=$false; for($i=0;$i -lt 90 -and -not $ok;$i++){ try { Invoke-WebRequest -UseBasicParsing http://localhost:5080/swagger/v1/swagger.json -TimeoutSec 2 | Out-Null; Invoke-WebRequest -UseBasicParsing http://localhost:5173 -TimeoutSec 2 | Out-Null; $ok=$true } catch { Start-Sleep 2 } }; if(-not $ok){ exit 1 }"
if errorlevel 1 (
  echo [NX-MTD] Server chua len sau 3 phut. Xem 2 cua so Backend/Frontend de biet loi.
  pause
  exit /b 1
)

start "" http://localhost:5173
start "" http://localhost:5080/swagger
echo [NX-MTD] San sang. Dong 2 cua so Backend/Frontend de tat server.
timeout /t 5 >nul
