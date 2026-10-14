@echo off
rem Compila CapturaTela.exe usando o csc.exe do .NET Framework 4 (ja incluso no Windows).
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:CapturaTela.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs
if errorlevel 1 (echo Falha na compilacao.) else (echo OK: CapturaTela.exe gerado.)
