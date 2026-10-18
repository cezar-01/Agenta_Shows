# Aprovações de design (gate da seção 14 do PLANO_UMURA.md)

| Tela | Conceito | Prints | Status |
|---|---|---|---|
| `/` home com mapa | `conceitos/home-mapa.html` | `prints/home-mapa-390*.png`, `prints/home-mapa-1440*.png` | **Aprovado pelo Gabriel em 18/10/2026** |
| `/eventos/{slug}` evento | `conceitos/evento.html` | `prints/evento-390*.png`, `prints/evento-1440*.png` | **Aprovado pelo Gabriel em 18/10/2026** |

Base de todos os conceitos: `template-conceito-umura.html` (tokens `--um-*` claro/escuro, componentes, motion inline).

## O que o conceito decidiu e a implementação tem que manter

- Coral só onde é "agora": pino selecionado, traço de 3px do card aberto, ponto do chip "Hoje" e do evento acontecendo agora, CTA "Ver ingresso". Nenhum texto em coral.
- Categoria = ícone + texto (pino com ícone da categoria, todos os pinos em Deep Green).
- Mapa em tons calmos: no app, tile "smooth/dataviz" ou filtro CSS no `.leaflet-tile-pane` (o tile OSM padrão briga com o pino).
- Badges de status com tinta escura própria (`--um-*-ink`): o tom cheio da seção 2 reprova como texto 12px sobre o fundo 12%.
- Mobile: barra de ação do evento `position: sticky; bottom: 0` no fim da página; home com sheet (peek ≈ 304px, abre em 400ms com `cubic-bezier(.32,.72,0,1)`).
- Estados: home carregando (skeleton + pill "Carregando eventos…"), vazio ("Nenhum evento hoje por aqui" → "Ver o fim de semana"), lista aberta; evento cancelado (alerta, badge "Cancelado", data riscada, imagem em cinza, CTA vira "Ver outros eventos hoje" em Deep Green).
- Movimento: home = busca → chips → sheet sobe → pinos em stagger (único da tela); evento = hero com blur 6→0 só no título.
- As imagens dos eventos e o mapa são ilustrativos (SVG): no app entram as fotos reais e o Leaflet.
