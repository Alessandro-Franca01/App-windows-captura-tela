# Ferramenta de Captura (Windows)

Clone portatil da Ferramenta de Recorte do Windows, em um unico `.exe` (~25 KB), sem instalacao e sem dependencias alem do .NET Framework 4 que ja vem no Windows 10/11.

## Recursos
- Modos: retangulo, forma livre, janela e tela inteira (multi-monitor, DPI alto)
- Atraso de 1 a 10 segundos
- Atalhos globais: `Print Screen` e `Ctrl+Shift+S`
- Editor: caneta, marca-texto, borracha, cor, espessura e desfazer
- Copia para a area de transferencia automaticamente; salva em PNG, JPG, BMP ou GIF

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
| Ctrl+C / Ctrl+S / Ctrl+Z | Copiar / Salvar / Desfazer |
| Esc ou clique direito | Cancelar selecao |

## Estrutura
- `src/CapturaTela.cs` - codigo-fonte completo
- `build.bat` - script de compilacao
