# Ferramenta de Captura (Windows)

Clone portatil da Ferramenta de Recorte do Windows, em um unico `.exe` (~25 KB), sem instalacao e sem dependencias alem do .NET Framework 4 que ja vem no Windows 10/11.

## Recursos
- Modos: retangulo, forma livre, janela e tela inteira (multi-monitor, DPI alto)
- Atraso de 1 a 10 segundos
- Atalhos globais: `Print Screen` e `Ctrl+Shift+S`
- Editor: caneta, marca-texto, seta, linha, retangulo, elipse, texto, marcador numerado, desfoque, pixelar, cortar, conta-gotas e borracha
- Desfazer/refazer ilimitado e zoom (Ctrl + roda do mouse); Shift restringe angulos e proporcoes
- Copia para a area de transferencia automaticamente; salva em PNG, JPG, BMP ou GIF
- Fica na bandeja do sistema (fechar a janela nao encerra o app) com menu de captura rapida
- Salvamento automatico de cada captura em uma pasta (PNG/JPG)
- Atalho global configuravel e opcao de iniciar com o Windows (/tray)
- Configuracoes lembradas entre execucoes em CapturaTela.ini (ao lado do .exe)

## Compilar
```
build.bat
```
Gera `CapturaTela.exe` na raiz. Nao precisa de SDK, apenas do `csc.exe` que acompanha o Windows.

## Atalhos
| Atalho | Acao |
|---|---|
| Print Screen / Ctrl+Shift+S | Nova captura |
| Ctrl+N | Nova captura (janela em foco) |
| Ctrl+C / Ctrl+S | Copiar / Salvar |
| Ctrl+Z / Ctrl+Y | Desfazer / Refazer |
| Ctrl + roda do mouse | Zoom |
| Esc ou clique direito | Cancelar selecao |

## Estrutura
- `src/` - codigo-fonte (Native, OverlayForm, Canvas, Annotations, TextPrompt, Settings, SettingsForm, MainForm, Program)
- `build.bat` - script de compilacao


