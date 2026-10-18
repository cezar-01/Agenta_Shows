#!/usr/bin/env python3
"""Gera as ilustrações flat dos eventos e nichos de demonstração da UMURA (SVG).

Estilo do conceito aprovado (design/conceitos/home-mapa.html): cenas planas, bonequinhos de cabeça redonda e corpo em
pílula, paleta da marca (Deep Green, Coral, Off White, Worker Green, Premium Grey) e um ou outro tom de apoio. Uma cena
por evento (o lugar de Curitiba entra quando ajuda: Ópera de Arame, Pedreira, MON, estufa do Jardim Botânico, prédio da
UFPR na Santos Andrade, petit-pavé da XV) e um avatar por nicho.

Uso:  python gerar.py            → escreve svg/eventos/*.svg e svg/nichos/*.svg
      depois  node render.mjs     → vira PNG no Chrome headless
      depois  python webp.py      → grava os .webp em src/Agenda_Shows.UI/wwwroot/img (mesmos nomes de antes)
"""
import math
import os
import random

PASTA = os.path.dirname(os.path.abspath(__file__))

# ── paleta ──────────────────────────────────────────────────────────────────────────────────────────────────────────
DG, DG2, DG3, DG4 = '#0B3C3F', '#0F4A4D', '#15575A', '#1E6366'
CO, CO2, CO3 = '#FF5A3C', '#FF8A70', '#FFB4A3'
OF, OF2 = '#EDECE7', '#F6F5F1'
SA, SA2, SA3 = '#E4E2DB', '#CFC9BB', '#BDB6A6'
WG, WG2 = '#6E6F5E', '#A9B3A6'
GR, GR2 = '#282828', '#3F3F3F'
AM, WA = '#F2C27A', '#FFD9A0'
TE, TE2 = '#3FA7A0', '#7CC4BD'
NI, NI2 = '#061315', '#0B1F21'
BL, BL2, BL3 = '#2F6F9F', '#7FB2D9', '#1F4E79'
TC = '#C9785A'
VD, VD2, VD3 = '#6E8B6A', '#9DB39B', '#2F4A3C'
MAD, MAD2 = '#8B6A4E', '#6B5141'


def n(v):
    s = f'{v:.2f}'.rstrip('0').rstrip('.')
    return '0' if s in ('-0', '') else s


def _stops(stops):
    out = []
    for s in stops:
        a = f' stop-opacity="{s[2]}"' if len(s) > 2 else ''
        out.append(f'<stop offset="{s[0]}" stop-color="{s[1]}"{a}/>')
    return ''.join(out)


class Cena:
    def __init__(self, w=160, h=90):
        self.w, self.h = w, h
        self.defs, self.els, self.k = [], [], 0

    def _id(self):
        self.k += 1
        return f'g{self.k}'

    def lin(self, stops, x1=0, y1=0, x2=0, y2=1):
        i = self._id()
        self.defs.append(f'<linearGradient id="{i}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}">{_stops(stops)}</linearGradient>')
        return f'url(#{i})'

    def rad(self, stops, cx=.5, cy=.5, r=.5):
        i = self._id()
        self.defs.append(f'<radialGradient id="{i}" cx="{cx}" cy="{cy}" r="{r}">{_stops(stops)}</radialGradient>')
        return f'url(#{i})'

    def __iadd__(self, el):
        self.els.append(el)
        return self

    def svg(self, pw, ph):
        return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.w} {self.h}" width="{pw}" height="{ph}" '
                f'preserveAspectRatio="xMidYMid slice"><defs>{"".join(self.defs)}</defs>{"".join(self.els)}</svg>')


# ── primitivas ──────────────────────────────────────────────────────────────────────────────────────────────────────
def _op(op):
    return f' opacity="{n(op)}"' if op is not None and op != 1 else ''


def R(x, y, w, h, fill, rx=0, op=None, tr=None):
    r = f' rx="{n(rx)}"' if rx else ''
    t = f' transform="{tr}"' if tr else ''
    return f'<rect x="{n(x)}" y="{n(y)}" width="{n(w)}" height="{n(h)}"{r} fill="{fill}"{_op(op)}{t}/>'


def C(cx, cy, r, fill, op=None):
    return f'<circle cx="{n(cx)}" cy="{n(cy)}" r="{n(r)}" fill="{fill}"{_op(op)}/>'


def E(cx, cy, rx, ry, fill, op=None, tr=None, stroke=None, sw=None):
    t = f' transform="{tr}"' if tr else ''
    s = f' stroke="{stroke}" stroke-width="{n(sw)}"' if stroke else ''
    return f'<ellipse cx="{n(cx)}" cy="{n(cy)}" rx="{n(rx)}" ry="{n(ry)}" fill="{fill}"{s}{_op(op)}{t}/>'


def P(d, fill='none', stroke=None, sw=None, op=None, cap='round', tr=None):
    s = f' stroke="{stroke}" stroke-width="{n(sw)}" stroke-linecap="{cap}" stroke-linejoin="round"' if stroke else ''
    t = f' transform="{tr}"' if tr else ''
    return f'<path d="{d}" fill="{fill}"{s}{_op(op)}{t}/>'


def L(x1, y1, x2, y2, cor, sw, op=None, cap='round'):
    return P(f'M{n(x1)} {n(y1)}L{n(x2)} {n(y2)}', stroke=cor, sw=sw, op=op, cap=cap)


def G(els, op=None, tr=None):
    t = f' transform="{tr}"' if tr else ''
    return f'<g{_op(op)}{t}>{"".join(els)}</g>'


def ceu(c, topo, base, meio=None):
    stops = [(0, topo), (1, base)] if meio is None else [(0, topo), (.55, meio), (1, base)]
    return R(0, 0, c.w, c.h, c.lin(stops))


def brilho(c, cx, cy, rx, ry, cor, op):
    return E(cx, cy, rx, ry, c.rad([(0, cor, op), (1, cor, 0)]))


def cone(c, xt, yt, xb1, xb2, yb, cor, op=.3, topo=1.4):
    f = c.lin([(0, cor, op), (1, cor, 0)])
    return P(f'M{n(xt - topo)} {n(yt)}L{n(xt + topo)} {n(yt)}L{n(xb2)} {n(yb)}L{n(xb1)} {n(yb)}z', f)


def facho(c, xb, yb, x1, x2, yt, cor, op=.14):
    """Facho de projetor de baixo pra cima."""
    f = c.lin([(0, cor, 0), (1, cor, op)])
    return P(f'M{n(x1)} {n(yt)}L{n(x2)} {n(yt)}L{n(xb + 1.5)} {n(yb)}L{n(xb - 1.5)} {n(yb)}z', f)


def estrelas(rng, qtd, y0, y1, cor='#FFFFFF', op=.7, w=160):
    return ''.join(C(rng.uniform(0, w), rng.uniform(y0, y1), rng.uniform(.25, .6), cor, op * rng.uniform(.4, 1))
                   for _ in range(qtd))


def faisca(x, y, s, cor, op=None):
    return P(f'M{n(x)} {n(y - s)}Q{n(x)} {n(y)} {n(x + s)} {n(y)}Q{n(x)} {n(y)} {n(x)} {n(y + s)}'
             f'Q{n(x)} {n(y)} {n(x - s)} {n(y)}Q{n(x)} {n(y)} {n(x)} {n(y - s)}z', cor, op=op)


def _bez(x0, y0, cx, cy, x1, y1, t):
    return ((1 - t) ** 2 * x0 + 2 * (1 - t) * t * cx + t * t * x1, (1 - t) ** 2 * y0 + 2 * (1 - t) * t * cy + t * t * y1)


def luzinhas(x0, y0, x1, y1, queda, qtd, fio, luz, r=1.0, halo=True):
    cx, cy = (x0 + x1) / 2, max(y0, y1) + queda
    out = [P(f'M{n(x0)} {n(y0)}Q{n(cx)} {n(cy)} {n(x1)} {n(y1)}', stroke=fio, sw=.4)]
    for i in range(qtd):
        px, py = _bez(x0, y0, cx, cy, x1, y1, (i + .5) / qtd)
        if halo:
            out.append(C(px, py + r, r * 2.8, luz, .16))
        out.append(C(px, py + r, r, luz))
    return ''.join(out)


def bandeirinhas(x0, y0, x1, y1, queda, qtd, cores, fio=WG, tam=3.2):
    cx, cy = (x0 + x1) / 2, max(y0, y1) + queda
    out = [P(f'M{n(x0)} {n(y0)}Q{n(cx)} {n(cy)} {n(x1)} {n(y1)}', stroke=fio, sw=.35)]
    for i in range(qtd):
        px, py = _bez(x0, y0, cx, cy, x1, y1, (i + .5) / qtd)
        out.append(P(f'M{n(px - tam / 2)} {n(py)}L{n(px + tam / 2)} {n(py)}L{n(px)} {n(py + tam * 1.25)}z', cores[i % len(cores)]))
    return ''.join(out)


def confete(rng, qtd, x0, x1, y0, y1, cores):
    out = []
    for _ in range(qtd):
        x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
        w = rng.uniform(.8, 1.9)
        out.append(R(x, y, w, w * .5, rng.choice(cores), tr=f'rotate({n(rng.uniform(0, 180))} {n(x)} {n(y)})'))
    return ''.join(out)


def tijolos(cor, op, y0=0, y1=60, w=160):
    out = []
    for row, y in enumerate(range(y0, y1, 4)):
        off = 0 if row % 2 == 0 else 4
        for x in range(-8 + off, w + 8, 8):
            out.append(R(x + .3, y + .3, 7.4, 3.4, cor))
    return G(out, op=op)


# ── bonequinhos ─────────────────────────────────────────────────────────────────────────────────────────────────────
def pessoa(x, base, h, cor, pose='pe', lado=1, roupa=None, cabelo=None):
    """Bonequinho de pé: cabeça redonda + corpo em pílula.
    pose: pe, bracos (os dois pra cima), braco (um pra cima), aponta, mic (microfone na boca), cintura, mesa (mãos à frente)."""
    corpo = roupa or cor
    r = h * .12
    cy = base - h + r
    topo = cy + r + h * .04
    w = h * .32
    sw = w * .32
    oy = topo + w * .38
    out = []

    def braco(dx, ex, ey, mx=None, my=None):
        if mx is None:
            out.append(L(x + dx, oy, ex, ey, corpo, sw))
        else:
            out.append(P(f'M{n(x + dx)} {n(oy)}L{n(mx)} {n(my)}L{n(ex)} {n(ey)}', stroke=corpo, sw=sw))

    if pose == 'bracos':
        braco(-w * .3, x - w * .95, topo - h * .22)
        braco(w * .3, x + w * .95, topo - h * .22)
    elif pose == 'braco':
        braco(lado * w * .3, x + lado * w * .9, topo - h * .24)
    elif pose == 'aponta':
        braco(lado * w * .3, x + lado * w * 1.75, oy - h * .07)
    elif pose == 'mic':
        ex, ey = x + lado * r * 1.15, cy + r * .55
        braco(lado * w * .3, ex, ey, x + lado * w * .8, oy + h * .17)
        out.append(R(ex - r * .2, ey - r * .1, r * .4, r * .9, '#151515' if corpo != '#151515' else '#444', rx=r * .2))
        out.append(C(ex, ey - r * .1, r * .36, '#2b2b2b'))
    elif pose == 'cintura':
        for s in (1, -1):
            braco(s * w * .3, x + s * w * .42, oy + h * .22, x + s * w * .95, oy + h * .11)
    elif pose == 'mesa':
        for s in (1, -1):
            braco(s * w * .3, x + s * w * .35, oy + h * .34, x + s * w * .8, oy + h * .18)
    out.append(P(f'M{n(x - w / 2)} {n(base)}V{n(topo + w / 2)}a{n(w / 2)} {n(w / 2)} 0 0 1 {n(w)} 0V{n(base)}z', corpo))
    if cabelo:
        out.append(C(x - lado * r * .1, cy - r * .12, r * 1.25, cabelo))
    out.append(C(x, cy, r, cor))
    return ''.join(out)


def mao(x, base, h, pose, lado=1):
    """Onde fica a mão da pose (pra pôr um objeto nela)."""
    r = h * .12
    cy = base - h + r
    topo = cy + r + h * .04
    w = h * .32
    oy = topo + w * .38
    if pose == 'braco':
        return x + lado * w * .9, topo - h * .24
    if pose == 'aponta':
        return x + lado * w * 1.75, oy - h * .07
    if pose == 'mesa':
        return x + lado * w * .35, oy + h * .34
    return x, oy


def busto(x, base, r, cor, bracos=0, lado=1):
    """Cabeça + ombros (a plateia do conceito). bracos: 0, 1 ou 2 braços pra cima."""
    ts = base - r * 2.4
    cy = ts - r * .8
    out = []
    if bracos >= 1:
        out.append(L(x + lado * r * 1.2, ts + r * .5, x + lado * r * 2.0, ts - r * 3.0, cor, r * .72))
    if bracos >= 2:
        out.append(L(x - lado * r * 1.2, ts + r * .5, x - lado * r * 2.0, ts - r * 3.0, cor, r * .72))
    out.append(P(f'M{n(x - 2 * r)} {n(base)}c0 {n(-1.8 * r)} {n(.8 * r)} {n(-2.4 * r)} {n(2 * r)} {n(-2.4 * r)}'
                 f's{n(2 * r)} {n(.6 * r)} {n(2 * r)} {n(2.4 * r)}z', cor))
    out.append(C(x, cy, r, cor))
    return ''.join(out)


