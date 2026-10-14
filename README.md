# Ferramenta de Captura (Windows)

Clone portatil da Ferramenta de Recorte do Windows, em um unico `.exe` (~25 KB), sem instalacao e sem dependencias alem do .NET Framework 4 que ja vem no Windows 10/11.

## Recursos
- Modos: retangulo, forma livre, janela, tela inteira, **rolagem automatica**, **rolagem manual** e **gravacao em GIF** (multi-monitor, DPI alto); voce escolhe o modo na lista ao lado de "+ Novo", na barra durante a selecao ou no menu da bandeja
- Barra flutuante durante a selecao para trocar de modo (ou teclas 1-7), com **lupa** de pixel, coordenadas e cor; setas ajustam 1 px
- **Rolagem automatica**: o app rola o conteudo sob a area com a roda do mouse e costura os quadros (Esc interrompe). Se nada rolar, o app sugere o modo manual
- **Rolagem manual**: voce marca a area e rola com a roda do mouse, devagar; o app fotografa a cada pausa e costura (voltar para cima e seguro). Termine em "Concluir", Enter ou no atalho global; Esc cancela. Serve para o que nao reage a rolagem automatica
- Paineis e molduras do proprio app nao aparecem nas capturas (Windows 10 2004+)
- **Ajuda dentro do app**: botao "Ajuda" ou tecla F1 abre o guia do usuario (o mesmo texto de [docs/GUIA.md](docs/GUIA.md), embutido no .exe)
- GIF: ate 30 s a 10 fps, moldura vermelha fora da area gravada, encoder GIF proprio (sem dependencias)
- Atraso de 1 a 10 segundos
- Atalhos globais: `Print Screen` e `Ctrl+Shift+S`
- Editor: caneta, marca-texto, seta, linha, retangulo, elipse, texto, marcador numerado, desfoque, pixelar, cortar, conta-gotas e borracha
- Desfazer/refazer ilimitado e zoom (Ctrl + roda do mouse); Shift restringe angulos e proporcoes
- Copia para a area de transferencia automaticamente; salva em PNG, JPG, BMP ou GIF
- Fica na bandeja do sistema (fechar a janela nao encerra o app) com menu de captura rapida
- Salvamento automatico de cada captura em uma pasta (PNG/JPG)
- Atalho global configuravel e opcao de iniciar com o Windows (/tray)
- **OCR** (botao Copiar texto): reconhece o texto da imagem com o motor nativo do Windows e copia
- Notificacoes do Windows ao capturar/gravar (clique abre o arquivo ou o editor)
- Icone proprio, tema claro/escuro (ou seguir o Windows)
- Configuracoes lembradas entre execucoes em CapturaTela.ini (ao lado do .exe)

## Primeiros passos
Nunca usou? Abra o app e aperte **F1**, ou leia o [guia do usuario](docs/GUIA.md).

## Compilar
```
build.bat
```
Gera `CapturaTela.exe` (app) e `CapturaTelaCli.exe` (linha de comando) na raiz. Nao precisa de SDK, apenas do `csc.exe` que acompanha o Windows.

## Linha de comando (para scripts e agentes de IA)
`CapturaTelaCli.exe` captura, grava GIF, rola, faz OCR e anota imagens sem janela, devolvendo JSON em stdout:
```
CapturaTelaCli.exe capture --window "meu app" --max-width 1280 --out a.png
CapturaTelaCli.exe ocr --image a.png
CapturaTelaCli.exe annotate --in a.png --out b.png --pixelate 10,10,300,20
```
Referencia completa em [docs/CLI.md](docs/CLI.md).

## Uso por IA (MCP) e permissoes
`CapturaTelaCli.exe mcp` e um servidor MCP: clientes de IA capturam janelas, fazem OCR e anotam imagens, **sempre com permissao do usuario** (uma vez / sempre / negar). Veja [docs/MCP.md](docs/MCP.md) e [docs/PERMISSOES.md](docs/PERMISSOES.md).

