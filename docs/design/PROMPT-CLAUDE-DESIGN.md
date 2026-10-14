# Prompt para o Claude Design — redesign da Ferramenta de Captura

## Como usar
1. Abra o Claude Design e inicie um projeto novo.
2. **Anexe as imagens** da pasta [screens/](screens/):
   - obrigatórias: `00-visao-geral-claro.png` e `00-visao-geral-escuro.png` (todas as telas em uma imagem cada);
   - opcionais, para detalhe: os prints individuais (`01-…` a `19-…`), principalmente `01`, `02`, `05`, `10`, `12`, `16` e `18`.
3. Cole o texto da seção **Prompt** (tudo dentro do bloco).
4. Traga o resultado de volta (telas, tokens, ícones, especificação): a implementação no código é feita em etapas, em PRs.

Os prints foram feitos com escala de tela de 125% sobre uma **área de trabalho fictícia**: nenhum dado pessoal aparece neles.

## Prompt

```text
Você é um(a) designer de produto sênior, especialista em aplicativos desktop para Windows. Preciso redesenhar a interface gráfica de um aplicativo real, a "Ferramenta de Captura": um clone portátil da Ferramenta de Recorte do Windows. Ele captura a tela (retângulo, forma livre, janela, tela inteira, rolagem automática e manual, gravação em GIF), tem um editor de anotações (caneta, marca-texto, seta, linha, retângulo, elipse, texto, marcador numerado, desfocar, pixelar, cortar, conta-gotas, borracha), faz OCR, vive na bandeja do sistema e pode ser usado por programas de IA, sempre com permissão do usuário.

Anexei prints de TODAS as telas atuais, em tema claro e escuro, na escala de 125%. Os nomes dos arquivos indicam a tela (ex.: "10-permissoes-da-ia-escuro.png").

## Objetivo
Uma interface moderna, coerente e agradável, com cara de aplicativo nativo do Windows 11 (Fluent), SEM mudar funcionalidades, modos nem atalhos. Prioridades, nesta ordem:
1. Clareza e rapidez no fluxo principal: capturar → anotar → salvar/copiar.
2. Consistência entre todas as telas e entre os temas claro e escuro.
3. Acessibilidade.
4. Ser implementável com a tecnologia atual (veja as restrições).

## Restrições técnicas (importante)
- WinForms + GDI+ (.NET Framework 4.x), um único .exe portátil, sem bibliotecas externas. NÃO é WPF, WinUI, XAML nem Web. Desenho personalizado (owner-draw, renderer de ToolStrip, controles próprios) é possível; blur/acrílico, animações complexas e sombras suaves em janelas filhas não são.
- Windows 10 e 11; escalas de 100% a 200%. Fonte do sistema: Segoe UI (Segoe UI Variable não é garantida).
- Ícones: vetoriais simples de traço, que eu consiga desenhar com GDI+ ou embutir como PNG em 1x/1,25x/1,5x/2x. Sem fontes de ícones externas (Segoe Fluent Icons só existe no Windows 11: pode ser sugerida, com alternativa).
- Textos em português do Brasil COM ACENTOS (hoje o app está sem acentos por uma limitação do código, que vou corrigir): escreva os textos corretos.
- Temas: claro, escuro e "seguir o Windows". A janela principal precisa funcionar a partir de 700×400 px.
- Caixas do sistema (Salvar como, Escolher cor, Escolher pasta) não são redesenháveis.

## Telas anexadas (com o estado de cada print)
01 Janela principal vazia (estado inicial) — claro e escuro.
02 Janela principal com uma captura aberta no editor, com anotações (retângulo, seta, texto, passos 1 e 2, área pixelada) — claro e escuro.
03 Menu do ícone da bandeja — claro e escuro.
04 a 08 Tela de seleção de área (tela inteira congelada e escurecida), só em um tema: 04 início (barra de modos, dica, lupa), 05 arrastando um retângulo (tamanho e lupa com coordenadas e cor), 06 modo Janela (janela destacada), 07 modo Forma livre, 08 modo Rolagem manual (dica).
09 Configurações — claro e escuro.
10 Permissões da IA (lista de permissões salvas, chaves gerais) e 11 Adicionar permissão — claro e escuro.
12 Pedido de permissão (a IA pede algo; Permitir uma vez / Permitir sempre / Negar / Negar sempre, com contagem regressiva de 60 s) — claro e escuro.
13 Ajuda (assuntos à esquerda, texto à direita) — claro e escuro.
14 Caixa de texto da anotação e 15 Resultado do OCR — claro e escuro.
16 e 17 Gravador de GIF (painel pequeno + moldura vermelha ao redor da área): pronto para gravar e gravando — claro e escuro.
18 e 19 Painel da rolagem manual: normal e "perdi o encaixe" — claro e escuro.
Sem print (descreva você): a notificação do Windows exibida depois de capturar/gravar (texto curto; clicar abre o arquivo) e o ícone do aplicativo (visor azul com ponto central).

## Diagnóstico do que está ruim hoje
Janela principal
- Duas barras só de texto, sem ícones nem agrupamento; "+ Novo" quase não se destaca; a ferramenta selecionada tem só um contorno fino; botões desativados parecem texto comum.
- Modo e atraso são listas suspensas nativas pequenas; no tema escuro ficam com caixa branca (inconsistente). "Ajuda" e "Configurações" no canto não têm destaque.
- Cor atual é só "■ Cor"; a espessura é uma lista sem pré-visualização. Zoom (−, +, 100%, Ajustar) fica no fim da segunda barra e some em janela estreita.
- Estado vazio: texto cinza sobre fundo escuro, sem chamada visual para a ação principal. Barra de status é um texto corrido.
Seleção de área
- Barra de modos com 7 botões só de texto + "X"; "Rolagem manual" quebra em duas linhas; larguras irregulares; não mostra os atalhos 1–7.
- Dica, rótulo de tamanho e lupa são caixas pretas pesadas; a lupa mistura imagem, coordenadas e cor sem hierarquia.
Menu da bandeja: não segue o tema escuro, não tem ícones nem atalhos.
Configurações: lista plana de campos, sem grupos nem títulos; o atalho aparece como texto selecionado em azul (parece erro); "Permissões da IA…" fica misturado com OK/Cancelar; controles nativos antigos.
Permissões da IA: Permitido/Negado só por cor verde/vermelha (ruim para daltonismo); a coluna "Chave" expõe um nome técnico; botões com textos longos; nota de rodapé com baixo contraste.
Pedido de permissão: quatro botões com o mesmo peso (uma decisão de risco sem hierarquia: "Permitir sempre" parece igual a "Negar"); não mostra visualmente de qual programa/janela se trata nem o nível de risco; contagem regressiva só em texto.
Ajuda: texto puro, sem títulos nem destaques.
Gravador de GIF e rolagem manual: painéis minúsculos com barra de título de janela de ferramenta; no gravador os botões ficam cortados na base e o tema escuro não é aplicado; sem indicador "REC"/tempo (gravador) nem indicador visual de progresso (rolagem).
Em geral: sem ícones em lugar nenhum; sem acentos; hierarquia tipográfica quase inexistente; espaçamentos inconsistentes entre as telas.

## O que preciso que você entregue
1. DIREÇÃO VISUAL. Duas propostas para a janela principal + barra de seleção: A) Fluent nativo, claro primeiro; B) utilitário compacto, escuro primeiro. Recomende uma, justificando. Depois, desenvolva TODAS as telas na direção escolhida (ou na que eu indicar).
2. DESIGN SYSTEM. Tokens para claro e escuro (hex): fundo, superfície, superfície elevada, borda, texto, texto secundário, destaque, perigo, sucesso, aviso, foco, seleção, além dos estados; escala tipográfica (px a 100%): título, subtítulo, corpo, legenda, rótulo de botão; espaçamento em grade de 4 px; raios; elevação. Componentes com estados (padrão, hover, pressionado, foco, desativado, selecionado): botão primário, secundário, sutil e de perigo; botão de ícone; botão de alternância/ferramenta; caixa de seleção; opção; lista suspensa; campo de texto; lista; cartão/grupo; dica (tooltip); barra de status; diálogo; faixa de aviso.
3. ÍCONES. Conjunto coerente, grade de 16/20/24 px, traço de 1,5 px, cantos arredondados, com versão preenchida para "selecionado": novo, retângulo, forma livre, janela, tela inteira, rolagem automática, rolagem manual, gravar GIF, desfazer, refazer, copiar, salvar, copiar texto (OCR), caneta, marca-texto, seta, linha, retângulo, elipse, texto, passo numerado, desfocar, pixelar, cortar, conta-gotas, borracha, cor, zoom −/+/ajustar, ajuda, configurações, IA/permissões, fechar. Entregue em SVG.
4. TELAS REDESENHADAS, uma para cada print (mesmos estados), em claro e escuro, com medidas (px a 100%) e tokens anotados. Inclua também: janela principal com a ferramenta selecionada e com mouse sobre um botão; janela estreita (700 px) mostrando como a barra se comporta; pedido de permissão em três níveis de risco (leitura de informação, captura de janela, captura de tela inteira/arquivos); gravador gravando (tempo e indicador REC); rolagem manual nos três estados (parado, capturando, perdi o encaixe); OCR em andamento e com erro ("sem idioma instalado"); aviso de atalho em uso em Configurações; a notificação do Windows.
5. REVISÃO DE TEXTOS em português do Brasil com acentos: rótulos, dicas, mensagens de status e de erro, em tabela "atual → proposto", mais curtos e consistentes.
6. GUIA DE IMPLEMENTAÇÃO para WinForms: o que é simples (cores, espaçamentos), o que exige desenho personalizado, o que não é viável (e a alternativa); tamanhos em px e como escalar com o DPI; e uma ordem sugerida de etapas, com os "ganhos rápidos" primeiro.

## Regras
- Não invente funções; mantenha modos, atalhos e o fluxo da seleção em tela cheia (imagem congelada + escurecimento + lupa).
- Acessibilidade WCAG AA: contraste 4,5:1 para texto e 3:1 para componentes; nunca só por cor (use ícone/texto para Permitido × Negado); alvos de toque/clique ≥ 32 px nas barras; foco visível e navegação por teclado; mostre o atalho nos botões e nas dicas.
- Se algo for ambíguo, assuma o padrão mais comum do Windows 11 e registre a suposição.
- Entregue como protótipo visual navegável tela a tela, mais um documento de especificação.
```
