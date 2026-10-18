#!/usr/bin/env python3
"""Converte os PNGs de png/ (saída do render.mjs) nos .webp do site, com os mesmos nomes que o seed já usa:
eventos/<nome>.webp (1200×675), eventos/<nome>-card.webp (640×360) e nichos/<nicho>.webp (320×320)."""
from pathlib import Path

from PIL import Image

aqui = Path(__file__).resolve().parent
img = aqui.parent.parent / 'src' / 'Agenda_Shows.UI' / 'wwwroot' / 'img'


def salvar(im, destino):
    im.save(destino, 'WEBP', quality=90, method=6)


total = 0
for png in sorted((aqui / 'png' / 'eventos').glob('*.png')):
    im = Image.open(png).convert('RGB')
    salvar(im.resize((1200, 675), Image.LANCZOS), img / 'eventos' / f'{png.stem}.webp')
    salvar(im.resize((640, 360), Image.LANCZOS), img / 'eventos' / f'{png.stem}-card.webp')
    total += 2
for png in sorted((aqui / 'png' / 'nichos').glob('*.png')):
    salvar(Image.open(png).convert('RGB').resize((320, 320), Image.LANCZOS), img / 'nichos' / f'{png.stem}.webp')
    total += 1
print(f'{total} arquivos .webp gravados em {img}')
