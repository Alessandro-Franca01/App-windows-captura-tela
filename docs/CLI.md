# Interface de linha de comando (CLI)

Para scripts e para agentes (LLMs) usarem o aplicativo sem interface grafica. Dois executaveis fazem o mesmo:

| Executavel | Quando usar |
|---|---|
| `CapturaTelaCli.exe` | Padrao para terminais, scripts e agentes (subsistema de console: stdout e codigo de saida funcionam em qualquer pipe). |
| `CapturaTela.exe <comando>` | O app com janela tambem aceita os comandos, mas sua saida so aparece se o stdout for redirecionado. |

Ambos sao gerados por `build.bat`. Rodar `CapturaTelaCli.exe` sem argumentos (ou `help`) imprime a ajuda.

## Contrato de saida
- **Uma linha de JSON em stdout**, sempre ASCII (acentos viram `\uXXXX`), sem BOM.
- `"ok": true` em sucesso; `"ok": false` com `"error"` em falha.
- Codigo de saida: `0` ok, `1` erro ao executar (janela nao encontrada, area fora da tela...), `2` uso incorreto (opcao invalida), `3` permissao negada (veja [PERMISSOES.md](PERMISSOES.md); so se a opcao "exigir permissao na CLI" estiver ligada).
- Coordenadas em **pixels fisicos**. A tela virtual pode ter origem negativa (monitor a esquerda do principal): consulte `info`.
- Sem `--out`, os arquivos vao para `%TEMP%\CapturaTela\` e o caminho vem no JSON.

## Alvos
Comandos que capturam a tela pedem **exatamente um** destes:

| Alvo | Significado |
|---|---|
| `--fullscreen` | Toda a area virtual (todos os monitores) |
| `--screen N` | Monitor N (0 = primeiro de `info.screens`) |
| `--region x,y,w,h` | Retangulo em coordenadas de tela |
| `--window "texto"` | Primeira janela visivel (da frente para tras) cujo titulo **ou** processo contem o texto. A janela e trazida para frente antes da captura; use `--no-activate` para nao mexer nela |
| `--active` | Janela em primeiro plano |

Dica: janelas lentas para redesenhar podem sair incompletas; use `--delay 800`.

## Comandos

### `info`
Versao, monitores, area virtual, posicao do cursor e pasta de saida.
```
CapturaTelaCli.exe info
{"ok":true,"command":"info","version":"2.1.0.0","virtualScreen":{"x":0,"y":0,"width":1920,"height":1080},"screens":[{"index":0,"primary":true,...}],"cursor":{"x":973,"y":230},"outputDir":"C:\\Users\\...\\Temp\\CapturaTela"}
```

### `windows [--filter texto]`
Janelas visiveis com `handle`, `title`, `process`, `pid` e `bounds`.

### `pixel --at x,y` | `pixel --image arq --at x,y`
Cor de um pixel da tela ou de uma imagem: `"color":"#RRGGBB"`, `r`, `g`, `b`.

### `capture <alvo> [opcoes]`
| Opcao | Efeito |
|---|---|
| `--out arq` | Caminho de saida (`.png`, `.jpg`, `.bmp`); padrao PNG em `%TEMP%\CapturaTela` |
| `--format png\|jpg` | Formato quando nao ha `--out` |
| `--max-width N` | Reduz mantendo a proporcao (util para enviar a um modelo: menos tokens) |
| `--delay ms` | Espera antes de capturar (depois de trazer a janela para frente) |
| `--cursor` | Desenha o ponteiro do mouse |
| `--clipboard` | Tambem copia para a area de transferencia |
```
CapturaTelaCli.exe capture --window code --max-width 1280
{"ok":true,"command":"capture","file":"C:\\...\\captura-20261013-211530-123.png","width":1280,"height":680,"bytes":98211,"region":{...},"target":{"type":"window","title":"...","process":"Code","handle":9572058},"capturedWidth":1920,"capturedHeight":1020}
```

### `gif <alvo> [--seconds 5] [--fps 10] [--max-width 960] [--cursor] [--out arq.gif]`
Grava a area em GIF animado (ate 120 s, 25 fps). Recusa gravacoes que usariam mais de ~900 MB de memoria. O JSON traz `frames`, `bytes` e dimensoes. Quadros iguais viram um so quadro mais longo, entao `frames` pode ser menor que `seconds * fps`.

### `scroll <alvo> [--max-steps 60] [--out arq]`
Rola o conteudo sob a area com a roda do mouse (o cursor vai para o centro da area e e restaurado depois) e costura as imagens. Para quando o conteudo acaba ou ao apertar **Esc**.

### `ocr (<alvo> | --image arq)`
Reconhece o texto com o motor de OCR do Windows (idiomas instalados no perfil). Devolve `text`, `lines`, `chars`. Falha com codigo 1 se nao houver idioma de OCR.

### `annotate --in arq --out arq [operacoes]`
Aplica as operacoes **na ordem em que aparecem**. Coordenadas no espaco da imagem de entrada (mas depois de um `--crop`, as seguintes usam a imagem ja cortada).

| Operacao | Formato |
|---|---|
| `--color #RRGGBB` / `--width N` | Valem para as operacoes seguintes (padrao vermelho, 4) |
| `--rect`, `--ellipse` | `x,y,w,h` |
| `--arrow`, `--line` | `x1,y1,x2,y2` |
| `--text` | `x,y,texto` (o texto pode ter virgulas) |
| `--step` | `x,y[,n]` marcador numerado; sem `n` continua a contagem |
| `--blur`, `--pixelate` | `x,y,w,h` — esconde dados sensiveis; calculado a partir da imagem, nao e reversivel |
| `--crop` | `x,y,w,h` |
| `--max-width N` | Reduz a imagem final |

```
CapturaTelaCli.exe annotate --in tela.png --out saida.png --pixelate 40,30,200,24 --color #00AA00 --arrow 300,200,120,60 --text 310,205,Clique aqui
```

## Exemplo de fluxo para um agente
```
CapturaTelaCli.exe capture --window "meu app" --max-width 1280 --out C:\tmp\a.png   # 1. ver a janela
CapturaTelaCli.exe ocr --image C:\tmp\a.png                                         # 2. ler o texto
CapturaTelaCli.exe annotate --in C:\tmp\a.png --out C:\tmp\b.png --pixelate 10,10,300,20   # 3. esconder um e-mail
```

## Privacidade
Capturar a tela revela tudo o que estiver visivel (e-mails, senhas, conversas). Um agente deve capturar **janelas especificas** em vez de `--fullscreen` sempre que possivel, e usar `--blur`/`--pixelate` antes de compartilhar uma imagem.
