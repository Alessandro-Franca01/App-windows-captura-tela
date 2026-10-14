# Design da interface

Material para redesenhar a interface gráfica com o Claude Design.

- [PROMPT-CLAUDE-DESIGN.md](PROMPT-CLAUDE-DESIGN.md): o prompt pronto para colar, com instruções de uso.
- [screens/](screens/): prints de todas as telas, em tema claro e escuro, na escala de 125%.
  - `00-visao-geral-claro.png` e `00-visao-geral-escuro.png`: todas as telas em uma imagem (para anexar).
  - `01-…` a `19-…`: um print por tela e estado. Os de seleção de área (`04` a `08`) existem só em um tema.

Os prints são feitos sobre uma **área de trabalho fictícia** e com caminhos de exemplo, então não mostram dados pessoais.

## Refazer os prints
Depois de mudar a interface:
```
gerar-prints.bat
```
O script (`tools/DesignShots.cs`) abre cada tela do aplicativo de verdade, em estados realistas, e salva em `docs/design/screens/`. Precisa de uma sessão de desktop ativa: durante cerca de 1 minuto uma área de trabalho fictícia cobre a tela e janelas do app abrem e fecham sozinhas; não use o computador nesse tempo. Se o app estiver aberto, o atalho dele não é afetado.

As duas imagens de visão geral (`00-…`) são montagens e não são geradas pelo script; para refazê-las, junte os prints de cada tema em uma grade de 3 colunas.