def plateia(rng, base, x0, x1, r, cor, bracos=0.0, passo=None, dy=.5):
    passo = passo or r * 4.1
    out = []
    x = x0 + rng.uniform(0, passo * .5)
    while x < x1:
        b = 0
        if rng.random() < bracos:
            b = 2 if rng.random() < .45 else 1
        out.append(busto(x, base + rng.uniform(-dy, dy) * r, r * rng.uniform(.9, 1.1), cor, b, rng.choice([-1, 1])))
        x += passo * rng.uniform(.85, 1.15)
    return ''.join(out)


def sentado(x, base, h, cor, lado=1, roupa=None, braco=None):
    """Bonequinho sentado: tronco, coxa pra frente e canela até o chão."""
    corpo = roupa or cor
    r = h * .16
    assento = base - h * .3
    cy = base - h + r
    topo = cy + r + h * .04
    w = h * .36
    out = [L(x, assento, x + lado * h * .38, assento, corpo, w * .85),
           L(x + lado * h * .38, assento, x + lado * h * .41, base - w * .22, corpo, w * .6)]
    if braco == 'cima':
        out.append(L(x + lado * w * .2, topo + w * .4, x + lado * w * .9, topo - h * .22, corpo, w * .3))
    elif braco == 'frente':
        out.append(L(x + lado * w * .2, topo + w * .45, x + lado * h * .34, topo + h * .2, corpo, w * .3))
    out.append(P(f'M{n(x - w / 2)} {n(assento + w * .3)}V{n(topo + w / 2)}a{n(w / 2)} {n(w / 2)} 0 0 1 {n(w)} 0'
                 f'V{n(assento + w * .3)}z', corpo))
    out.append(C(x, cy, r, cor))
    return ''.join(out)


def deitado(x, y, comp, cor, roupa=None, lado=1):
    """Deitado de lado na grama (cinema ao ar livre): cabeça apoiada e corpo esticado."""
    corpo = roupa or cor
    r = comp * .1
    return (L(x, y, x + lado * comp * .85, y + r * .4, corpo, r * 1.6)
            + L(x + lado * comp * .1, y - r * .2, x - lado * comp * .02, y - r * 1.6, corpo, r * .8)
            + C(x - lado * comp * .05, y - r * 2.4, r, cor))


def breaker(x, chao, h, cor, roupa=None, tenis=CO):
    """B-boy de cabeça pra baixo num freeze: mãos no chão, pernas abertas pro alto."""
    corpo = roupa or cor
    r = h * .12
    hx, hy = x + h * .08, chao - h * .5
    out = [L(x, chao - r * 2, x - h * .26, chao, corpo, h * .1), L(x, chao - r * 2, x + h * .24, chao, corpo, h * .1),
           L(x, chao - r * 2.1, hx, hy, corpo, h * .28),
           L(hx, hy, x - h * .26, chao - h * 1.05, corpo, h * .13), L(hx, hy, x + h * .4, chao - h * .92, corpo, h * .13),
           C(x - h * .27, chao - h * 1.08, h * .07, tenis), C(x + h * .42, chao - h * .95, h * .07, tenis),
           C(x - h * .02, chao - r * 1.05, r, cor)]
    return ''.join(out)


# ── objetos ─────────────────────────────────────────────────────────────────────────────────────────────────────────
def violao(x, y, s, cor, ang=-30, boca=None):
    els = [E(x - s * .15, y, s * .55, s * .45, cor), E(x + s * .45, y, s * .42, s * .36, cor),
           R(x + s * .7, y - s * .09, s * 1.5, s * .18, cor), R(x + s * 2.1, y - s * .14, s * .32, s * .28, cor)]
    if boca:
        els.append(C(x + s * .2, y, s * .14, boca))
    return G(els, tr=f'rotate({ang} {n(x)} {n(y)})')


def pedestal(x, base, h, cor, sw=.5):
    return L(x, base, x, base - h, cor, sw) + L(x - 2.2, base, x + 2.2, base, cor, sw) + C(x, base - h, sw * 1.8, cor)


def arvore(x, base, h, cor, tronco=None):
    t = tronco or cor
    r = h * .36
    return (R(x - h * .04, base - h * .5, h * .08, h * .5, t) + C(x, base - h + r, r, cor)
            + C(x - r * .65, base - h + r * 1.55, r * .72, cor) + C(x + r * .65, base - h + r * 1.5, r * .76, cor))


def araucaria(x, base, h, cor):
    """A árvore de Curitiba: tronco fino e copa em taça."""
    out = [R(x - h * .018, base - h * .92, h * .036, h * .92, cor)]
    for yy, ww in ((0.0, .3), (.13, .4), (.26, .46)):
        y0 = base - h + h * yy
        wp = h * ww
        out.append(P(f'M{n(x - wp)} {n(y0 + h * .06)}Q{n(x)} {n(y0 - h * .07)} {n(x + wp)} {n(y0 + h * .06)}'
                     f'Q{n(x)} {n(y0 + h * .02)} {n(x - wp)} {n(y0 + h * .06)}z', cor))
    return ''.join(out)


def moitas(rng, base, x0, x1, r, cor):
    out, x = [], x0
    while x < x1:
        rr = r * rng.uniform(.7, 1.3)
        out.append(C(x, base - rr * .4, rr, cor))
        x += rr * 1.3
    return ''.join(out)


def predios(rng, base, x0, x1, hmin, hmax, cor, janela=None, prob=.25, wmin=8, wmax=18):
    out, x = [], x0
    while x < x1:
        w, h = rng.uniform(wmin, wmax), rng.uniform(hmin, hmax)
        out.append(R(x, base - h, w + .3, h + 1, cor))
        if rng.random() < .3:
            out.append(R(x + w * .35, base - h - 3, w * .3, 3.2, cor))
        if janela:
            yy = base - h + 3
            while yy < base - 3:
                xx = x + 1.8
                while xx < x + w - 2:
                    if rng.random() < prob:
                        out.append(R(xx, yy, 1.4, 1.9, janela, op=.9))
                    xx += 3.2
                yy += 4
        x += w + rng.uniform(-.5, 1.5)
    return ''.join(out)


def food_truck(x, base, w, h, cor, toldo1, toldo2, janela=OF2, escuro=NI2):
    out = [R(x, base - h - 2, w, h, cor, rx=2.2),
           R(x + w - 2, base - h * .62 - 2, w * .26, h * .62, cor, rx=2),
           R(x + w + w * .03, base - h * .55, w * .14, h * .22, janela, rx=.8, op=.85),
           R(x + w * .12, base - h * .8 - 2, w * .64, h * .4, janela)]
    k = 7
    lw = w * .72 / k
    for i in range(k):
        cor_l = toldo1 if i % 2 == 0 else toldo2
        out.append(R(x + w * .08 + i * lw, base - h * .96 - 2, lw + .05, h * .16, cor_l))
        out.append(P(f'M{n(x + w * .08 + i * lw)} {n(base - h * .8 - 2)}q{n(lw / 2)} {n(1.6)} {n(lw)} 0z', cor_l))
    out.append(R(x + w * .1, base - h * .42 - 2, w * .68, 1.3, escuro, op=.55))
    for wx in (x + w * .22, x + w * 1.02):
        out.append(C(wx, base - 1.6, 2.7, escuro))
        out.append(C(wx, base - 1.6, 1, '#8C8F7E'))
    return ''.join(out)


def barraca(x, base, w, h, c1, c2, mesa, k=6, poste=GR2):
    out = [L(x + .6, base, x + .6, base - h, poste, .8), L(x + w - .6, base, x + w - .6, base - h, poste, .8)]
    lw = w / k
    for i in range(k):
        cor = c1 if i % 2 == 0 else c2
        out.append(R(x + i * lw, base - h - 4, lw + .05, 4, cor))
        out.append(P(f'M{n(x + i * lw)} {n(base - h)}q{n(lw / 2)} {n(2.2)} {n(lw)} 0z', cor))
    out.append(R(x - .5, base - h * .42, w + 1, h * .42, mesa, rx=.6))
    return ''.join(out)


def tenda(x, base, w, h, cor, sombra, mesa='#C9B79C'):
    return (L(x + .8, base, x + .8, base - h, GR2, .7) + L(x + w - .8, base, x + w - .8, base - h, GR2, .7)
            + P(f'M{n(x - 1)} {n(base - h)}L{n(x + w / 2)} {n(base - h - w * .3)}L{n(x + w + 1)} {n(base - h)}z', cor)
            + R(x - 1, base - h, w + 2, 2.4, sombra) + R(x + 1, base - h * .4, w - 2, h * .4, mesa, rx=.5))


def vaso(x, base, s, cor):
    return P(f'M{n(x - s * .28)} {n(base)}Q{n(x - s * .62)} {n(base - s * .5)} {n(x - s * .2)} {n(base - s * .88)}'
             f'L{n(x - s * .26)} {n(base - s)}H{n(x + s * .26)}L{n(x + s * .2)} {n(base - s * .88)}'
             f'Q{n(x + s * .62)} {n(base - s * .5)} {n(x + s * .28)} {n(base)}z', cor)


def tigela(x, base, s, cor):
    return P(f'M{n(x - s)} {n(base - s * .7)}Q{n(x - s * .9)} {n(base)} {n(x)} {n(base)}Q{n(x + s * .9)} {n(base)} '
             f'{n(x + s)} {n(base - s * .7)}z', cor)


def quadro(x, y, w, h, conteudo, moldura='#2A2A2A', fundo='#F4F2EC', esp=1.2):
    return R(x, y, w, h, moldura) + R(x + esp, y + esp, w - 2 * esp, h - 2 * esp, fundo) + conteudo


def janela_arco(x, y, w, h, cor, op=None):
    return P(f'M{n(x)} {n(y + h)}V{n(y + w / 2)}a{n(w / 2)} {n(w / 2)} 0 0 1 {n(w)} 0V{n(y + h)}z', cor, op=op)


def estante(rng, x, y, w, h, madeira=MAD, cores=(CO, AM, TE, DG3, WG, OF2, CO3, BL)):
    out = [R(x, y, w, h, madeira)]
    prat = 5
    ph = (h - 2) / prat
    for i in range(prat):
        yy = y + 1 + i * ph
        out.append(R(x + 1, yy, w - 2, ph - 1.2, '#4A3829'))
        xx = x + 1.4
        while xx < x + w - 2.4:
            bw = rng.uniform(1.1, 2.2)
            bh = (ph - 1.6) * rng.uniform(.7, .98)
            if rng.random() < .1:
                xx += bw
                continue
            out.append(R(xx, yy + ph - 1.2 - bh, bw, bh, rng.choice(cores)))
            xx += bw + .25
        out.append(R(x, yy + ph - 1.2, w, 1.2, madeira))
    return ''.join(out)


def ufpr(x, base, s, cor=OF2, sombra='#D8D0BE'):
    """O prédio histórico da UFPR na Praça Santos Andrade: colunata, frontão e cúpula."""
    out = [R(x + s * .3, base - s * .56, s * .4, s * .1, cor),
           P(f'M{n(x + s * .33)} {n(base - s * .56)}Q{n(x + s / 2)} {n(base - s * .8)} {n(x + s * .67)} {n(base - s * .56)}z', '#D9CDB4'),
           R(x + s * .49, base - s * .84, s * .02, s * .06, '#B5A98F'),
           R(x, base - s * .38, s, s * .38, cor),
           P(f'M{n(x + s * .2)} {n(base - s * .38)}L{n(x + s / 2)} {n(base - s * .5)}L{n(x + s * .8)} {n(base - s * .38)}z', cor),
           P(f'M{n(x + s * .26)} {n(base - s * .395)}L{n(x + s / 2)} {n(base - s * .47)}L{n(x + s * .74)} {n(base - s * .395)}z', 'none', stroke=sombra, sw=s * .008),
           R(x - s * .04, base - s * .07, s * 1.08, s * .04, sombra), R(x - s * .08, base - s * .035, s * 1.16, s * .04, SA2)]
    for i in range(10):
        cx = x + s * .07 + i * s * .095
        out.append(R(cx, base - s * .33, s * .035, s * .26, '#FFFFFF'))
        out.append(R(cx + s * .035, base - s * .33, s * .012, s * .26, sombra))
    out.append(R(x, base - s * .38, s, s * .04, sombra))
    return ''.join(out)


def petit_pave(c, y0, y1, claro=OF2, escuro=GR2):
    """O petit-pavé em ondas da Rua XV."""
    out = [R(0, y0, c.w, y1 - y0, claro)]
    y, esp = y0 + 1.5, 2.6
    while y < y1 + 4:
        a = esp * .7
        d = f'M-10 {n(y)}' + ''.join(f'q{n(10)} {n(-a)} {n(20)} 0t{n(20)} 0' for _ in range(5))
        out.append(P(d, stroke=escuro, sw=esp * .42))
        y += esp * 1.25
        esp *= 1.16
    return ''.join(out)


