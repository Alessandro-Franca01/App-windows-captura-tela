@echo off
rem Gera os prints de todas as telas em docs\design\screens (temas claro e escuro, sobre uma area de trabalho ficticia).
rem Cobre a tela por ~1 minuto: nao use o computador enquanto roda.
cd /d "%~dp0"
if not exist build mkdir build
set FILES=
for %%f in (src\*.cs) do if /i not "%%~nxf"=="Program.cs" call set FILES=%%FILES%% %%f
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /out:build\design-shots.exe /resource:assets\icon.ico,icon.ico /resource:docs\GUIA.md,guia.md /r:System.Windows.Forms.dll /r:System.Drawing.dll tools\DesignShots.cs %FILES%
if errorlevel 1 (echo Falha na compilacao. & exit /b 1)
build\design-shots.exe "%~dp0docs\design\screens"
