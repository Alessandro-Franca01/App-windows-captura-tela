@echo off
rem Compila CapturaTela.exe usando o csc.exe do .NET Framework 4 (ja incluso no Windows).
rem O icone (assets\icon.ico) vai como icone do .exe e como recurso para a janela/bandeja.
rem Para regenerar o icone: veja tools\MakeIcon.cs
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:CapturaTela.exe /win32icon:assets\icon.ico /resource:assets\icon.ico,icon.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs
if errorlevel 1 (echo Falha na compilacao.) else (echo OK: CapturaTela.exe gerado.)
