@echo off
rem Compila e roda os testes automatizados (tests\Tests.cs + todo src\, exceto o Main do app).
rem Saida: build\tests.exe. Codigo de saida 0 = tudo certo.
cd /d "%~dp0"
if not exist build mkdir build
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /main:CapturaTela.Tests.TestRunner /out:build\tests.exe /resource:docs\GUIA.md,guia.md /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs tests\*.cs
if errorlevel 1 (echo Falha na compilacao dos testes. & exit /b 2)
build\tests.exe
exit /b %errorlevel%
