# Permissoes da IA

O usuario decide tudo o que uma IA pode fazer com a Ferramenta de Captura. Quando uma IA pede algo que o usuario ainda nao decidiu, aparece uma janela (sempre no topo, com som) com quatro respostas:

| Resposta | Efeito |
|---|---|
| **Permitir uma vez** | Vale so para a chamada em andamento. Nada e salvo |
| **Permitir sempre** | Fica salvo e nao pergunta de novo |
| **Negar** | Nega este pedido (tambem se nao houver resposta em 60 s) |
| **Negar sempre** | Fica salvo: a IA recebe "negado" sem incomodar o usuario |

## O que e controlado
| Chave | Significado |
|---|---|
| `info` | Ver monitores e posicao do cursor |
| `windows` | Listar janelas abertas (titulos e programas) |
| `target:screen` | Capturar tela inteira, monitor ou uma regiao (tambem ler a cor de um pixel da tela) |
| `target:window:<programa>` | Capturar janelas de um programa (ex.: `target:window:code`); `target:window:*` libera todos |
| `action:record` | Gravar GIF e capturar com rolagem (alem da permissao do alvo) |
| `action:ocr` | Ler texto de imagens (alem da permissao do alvo/arquivo) |
| `file:read:<caminho>` | Ler um arquivo; com `\*` no fim, toda a pasta |
| `file:write:<caminho>` | Gravar fora da pasta de saida do app; com `\*`, toda a pasta |

Sempre permitido, sem perguntar: ler e gravar dentro de `%TEMP%\CapturaTela` (onde o app guarda suas proprias capturas).
A janela e verificada **antes** de ser trazida para frente, e a escrita e verificada **antes** de capturar.

## Onde o usuario gerencia
**Configuracoes > Permissoes da IA...** mostra tudo o que foi salvo e permite revogar, adicionar manualmente (permitir ou negar) e usar duas chaves gerais:
- *Permitir que programas de IA (servidor MCP) usem a Ferramenta de Captura* — desligada, nada e permitido, nem as permissoes salvas.
- *Exigir permissao tambem na linha de comando* — por padrao a CLI e do proprio usuario e nao pergunta; ligada, a CLI passa pelas mesmas regras (e pelo mesmo dialogo).

As permissoes ficam em `CapturaTela.permissoes.ini` (ao lado do `.exe`, ou em `%APPDATA%\CapturaTela`). O caminho pode ser trocado pela variavel de ambiente `CAPTURATELA_PERMISSIONS_FILE`. Uma negacao sempre vence uma permissao.

## Limites (leia)
- Se a IA tambem tem acesso a um **terminal**, ela pode chamar `CapturaTelaCli.exe` diretamente. Para que a CLI tambem respeite as permissoes, ligue *Exigir permissao tambem na linha de comando*.
- O arquivo de permissoes e do proprio usuario: um programa (ou uma IA com acesso a arquivos) que o edite contorna o controle. Ele protege contra pedidos indevidos, nao contra codigo malicioso rodando como voce.
- Codigo de saida `3` e `"denied": true` no JSON indicam permissao negada.
