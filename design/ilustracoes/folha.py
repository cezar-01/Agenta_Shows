#!/usr/bin/env python3
"""Monta uma folha de contato (HTML) com todas as ilustrações, pra revisar de uma vez. Uso: python folha.py <saida.html>"""
import os
import sys
from pathlib import Path

aqui = Path(__file__).resolve().parent
ev = sorted((aqui / 'svg' / 'eventos').glob('*.svg'), key=lambda p: (p.stem.split('-')[0], int(p.stem.split('-')[1])))
ni = sorted((aqui / 'svg' / 'nichos').glob('*.svg'))
cells = ''.join(f'<figure><img src="{p.as_uri()}"><figcaption>{p.stem}</figcaption></figure>' for p in ev)
nic = ''.join(f'<figure class="n"><img src="{p.as_uri()}"><figcaption>{p.stem}</figcaption></figure>' for p in ni)
Path(sys.argv[1]).write_text(f'''<!doctype html><meta charset="utf-8"><style>
body{{margin:0;padding:12px;background:#ddd;font:12px sans-serif}} .g{{display:grid;grid-template-columns:repeat(5,1fr);gap:8px}}
figure{{margin:0}} img{{width:100%;display:block;border-radius:6px}} .n img{{border-radius:50%}}
.ns{{display:grid;grid-template-columns:repeat(9,1fr);gap:8px;margin-top:10px}}
</style><div class="g">{cells}</div><div class="ns">{nic}</div>''', encoding='utf-8')
