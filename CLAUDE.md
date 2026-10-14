# CLAUDE.md

Guia para agentes (LLMs) e pessoas que mexem neste repositorio. Leia antes de editar.

## O que e
Clone portatil da Ferramenta de Recorte do Windows: um unico `CapturaTela.exe` em C# (WinForms), sem dependencias alem do .NET Framework 4 que ja vem no Windows 10/11. Interface e textos em portugues (sem acentos no codigo-fonte e nas strings, para evitar problemas de codificacao no `csc`).

## Comandos
| Tarefa | Comando |
|---|---|
| Compilar o app | `build.bat` (gera `CapturaTela.exe` na raiz) |
| Rodar os testes | `test.bat` (gera `build\tests.exe`, sai com codigo 0 se tudo passou) |
| Regenerar o icone | veja `tools\MakeIcon.cs` |

Nao ha SDK do .NET: tudo compila com o `csc.exe` do .NET Framework 4 (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`).

> No shell Bash do Claude Code, `cmd /c build.bat` nao acha o arquivo. Use o caminho absoluto: `cmd /c "C:\caminho\do\projeto\build.bat"` (os `.bat` ja fazem `cd /d "%~dp0"`).
> Se o build falhar com "arquivo em uso", ha um `CapturaTela.exe` aberto (instancia unica, tambem na bandeja). Feche-o antes de recompilar.

## Restricoes do compilador (C# 5)
O `csc` 4.0 so entende C# 5. **Nao use**: `?.`, `??=`, `$"..."` (interpolacao), `nameof`, membros com `=>`, inicializadores de propriedade automatica (`{ get; } = x`), funcoes locais, `out var`, tuplas, `async` moderno com `?`. Use `string.Format`/concatenacao, `delegate { }` ou lambdas, e `out` com variavel declarada antes.

## Estrutura (`src/`)
| Arquivo | Papel |
|---|---|
| `Program.cs` | `Main`, instancia unica (mutex), argumento `/tray` |
| `MainForm.cs` | Janela principal, barras, bandeja, atalhos globais, fluxo de captura |
| `OverlayForm.cs` | Tela congelada para selecionar a area; barra de modos; lupa |
| `Canvas.cs` + `Annotations.cs` | Editor: anotacoes, historico desfazer/refazer, zoom, `Flatten()` |
| `ScrollCapture.cs` | Captura com rolagem (rola com a roda do mouse e costura por hash de linhas) |
| `RecorderForm.cs` + `GifWriter.cs` | Gravacao em GIF; encoder GIF proprio (median-cut + LZW) |
| `Ocr.cs` | OCR via `Windows.Media.Ocr` chamado por PowerShell |
| `Settings.cs` + `SettingsForm.cs` | Configuracoes em `CapturaTela.ini` ao lado do `.exe` |
| `Theme.cs` | Tema claro/escuro e icone embutido |
| `Native.cs` | P/Invoke (hotkeys, janelas, DWM, mouse) |
| `tests/Tests.cs` | Testes automatizados |

## Convencoes
- Coordenadas de tela sao **pixels fisicos** (o app e DPI-aware via `SetProcessDPIAware`); o monitor virtual pode ter origem negativa.
- Anotacoes vivem no espaco da imagem; o zoom so afeta a pintura.
- Desfocar/pixelar sempre le a imagem original (nao as outras anotacoes) para que o conteudo nao seja recuperavel ao exportar.
- Nao grave no registro nem fora da pasta do `.exe`, exceto o "Iniciar com o Windows" (opt-in) e o `.ini`.
- Arquivos novos em `src/` entram no build automaticamente (`src\*.cs`).

## Como validar mudancas
1. `test.bat` precisa terminar com 0 falhas. Teste logica nova em `tests/Tests.cs` (use `TestRunner.Run("nome", delegate { ... })` e `Assert`).
2. Para UI, compile e abra o app. Daria para simular `Print Screen`/mouse por script e fotografar a tela, mas e fragil: prefira testar a logica sem janela.
3. Nunca deixe uma instancia do app aberta ao terminar.

## Git
- Fluxo: uma branch por assunto a partir de `main`, commit em portugues no imperativo, PR para `main`.
- `*.exe`, `*.ini` e `build/` estao no `.gitignore`; o `.exe` nao e versionado.
