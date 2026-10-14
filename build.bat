@echo off
rem Compila usando o csc.exe do .NET Framework 4 (ja incluso no Windows).
rem   CapturaTela.exe     - aplicativo com janela (e aceita os comandos da CLI)
rem   CapturaTelaCli.exe  - mesma base, subsistema de console: stdout/stderr funcionam em terminais e pipes
rem O icone (assets\icon.ico) vai como icone do .exe e como recurso para a janela/bandeja.
rem O guia do usuario (docs\GUIA.md) vai embutido: e o que a janela de Ajuda (F1) mostra.
rem Para regenerar o icone: veja tools\MakeIcon.cs
cd /d "%~dp0"
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set REFS=/r:System.Windows.Forms.dll /r:System.Drawing.dll
set ICON=/win32icon:assets\icon.ico /resource:assets\icon.ico,icon.ico /resource:docs\GUIA.md,guia.md

%CSC% /nologo /codepage:65001 /target:winexe /optimize+ /out:CapturaTela.exe %ICON% %REFS% src\*.cs
if errorlevel 1 (echo Falha na compilacao de CapturaTela.exe. & exit /b 1)
%CSC% /nologo /codepage:65001 /target:exe /define:CLI /optimize+ /out:CapturaTelaCli.exe %ICON% %REFS% src\*.cs
if errorlevel 1 (echo Falha na compilacao de CapturaTelaCli.exe. & exit /b 1)
echo OK: CapturaTela.exe e CapturaTelaCli.exe gerados.
