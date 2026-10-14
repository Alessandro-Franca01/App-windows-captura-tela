A Ferramenta de Captura é um clone portátil da Ferramenta de Recorte do Windows: captura a tela (retângulo, forma livre, janela, tela inteira, rolagem automática e manual, GIF), anota a imagem, faz OCR, vive na bandeja e pode ser usada por programas de IA, sempre com permissão do usuário. É um único `.exe` em WinForms + GDI+ (.NET Framework 4), com aparência nativa do Windows. Este sistema descreve o que o código tem hoje e as regras do redesenho em andamento.

## Fundamentos de conteúdo
- Escreva em português do Brasil, direto e curto, no imperativo para ações: "Salvar", "Copiar texto", "Permitir uma vez". O código-fonte ainda grava os textos sem acentos ("Configuracoes", "Permissoes da IA") por limitação do compilador; em telas novas e nos mockups use a grafia correta, com acentos.
- Chame o usuário de "você" e o programa de IA pelo nome do cliente, como em "Claude Code (IA) quer:". Sem emoji; o único símbolo decorativo do código é o "●" do botão "● Iniciar".
- Modos de captura têm sempre estes nomes: Retângulo, Forma livre, Janela, Tela inteira, Rolagem automática, Rolagem manual, Gravar GIF. Mostre o atalho (teclas 1 a 7 na seleção; F1 abre a Ajuda; Print Screen e Ctrl+Shift+S capturam).
- Um pedido de permissão diz o que a IA quer fazer e oferece quatro respostas: "Permitir uma vez", "Permitir sempre", "Negar", "Negar sempre". Sem resposta em 60 s, nega.

## Fundamentos visuais
- Temas: claro, escuro e "seguir o Windows" (`Theme.IsDark`). No claro, formulários usam `surface` e `ink`, que são cores do sistema; no escuro, `surface` (#202020) e `ink` (#f0f0f0), e a barra de título é escurecida pelo DWM.
- Campos, listas suspensas, botões e barras no escuro ficam em `surface-raised` com contorno `border-dark`; item sob o mouse usa `hover-dark` (ou `hover-dark-strong` em botão de barra). Não use `border-dark` ou `hover-dark` no tema claro: ali o Windows desenha.
- A cor de destaque é só `accent` (#0078d7). Ferramenta ativa e botão pressionado usam `accent`; botão marcado no escuro usa `accent-checked`; texto sobre ambos é `on-accent`.
- A imagem capturada sempre aparece sobre `canvas-well`, escuro nos dois temas. O texto do estado vazio usa `ink-on-well` e o estilo `placeholder`.
- Na seleção de área, a tela congelada recebe o véu `overlay-dim`; lupa e barra de modos usam `overlay-hud`, dica e rótulo de tamanho usam `overlay-tag`, com texto branco. Moldura de gravação e anotações padrão usam `annotation-red`.
- Texto de apoio usa `ink-muted` com o estilo `body`. Atenção: esse cinza só tem 2,95:1 sobre `surface` claro; no redesenho, escureça-o no tema claro.
- Permitido e Negado hoje distinguem-se por `success` e `danger`. Nunca use só a cor: escreva "Permitido" ou "Negado" e acrescente um ícone. `danger` tem apenas 2,44:1 sobre `surface` escuro.
- Tipografia: Segoe UI em todo o app (Segoe UI Variable não é garantida). Use `body` por padrão, `body-large` em diálogos, `dialog-title` para a frase principal de um diálogo e `help-title` e `reader` na Ajuda. Tamanhos estão em px a 100%; a escala do Windows (100% a 200%) multiplica tudo, e diálogos devem chamar `Theme.ScaleForDpi`.
- Espaçamento: o código usa margens de 10, 12, 16 e 20 px (`inset-10` a `inset-20`), não uma grade de 4 px. Cantos são retos; não há raio nem sombra no código atual.
- Alvos clicáveis nas barras devem ter pelo menos 32 px no redesenho; foco visível e navegação por teclado são obrigatórios.

## Iconografia
O app não tem ícones de interface: barras e botões são só texto. O único ícone é o do aplicativo (`assets/Logos/app-icon.png`, um visor azul com ponto central, extraído de `assets/icon.ico`). Para o redesenho, o plano é um conjunto de traço de 1,5 px em grade de 16/20/24 px, com versão preenchida para "selecionado", desenhável em GDI+ ou embutido como PNG em 1x, 1,25x, 1,5x e 2x. Sem fontes de ícones externas.

## Restrições de implementação
WinForms, GDI+ e um `.exe` sem bibliotecas. Desenho personalizado (owner-draw, renderer de ToolStrip) é possível; acrílico, animações complexas e sombras suaves em janelas filhas não são. Caixas do sistema (Salvar como, Escolher cor, Escolher pasta) não se redesenham. A janela principal precisa funcionar a partir de 700×400 px.

## Telas de referência
`assets/Screens/` traz as visões gerais de todas as telas atuais nos temas claro e escuro (escala de 125%, sobre uma área de trabalho fictícia). O prompt de redesenho completo está em `docs/design/PROMPT-CLAUDE-DESIGN.md` do repositório.

## Não sincronizado
Não há biblioteca de componentes (os controles são nativos do WinForms), então não há `bundle.js`, previews nem guidelines de componentes. Valores vindos de cores do sistema (`SystemColors`) foram gravados com o valor padrão do Windows 10/11. Variáveis ausentes: raios, sombras e bordas do tema claro, que o código não define. Fonte: `Alessandro-Franca01/App-windows-captura-tela`, PR #14 (`14b7bff`), sobre `main@101c050`.
