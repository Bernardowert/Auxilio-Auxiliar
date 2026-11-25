@echo off
cd /d "%~dp0"
title Gerar GrandTheftAuxilio.exe
echo ==========================================
echo   Gerando GrandTheftAuxilio.exe
echo   (usa o compilador que ja vem no Windows)
echo ==========================================
echo.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [ERRO] Compilador do .NET Framework 4 nao encontrado em %WINDIR%\Microsoft.NET
  echo Ative o recurso ".NET Framework 4.8" em "Recursos do Windows" e tente de novo.
  pause
  exit /b 1
)

if not exist dist mkdir dist

"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /platform:anycpu /out:dist\GrandTheftAuxilio.exe /win32manifest:app.manifest /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Launcher.cs
if errorlevel 1 (
  echo.
  echo [ERRO] Falha ao compilar. Veja as mensagens acima.
  pause
  exit /b 1
)

if not exist dist\launcher-config.json copy /Y launcher-config.json dist\launcher-config.json >nul

echo.
echo ==========================================
echo   Pronto! Arquivos em: %cd%\dist
echo     GrandTheftAuxilio.exe
echo     launcher-config.json  (edite para trocar o magnet)
echo ==========================================
pause
