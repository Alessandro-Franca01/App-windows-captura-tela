# Servidor MCP

`CapturaTelaCli.exe mcp` e um servidor [MCP](https://modelcontextprotocol.io) por **stdio**: clientes de IA (Claude Code, Claude Desktop e outros) usam a Ferramenta de Captura como ferramentas. Nao precisa instalar nada alem do `.exe`.

> **O usuario decide tudo.** A primeira vez que a IA pede algo, aparece uma janela com *Permitir uma vez / Permitir sempre / Negar / Negar sempre*. Veja [PERMISSOES.md](PERMISSOES.md). Os botoes ficam ativos 1,2 s depois de abrir (para um Enter digitado em outra janela nao aprovar por acidente) e, sem resposta em 60 s, o pedido e negado.

## Instalar no cliente
Claude Code:
```
claude mcp add captura-tela -- "C:\caminho\para\CapturaTelaCli.exe" mcp
```
Claude Desktop e clientes que usam JSON (`claude_desktop_config.json`, `.mcp.json`...):
```json
{
  "mcpServers": {
    "captura-tela": {
      "command": "C:\caminho\para\CapturaTelaCli.exe",
      "args": ["mcp"]
    }
  }
}
```
O servidor precisa rodar na **sessao interativa do usuario** (e o que acontece quando o cliente o inicia), senao a janela de permissao nao aparece.

## Ferramentas
| Ferramenta | O que faz | Permissoes pedidas |
|---|---|---|
| `screen_info` | Monitores, area virtual e cursor | `info` |
| `list_windows` | Janelas visiveis (titulo, programa, retangulo) | `windows` |
| `capture` | Captura janela / regiao / monitor / tela inteira e **devolve a imagem** (reduzida a 1280 px por padrao) | `target:window:<programa>` ou `target:screen`; `file:write` se usar `out` fora da pasta de saida |
| `get_pixel` | Cor de um pixel da tela ou de uma imagem | `target:screen` ou `file:read` |
| `ocr` | Texto de uma imagem ou de uma captura | `action:ocr` + alvo/arquivo |
| `annotate` | Desenha formas/texto/passos, desfoca/pixela (irreversivel) e recorta; devolve a imagem | `file:read` (entrada) e `file:write` (saida) |
| `record_gif` | Grava GIF e devolve o caminho | `action:record` + alvo |
| `scroll_capture` | Pagina inteira por rolagem; devolve a imagem | `action:record` + alvo |

Argumentos de alvo (exatamente um): `window`, `active`, `region` (`"x,y,w,h"`), `screen`, `fullscreen`. Os esquemas completos vem em `tools/list`.

### Resultado de uma chamada
- `content`: a imagem (`type: image`, PNG ou JPEG se for grande, lado maior ate 1568 px) e um texto com o JSON da CLI (caminho do arquivo completo, tamanho etc.).
- `isError: true` quando falhou; se foi permissao negada, o texto traz `"denied": true`.
- O arquivo completo (sem reducao alem de `max_width`) fica em `%TEMP%\CapturaTela` ou no `out` pedido.

## Privacidade
- A IA recebe o que a tela mostra. Para limitar, conceda `target:window:<programa>` so aos programas desejados e **negue** `target:screen`.
- `annotate` com `blur`/`pixelate` permite esconder dados antes de devolver a imagem (a IA tambem pode pedir isso a si mesma, mas so depois de ter visto a captura original: o controle real e a permissao).
- Imagens vistas pela IA seguem para o provedor do modelo, como qualquer entrada dela.

## Protocolo
JSON-RPC 2.0, uma mensagem por linha. Suporta `initialize` (versoes 2025-06-18, 2025-03-26 e 2024-11-05), `ping`, `tools/list` e `tools/call`. Nao ha recursos, prompts, nem notificacoes de progresso/cancelamento (ignoradas). Chamadas sao sequenciais; uma gravacao longa ocupa o servidor ate terminar.
