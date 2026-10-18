# Créditos das imagens

As imagens dos eventos e dos nichos são **ilustrações próprias do projeto**, no estilo do conceito aprovado
(`design/conceitos/home-mapa.html`): cenas planas, bonequinhos de cabeça redonda e a paleta da UMURA. Elas não usam
foto, banco de imagem nem nenhuma pessoa real, e não precisam de crédito de terceiros.

Arquivos:
- `eventos/<nome>.webp`: 1200×675 (16:9).
- `eventos/<nome>-card.webp`: 640×360.
- `nichos/<nicho>.webp`: 320×320, a capa redonda do nicho.

Onde mexer: `design/ilustracoes/`.
- `gerar.py` tem uma cena por evento e um avatar por nicho.
- `render.mjs` renderiza no Chrome headless.
- `webp.py` grava aqui com os mesmos nomes.

O passo a passo está no `README.md` de lá.

Lugares de Curitiba que aparecem desenhados:
- Ópera de Arame.
- Pedreira Paulo Leminski.
- Museu Oscar Niemeyer (o Olho).
- Estufa do Jardim Botânico.
- Prédio histórico da UFPR na Praça Santos Andrade.
- Petit-pavé da Rua XV.
- Parque Barigui, com as araucárias.
- Largo da Ordem.
- Praça do Japão.