def balao_fala(x, y, w, h, cor, lado=1, tinta=DG):
    tx = x + w * (.25 if lado < 0 else .75)
    out = [R(x, y, w, h, cor, rx=h / 2),
           P(f'M{n(tx - 1.6)} {n(y + h - .2)}L{n(tx + lado * 2.8)} {n(y + h + 3)}L{n(tx + 1.6)} {n(y + h - .2)}z', cor)]
    for i in (-1, 0, 1):
        out.append(C(x + w / 2 + i * w * .2, y + h / 2, h * .1, tinta))
    return ''.join(out)


def interrogacao(x, y, s, cor):
    return (P(f'M{n(x - s * .45)} {n(y - s * .35)}q0 {n(-s * .65)} {n(s * .45)} {n(-s * .65)}q{n(s * .45)} 0 {n(s * .45)} {n(s * .5)}'
              f'q0 {n(s * .3)} {n(-s * .45)} {n(s * .55)}v{n(s * .2)}', stroke=cor, sw=s * .22)
            + C(x, y + s * .75, s * .13, cor))


def bola_espelhada(x, y, r):
    out = [C(x, y, r, '#D9DCD6')]
    for dy in (-.6, -.2, .2, .6):
        half = r * math.sqrt(1 - dy * dy)
        out.append(L(x - half, y + dy * r, x + half, y + dy * r, '#9AA3A0', .25))
    for dx in (-.5, 0, .5):
        out.append(E(x + dx * r * .3, y, r * abs(1 - abs(dx)) * .9 + .2, r * .98, 'none', stroke='#9AA3A0', sw=.25))
    out.append(C(x - r * .35, y - r * .35, r * .25, '#FFFFFF', .9))
    return ''.join(out)


def cadeira_praia(x, base, cor1, cor2):
    out = [L(x - 5, base, x + 3, base - 9, GR2, .6), L(x + 5, base, x - 2, base - 6, GR2, .6)]
    for i in range(4):
        out.append(R(x - 4.6 + i * 2.3, base - 10.5, 2.3, 8, cor1 if i % 2 == 0 else cor2))
    return ''.join(out)


def pipoca(x, base, s=1.0):
    out = [P(f'M{n(x - 3 * s)} {n(base - 7 * s)}L{n(x + 3 * s)} {n(base - 7 * s)}L{n(x + 2.3 * s)} {n(base)}L{n(x - 2.3 * s)} {n(base)}z', OF2)]
    for i in (-1, 1):
        out.append(P(f'M{n(x + i * 1.1 * s - .5 * s)} {n(base - 7 * s)}h{n(s)}l{n(-.2 * s)} {n(7 * s)}h{n(-.6 * s)}z', CO))
    for dx, dy in ((-2, -7.6), (-.6, -8.4), (1, -7.8), (2.2, -7.2), (.2, -7)):
        out.append(C(x + dx * s, base + dy * s, 1.05 * s, '#FFF6E2'))
    return ''.join(out)


def tela_animacao(c, x, y, w, h):
    """Desenho animado no telão: céu, morro, sol e um bichinho redondo sorrindo."""
    return (R(x, y, w, h, '#9ED3CC') + C(x + w * .78, y + h * .26, h * .14, AM)
            + P(f'M{n(x)} {n(y + h * .75)}Q{n(x + w * .3)} {n(y + h * .45)} {n(x + w * .6)} {n(y + h * .72)}T{n(x + w)} {n(y + h * .6)}'
                f'V{n(y + h)}H{n(x)}z', DG3)
            + C(x + w * .32, y + h * .62, h * .2, CO) + C(x + w * .28, y + h * .58, h * .035, NI) + C(x + w * .36, y + h * .58, h * .035, NI)
            + P(f'M{n(x + w * .27)} {n(y + h * .66)}q{n(w * .05)} {n(h * .07)} {n(w * .1)} 0', stroke=NI, sw=h * .025))


def gorro(x, base, h, cor):
    r = h * .12
    cy = base - h + r
    return P(f'M{n(x - r * 1.05)} {n(cy - r * .1)}a{n(r * 1.05)} {n(r * 1.05)} 0 0 1 {n(r * 2.1)} 0z', cor)


# ── cenas dos eventos ───────────────────────────────────────────────────────────────────────────────────────────────
def shows_1(c, rng):  # Noite de Pop Rock na Ópera de Arame
    c += ceu(c, '#082527', DG3, DG2)
    c += estrelas(rng, 34, 2, 40)
    c += brilho(c, 80, 48, 46, 26, AM, .5)
    c += R(0, 58, 160, 14, '#0A3133')
    c += G([L(18, 62, 46, 62, AM, .5), L(64, 65, 96, 65, CO2, .5), L(110, 62, 140, 62, AM, .5),
            L(38, 68, 70, 68, AM, .4), L(92, 69, 126, 69, CO2, .4)], op=.5)
    c += cone(c, 80, 24, 62, 98, 58, CO, .34)
    for x, pose in ((70, 'braco'), (80, 'mic'), (90, 'pe')):
        c += pessoa(x, 58, 10, NI, pose, lado=-1 if x == 80 else 1)
    c += G([P(f'M{n(80 - wd)} 58Q80 {n(58 - ht)} {n(80 + wd)} 58', stroke=OF, sw=.85) for wd, ht in ((36, 74), (28, 56), (20, 38))], op=.9)
    c += G([L(80, 21, 80, 58, OF, .6), L(68, 30, 68, 58, OF, .5), L(92, 30, 92, 58, OF, .5),
            L(58, 42, 58, 58, OF, .5), L(102, 42, 102, 58, OF, .5)], op=.55)
    c += L(42, 58, 118, 58, OF, 1.2)
    for x, h in ((16, 40), (140, 46), (153, 30)):
        c += araucaria(x, 60, h, '#072325')
    c += R(0, 72, 160, 18, NI2)
    c += plateia(rng, 84, -4, 164, 3.4, '#0B2A2C', .3)
    c += plateia(rng, 93, -6, 166, 4.8, NI, .38)


def shows_2(c, rng):  # Rock autoral no porão do Largo
    c += ceu(c, '#0A1A1B', '#10302F')
    c += tijolos(OF, .035)
    c += R(10, 5, 140, 1.4, '#0F1415')
    for x, cor in ((30, CO), (62, AM), (98, CO), (130, AM)):
        c += cone(c, x, 8, x - 22, x + 22, 62, cor, .3)
        c += R(x - 2, 5.6, 4, 3.6, '#1A1F20', rx=.8) + C(x, 9.3, 1.1, WA)
    c += busto(80, 54, 3.2, NI)
    c += E(70, 44, 4, .7, AM, .85) + L(70, 44, 70, 60, NI, .5) + E(91, 46, 3.6, .7, AM, .85) + L(91, 46, 91, 60, NI, .5)
    c += C(80, 55, 5, NI) + C(80, 55, 4.1, '#123B3C') + C(80, 55, 1.3, CO)
    c += pessoa(28, 61, 25, NI) + violao(27, 47.5, 4, TE, ang=-28, boca=NI)
    c += pessoa(52, 61, 27, NI) + violao(51, 47, 4.2, CO, ang=-24, boca=NI)
    c += pessoa(112, 61, 27, NI, 'mic', lado=-1) + pedestal(106, 61, 18, NI)
    c += R(0, 60, 160, 4, '#050B0C')
    c += plateia(rng, 80, -4, 164, 3.6, '#0A2122', .35)
    c += plateia(rng, 93, -6, 166, 5, NI, .48)


def shows_3(c, rng):  # Festival Pop na Pedreira Paulo Leminski
    c += ceu(c, '#F6D2B0', '#FBEBDD')
    c += C(130, 21, 13, CO2, .22) + C(130, 21, 8.5, CO2, .95)
    c += P('M0 22L14 16L30 20L44 11L60 17V60H0z', '#9A9786') + P('M100 60V15L114 8L128 16L144 11L160 18V60z', '#9A9786')
    c += G([L(x, y, x + 2, 58, '#7E7B6B', .6) for x, y in ((8, 24), (20, 20), (34, 22), (48, 16), (106, 18), (120, 14), (136, 18), (150, 18))], op=.8)
    c += moitas(rng, 21, -2, 60, 3.4, '#5E7B5E') + moitas(rng, 17, 100, 162, 3.4, '#5E7B5E')
    c += R(50, 30, 5, 26, NI2) + R(105, 30, 5, 26, NI2)
    c += R(62, 28, 36, 20, c.lin([(0, CO), (1, AM)]))
    c += P('M54 23L80 13L106 23z', DG) + R(54, 23, 52, 3, DG) + L(57, 26, 57, 56, DG, 1.6) + L(103, 26, 103, 56, DG, 1.6)
    for x, pose in ((72, 'braco'), (80, 'mic'), (88, 'bracos')):
        c += pessoa(x, 54, 9, NI2, pose, lado=-1 if x == 80 else 1)
    c += R(54, 54, 52, 4, NI2)
    c += R(0, 58, 160, 32, c.lin([(0, '#93AA8C'), (1, '#5E7B5E')]))
    for base, r, cor in ((62, 1.2, '#55705A'), (65.5, 1.5, '#4B6450'), (70, 1.9, '#3F5444'), (76, 2.5, '#33453A')):
        c += plateia(rng, base, -4, 164, r, cor, .25)
    c += plateia(rng, 93, -6, 166, 4.8, GR, .5)


def shows_4(c, rng):  # Som no Terraço: bandas independentes ao pôr do sol
    c += ceu(c, '#F7A68E', '#FBE9D7', '#FBD3B5')
    c += C(108, 60, 16, CO, .88)
    c += predios(rng, 64, -2, 162, 12, 36, '#8FA3A0', wmin=10, wmax=20)
    c += predios(rng, 64, -4, 164, 8, 28, '#3E5F5E', janela=AM, prob=.18)
    c += R(0, 63, 160, 27, '#183E3F')
    c += L(0, 59, 160, 59, '#0B2A2B', .9) + G([L(x, 59, x, 63, '#0B2A2B', .5) for x in range(3, 160, 6)])
    c += luzinhas(-2, 8, 162, 12, 16, 17, '#5B4A3F', WA)
    c += L(60, 82, 61, 71, NI2, .7) + L(68, 82, 67, 71, NI2, .7) + R(58.5, 70, 11, 1.6, NI2, rx=.6)
    c += sentado(64, 82, 19, NI2) + violao(67.5, 73.5, 3.3, CO, ang=-18, boca=NI2)
    c += pessoa(84, 82, 25, NI2, 'mic', lado=-1)
    c += pessoa(22, 92, 27, NI2) + pessoa(36, 90, 24, NI2, 'braco', lado=1, cabelo=NI2)
    c += pessoa(118, 90, 26, NI2, 'braco', lado=-1) + pessoa(132, 92, 28, NI2) + pessoa(146, 90, 25, NI2, 'cintura')


def shows_5(c, rng):  # MPB no Parque Barigui ao entardecer
    c += ceu(c, '#F2D7BE', '#F4EBDD')
    c += C(28, 38, 7, CO2, .85)
    c += moitas(rng, 50, -4, 164, 4, VD2)
    c += R(0, 49, 160, 8, '#B7CFC8') + G([L(10, 52, 40, 52, OF2, .5), L(70, 54, 110, 54, OF2, .5), L(120, 51, 150, 51, OF2, .5)], op=.7)
    c += R(0, 57, 160, 33, c.lin([(0, '#9DB596'), (1, '#7C9A76')]))
    c += araucaria(14, 60, 38, VD3) + araucaria(150, 60, 46, VD3) + araucaria(137, 60, 30, '#3D5A4A')
    c += R(88, 55, 42, 4, '#5B4A3F') + L(89, 55, 89, 38, GR2, .7) + L(129, 55, 129, 38, GR2, .7)
    c += P('M86 38L109 29L132 38z', OF2) + R(88, 38, 42, 3, CO)
    c += pessoa(96, 55, 11, GR2) + violao(95.5, 50, 1.6, CO, ang=-25) + pessoa(106, 55, 11.5, GR2, 'mic', lado=-1)
    c += pessoa(116, 55, 11, GR2) + sentado(124, 55, 9, GR2, lado=-1)
    for x, y, w, cor, a in ((8, 74, 28, CO, -4), (46, 79, 26, AM, 3), (94, 77, 30, TE, -2), (130, 81, 24, OF2, 4)):
        c += R(x, y, w, 7, cor, rx=.6, tr=f'rotate({a} {x + w / 2} {y + 3.5})')
    c += sentado(17, 78, 15, GR, 1, roupa=DG3) + sentado(29, 78, 13, GR2, 1) + sentado(55, 83, 15, GR, 1, roupa=WG)
    c += sentado(66, 83, 12, GR2, 1, braco='cima') + sentado(104, 81, 15, GR, 1) + sentado(140, 85, 14, GR2, 1, roupa=CO)