## Usar em outros projetos com o Claude Code
O Claude Code usa o app como um servidor MCP: ele pode **ver a tela** do seu programa (janela, OCR, GIF) enquanto testa ou desenvolve outro projeto. Nao e preciso copiar codigo para o outro projeto, so o `.exe`.

**1. Uma vez, no seu computador**
1. Crie uma pasta fixa, por exemplo `C:\Ferramentas\CapturaTela\`.
2. Copie para ela `CapturaTelaCli.exe` **e** `CapturaTela.exe` (o segundo traz a tela *Configuracoes > Permissoes da IA*; os dois precisam ficar na mesma pasta para dividir o arquivo de permissoes).
3. Registre o servidor para todos os seus projetos:
```
claude mcp add --scope user captura-tela -- "C:\Ferramentas\CapturaTela\CapturaTelaCli.exe" mcp
```

**2. Em cada projeto**
1. Abra o Claude Code na pasta do projeto e digite `/mcp`: `captura-tela` deve aparecer conectado.
2. Peca normalmente: *"abra o app, capture a janela dele e confira se o botao Salvar aparece"*.
3. Na primeira vez de cada acao, o app abre uma janela de permissao: **Permitir uma vez**, **Permitir sempre**, **Negar** ou **Negar sempre**. A decisao e sua.

**Opcional: equipe**
Para que todos que clonarem o projeto ja recebam a configuracao, copie [docs/exemplos/mcp.json](docs/exemplos/mcp.json) para a raiz do projeto com o nome `.mcp.json` e peca a cada pessoa que defina a variavel de ambiente `CAPTURA_TELA` com a pasta do passo 1. Cole tambem o trecho de [docs/exemplos/CLAUDE-trecho.md](docs/exemplos/CLAUDE-trecho.md) no `CLAUDE.md` do projeto, para o Claude saber quando e como usar.

Cuidados: o app so **le** a tela (nao clica nem digita); voce precisa estar no computador para responder aos pedidos de permissao (sem resposta em 60 s o pedido e negado); e as imagens vistas pela IA vao para o provedor do modelo, entao prefira capturar uma janela e esconda dados pessoais com `annotate`. Mais detalhes em [docs/MCP.md](docs/MCP.md) e [docs/PERMISSOES.md](docs/PERMISSOES.md).

## Testes
```
test.bat
```
Roda os testes automatizados (anotacoes, GIF, rolagem, configuracoes, OCR). Veja [CLAUDE.md](CLAUDE.md) para o guia de desenvolvimento.

## Atalhos
| Atalho | Acao |
|---|---|
| Print Screen / Ctrl+Shift+S | Nova captura |
| Ctrl+N | Nova captura (janela em foco) |
| Ctrl+C / Ctrl+S | Copiar / Salvar |
| Ctrl+Z / Ctrl+Y | Desfazer / Refazer |
| Ctrl + roda do mouse | Zoom |
| Esc ou clique direito | Cancelar selecao |
| 1-7 (na selecao) | Retangulo, forma livre, janela, tela inteira, rolagem auto, rolagem manual, GIF |
| Atalho global (durante gravacao) | Para a gravacao |

## Estrutura
- `src/` - codigo-fonte (Native, OverlayForm, Canvas, Annotations, TextPrompt, ScrollCapture, RecorderForm, GifWriter, Ocr, Theme, Settings, SettingsForm, MainForm, Program)
- `build.bat` - script de compilacao
- `assets/icon.ico` - icone (gerado por `tools/MakeIcon.cs`)



## Limitacoes conhecidas
- Captura com rolagem: cabecalhos, barras de ferramentas e rodapes fixos aparecem uma unica vez; a barra de rolagem da janela pode sair com pequenos defeitos na faixa do lado direito. Conteudo que anima ou muda sozinho (videos, relogios) atrapalha o alinhamento.
- Gravacao: somente GIF (sem MP4) e sem audio.
- OCR: exige ao menos um idioma de OCR instalado no Windows (Configuracoes > Hora e idioma > Idioma e regiao).