def festas_1(c, rng):  # Baile de Confete: Noite Eletrônica
    c += ceu(c, '#0A1E20', DG)
    c += brilho(c, 80, 30, 72, 40, CO, .34)
    c += cone(c, 8, 0, 34, 92, 72, AM, .17) + cone(c, 152, 0, 68, 126, 72, TE2, .17) + cone(c, 80, 0, 58, 102, 72, CO, .14)
    c += busto(80, 55, 4.4, NI, 1)
    c += R(58, 52, 44, 10, NI, rx=1) + R(60, 53.2, 40, .9, CO, op=.85) + R(66, 50.6, 8, 1.6, '#1D2B2C') + R(86, 50.6, 8, 1.6, '#1D2B2C')
    c += confete(rng, 120, 0, 160, 0, 80, [CO, AM, TE2, OF, CO3])
    c += plateia(rng, 82, -4, 164, 3.6, '#0A2324', .45)
    c += plateia(rng, 93, -6, 166, 5, NI, .55)
    c += confete(rng, 26, 0, 160, 46, 90, [CO, AM, TE2, OF])


def festas_2(c, rng):  # Galpão Sessions: techno no galpão do Rebouças
    c += R(0, 0, 160, 90, '#081012')
    for x in range(0, 160, 20):
        c += P(f'M{x} 18L{x + 20} 5V18', stroke='#1D3133', sw=.8)
    c += L(0, 18, 160, 18, '#1D3133', .8)
    c += R(20, 23, 120, 30, '#0D1F22')
    c += G([L(x, 23, x, 53, TE, .25) for x in range(20, 141, 8)] + [L(20, y, 140, y, TE, .25) for y in range(23, 54, 6)], op=.5)
    c += P('M44 50L64 28L84 50z', CO, op=.45) + P('M86 50L104 32L122 50z', TE2, op=.4) + C(80, 36, 6, AM, .5)
    for i, x in enumerate(range(-30, 191, 18)):
        c += L(80, 21, x, 90, CO if i % 2 == 0 else TE2, .35, op=.7)
    c += brilho(c, 80, 62, 90, 24, TE, .16)
    c += plateia(rng, 80, -4, 164, 3.4, '#0C1A1C', .4)
    c += plateia(rng, 93, -6, 166, 5, '#040809', .5)


def festas_3(c, rng):  # Noite das Divas – Flashback 2000
    c += ceu(c, DG, '#0A2A2C')
    c += C(80, 42, 30, CO, .92) + C(80, 42, 22, CO2, .95) + C(80, 42, 14, AM, .95)
    c += R(0, 62, 160, 28, '#0A2324')
    c += G([L(80, 10, x, 0 if x in (0, 160) else 90, OF, .25) for x in (0, 20, 50, 110, 140, 160)], op=.35)
    c += L(80, 0, 80, 5, OF, .4) + bola_espelhada(80, 10, 4.6)
    c += P('M70.5 62L74 40H86L89.5 62z', NI)
    c += pessoa(80, 62, 36, NI, 'mic', lado=-1, cabelo=NI)
    c += L(84.5, 34.5, 92, 22, NI, 3.7)
    for x, y, s in ((34, 22, 2.4), (126, 18, 2), (112, 50, 1.6), (46, 48, 1.8), (20, 40, 1.2), (142, 36, 1.4)):
        c += faisca(x, y, s, OF2)
    c += plateia(rng, 82, -4, 164, 3.6, '#0C2E30', .4)
    c += plateia(rng, 93, -6, 166, 5.2, NI, .5)


def festas_4(c, rng):  # Festa Neon Universitária
    c += R(0, 0, 160, 90, '#0A0E13')
    for _ in range(9):
        x, y, s, cor = rng.uniform(0, 160), rng.uniform(4, 60), rng.uniform(3, 7), rng.choice([TE2, CO, AM, '#B9F06A'])
        c += C(x, y, s, cor, .55) + ''.join(C(x + rng.uniform(-2.2, 2.2) * s, y + rng.uniform(-2, 2) * s, rng.uniform(.3, .9), cor, .6) for _ in range(6))
    c += P('M0 8L12 4L24 8L36 4L48 8L60 4L72 8L84 4L96 8L108 4L120 8L132 4L144 8L156 4L168 8', stroke=TE2, sw=.6, op=.8)
    for x, y, cor in ((22, 26, CO), (30, 20, AM), (128, 22, TE2), (138, 28, CO), (70, 14, '#B9F06A')):
        c += P(f'M{x} {y + 4.2}q-1.6 6 1 12', stroke=OF, sw=.25, op=.6) + E(x, y, 3.4, 4.2, cor) + E(x - 1, y - 1.4, .8, 1.2, '#FFFFFF', .5)
    c += plateia(rng, 82, -4, 164, 3.6, '#121A22', .45)
    c += plateia(rng, 93, -6, 166, 5, '#0D131A', .55)
    for _ in range(46):
        x, y = rng.uniform(0, 160), rng.uniform(58, 90)
        c += C(x, y, rng.uniform(.4, 1.1), rng.choice([TE2, CO, AM, '#B9F06A']), .9)


def festas_5(c, rng):  # Baile Disco Groove (só vinil)
    c += ceu(c, '#0E2B2C', DG)
    c += G([L(80, 13, x, y, OF, .3) for x, y in ((0, 30), (0, 70), (30, 90), (70, 90), (110, 90), (160, 70), (160, 30), (150, 90))], op=.14)
    c += estrelas(rng, 36, 0, 56, OF2, .7)
    vy, y0 = 30, 58
    rows = [58, 61.5, 66, 72, 80, 91]
    cores = [AM, CO, TE2, DG4]
    for i in range(len(rows) - 1):
        for j, xb in enumerate(range(-80, 240, 20)):
            def px(xbot, y):
                return 80 + (xbot - 80) * (y - vy) / (90 - vy)
            ya, yb = rows[i], rows[i + 1]
            d = (f'M{n(px(xb, ya))} {n(ya)}L{n(px(xb + 20, ya))} {n(ya)}L{n(px(xb + 20, yb))} {n(yb)}L{n(px(xb, yb))} {n(yb)}z')
            c += P(d, cores[(i + j) % 4], op=.55 if (i + j) % 2 == 0 else .3)
    c += L(80, 0, 80, 7, OF, .4) + bola_espelhada(80, 13, 6)
    c += pessoa(50, 82, 30, NI, 'bracos', cabelo=NI) + pessoa(66, 80, 28, NI, 'aponta', lado=1)
    c += pessoa(98, 82, 30, NI, 'braco', lado=1, cabelo=NI) + pessoa(114, 80, 28, NI, 'cintura')
    c += R(132, 70, 22, 8, NI2, rx=1) + E(138, 70.2, 4.4, 1.1, '#151515') + C(138, 70.2, .8, CO) + E(148, 70.2, 4.4, 1.1, '#151515') + C(148, 70.2, .8, AM)


def teatro_1(c, rng):  # Noite de Stand-up no Largo
    c += R(0, 0, 160, 90, '#1A2526')
    c += tijolos(OF, .04, 0, 66)
    c += C(124, 22, 8, 'none') + P('M116 22a8 8 0 1 1 16 0a8 8 0 1 1 -16 0', stroke=CO, sw=.9, op=.85)
    c += cone(c, 80, 0, 58, 102, 66, WA, .3) + E(80, 66, 20, 3.6, WA, .25)
    c += R(30, 64, 100, 4, '#0D1516') + R(30, 68, 100, 2, '#071011')
    c += R(91, 51, 7, 1.4, NI) + L(92, 52, 90.6, 64, NI, .6) + L(97, 52, 98.4, 64, NI, .6) + R(93.6, 48.4, 1.7, 2.7, OF, op=.6)
    c += pessoa(80, 64, 30, NI, 'mic', lado=1)
    for x in (18, 58, 102, 142):
        c += C(x, 76, 6, AM, .2) + C(x, 76, 1.3, WA) + R(x - .6, 79, 1.2, 12, NI) + E(x, 79, 9, 1.6, '#0E1617')
    for x, r in ((6, 4.4), (32, 4.6), (46, 4.4), (72, 4.7), (90, 4.4), (116, 4.6), (130, 4.4), (156, 4.6)):
        c += busto(x, 93 + rng.uniform(-.5, .5), r, NI)


def teatro_2(c, rng):  # Mostra de Teatro Popular no Guaíra
    c += R(0, 0, 160, 90, NI2)
    for x in (52, 80, 108):
        c += cone(c, x, 8, x - 12, x + 12, 64, WA, .17)
    c += P('M10 62H150L160 71H0z', '#5A4636') + R(0, 71, 160, 3, '#3B2E24')
    for x in (52, 80, 108):
        c += E(x, 64, 13, 2.4, WA, .3)
    c += pessoa(52, 64, 25, '#2A2220', 'bracos', roupa=AM) + pessoa(80, 64, 28, '#2A2220', 'aponta', lado=1, roupa=CO)
    c += pessoa(108, 64, 24, '#2A2220', 'cintura', roupa=TE)
    for lado in (1, -1):
        x0 = 0 if lado == 1 else 160
        c += P(f'M{x0} 0H{x0 + lado * 28}Q{x0 + lado * 22} 30 {x0 + lado * 32} 64H{x0}z', '#B8412C')
        c += G([P(f'M{x0 + lado * k} 0Q{x0 + lado * (k - 4)} 30 {x0 + lado * (k + 3)} 64', stroke='#9E3524', sw=1.2) for k in (7, 15, 22)])
        c += P(f'M{x0 + lado * 3} 0Q{x0 + lado * 1} 30 {x0 + lado * 6} 64', stroke='#D2553C', sw=.8, op=.7)
    c += R(0, 0, 160, 7, '#9E3524') + G([P(f'M{x} 7q4 4 8 0z', '#9E3524') for x in range(0, 160, 8)]) + R(0, 7, 160, .8, AM, op=.8)
    c += plateia(rng, 88, -4, 164, 4, NI, .05, passo=13)
    c += G([R(x - 5.5, 83, 11, 9, '#0F1A1B', rx=2.4) for x in range(4, 164, 13)])


def teatro_3(c, rng):  # Estreia de Comédia no Teatro Paiol (o casamento que dá errado)
    c += ceu(c, '#F1E6D8', '#E6D6C2')
    c += G([C(x, y, r, OF2) for x, y, r in ((28, 18, 6), (36, 16, 7), (44, 19, 5), (118, 14, 6), (127, 12, 7), (135, 15, 5))], op=.9)
    c += R(0, 64, 160, 26, '#B79A7E') + R(0, 64, 160, 1.2, '#9C8064')
    c += P('M54 64V36Q80 8 106 36V64', stroke='#7E9A84', sw=2.4)
    for i in range(14):
        a = math.pi * i / 13
        c += C(80 - 26 * math.cos(a), 36 - 22 * math.sin(a) + (0 if i not in (0, 13) else 4), 1.5, (CO, OF2, AM)[i % 3])
    c += P('M68.8 34L64 64H73.6z', OF2, op=.95)
    c += pessoa(70, 64, 26, GR, 'bracos', roupa=OF2)
    c += pessoa(90, 64, 28, GR, 'aponta', lado=1, roupa=DG)
    c += pessoa(122, 64, 24, GR, 'braco', lado=-1, roupa=WG)
    c += G([R(100, 52, 10, 4, OF2, rx=.6), R(101.5, 48, 7, 4, OF2, rx=.6), R(103, 45, 4, 3, OF2, rx=.6), R(100, 52, 10, 1, CO3),
            C(105, 44.2, .9, CO)], tr='rotate(28 105 50)')
    c += G([L(111, 42, 114, 39, GR2, .5), L(113, 46, 117, 45, GR2, .5), L(110, 39, 111, 35, GR2, .5)], op=.6)
    c += plateia(rng, 93, -6, 166, 4.8, GR, .12)


def teatro_4(c, rng):  # Festival de Palhaçaria e Humor na praça
    c += ceu(c, '#D6E8E4', OF)
    c += arvore(16, 60, 36, '#7E9A84', '#5B4A3F') + arvore(146, 60, 40, VD, '#5B4A3F') + arvore(126, 58, 26, VD2, '#5B4A3F')
    c += R(0, 58, 160, 32, '#DDD5C5')
    for i, x in enumerate(range(52, 108, 7)):
        c += R(x, 26, 7, 30, CO if i % 2 == 0 else OF2)
    c += P('M48 27L80 13L112 27z', CO) + G([P(f'M{x} 26q3.5 3 7 0z', CO if (x // 7) % 2 == 0 else OF2) for x in range(52, 108, 7)])
    c += R(48, 56, 64, 4, '#7A5A44')
    c += pessoa(80, 56, 26, GR, 'bracos', roupa=TE) + P('M77 32.4L80 25L83 32.4z', AM) + C(80, 36, .9, CO)
    c += E(76, 56, 3.4, 1.3, CO) + E(84, 56, 3.4, 1.3, CO)
    c += P('M66 30Q80 10 94 30', stroke=GR2, sw=.3, op=.4) + C(67, 27, 1.8, AM) + C(80, 17, 1.8, CO) + C(93, 27, 1.8, DG)
    c += pessoa(24, 90, 32, GR2) + pessoa(35, 90, 18, GR, 'braco', lado=1) + pessoa(124, 92, 34, GR) + pessoa(136, 90, 16, GR2, 'bracos')
    c += L(139.8, 70, 142, 50, GR2, .3) + E(142, 46, 3.2, 4, CO)
    c += sentado(58, 90, 16, GR, 1, roupa=AM) + sentado(98, 90, 15, GR2, -1, roupa=DG3)


def teatro_5(c, rng):  # Sessão de Improviso: a plateia no comando
    c += ceu(c, DG, '#082B2D')
    c += R(40, 12, 80, 30, DG2, rx=1)
    for _ in range(22):
        x, y = rng.uniform(42, 114), rng.uniform(14, 36)
        c += R(x, y, 4.6, 4.6, rng.choice([AM, CO2, TE2, OF2]), tr=f'rotate({n(rng.uniform(-12, 12))} {n(x + 2.3)} {n(y + 2.3)})')
    c += R(0, 62, 160, 28, '#062224') + E(64, 63, 16, 3, WA, .25) + E(96, 63, 16, 3, WA, .25)
    c += pessoa(64, 63, 28, '#14100E', 'aponta', lado=1, roupa=CO) + pessoa(96, 63, 27, '#14100E', 'bracos', roupa=AM)
    c += balao_fala(36, 30, 20, 9, OF2, lado=1) + R(100, 18, 14, 13, OF2, rx=4) + P('M104 30.6L102 35L108 30.6z', OF2)
    c += interrogacao(107, 22.5, 6.4, DG)
    c += plateia(rng, 93, -6, 166, 5, NI, .45)


def arte_1(c, rng):  # Arte que Conversa: instalações no MON (o Olho)
    c += ceu(c, '#CFE3DF', '#EEF1EC')
    c += moitas(rng, 60, -4, 164, 3.2, '#A9C0A8')
    c += R(108, 49, 56, 11, OF2) + R(108, 49, 56, 1.6, SA3) + G([L(x, 51, x, 60, SA3, .4) for x in range(112, 162, 6)])
    c += R(0, 60, 160, 30, '#DCD7CB') + R(0, 66, 160, 11, '#C3D6D2')
    c += E(80, 71, 34, 3.2, DG3, .35) + R(72, 66, 16, 4, AM, op=.3)
    c += R(72, 44, 16, 22, AM) + R(72, 44, 3, 22, '#E2AE5E')
    c += P('M40 34Q80 4 120 34Q80 62 40 34z', DG)
    c += G([L(x, 22 + abs(x - 80) * .25, x, 46 - abs(x - 80) * .25, '#4E8A8A', .7) for x in range(52, 110, 8)], op=.5)
    c += P('M48 33Q80 11 112 33', stroke='#6FA7A5', sw=.8, op=.8)
    c += P('M8 62Q40 55 72 60', stroke=OF2, sw=2.6)
    c += pessoa(30, 84, 18, GR, 'aponta', lado=1) + pessoa(40, 86, 20, GR2) + pessoa(50, 84, 11, GR, 'braco', lado=1)
    c += pessoa(120, 87, 20, GR) + pessoa(131, 85, 18, GR2, 'cintura', cabelo=GR2)


def arte_2(c, rng):  # Visita guiada: Arte Contemporânea
    c += R(0, 0, 160, 62, '#EFECE5') + R(0, 62, 160, 28, '#D3CBBB') + R(0, 61, 160, 1.4, '#C3BBAA')
    for x in (33, 82, 129):
        c += cone(c, x, 0, x - 18, x + 18, 58, WA, .2)
    c += quadro(18, 14, 30, 36, R(20, 16, 26, 32, DG) + C(37, 26, 6, CO) + R(22, 38, 14, 8, AM))
    c += quadro(62, 10, 40, 30, R(64, 12, 36, 26, TE2) + C(90, 18, 3.4, AM) + P('M64 30Q76 20 88 30T100 28V38H64z', DG3) + P('M64 34Q80 28 100 34V38H64z', WG))
    c += quadro(116, 16, 26, 32, R(118, 18, 22, 28, CO3) + busto(129, 46, 4.6, GR2))
    c += R(30, 53, 6, 3, '#C9C2B3') + R(79, 44, 6, 3, '#C9C2B3') + R(126, 51, 6, 3, '#C9C2B3')
    c += pessoa(56, 84, 30, GR, 'braco', lado=1, roupa=CO)
    c += pessoa(76, 88, 28, GR2) + pessoa(88, 86, 26, GR, cabelo=GR) + pessoa(100, 89, 29, GR2, 'cintura') + pessoa(113, 87, 27, GR)
    c += pessoa(124, 88, 16, GR2, 'aponta', lado=1)


def arte_3(c, rng):  # Exposição Azul Profundo (pintura abstrata)
    c += R(0, 0, 160, 64, '#F2F0EA') + R(0, 64, 160, 26, '#DAD3C5') + R(0, 63, 160, 1.2, '#C9C0AE')
    c += R(18, 9, 74, 48, '#FFFFFF') + R(20, 11, 70, 44, BL3)
    c += E(40, 30, 17, 12, BL) + E(68, 41, 19, 10, BL2, .85) + C(78, 20, 5, DG) + P('M22 46Q40 34 58 44T88 38', stroke=OF2, sw=1.1, op=.85)
    c += R(102, 14, 42, 40, '#FFFFFF') + R(104, 16, 38, 36, BL) + R(110, 16, 8, 36, BL3) + R(126, 16, 6, 36, BL2) + C(122, 30, 4, OF2, .9)
    c += R(46, 70, 44, 3, '#5B4A3F') + L(50, 73, 50, 80, '#5B4A3F', .9) + L(86, 73, 86, 80, '#5B4A3F', .9)
    c += sentado(62, 80, 24, GR, 1, roupa=CO)
    c += pessoa(122, 86, 30, GR2, cabelo=GR2)


def arte_4(c, rng):  # Aquarelas do Litoral
    c += ceu(c, '#F4EFE6', '#ECE5D8') + R(0, 62, 160, 28, '#CDBFA8') + R(0, 61, 160, 1.2, '#B9AA90')
    frames = [(10, 16), (46, 16), (82, 16), (118, 16)]
    for i, (x, y) in enumerate(frames):
        cont = R(x + 1.4, y + 1.4, 25.2, 21.2, '#F8F6F1')
        ix, iy = x + 1.4, y + 1.4
        if i == 0:
            cont += R(ix, iy + 12, 25.2, 9.2, BL2, op=.7) + P(f'M{ix + 4} {iy + 13}Q{ix + 12} {iy + 3} {ix + 20} {iy + 13}z', VD, op=.7) + R(ix + 16, iy + 5, 1.6, 6, OF2) + R(ix + 16, iy + 5, 1.6, 1.6, CO)
        elif i == 1:
            for k, cor in enumerate((CO3, AM, TE2, OF, CO2)):
                cont += R(ix + 1 + k * 4.8, iy + 9, 4.4, 8, cor, op=.8) + P(f'M{ix + 1 + k * 4.8} {iy + 9}l2.2 -2.4l2.2 2.4z', TC, op=.8)
            cont += R(ix, iy + 17, 25.2, 4.2, BL2, op=.6)
        elif i == 2:
            cont += R(ix, iy + 11, 25.2, 10.2, BL2, op=.65) + P(f'M{ix + 9} {iy + 12}h8l-1.4 2h-5.2z', MAD) + P(f'M{ix + 13} {iy + 4}v8h-4z', OF2)
        else:
            cont += P(f'M{ix} {iy + 16}L{ix + 7} {iy + 6}L{ix + 13} {iy + 13}L{ix + 18} {iy + 7}L{ix + 25.2} {iy + 15}V{iy + 21.2}H{ix}z', WG2, op=.8)
            cont += P(f'M{ix} {iy + 21.2}V{iy + 17}Q{ix + 12} {iy + 12} {ix + 25.2} {iy + 18}V{iy + 21.2}z', DG3, op=.6)
        c += R(x, y, 28, 24, '#B79A7E') + cont
    c += pessoa(38, 86, 30, GR) + pessoa(96, 84, 28, GR2, 'aponta', lado=1, roupa=TE) + pessoa(112, 87, 30, GR, cabelo=GR)


def arte_5(c, rng):  # Arte Digital Imersiva no Paço da Liberdade
    c += R(0, 0, 160, 90, '#05090C')
    c += R(30, 10, 100, 52, '#0B1418') + P('M0 0L30 10V62L0 90z', '#081013') + P('M160 0L130 10V62L160 90z', '#081013') + P('M30 62H130L160 90H0z', '#0A1215')
    for i, r in enumerate(range(4, 62, 5)):
        c += C(80, 36, r, 'none') + P(f'M{80 - r} 36a{r} {r} 0 1 1 {2 * r} 0a{r} {r} 0 1 1 {-2 * r} 0', stroke=(CO, TE2, AM)[i % 3], sw=.7, op=.55)
    for i, rx in enumerate(range(10, 80, 10)):
        c += E(80, 76, rx, rx * .16, 'none', stroke=(TE2, CO)[i % 2], sw=.5, op=.45)
    c += estrelas(rng, 40, 0, 90, OF2, .5)
    figs = [(52, 84, 30, 'braco', 1), (70, 86, 32, 'pe', 1), (106, 85, 31, 'aponta', -1), (118, 84, 18, 'bracos', 1)]
    for x, b, h, pose, lado in figs:
        c += G([pessoa(x + .6, b, h + .6, TE2, pose, lado)], op=.55)
    for x, b, h, pose, lado in figs:
        c += pessoa(x, b, h, '#020405', pose, lado)


def feiras_1(c, rng):  # Food Truck Day no Jardim Botânico
    c += ceu(c, '#D2E6E2', OF)
    vid, aro = '#A7C7C1', '#F8F8F4'
    c += R(40, 36, 26, 22, vid) + P('M40 36Q53 26 66 36z', vid) + R(94, 36, 26, 22, vid) + P('M94 36Q107 26 120 36z', vid)
    c += R(65, 26, 30, 32, vid) + P('M65 26Q80 4 95 26z', vid) + R(78.6, 7.4, 2.8, 4, aro) + P('M78.6 7.4Q80 4.6 81.4 7.4z', aro)
    for x in list(range(44, 66, 4)) + list(range(98, 120, 4)):
        c += L(x, 33 if x < 66 else 33, x, 58, aro, .45)
    for x in range(69, 95, 4):
        c += L(x, 16 + abs(x - 80) * .45, x, 58, aro, .45)
    c += G([L(40, y, 66, y, aro, .45) for y in (44, 51)] + [L(94, y, 120, y, aro, .45) for y in (44, 51)] + [L(65, y, 95, y, aro, .45) for y in (36, 46)])
    c += P('M40 36Q53 26 66 36M94 36Q107 26 120 36M65 26Q80 4 95 26', stroke=aro, sw=.8)
    c += R(0, 58, 160, 32, '#E2DACB') + P('M10 62H60L50 66H20z', VD2) + P('M100 62H150L140 66H110z', VD2) + P('M66 61H94V64H66z', '#7E9A84')
    c += bandeirinhas(-2, 50, 162, 50, 10, 24, [CO, AM, TE, OF2])
    c += food_truck(4, 84, 40, 21, CO, OF2, CO3) + food_truck(60, 86, 38, 20, DG, AM, OF2) + food_truck(114, 84, 36, 20, AM, DG, OF2, escuro=GR)
    c += pessoa(52, 92, 26, GR) + pessoa(106, 93, 28, GR2, 'braco', lado=-1, cabelo=GR2) + pessoa(157, 92, 26, GR)


def feiras_2(c, rng):  # Rota dos Food Trucks no São Francisco (à noite)
    c += ceu(c, '#0A2526', '#123F40')
    c += estrelas(rng, 26, 0, 30)
    c += arvore(18, 64, 34, '#0A2C2D') + arvore(140, 64, 38, '#0A2C2D') + arvore(84, 60, 26, '#0C3132')
    c += R(0, 70, 160, 20, '#0A1D1E')
    c += luzinhas(-2, 10, 80, 14, 14, 10, '#33504D', WA) + luzinhas(80, 14, 162, 8, 14, 10, '#33504D', WA)
    for x, w, cor, t1 in ((4, 44, '#13494B', CO), (58, 42, '#3B4A3E', AM), (112, 42, '#1E5658', TE2)):
        c += E(x + w * .45, 76, w * .5, 4, WA, .18)
        c += food_truck(x, 76, w, 22, cor, t1, OF2, janela=WA, escuro=NI)
    c += pessoa(20, 92, 26, NI) + pessoa(31, 90, 24, NI, 'braco', lado=1) + pessoa(71, 92, 27, NI, cabelo=NI) + pessoa(82, 91, 25, NI, 'cintura')
    c += pessoa(128, 92, 28, NI) + pessoa(140, 90, 22, NI)


def feiras_3(c, rng):  # Feira Orgânica do Passeio Público
    c += ceu(c, '#F3E9D8', OF)
    c += arvore(12, 56, 40, '#7E9A84', '#5B4A3F') + arvore(46, 54, 34, VD, '#5B4A3F') + arvore(118, 56, 38, '#7E9A84', '#5B4A3F') + arvore(150, 54, 34, VD2, '#5B4A3F')
    c += R(0, 56, 160, 34, '#E3DACA')
    for x, w, c1 in ((6, 46, DG), (58, 44, CO), (108, 48, TE)):
        c += barraca(x, 74, w, 22, c1, OF2, '#A57C5B')
        for k in range(int(w / 3.6)):
            cor = rng.choice([VD, VD2, CO, AM, '#B5523B'])
            c += C(x + 2 + k * 3.6, 63.6, 1.6, cor) + C(x + 3.8 + k * 3.6, 61.6, 1.5, rng.choice([VD, CO, AM]))
    c += pessoa(30, 92, 28, GR) + R(35, 77, 5, 6, AM, rx=.6) + P('M35.6 77q1.9 -3 3.8 0', stroke=AM, sw=.5)
    c += pessoa(84, 90, 26, GR2, 'aponta', lado=-1, roupa=WG) + pessoa(132, 92, 28, GR, cabelo=GR) + R(126, 77, 5, 6, TE, rx=.6)


def feiras_4(c, rng):  # Feira de Artesanato do Barigui
    c += ceu(c, '#DCEBE7', OF)
    c += moitas(rng, 46, -4, 164, 3.6, VD2) + R(0, 46, 160, 8, '#B7CFC8')
    c += R(0, 54, 160, 36, c.lin([(0, '#AFC2A2'), (1, '#93AA88')]))
    c += araucaria(18, 50, 32, '#4E6B58') + araucaria(146, 50, 36, '#4E6B58')
    for x, w, s2 in ((4, 40, SA2), (54, 38, CO3), (102, 44, SA2)):
        c += tenda(x, 76, w, 22, OF2, s2)
        for k in range(4):
            vx = x + 6 + k * (w - 10) / 3
            c += (vaso if k % 2 == 0 else tigela)(vx, 67.2, 4.6 if k % 2 == 0 else 2.6, rng.choice([TC, CO2, DG3, AM, WG]))
    c += pessoa(48, 90, 26, GR, 'aponta', lado=1) + pessoa(98, 92, 28, GR2) + pessoa(150, 90, 26, GR, cabelo=GR) + pessoa(140, 90, 14, GR2, 'braco', lado=1)


def feiras_5(c, rng):  # Feira do Largo da Ordem: especial de primavera
    c += ceu(c, '#D6E8E4', OF)
    casas = [(-2, 32, 40, AM), (30, 22, 34, CO3), (52, 0, 0, None), (84, 26, 36, TE2), (110, 22, 30, OF2), (132, 30, 40, AM)]
    for x, w, h, cor in casas:
        if cor is None:
            continue
        c += R(x, 58 - h, w, h, cor) + R(x - .5, 58 - h, w + 1, 1.6, '#B5A98F')
        for k in range(int(w / 8)):
            c += janela_arco(x + 2.4 + k * 8, 58 - h + 5, 4, 8, '#5B4A3F', .85)
    c += R(54, 14, 26, 44, OF2) + R(59, 3, 16, 12, OF2) + P('M58 3L67 -4L76 3z', '#B5A98F') + janela_arco(64.5, 5, 5, 7, '#5B4A3F')
    c += janela_arco(62, 36, 10, 22, '#5B4A3F') + C(67, 24, 3, '#5B4A3F') + L(67, -8, 67, -3, '#5B4A3F', .6)
    c += R(0, 58, 160, 32, '#D9D1C1') + G([E(rng.uniform(0, 160), rng.uniform(60, 90), 1.4, .7, '#BFB5A1') for _ in range(70)], op=.6)
    c += bandeirinhas(-2, 20, 162, 22, 14, 26, [CO, AM, TE, OF2, CO3])
    for x, w, c1 in ((6, 40, CO), (114, 42, TE)):
        c += barraca(x, 78, w, 20, c1, OF2, '#A57C5B')
        for k in range(int(w / 3)):
            c += C(x + 1.6 + k * 3, 68.4, 1.4, rng.choice([CO, AM, OF2, CO3, '#E07AA0'])) + C(x + 1.6 + k * 3, 70, .7, VD)
    c += pessoa(58, 90, 28, GR) + pessoa(70, 88, 25, GR2, 'braco', lado=1, roupa=CO) + pessoa(96, 92, 28, GR, cabelo=GR)


def oficinas_1(c, rng):  # Oficina de Cerâmica para Iniciantes
    c += R(0, 0, 160, 90, '#EADFCF')
    for y in (20, 36):
        c += R(8, y, 64, 1.6, MAD)
        for k in range(6):
            x = 13 + k * 10
            c += (vaso if k % 2 == 0 else tigela)(x, y, rng.uniform(5, 8) if k % 2 == 0 else 3.2, rng.choice([TC, SA3, DG3, CO2, OF2]))
    c += R(100, 8, 46, 32, '#CFE3DF') + R(100, 8, 46, 32, 'none') + G([L(123, 8, 123, 40, OF2, 1), L(100, 24, 146, 24, OF2, 1)])
    c += P('M100 8h46v32h-46z', stroke=MAD, sw=1) + vaso(142, 47, 6, TC) + C(142, 38, 3.6, VD) + C(139, 40, 2.6, VD2)
    c += pessoa(52, 74, 34, GR, 'mesa', roupa=TE) + pessoa(106, 74, 34, GR2, 'mesa', roupa=CO, cabelo=GR2)
    c += R(0, 62, 160, 5, MAD) + R(0, 67, 160, 23, MAD2)
    c += vaso(52, 62, 8, TC) + tigela(64, 62, 3, SA3) + vaso(106, 62, 7, '#B4A08A') + P('M90 61.4q2 -1.6 4 0t4 0', stroke=TC, sw=1.2)
    c += R(30, 61, 6, .9, GR2) + R(124, 61, 5, .9, GR2)


def oficinas_2(c, rng):  # Workshop de Pintura em Tela
    c += R(0, 0, 160, 66, '#EFEAE0') + R(0, 66, 160, 24, '#D6CCBB')
    c += R(56, 6, 48, 34, '#DCEBE7') + G([L(80, 6, 80, 40, OF2, 1), L(56, 23, 104, 23, OF2, 1)]) + P('M56 6h48v34h-48z', stroke=SA3, sw=1)
    def cavalete(x, base, conteudo):
        return (L(x, base, x + 8, base - 40, MAD, 1) + L(x + 16, base, x + 8, base - 40, MAD, 1) + L(x + 8, base - 36, x + 12, base, MAD, .8)
                + R(x - 2, base - 36, 20, 18, '#FFFFFF') + conteudo + R(x - 3, base - 18, 22, 1.4, MAD))
    c += cavalete(14, 80, R(-0, 0, 0, 0, 'none') + C(22, 54, 5, CO) + P('M13 58Q20 50 28 56T33 50', stroke=TE, sw=1.4) + R(14, 46, 6, 4, AM))
    c += cavalete(122, 80, C(136, 49, 3, AM) + P('M120 62Q128 52 134 58T140 56V62z', DG3) + P('M120 62Q130 57 140 60V62z', VD2))
    c += pessoa(46, 88, 32, GR, 'aponta', lado=-1, roupa=OF2)
    hx, hy = mao(46, 88, 32, 'aponta', -1)
    c += L(hx, hy, hx - 4, hy - 2, MAD, .8) + C(hx - 4.4, hy - 2.2, .9, CO)
    c += E(52, 66, 5, 3, OF2) + C(50, 65, .9, CO) + C(53, 64.6, .9, TE) + C(54.6, 66.6, .9, AM)
    c += pessoa(110, 88, 30, GR2, 'aponta', lado=1, roupa=TE)
    hx, hy = mao(110, 88, 30, 'aponta', 1)
    c += L(hx, hy, hx + 4, hy - 2, MAD, .8) + C(hx + 4.4, hy - 2.2, .9, DG3)
    c += pessoa(80, 90, 26, GR, cabelo=GR, roupa=CO)


def oficinas_3(c, rng):  # Aulão aberto de dança de salão na Praça do Japão
    c += ceu(c, '#F6E3D9', OF)
    c += R(102, 34, 38, 20, '#E9E1D3') + P('M96 34Q121 25 146 34L141 37H101z', DG) + P('M108 27Q121 21 134 27L131 29H111z', DG) + R(119, 18, 4, 5, DG)
    c += G([R(x, 37, 1.6, 17, '#5B4A3F') for x in (104, 114, 127, 137)])
    for x, base, h in ((18, 60, 34), (60, 58, 26), (156, 60, 30)):
        c += R(x - 1, base - h * .5, 2, h * .5, '#5B4A3F')
        for _ in range(14):
            c += C(x + rng.uniform(-h * .4, h * .4), base - h * .55 + rng.uniform(-h * .35, h * .2), rng.uniform(2.4, 4.2), rng.choice([CO3, '#F7CFC4', OF2]))
    c += R(0, 54, 160, 36, '#E3DACA') + E(44, 60, 38, 4.4, '#B7CFC8')
    c += R(148, 60, 6, 1.4, SA3) + R(149.4, 55, 3.2, 5, SA3) + P('M147 55L151 52L155 55z', SA3)
    for x0, base, h, cor_saia in ((36, 88, 30, CO), (102, 90, 31, TE), (128, 74, 18, AM)):
        c += pessoa(x0, base, h, GR) + P(f'M{n(x0 + h * .38 - h * .24)} {n(base)}L{n(x0 + h * .38 - h * .1)} {n(base - h * .42)}H{n(x0 + h * .38 + h * .1)}L{n(x0 + h * .38 + h * .24)} {n(base)}z', cor_saia)
        c += pessoa(x0 + h * .38, base, h * .94, GR2, roupa=cor_saia)
        topo = base - h + h * .28
        c += L(x0 + h * .06, topo + h * .06, x0 + h * .19, topo - h * .18, GR, h * .1) + L(x0 + h * .32, topo + h * .08, x0 + h * .19, topo - h * .18, cor_saia, h * .1)
    c += C(70, 30, 1.3, DG) + L(71.2, 30, 71.2, 24, DG, .5) + C(76, 26, 1.3, DG) + L(77.2, 26, 77.2, 20, DG, .5) + L(71.2, 24, 77.2, 20, DG, .9)


def oficinas_4(c, rng):  # Curso intensivo de torno cerâmico
    c += ceu(c, '#E9DCCB', '#E2D2BD') + R(0, 70, 160, 20, '#BCA58A')
    c += R(8, 20, 42, 1.6, MAD) + R(8, 38, 42, 1.6, MAD)
    for y in (20, 38):
        for k in range(4):
            c += (vaso if k % 2 == 0 else tigela)(14 + k * 10, y, 7 if k % 2 == 0 else 3, rng.choice([SA2, OF2, SA3]))
    c += G([L(150, 0, 110, 70, WA, 6), L(160, 6, 128, 70, WA, 5)], op=.25)
    c += R(76, 66, 30, 12, GR2, rx=2) + E(91, 66, 18, 4, '#5B5B55') + E(91, 64, 11, 2.4, '#8C8F7E')
    c += sentado(62, 90, 40, GR, 1, roupa=CO)
    c += L(64, 63, 84, 54, CO, 3.4) + L(63, 66, 98, 58, CO, 3.2)
    c += vaso(91, 64, 15, TC)
    c += G([P('M83 55q8 3 16 0', stroke=OF2, sw=.5), P('M84 60q7 2.6 14 0', stroke=OF2, sw=.5)], op=.6)
    c += C(84, 54.2, 1.6, GR) + C(98, 58.2, 1.6, GR)


def oficinas_5(c, rng):  # Oficina de Arte em Resina
    c += R(0, 0, 160, 60, '#EEF1EC') + R(20, 8, 120, 30, '#E2E6DF', rx=1)
    c += G([C(x, y, .6, SA3) for x in range(24, 138, 6) for y in range(12, 36, 6)], op=.8)
    c += L(40, 14, 40, 26, GR2, .8) + R(56, 14, 8, 10, AM, rx=.6) + L(110, 12, 116, 28, GR2, .8) + C(128, 20, 4, 'none') + P('M124 20a4 4 0 1 1 8 0a4 4 0 1 1 -8 0', stroke=TE, sw=1)
    c += pessoa(46, 72, 34, GR, 'mesa', roupa=DG3) + pessoa(114, 72, 34, GR2, 'mesa', roupa=CO, cabelo=GR2)
    c += R(0, 60, 160, 5, '#DAD3C5') + R(0, 65, 160, 25, SA3)
    c += E(46, 61, 14, 2.4, TE) + P('M36 61q5 -1.6 10 0t10 0', stroke=OF2, sw=.6) + E(114, 61, 13, 2.2, BL) + P('M105 61q5 -1.4 9 0t9 0', stroke=BL2, sw=.6)
    c += C(70, 61, 3, TE2) + C(78, 61, 3, AM) + C(90, 61, 3, CO3)
    c += R(80, 51, 5, 7, OF2, rx=.6) + L(82.5, 58, 82.5, 61, TE2, .8)


def cinema_1(c, rng):  # Cineclube de Suspense (Janela Indiscreta)
    c += R(0, 0, 160, 90, '#070B0C') + brilho(c, 80, 34, 76, 36, '#9FB7B3', .22)
    c += R(20, 8, 120, 52, '#1A2224') + R(22, 10, 116, 48, '#1C3436') + R(22, 22, 116, 36, '#34494A')
    for row in range(4):
        for col in range(9):
            x, y = 27 + col * 12.2, 25 + row * 8.2
            lit = (row, col) in ((1, 3), (2, 6))
            c += R(x, y, 7, 5.4, AM if lit else '#1E2828')
    c += busto(66.5, 40.6, 1.2, NI) + R(101.8, 41.6, 1.4, 5.4, CO3)
    c += C(118, 15, 3, OF2, .9)
    c += plateia(rng, 76, -4, 164, 3.4, '#030606', .02)
    c += G([R(x - 5, 72, 10, 8, '#0A1213', rx=2) for x in range(2, 164, 12)])
    c += plateia(rng, 93, -6, 166, 5, '#020404', .02)
    c += pipoca(126, 90, 1.3)


def cinema_2(c, rng):  # Cinema ao Ar Livre no Barigui (telão no gramado)
    c += ceu(c, '#1E5658', '#F2C27A', '#6E8F8A')
    c += estrelas(rng, 14, 0, 20)
    c += araucaria(14, 66, 46, '#0B2A2B') + araucaria(134, 66, 38, '#0B2A2B') + arvore(152, 66, 30, '#0B2A2B')
    c += R(0, 64, 160, 26, '#0E3334')
    c += facho(c, 80, 92, 46, 114, 58, WA, .14)
    c += R(42, 16, 76, 48, OF2, rx=6) + tela_animacao(c, 46, 20, 68, 38) + R(40, 61, 80, 3.4, OF2, rx=1.6)
    c += plateia(rng, 93, -6, 166, 4.6, NI, .1)
    c += busto(62, 92, 3, NI, 1) + pipoca(98, 88, 1.1)


def cinema_3(c, rng):  # Sessão na Grama da Praça da Espanha (curtas sob as estrelas)
    c += ceu(c, '#061A1C', '#0F3D3F')
    c += estrelas(rng, 56, 0, 52) + C(132, 13, 4, OF2) + C(133.8, 12, 3.6, '#081F21')
    c += arvore(18, 70, 48, '#082224') + arvore(142, 70, 50, '#082224')
    c += L(24, 30, 44, 22, '#2A4446', .5) + L(116, 22, 136, 30, '#2A4446', .5)
    c += R(44, 20, 72, 38, OF2) + R(46, 22, 68, 34, c.lin([(0, CO2), (1, AM)])) + R(46, 44, 68, 12, DG3) + P('M78 43V33L86 43z', OF2) + R(76, 43, 12, 1.4, '#5B4A3F')
    c += facho(c, 80, 92, 46, 114, 56, WA, .1)
    c += R(0, 68, 160, 22, '#0A2A2C')
    for x, y, w, cor, a in ((8, 78, 30, CO, -3), (52, 82, 32, AM, 2), (100, 79, 30, TE, -2), (136, 84, 26, OF2, 3)):
        c += R(x, y, w, 8, cor, rx=.6, op=.9, tr=f'rotate({a} {x + w / 2} {y + 4})')
    c += deitado(14, 80, 26, NI) + sentado(60, 86, 16, NI, 1) + sentado(72, 86, 13, NI, 1) + deitado(106, 81, 24, NI, roupa=NI2) + sentado(142, 88, 15, NI, -1)


def cinema_4(c, rng):  # Cineclube no Terraço: clássicos ao anoitecer
    c += ceu(c, '#2B5D5F', '#F6D5B5', '#E8A889')
    c += predios(rng, 62, -4, 164, 10, 32, '#1D3F40', janela=AM, prob=.2)
    c += R(0, 60, 160, 30, '#123536') + L(0, 64, 160, 64, '#0B2425', .8)
    c += luzinhas(-2, 6, 162, 8, 12, 16, '#2A3F3F', WA)
    c += R(52, 14, 56, 36, '#1A1A1A') + R(54, 16, 52, 32, '#D9D9D4') + R(54, 16, 52, 32, c.rad([(0, '#FFFFFF', 0), (1, '#555', .45)]))
    c += busto(73, 48, 5, '#3A3A3A') + busto(87, 48, 5, '#3A3A3A') + L(80, 48, 80, 54, GR2, .6) + L(72, 54, 88, 54, GR2, .6)
    for x, c1, c2 in ((18, CO, OF2), (44, TE, OF2), (116, AM, OF2), (142, CO, OF2)):
        c += busto(x, 82, 4, NI2) + cadeira_praia(x, 90, c1, c2)


def cinema_5(c, rng):  # Mostra de Comédia Brasileira no Cine Passeio
    c += R(0, 0, 160, 90, '#0A1112') + brilho(c, 80, 32, 76, 34, AM, .25)
    c += R(24, 8, 112, 46, AM)
    c += C(80, 31, 15, OF2) + P('M71 27q3 -3 6 0M83 27q3 -3 6 0', stroke=GR, sw=1.1) + P('M70 33q10 13 20 0z', CO) + P('M73.5 33.4q6.5 2 13 0', stroke=OF2, sw=.9)
    c += E(68.6, 29.6, 1, 1.6, TE2) + E(91.4, 29.6, 1, 1.6, TE2)
    c += G([L(56, 18, 60, 22, CO, 1), L(52, 30, 58, 30, CO, 1), L(104, 18, 100, 22, CO, 1), L(108, 30, 102, 30, CO, 1)])
    c += plateia(rng, 78, -4, 164, 3.4, '#050909', .2)
    c += G([R(x - 5, 74, 10, 8, '#0F2E30', rx=2) for x in range(2, 164, 12)])
    c += plateia(rng, 93, -6, 166, 5, '#020404', .25)


def rua_1(c, rng):  # Roda de Breaking na Praça Santos Andrade
    c += ceu(c, '#D6E8E4', OF)
    c += ufpr(30, 60, 100)
    c += arvore(10, 62, 40, '#7E9A84', '#5B4A3F') + arvore(150, 62, 42, VD, '#5B4A3F')
    c += R(0, 60, 160, 30, '#DDD5C5') + E(80, 80, 32, 6, 'none', stroke=OF2, sw=1)
    c += breaker(80, 80, 22, GR, roupa=CO)
    c += R(100, 78, 10, 6, GR2, rx=1) + C(102.6, 81, 1.8, OF2) + C(107.4, 81, 1.8, OF2) + L(102, 78, 104, 75.6, GR2, .5) + L(108, 78, 106, 75.6, GR2, .5) + L(104, 75.6, 106, 75.6, GR2, .5)
    c += pessoa(34, 86, 28, GR, 'braco', lado=1) + pessoa(46, 80, 24, GR2, cabelo=GR2) + pessoa(120, 80, 24, GR2, 'cintura')
    c += pessoa(130, 86, 28, GR, 'bracos', roupa=TE) + pessoa(60, 92, 26, GR2) + pessoa(146, 92, 30, GR)


def rua_2(c, rng):  # Batalha de Rima do Largo (à noite)
    c += ceu(c, '#08201F', '#0F3D3F')
    for x, w, h, cor in ((-2, 30, 44, '#0C2A2B'), (28, 26, 38, '#11393A'), (54, 34, 46, '#0C2A2B'), (88, 28, 40, '#11393A'), (116, 46, 44, '#0C2A2B')):
        c += R(x, 74 - h, w, h, cor)
        for k in range(int(w / 8)):
            c += janela_arco(x + 2.6 + k * 8, 74 - h + 6, 4, 8, WA, rng.choice([.15, .6, .2, .75]))
    c += R(0, 72, 160, 18, '#0A1F20')
    c += C(136, 30, 14, WA, .14) + L(136, 84, 136, 31, NI, 1) + R(133.6, 28, 4.8, 3, NI, rx=1) + C(136, 31.6, 1.8, WA)
    c += pessoa(66, 80, 30, NI, 'mic', lado=1) + gorro(66, 80, 30, CO) + pessoa(94, 80, 31, NI, 'mic', lado=-1) + gorro(94, 80, 31, AM)
    c += plateia(rng, 85, -4, 164, 3.6, '#0A2627', .35)
    c += plateia(rng, 93, -6, 166, 5, NI, .5)
    for _ in range(5):
        c += R(rng.uniform(4, 156), rng.uniform(56, 70), 1.4, 2.2, OF2, rx=.3, op=.9)


def rua_3(c, rng):  # Mutirão de Grafite no Rebouças
    c += ceu(c, '#D6E8E4', OF)
    c += R(0, 22, 160, 46, '#D9D3C4') + R(0, 21, 160, 2, SA3)
    c += P('M10 60C6 40 22 28 36 34C48 26 62 36 58 50C66 60 52 66 40 62C30 70 14 68 10 60z', CO) + P('M10 60C6 40 22 28 36 34C48 26 62 36 58 50C66 60 52 66 40 62C30 70 14 68 10 60z', stroke=GR, sw=.9)
    c += P('M54 42Q74 28 94 42T134 42V56Q114 46 94 56T54 56z', TE) + P('M54 42Q74 28 94 42T134 42V56Q114 46 94 56T54 56z', stroke=GR, sw=.9)
    c += C(132, 36, 9, AM) + P('M123 36a9 9 0 1 1 18 0a9 9 0 1 1 -18 0', stroke=GR, sw=.9) + C(129, 34, 1.6, GR) + C(135, 34, 1.6, GR) + P('M128 39q4 3 8 0', stroke=GR, sw=.9)
    c += G([L(x, y, x, y + rng.uniform(2, 6), cor, .7) for x, y, cor in ((20, 64, CO), (30, 66, CO), (70, 55, TE), (100, 56, TE), (118, 51, TE))])
    c += P('M146 30h10v24h-10z', stroke=GR, sw=.4, op=.35) + L(146, 42, 156, 42, GR, .3, op=.35)
    c += R(0, 68, 160, 22, '#CFC6B4') + L(0, 70, 160, 70, SA3, .6)
    c += L(112, 88, 118, 40, '#8C8F7E', .9) + L(122, 88, 128, 40, '#8C8F7E', .9) + G([L(112 + (88 - y) / 8, y, 122 + (88 - y) / 8, y, '#8C8F7E', .7) for y in range(46, 88, 7)])
    c += pessoa(48, 88, 32, GR, 'braco', lado=1, roupa=DG)
    hx, hy = mao(48, 88, 32, 'braco', 1)
    c += R(hx - .8, hy - 3, 1.8, 3.4, CO, rx=.4)
    c += pessoa(122, 66, 22, GR2, 'braco', lado=-1, roupa=TE)
    hx, hy = mao(122, 66, 22, 'braco', -1)
    c += R(hx - .8, hy - 2.6, 1.6, 3, AM, rx=.4)
    c += pessoa(150, 90, 28, GR) + pessoa(84, 88, 16, GR2, 'aponta', lado=1)
    c += R(66, 85, 1.8, 3.6, CO, rx=.4) + R(69, 85.4, 1.8, 3.2, TE, rx=.4) + R(72, 85, 1.8, 3.6, AM, rx=.4)


def rua_4(c, rng):  # Sessão de Skate no Barigui
    c += ceu(c, '#CFE3DF', OF)
    c += araucaria(14, 58, 34, '#4E6B58') + arvore(150, 58, 30, '#7E9A84', '#5B4A3F') + araucaria(40, 58, 26, '#5E7B66') + moitas(rng, 58, 50, 140, 3, VD2)
    c += R(0, 58, 160, 26, '#C9C6BC')
    c += P('M92 86Q138 86 138 44H152V86z', '#B5B1A6') + L(138, 44, 152, 44, WG, 1.4) + R(0, 84, 160, 6, '#BDB9AE')
    c += G([pessoa(70, 60, 26, GR, 'bracos', roupa=CO), R(59, 61, 22, 2, DG, rx=1), C(63, 63.6, 1.1, GR2), C(77, 63.6, 1.1, GR2)], tr='rotate(-14 70 52)')
    c += G([P('M86 60q6 -2 10 -8', stroke=GR2, sw=.5), P('M88 66q6 -2 10 -8', stroke=GR2, sw=.5)], op=.5)
    c += pessoa(18, 90, 26, GR, 'braco', lado=1) + pessoa(30, 88, 22, GR2, cabelo=GR2) + sentado(44, 88, 16, GR, 1, roupa=TE)
    c += R(40, 87, 12, 1.4, AM, rx=.7) + pessoa(146, 44, 14, GR2, 'braco', lado=-1)


def rua_5(c, rng):  # Street Dance na Rua XV (petit-pavé)
    c += ceu(c, '#D6E8E4', OF)
    c += predios(rng, 54, -2, 162, 18, 40, '#C9C2B3', janela='#9AA3A0', prob=.5)
    c += petit_pave(c, 54, 90)
    c += pessoa(50, 84, 30, GR, 'bracos', roupa=CO) + pessoa(70, 82, 28, GR, 'aponta', lado=1, roupa=TE, cabelo=GR)
    c += pessoa(96, 84, 31, GR, 'braco', lado=-1, roupa=AM) + pessoa(116, 82, 28, GR, 'cintura', roupa=DG3)
    c += pessoa(14, 90, 30, GR2) + pessoa(150, 90, 30, GR2, cabelo=GR2) + pessoa(138, 88, 22, GR2, 'braco', lado=-1)


def sarau_1(c, rng):  # Microfone Aberto no Quintal da Casa Amarela
    c += ceu(c, '#173F41', '#2B5D5F')
    c += estrelas(rng, 18, 0, 16)
    c += R(0, 20, 160, 52, '#E8B65E') + R(0, 18, 160, 3, '#B97A3A')
    c += R(16, 30, 18, 16, WA) + R(14, 30, 2.4, 16, DG3) + R(34, 30, 2.4, 16, DG3) + L(25, 30, 25, 46, '#B97A3A', .6)
    c += R(120, 32, 16, 40, '#5B4A3F') + C(133, 52, .8, AM)
    c += luzinhas(-2, 20, 162, 22, 18, 18, '#5B4A3F', WA)
    for x in (48, 104, 150):
        c += vaso(x, 72, 7, TC) + E(x - 2, 64, 2, 4, VD3, tr=f'rotate(-20 {x - 2} 64)') + E(x + 2, 63, 2, 4.6, VD, tr=f'rotate(18 {x + 2} 63)')
    c += R(0, 72, 160, 18, '#9C8064')
    c += pessoa(80, 76, 30, GR, 'mic', lado=1, roupa=DG) + R(70, 59, 4.4, 5.6, OF2, tr='rotate(-10 72 62)') + L(77, 59.6, 72.5, 61, DG, 2.4)
    c += plateia(rng, 93, -6, 166, 4.8, GR, .1)


def sarau_2(c, rng):  # Roda de Leitura Entre Elas (Biblioteca Pública)
    c += R(0, 0, 160, 62, '#EFE9DD') + R(0, 62, 160, 28, '#D6CCBB')
    c += estante(rng, 6, 8, 64, 50) + estante(rng, 90, 8, 64, 50)
    c += E(80, 78, 58, 9, CO3, .75) + E(80, 78, 50, 7, 'none', stroke=CO, sw=.5, op=.5)
    pessoas = [(40, 82, 22, 1, CO), (58, 74, 18, 1, TE), (80, 72, 18, 1, AM), (102, 74, 18, -1, DG3), (120, 82, 22, -1, WG)]
    for x, base, h, lado, roupa in pessoas:
        c += sentado(x, base, h, GR if lado > 0 else GR2, lado, roupa=roupa, braco='frente')
        bx, by = x + lado * h * .34, base - h + h * .16 * 2 + h * .24
        c += P(f'M{n(bx - 2)} {n(by - 1)}L{n(bx)} {n(by)}L{n(bx + 2)} {n(by - 1)}V{n(by - 3)}L{n(bx)} {n(by - 2)}L{n(bx - 2)} {n(by - 3)}z', OF2)


def sarau_3(c, rng):  # Feira do Livro da Santos Andrade
    c += ceu(c, '#D6E8E4', OF)
    c += ufpr(34, 56, 92)
    c += R(0, 56, 160, 34, '#DDD5C5')
    for x, w in ((4, 44), (58, 44), (112, 44)):
        c += tenda(x, 78, w, 22, OF2, SA2)
        xx = x + 2
        while xx < x + w - 2:
            bh = rng.uniform(3, 5)
            c += R(xx, 69.2 - bh, rng.uniform(1.2, 2), bh, rng.choice([CO, AM, TE, DG3, WG, BL, CO3]))
            xx += 2.2
    c += pessoa(50, 90, 26, GR, 'mesa', roupa=TE) + R(46.6, 74, 7, 4, OF2, rx=.4) + pessoa(102, 92, 28, GR2, 'aponta', lado=1, cabelo=GR2)
    c += pessoa(150, 90, 26, GR, roupa=CO)


def sarau_4(c, rng):  # Café com Autor na livraria
    c += R(0, 0, 160, 90, '#EFE9DD')
    c += estante(rng, 0, 4, 52, 56) + estante(rng, 108, 4, 52, 56)
    for x in (66, 94):
        c += L(x, 0, x, 14, GR2, .4) + P(f'M{x - 4} 18L{x - 2} 13H{x + 2}L{x + 4} 18z', DG) + C(x, 22, 9, WA, .25)
    c += R(0, 60, 160, 30, '#CFC2AE')
    c += pessoa(80, 70, 30, GR, 'mesa', roupa=CO, cabelo=GR)
    c += R(54, 57, 52, 3, MAD) + R(56, 60, 48, 10, MAD2)
    c += pedestal(70, 57, 8, GR2, .4) + R(88, 53.4, 3, 3.6, OF2, rx=.4) + P('M89 52q-1 -2 0 -3M90.6 52q-1 -2 0 -3', stroke=SA3, sw=.4)
    c += R(94, 54.6, 8, 1.2, CO) + R(94.6, 53.4, 7, 1.2, TE) + R(95, 52.2, 6.4, 1.2, AM)
    c += plateia(rng, 93, -6, 166, 4.8, GR, .05)


def sarau_5(c, rng):  # Sarau Noturno no Porão do Largo
    c += R(0, 0, 160, 90, '#0D1B1B')
    for x in (0, 54, 108):
        c += P(f'M{x} 0H{x + 52}V62H{x + 46}V26Q{x + 26} 6 {x + 6} 26V62H{x}z', '#1A2A2A')
    c += tijolos(OF, .04, 0, 62)
    c += cone(c, 80, 0, 64, 96, 66, WA, .26) + E(80, 66, 16, 3, WA, .22)
    c += pessoa(80, 66, 28, NI, 'mic', lado=-1) + pedestal(76, 66, 16, NI) + R(83, 52, 4.4, 5.4, OF2, tr='rotate(8 85 55)')
    for x in (20, 50, 110, 140):
        c += E(x, 77, 8, 1.4, '#1B2626') + R(x - .6, 77, 1.2, 13, NI) + C(x, 73, 5.4, AM, .22) + R(x - .5, 73.4, 1, 3, OF2) + E(x, 72.6, .6, 1.1, WA)
    for x, r in ((6, 4.4), (32, 4.6), (64, 4.4), (96, 4.6), (124, 4.4), (154, 4.6)):
        c += busto(x, 93 + rng.uniform(-.5, .5), r, NI)


EVENTOS = {
    'shows-1': shows_1, 'shows-2': shows_2, 'shows-3': shows_3, 'shows-4': shows_4, 'shows-5': shows_5,
    'festas-1': festas_1, 'festas-2': festas_2, 'festas-3': festas_3, 'festas-4': festas_4, 'festas-5': festas_5,
    'teatro-1': teatro_1, 'teatro-2': teatro_2, 'teatro-3': teatro_3, 'teatro-4': teatro_4, 'teatro-5': teatro_5,
    'arte-1': arte_1, 'arte-2': arte_2, 'arte-3': arte_3, 'arte-4': arte_4, 'arte-5': arte_5,
    'feiras-1': feiras_1, 'feiras-2': feiras_2, 'feiras-3': feiras_3, 'feiras-4': feiras_4, 'feiras-5': feiras_5,
    'oficinas-1': oficinas_1, 'oficinas-2': oficinas_2, 'oficinas-3': oficinas_3, 'oficinas-4': oficinas_4, 'oficinas-5': oficinas_5,
    'cinema-1': cinema_1, 'cinema-2': cinema_2, 'cinema-3': cinema_3, 'cinema-4': cinema_4, 'cinema-5': cinema_5,
    'rua-1': rua_1, 'rua-2': rua_2, 'rua-3': rua_3, 'rua-4': rua_4, 'rua-5': rua_5,
    'sarau-1': sarau_1, 'sarau-2': sarau_2, 'sarau-3': sarau_3, 'sarau-4': sarau_4, 'sarau-5': sarau_5,
}


# ── avatares dos nichos (quadrados, mostrados em círculo) ───────────────────────────────────────────────────────────
def n_shows(c, rng):
    c += ceu(c, DG, DG3) + C(50, 38, 30, CO, .22) + cone(c, 50, 0, 26, 74, 100, WA, .2)
    c += pessoa(50, 104, 76, NI) + violao(46, 68, 9.4, CO, ang=-26, boca=NI)


def n_festas(c, rng):
    c += ceu(c, '#0A1E20', DG) + brilho(c, 50, 30, 50, 34, CO, .35)
    c += confete(rng, 60, 0, 100, 0, 90, [CO, AM, TE2, OF, CO3])
    c += busto(32, 106, 11, NI, 2) + busto(70, 108, 12, NI, 1, lado=-1)


def n_teatro(c, rng):
    c += R(0, 0, 100, 100, NI2) + cone(c, 50, 0, 28, 72, 92, WA, .3) + E(50, 92, 22, 4, WA, .3)
    for lado in (1, -1):
        x0 = 0 if lado == 1 else 100
        c += P(f'M{x0} 0H{x0 + lado * 22}Q{x0 + lado * 16} 50 {x0 + lado * 26} 100H{x0}z', '#B8412C')
        c += P(f'M{x0 + lado * 8} 0Q{x0 + lado * 5} 50 {x0 + lado * 11} 100', stroke='#9E3524', sw=1.6)
    c += pessoa(50, 94, 60, NI, 'mic', lado=1)


def n_arte(c, rng):
    c += R(0, 0, 100, 100, '#EFECE5') + R(0, 76, 100, 24, '#D3CBBB')
    c += quadro(16, 14, 50, 44, R(18, 16, 46, 40, DG) + C(46, 30, 9, CO) + R(22, 42, 22, 10, AM), esp=1.8)
    c += pessoa(72, 102, 58, GR, roupa=TE)


def n_feiras(c, rng):
    c += ceu(c, '#D2E6E2', OF) + R(0, 78, 100, 22, '#E2DACB')
    c += bandeirinhas(-2, 14, 102, 14, 12, 12, [CO, AM, TE, OF2], tam=5)
    c += food_truck(10, 84, 62, 34, CO, OF2, CO3)
    c += pessoa(86, 104, 40, GR)


def n_oficinas(c, rng):
    c += ceu(c, '#EADFCF', '#E2D2BD') + R(0, 78, 100, 22, '#BCA58A')
    c += R(24, 74, 52, 18, GR2, rx=3) + E(50, 74, 30, 6, '#5B5B55') + E(50, 71, 20, 4, '#8C8F7E')
    c += L(4, 60, 34, 46, CO, 7) + L(96, 60, 66, 46, TE, 7)
    c += vaso(50, 71, 32, TC) + C(34, 46, 3.6, GR) + C(66, 46, 3.6, GR)
    c += G([P('M36 40q14 5 28 0', stroke=OF2, sw=.9), P('M37 52q13 4 26 0', stroke=OF2, sw=.9)], op=.6)


def n_cinema(c, rng):
    c += R(0, 0, 100, 100, '#070B0C') + brilho(c, 50, 34, 50, 34, AM, .25)
    c += R(12, 12, 76, 46, OF2, rx=3) + tela_animacao(c, 15, 15, 70, 40)
    c += busto(30, 104, 10, NI) + busto(70, 106, 10.5, NI) + pipoca(50, 98, 2.2)


def n_rua(c, rng):
    c += ceu(c, '#D6E8E4', OF) + petit_pave(c, 70, 100)
    c += breaker(50, 86, 46, GR, roupa=CO)


def n_sarau(c, rng):
    c += ceu(c, '#173F41', '#2B5D5F') + luzinhas(-2, 10, 102, 12, 14, 9, '#5B4A3F', WA, r=1.6)
    c += pessoa(50, 104, 74, GR, 'mic', lado=1, roupa=AM)
    c += R(24, 58, 12, 15, OF2, tr='rotate(-10 30 65)') + L(42, 58, 31, 63, AM, 7.6)


NICHOS = {'shows': n_shows, 'festas': n_festas, 'teatro': n_teatro, 'arte': n_arte, 'feiras': n_feiras,
          'oficinas': n_oficinas, 'cinema': n_cinema, 'rua': n_rua, 'sarau': n_sarau}


def main():
    pe = os.path.join(PASTA, 'svg', 'eventos')
    pn = os.path.join(PASTA, 'svg', 'nichos')
    os.makedirs(pe, exist_ok=True)
    os.makedirs(pn, exist_ok=True)
    for nome, f in EVENTOS.items():
        c = Cena()
        f(c, random.Random(nome))
        with open(os.path.join(pe, nome + '.svg'), 'w', encoding='utf-8') as fh:
            fh.write(c.svg(1200, 675))
    for nome, f in NICHOS.items():
        c = Cena(100, 100)
        f(c, random.Random('nicho-' + nome))
        with open(os.path.join(pn, nome + '.svg'), 'w', encoding='utf-8') as fh:
            fh.write(c.svg(320, 320))
    print(f'{len(EVENTOS)} eventos + {len(NICHOS)} nichos em {os.path.join(PASTA, "svg")}')


if __name__ == '__main__':
    main()
