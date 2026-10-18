/* ============================================================================
   umura-motion.js — linguagem de movimento do UMURA (GSAP)
   ----------------------------------------------------------------------------
   Uso no app: copie para wwwroot/js/umura-motion.js e carregue no App.razor
   DEPOIS de gsap.min.js + ScrollTrigger.min.js (+ Flip.min.js) e ANTES de
   blazor.web.js, via @Assets["js/umura-motion.js"].

   API declarativa (o HTML só marca, o módulo liga):
     data-um-motion="hero"          + filhos [data-um-hero-item]
     data-um-motion="reveal"        (bloco avulso; opcional data-um-y="16")
     data-um-motion="reveal-group"  (anima os FILHOS diretos em batch/stagger)
     data-um-motion="card-in"       (entrada única de card de formulário)
     data-um-motion="counter"       + data-um-to="12400" [data-um-from]
                                    [data-um-prefix="R$ "] [data-um-suffix]
                                    [data-um-decimals="0"] [data-um-duration]
                                    [data-um-delay="0.1"]  (stagger entre KPIs)
     data-um-motion="parallax"      + [data-um-speed="0.08"]  (desktop only)
     data-um-motion="pix-wait"      (pulso informacional 2s — só estado real)

   O kit de gráficos (Components/Shared/Graficos, P1 do plano do BI — o
   movimento pesado foi pedido e aprovado pelo dono em 17/10/2026). Cada um
   entra UMA vez, quando chega à tela; o poll e a troca de período só mudam
   o desenho, sem repetir a entrada. O componente desenha o estado final e
   o preset só o encena:
     data-um-motion="graf-linha"    a grade se estende, a caneta corre e
                                    revela o traço, os marcos estouram, o
                                    "hoje" pulsa (só quando É hoje) e a mira
                                    segue o ponteiro com a dica
     data-um-motion="graf-spark"    o traço da sparkline se revela
     data-um-motion="graf-barras"   as barras crescem do zero, grupo a grupo
     data-um-motion="graf-pilha"    cada coluna sobe camada por camada
     data-um-motion="graf-ranking"  as linhas entram, a barra varre, o valor
                                    conta, a seta estoura
     data-um-motion="graf-medidor"  o realizado enche, a projeção avança em
                                    listras, as marcas caem no lugar
     data-um-motion="graf-calor"    as células acendem em onda diagonal

   API imperativa (interop / eventos):
     window.umMotion.scan(root?)         religa novos nós (idempotente)
     window.umMotion.cartPop(badgeEl)    badge do carrinho (back.out(2))
     window.umMotion.crossfade(a, b)     skeleton → conteúdo (250ms)
     window.umMotion.toast(el)           entra/vive 4s/sai (ou dur custom)
     window.umMotion.flipList(container, mutate)  filtro/reorder com Flip
     window.umMotion.sparkBurst(el)      burst de faíscas amarelas no elemento
     window.umMotion.sparkAmbient(el,on) faísca ocasional enquanto um modo dura
     window.umMotion.kill(root)          mata tweens/triggers de um subtree

   CSS obrigatório no app.css (estado inicial sem FOUC, respeitando reduce):
     @media (prefers-reduced-motion: no-preference) {
       [data-um-motion="reveal"], [data-um-motion="card-in"],
       [data-um-motion="reveal-group"] > *,
       [data-um-motion="hero"] [data-um-hero-item] { opacity: 0; }
     }
   ========================================================================== */
(function () {
  "use strict";

  /* Sem GSAP o módulo não anima — mas o guard de FOUC do app.css esconde
     [data-um-motion] justamente até o módulo LIGAR naquele nó. Sair daqui em
     silêncio deixaria a marcação escondida para sempre: em /login isso é a
     página inteira em branco, porque lá o nó marcado é o card do formulário.
     Então a saída é de porta ABERTA: marca tudo como ligado e devolve o
     conteúdo visível antes de desistir. */
  function liberarGuard() {
    var nos = document.querySelectorAll("[data-um-motion]");
    for (var i = 0; i < nos.length; i++) nos[i].setAttribute("data-um-bound", "1");
  }

  if (!window.gsap) {
    console.warn("[um-motion] gsap ausente — módulo inativo");
    if (document.readyState === "loading")
      document.addEventListener("DOMContentLoaded", liberarGuard);
    else liberarGuard();
    document.addEventListener("enhancedload", liberarGuard);
    return;
  }
  var gsap = window.gsap;
  var hasST = !!window.ScrollTrigger;
  if (hasST) gsap.registerPlugin(window.ScrollTrigger);
  if (window.Flip) gsap.registerPlugin(window.Flip);

  var reduce = window.matchMedia("(prefers-reduced-motion: reduce)");
  var isDesktop = function () { return window.matchMedia("(min-width: 1024px)").matches; };

  /* -------------------------------------------------- presets declarativos */
  var presets = {
    hero: function (el) {
      var items = el.querySelectorAll("[data-um-hero-item]");
      if (!items.length) return;
      var tl = gsap.timeline({ defaults: { ease: "power3.out" } });
      items.forEach(function (item, i) {
        var kind = item.dataset.umHeroItem; // "eyebrow" | "display" | "sub" | "cta" | "trust" | "media"
        if (kind === "display") {
          tl.fromTo(item, { autoAlpha: 0, y: 28, filter: "blur(6px)" },
            { autoAlpha: 1, y: 0, filter: "blur(0px)", duration: 0.7 }, i ? "-=0.15" : 0);
        } else if (kind === "cta") {
          tl.fromTo(item, { autoAlpha: 0, y: 16 },
            { autoAlpha: 1, y: 0, duration: 0.45, ease: "back.out(1.4)" }, "-=0.25");
        } else if (kind === "media") {
          tl.fromTo(item, { autoAlpha: 0, scale: 0.96 },
            { autoAlpha: 1, scale: 1, duration: 0.8 }, "<");
        } else if (kind === "feats") {
          // grupo de chips: entram staggered; o container fica visível na hora
          gsap.set(item, { autoAlpha: 1 });
          tl.fromTo(item.children, { autoAlpha: 0, y: 14 },
            { autoAlpha: 1, y: 0, duration: 0.45, stagger: 0.07 }, "-=0.2");
        } else {
          tl.fromTo(item, { autoAlpha: 0, y: 12 },
            { autoAlpha: 1, y: 0, duration: kind === "eyebrow" ? 0.4 : 0.5 }, i ? "-=0.3" : 0);
        }
      });
    },

    reveal: function (el) {
      var y = parseFloat(el.dataset.umY || "24");
      var vars = { autoAlpha: 1, y: 0, duration: 0.6, ease: "power2.out", overwrite: "auto" };
      gsap.set(el, { autoAlpha: 0, y: y });
      if (hasST) vars.scrollTrigger = { trigger: el, start: "top 88%", once: true };
      gsap.to(el, vars);
    },

    "reveal-group": function (el) {
      var kids = Array.prototype.slice.call(el.children);
      if (!kids.length) return;
      gsap.set(kids, { autoAlpha: 0, y: 24 });
      if (hasST) {
        window.ScrollTrigger.batch(kids, {
          start: "top 88%", once: true,
          onEnter: function (batch) {
            gsap.to(batch, { autoAlpha: 1, y: 0, duration: 0.6, ease: "power2.out", stagger: 0.1, overwrite: true });
          }
        });
      } else {
        gsap.to(kids, { autoAlpha: 1, y: 0, duration: 0.6, ease: "power2.out", stagger: 0.1 });
      }
    },

    "card-in": function (el) {
      gsap.fromTo(el, { autoAlpha: 0, y: 12 }, { autoAlpha: 1, y: 0, duration: 0.35, ease: "power2.out" });
    },

    counter: function (el) {
      var to = parseFloat(el.dataset.umTo || el.textContent.replace(/[^\d.,-]/g, "").replace(",", ".") || "0");
      var from = parseFloat(el.dataset.umFrom || "0");
      var dec = parseInt(el.dataset.umDecimals || "0", 10);
      var pre = el.dataset.umPrefix || "", suf = el.dataset.umSuffix || "";
      var dur = parseFloat(el.dataset.umDuration || "1.1");
      var obj = { v: from };
      var render = function () {
        el.textContent = pre + obj.v.toLocaleString("pt-BR",
          { minimumFractionDigits: dec, maximumFractionDigits: dec }) + suf;
      };
      // O zero SÓ entra na tela quando a contagem vai mesmo acontecer. Escrever o
      // "from" na hora do scan sobrescrevia o número que o servidor renderizou, e
      // num contador abaixo da dobra que o visitante nunca rola isso deixava um 0
      // MENTIROSO parado na tela (a prova social da home dizia "+0 entregues").
      var run = function () {
        render();
        gsap.to(obj, { v: to, duration: dur, ease: "power1.out", onUpdate: render, onComplete: render });
      };
      // data-um-delay é o stagger ENTRE cards de KPI (a receita P1 do painel pede
      // 100ms). Ele atrasa o DISPARO, não a duração: o número do servidor segue
      // na tela até a contagem começar, que é o que impede o zero mentiroso.
      var delay = parseFloat(el.dataset.umDelay || "0");
      var start = delay > 0 ? function () { gsap.delayedCall(delay, run); } : run;
      if (hasST) window.ScrollTrigger.create({ trigger: el, start: "top 92%", once: true, onEnter: start });
      else start();
    },

    parallax: function (el) {
      if (!hasST || !isDesktop()) return;
      var speed = parseFloat(el.dataset.umSpeed || "0.08") * 100;
      gsap.fromTo(el, { yPercent: -speed }, {
        yPercent: speed, ease: "none",
        scrollTrigger: { trigger: el.parentElement || el, start: "top bottom", end: "bottom top", scrub: 0.7 }
      });
    },

    "pix-wait": function (el) {
      el._umLoop = gsap.to(el, { opacity: 0.65, duration: 1, ease: "sine.inOut", yoyo: true, repeat: -1 });
    },

    /* ---- o kit de gráficos (helpers logo abaixo do objeto) ---- */

    /* A linha. A grade se estende e os valores do eixo deslizam; a caneta corre da esquerda revelando o traço (o
       SVG do traço é cortado por clip-path, que vale sobre qualquer fundo — uma cortina pintada precisaria da cor
       exata do que está atrás); o último ponto estoura, a linha de comparação se estende, o melhor ponto estoura
       com um anel e o rótulo sobe. O pulso do último ponto só existe quando ele é HOJE (o componente nem desenha o
       pulso fora disso): é o único laço da tela, e se mata quando o nó sai do documento. */
    "graf-linha": function (el) {
      var area = el.querySelector(".um-graf-area");
      if (!area) return;
      mira(area);
      var pontos = lerPontos(area);
      if (!pontos.length) return;
      var traco = area.querySelector(".um-graf-traco");
      var caneta = area.querySelector(".um-graf-caneta");
      var grades = el.querySelectorAll(".um-graf-grade");
      var ytxt = el.querySelectorAll(".um-graf-ytxt > span");
      var comp = el.querySelector(".um-graf-comp"), comptxt = el.querySelector(".um-graf-comptxt");
      var ultimo = el.querySelector(".um-graf-ponto-ultimo");
      var maior = el.querySelector(".um-graf-ponto-maior"), anel = el.querySelector(".um-graf-anel");
      var rot = el.querySelector(".um-graf-rot-maior > span");
      var soltos = el.querySelectorAll(".um-graf-ponto-solto");
      var baixo = el.querySelectorAll(".um-graf-datas, .um-graf-legenda");

      gsap.set(ou([comp, comptxt, ultimo, maior, rot, caneta]), { autoAlpha: 0 });
      gsap.set(ou(soltos), { autoAlpha: 0, scale: 0 });
      gsap.set(ou(grades), { scaleX: 0, transformOrigin: "0% 50%" });
      gsap.set(ou(ytxt), { autoAlpha: 0, x: -12 });
      gsap.set(ou(baixo), { opacity: 0, y: 10 });
      if (traco) traco.style.clipPath = "inset(0 100% 0 0)";

      quandoChegar(el, function () {
        // os pontos são relidos na hora da entrada: o período pode ter mudado entre ligar e chegar à tela
        var atuais = lerPontos(area);
        if (atuais.length) pontos = atuais;
        // a medida é lida UMA vez, antes do laço: ler layout a cada quadro, logo depois de escrever o clip-path,
        // forçaria um reflow por quadro
        var w = area.clientWidth, h = area.clientHeight, p = { v: 0 };
        var tl = gsap.timeline({ data: "um-graf", onComplete: function () { if (traco) traco.style.clipPath = ""; } });
        tl.to(ou(grades), { scaleX: 1, duration: 0.9, stagger: 0.07, ease: "power3.out" })
          .to(ou(ytxt), { autoAlpha: 1, x: 0, duration: 0.5, stagger: 0.06, ease: "power2.out" }, "<0.1")
          .set(ou(caneta), { autoAlpha: 1, x: 0, y: pontos[0][1] / 100 * h }, "<0.2")
          .to(p, {
            v: 100, duration: 2.1, ease: "power2.inOut",
            onUpdate: function () {
              if (traco) traco.style.clipPath = "inset(0 " + (100 - p.v).toFixed(2) + "% 0 0)";
              if (caneta) gsap.set(caneta, { x: p.v / 100 * w, y: alturaEm(pontos, p.v) / 100 * h });
            }
          }, "<")
          .to(ou(caneta), { autoAlpha: 0, scale: 2.4, duration: 0.35, ease: "power2.out" })
          .fromTo(ou(ultimo), { autoAlpha: 0, scale: 0 }, { autoAlpha: 1, scale: 1, duration: 0.55, ease: "back.out(3)" }, "<")
          .to(ou(soltos), { autoAlpha: 1, scale: 1, duration: 0.4, stagger: 0.05, ease: "back.out(2)" }, "<")
          .fromTo(ou(comp), { autoAlpha: 1, scaleX: 0, transformOrigin: "0% 50%" }, { scaleX: 1, duration: 0.9, ease: "power3.inOut" }, "<0.1")
          .to(ou(comptxt), { autoAlpha: 1, duration: 0.4 }, "-=0.3")
          .fromTo(ou(maior), { autoAlpha: 0, scale: 0 }, { autoAlpha: 1, scale: 1, duration: 0.5, ease: "back.out(4)" }, "-=0.6")
          .fromTo(ou(anel), { autoAlpha: 0.9, scale: 0.2 }, { autoAlpha: 0, scale: 3.2, duration: 0.9, ease: "power2.out" }, "<")
          .fromTo(ou(rot), { autoAlpha: 0, y: 8 }, { autoAlpha: 1, y: 0, duration: 0.45, ease: "back.out(2)" }, "<0.1")
          .to(ou(baixo), { opacity: 1, y: 0, duration: 0.45, stagger: 0.06, ease: "power2.out" }, "<")
          .add(function () {
            el.setAttribute("data-um-entrou", "1");
            var agora = el.querySelector(".um-graf-pulso");
            if (agora) pulsar(agora);
          });
      });
    },

    /* A sparkline: o traço se revela da esquerda (data-um-delay escalona os cartões de uma fileira de KPIs). */
    "graf-spark": function (el) {
      var svg = el.querySelector("svg");
      if (!svg) return;
      svg.style.clipPath = "inset(0 100% 0 0)";
      quandoChegar(el, function () {
        var p = { v: 0 };
        gsap.to(p, {
          data: "um-graf", v: 100, duration: 1.4, ease: "power2.inOut", delay: parseFloat(el.dataset.umDelay || "0"),
          onUpdate: function () { svg.style.clipPath = "inset(0 " + (100 - p.v).toFixed(2) + "% 0 0)"; },
          onComplete: function () { svg.style.clipPath = ""; }
        });
      });
    },

    /* As barras: a grade e os eixos chegam, o traço do "não medido" se estende, as barras crescem do zero com
       sobra (a negativa cresce para baixo), grupo a grupo; o grupo parcial chega inteiro e esmaece. */
    "graf-barras": function (el) {
      var barras = el.querySelectorAll(".um-graf-barra"), grupos = el.querySelectorAll(".um-graf-grupo");
      var grades = el.querySelectorAll(".um-graf-grade"), buracos = el.querySelectorAll(".um-graf-buraco");
      var ys = el.querySelectorAll(".um-graf-ytxt > span"), xs = el.querySelectorAll(".um-graf-xtxt > span");
      var baixo = el.querySelectorAll(".um-graf-legenda");
      Array.prototype.forEach.call(barras, function (r) {
        gsap.set(r, { scaleY: 0, transformOrigin: r.classList.contains("um-graf-neg") ? "50% 0%" : "50% 100%" });
      });
      gsap.set(ou(grades), { scaleX: 0, transformOrigin: "0% 50%" });
      gsap.set(ou(buracos), { scaleX: 0, transformOrigin: "0% 50%" });
      gsap.set(ou([ys, xs]), { autoAlpha: 0, y: 8 });
      gsap.set(ou(baixo), { opacity: 0, y: 8 });
      quandoChegar(el, function () {
        var tl = gsap.timeline({ data: "um-graf" });
        tl.to(ou(grades), { scaleX: 1, duration: 0.8, stagger: 0.05, ease: "power3.out" })
          .to(ou(ys), { autoAlpha: 1, y: 0, duration: 0.4, stagger: 0.05 }, "<")
          .to(ou(buracos), { scaleX: 1, duration: 0.5, stagger: 0.12, ease: "power2.out" }, "<0.2")
          .to(ou(xs), { autoAlpha: 1, y: 0, duration: 0.45, stagger: 0.08, ease: "power2.out" }, "<");
        Array.prototype.forEach.call(grupos, function (g, i) {
          var rs = g.querySelectorAll(".um-graf-barra");
          if (rs.length) tl.to(rs, { scaleY: 1, duration: 0.9, stagger: 0.14, ease: "back.out(1.6)" }, 0.35 + i * 0.16);
          if (g.classList.contains("um-graf-parcial"))
            tl.fromTo(g, { opacity: 1 }, { opacity: 0.42, duration: 0.6, clearProps: "opacity" }, ">-0.1");
        });
        tl.to(ou(baixo), { opacity: 1, y: 0, duration: 0.4 }, "-=0.4");
      });
    },

    /* A pilha: cada coluna sobe camada por camada — a de cima só cresce depois da de baixo, e cresce a partir da
       borda de baixo DELA, que é o topo da anterior. */
    "graf-pilha": function (el) {
      var colunas = el.querySelectorAll(".um-graf-coluna");
      var grades = el.querySelectorAll(".um-graf-grade"), buracos = el.querySelectorAll(".um-graf-buraco");
      var ys = el.querySelectorAll(".um-graf-ytxt > span"), xs = el.querySelectorAll(".um-graf-xtxt > span");
      var baixo = el.querySelectorAll(".um-graf-legenda");
      gsap.set(ou(el.querySelectorAll(".um-graf-camada")), { scaleY: 0, transformOrigin: "50% 100%" });
      gsap.set(ou(grades), { scaleX: 0, transformOrigin: "0% 50%" });
      gsap.set(ou(buracos), { scaleX: 0, transformOrigin: "0% 50%" });
      gsap.set(ou([ys, xs]), { autoAlpha: 0, y: 8 });
      gsap.set(ou(baixo), { opacity: 0, y: 8 });
      quandoChegar(el, function () {
        var tl = gsap.timeline({ data: "um-graf" });
        tl.to(ou(grades), { scaleX: 1, duration: 0.8, stagger: 0.05, ease: "power3.out" })
          .to(ou(ys), { autoAlpha: 1, y: 0, duration: 0.4, stagger: 0.05 }, "<")
          .to(ou(buracos), { scaleX: 1, duration: 0.5, stagger: 0.12, ease: "power2.out" }, "<0.2");
        Array.prototype.forEach.call(colunas, function (g, i) {
          var t0 = 0.3 + i * 0.18;
          Array.prototype.forEach.call(g.querySelectorAll(".um-graf-camada"), function (r, j) {
            tl.to(r, { scaleY: 1, duration: 0.55, ease: j === 0 ? "power3.out" : "power2.out" }, t0 + j * 0.38);
          });
          if (g.classList.contains("um-graf-parcial"))
            tl.fromTo(g, { opacity: 1 }, { opacity: 0.42, duration: 0.6, clearProps: "opacity" }, t0 + 1.1);
        });
        tl.to(ou(xs), { autoAlpha: 1, y: 0, duration: 0.45, stagger: 0.18, ease: "back.out(2)" }, 0.5)
          .to(ou(baixo), { opacity: 1, y: 0, duration: 0.4 }, ">-0.2");
      });
    },

    /* O ranking: as linhas entram da esquerda, a barra varre, o valor conta e a seta da variação estoura. */
    "graf-ranking": function (el) {
      var linhas = el.querySelectorAll(".um-graf-rank-linha");
      if (!linhas.length) return;
      // opacidade, não visibility: a lista É a versão acessível do dado, e o leitor de tela não pode achá-la vazia
      // enquanto o gráfico não entra na tela
      gsap.set(linhas, { opacity: 0, x: -30 });
      gsap.set(ou(el.querySelectorAll(".um-graf-rank-barra")), { scaleX: 0, transformOrigin: "0% 50%" });
      gsap.set(ou(el.querySelectorAll(".um-graf-rank-delta")), { opacity: 0, scale: 0.3 });
      quandoChegar(el, function () {
        var tl = gsap.timeline({ data: "um-graf" });
        Array.prototype.forEach.call(linhas, function (ln, i) {
          var d = i * 0.13;
          tl.to(ln, { opacity: 1, x: 0, duration: 0.6, ease: "power3.out" }, d);
          var barra = ln.querySelector(".um-graf-rank-barra");
          if (barra) tl.to(barra, { scaleX: 1, duration: 1.2, ease: "expo.out" }, d + 0.15);
          var conta = contar(ln.querySelector("[data-um-to]"));
          if (conta) tl.add(conta, d);
          var delta = ln.querySelector(".um-graf-rank-delta");
          if (delta) tl.to(delta, { opacity: 1, scale: 1, duration: 0.5, ease: "back.out(3)" }, d + 0.9);
        });
      });
    },

    /* O medidor: o número conta, o realizado enche, a projeção avança em listras, as marcas caem no lugar e o
       alerta (se houver) treme uma vez. As listras andam alguns ciclos e PARAM: o laço da tela é o "hoje" da
       linha, e um segundo laço disputaria o olho com ele. */
    "graf-medidor": function (el) {
      var feito = el.querySelector(".um-graf-med-feito"), ritmo = el.querySelector(".um-graf-med-ritmo");
      var listras = el.querySelector(".um-graf-med-listras"), alerta = el.querySelector(".um-graf-med-alerta");
      var marcas = el.querySelectorAll(".um-graf-med-marca"), legenda = el.querySelectorAll(".um-graf-med-legenda > span");
      gsap.set(ou([feito, ritmo]), { scaleX: 0, transformOrigin: "0% 50%" });
      gsap.set(ou(marcas), { autoAlpha: 0, y: -22 });
      gsap.set(ou(legenda), { autoAlpha: 0, y: 10 });
      quandoChegar(el, function () {
        var tl = gsap.timeline({ data: "um-graf" });
        var conta = contar(el.querySelector("[data-um-to]"));
        if (conta) tl.add(conta, 0);
        tl.to(ou(feito), { scaleX: 1, duration: 1.4, ease: "power3.inOut" }, 0)
          .to(ou(ritmo), { scaleX: 1, duration: 1.1, ease: "power2.out" }, ">-0.15")
          .to(ou(marcas), { autoAlpha: 1, y: 0, duration: 0.55, stagger: 0.15, ease: "bounce.out" }, "<0.2")
          .to(ou(legenda), { autoAlpha: 1, y: 0, duration: 0.4, stagger: 0.1 }, "<0.3");
        if (alerta) tl.to(alerta, { keyframes: { x: [-5, 5, -3, 3, 0] }, duration: 0.5, ease: "power1.inOut" });
        if (listras) tl.add(function () {
          // a listra mede 9 px: andar 9 px é voltar ao mesmo desenho, então o laço não salta nem no fim
          gsap.fromTo(listras, { x: 0 }, { data: "um-graf", x: 9, duration: 0.9, ease: "none", repeat: 5 });
        });
      });
    },

    /* O mapa de calor: os cabeçalhos chegam e as células acendem em onda diagonal, da 1ª linha para a última. */
    "graf-calor": function (el) {
      var linhas = el.querySelectorAll("tbody tr"), celulas = el.querySelectorAll("tbody td"), cabecas = el.querySelectorAll("th");
      if (!celulas.length) return;
      var colunas = linhas.length ? linhas[0].querySelectorAll("td").length : 1;
      // opacidade, não visibility: a tabela É o dado, e o leitor de tela não pode achá-la vazia antes da entrada
      gsap.set(celulas, { opacity: 0, scale: 0.4 });
      gsap.set(ou(cabecas), { opacity: 0, y: 8 });
      quandoChegar(el, function () {
        gsap.timeline({ data: "um-graf" })
          .to(ou(cabecas), { opacity: 1, y: 0, duration: 0.45, stagger: 0.04 })
          .to(celulas, {
            opacity: 1, scale: 1, duration: 0.55, ease: "back.out(2.2)",
            stagger: { grid: [linhas.length, colunas], from: "start", amount: 1.2 }
          }, "<");
      });
    }
  };

  /* ------------------------------------------- o kit de gráficos: helpers */

  // Alvo de tween que nunca está vazio. O GSAP avisa no console a cada alvo vazio ("GSAP target not found"), e um
  // gráfico sem comparação, sem buraco ou sem legenda é o caso comum — tirar o tween da timeline deslocaria os
  // vizinhos, que se posicionam pelo anterior ("<", "-=0.3"). O fantasma é um nó solto: animá-lo não custa nada.
  var fantasma = document.createElement("i");
  function ou(alvos) {
    var lista = [];
    (function juntar(a) {
      if (!a) return;
      if (a.nodeType === 1) { lista.push(a); return; }
      if (typeof a.length === "number") for (var i = 0; i < a.length; i++) juntar(a[i]);
    })(alvos);
    return lista.length ? lista : fantasma;
  }

  // A entrada toca quando o topo do gráfico passa de 85% da altura da tela — por IntersectionObserver, não por
  // ScrollTrigger: o ScrollTrigger guarda o ponto de partida calculado no instante em que liga, e o /dashboard carrega
  // por partes (o cartão "Hoje" chega antes ou depois da série). Medido no celular: o gráfico ligava a 543 px e descia a
  // 774 px; se o layout acima ENCOLHESSE depois de ligar, ele subiria para a tela e ficaria vazio até alguém rolar. O
  // observador recalcula a cada quadro de layout. Com o movimento reduzido ligado nesse meio-tempo, não toca.
  var observadores = [];
  function quandoChegar(el, fn) {
    if (!("IntersectionObserver" in window)) { fn(); return; }
    var io = new IntersectionObserver(function (entradas) {
      for (var i = 0; i < entradas.length; i++) {
        if (!entradas[i].isIntersecting) continue;
        io.disconnect();
        if (!reduce.matches) fn();
        return;
      }
    }, { rootMargin: "0px 0px -15% 0px" });
    io.observe(el);
    observadores.push({ el: el, io: io });
  }

  // o gráfico que saiu do documento antes de entrar na tela não segura o observador para sempre
  function soltaObservadoresOrfaos() {
    observadores = observadores.filter(function (o) {
      if (document.contains(o.el)) return true;
      o.io.disconnect();
      return false;
    });
  }

  // O contador do kit. Ele escreve o texto do nó — por isso o componente põe @key pelo VALOR em todo nó com
  // data-um-to (nó escrito por JS sai do diff do Blazor, e o valor seguinte nunca apareceria) — e termina no texto
  // que o servidor escreveu, não num número formatado aqui.
  // Os contadores no meio da conta: se o movimento reduzido for ligado com a página aberta, o applyReduce mata a timeline
  // e o onComplete não roda — o número ficaria parado num valor do meio. Daqui ele volta ao texto do servidor.
  var contando = [];
  function contar(el) {
    if (!el) return null;
    var alvo = parseFloat(el.dataset.umTo);
    if (isNaN(alvo)) return null;
    var casas = parseInt(el.dataset.umDecimals || "0", 10);
    var pre = el.dataset.umPrefix || "", suf = el.dataset.umSuffix || "", final = el.textContent, o = { v: 0 };
    var escrever = function () {
      el.textContent = pre + o.v.toLocaleString("pt-BR", { minimumFractionDigits: casas, maximumFractionDigits: casas }) + suf;
    };
    var registro = { el: el, final: final };
    return gsap.to(o, {
      v: alvo, duration: parseFloat(el.dataset.umDuration || "1.2"), ease: "expo.out",
      onStart: function () { contando.push(registro); escrever(); },
      onUpdate: escrever,
      onComplete: function () {
        el.textContent = final;
        contando = contando.filter(function (c) { return c !== registro; });
      }
    });
  }

  // Os pontos da linha, como o servidor os escreveu: [x %, y %, rótulo, valor, detalhe] — só os medidos.
  function lerPontos(area) {
    try {
      var p = JSON.parse(area.getAttribute("data-pontos") || "[]");
      return Array.isArray(p) ? p : [];
    } catch (e) { return []; }
  }

  // A altura do traço num x (0–100) da largura, interpolada entre os pontos — é por onde a caneta passa.
  function alturaEm(pontos, x) {
    if (x <= pontos[0][0]) return pontos[0][1];
    for (var i = 1; i < pontos.length; i++) {
      if (x <= pontos[i][0]) {
        var a = pontos[i - 1], b = pontos[i], t = (x - a[0]) / ((b[0] - a[0]) || 1);
        return a[1] + (b[1] - a[1]) * t;
      }
    }
    return pontos[pontos.length - 1][1];
  }

  function pulsar(pulso) {
    pulso.setAttribute("data-um-pulsando", "1");
    gsap.fromTo(pulso, { autoAlpha: 0.7, scale: 1 }, {
      data: "um-graf", autoAlpha: 0, scale: 3, duration: 1.6, ease: "power1.out", repeat: -1, repeatDelay: 0.4,
      // laço infinito num nó que o Blazor pode trocar: ele mesmo confere se ainda está no documento
      onRepeat: function () { if (!document.contains(pulso)) this.kill(); }
    });
  }

  /* A mira da linha: segue o ponteiro, gruda no ponto medido mais perto e mostra o valor na dica. O quickTo anda
     em PIXELS (ele não leva unidade), então a posição em % do servidor vira px pela largura da área na hora. A dica
     mora em cima do ponto e, perto do topo, vira para baixo. No toque, a dica fica um instante depois que o dedo
     sai — senão ela sumiria no mesmo gesto que a abriu. Os pontos são relidos a cada entrada: o poll e a troca de
     período mudam a série sem recriar a área. A dica é escrita por textContent (o rótulo pode vir de cadastro). */
  function ligaMiras(root) {
    var areas = root.querySelectorAll('[data-um-motion="graf-linha"] .um-graf-area:not([data-um-mira])');
    for (var i = 0; i < areas.length; i++) mira(areas[i]);
  }

  function mira(area) {
    if (area.hasAttribute("data-um-mira")) return;   // o scan e o preset chamam; liga uma vez só
    var linha = area.querySelector(".um-graf-mira"), marca = area.querySelector(".um-graf-mira-ponto");
    var dica = area.querySelector(".um-graf-dica");
    if (!linha || !marca || !dica) return;
    area.setAttribute("data-um-mira", "1");
    var forte = document.createElement("b"), rotulo = document.createElement("span"), det = document.createElement("span");
    det.className = "um-graf-dica-det";
    dica.textContent = "";
    dica.appendChild(forte); dica.appendChild(rotulo); dica.appendChild(det);
    // com movimento reduzido a mira salta para o ponto em vez de deslizar, e aparece/some sem esmaecer
    var desliza = reduce.matches ? 0 : 1;
    var irX = gsap.quickTo(linha, "x", { duration: 0.25 * desliza, ease: "power3.out" });
    var irMx = gsap.quickTo(marca, "x", { duration: 0.25 * desliza, ease: "power3.out" });
    var irMy = gsap.quickTo(marca, "y", { duration: 0.25 * desliza, ease: "power3.out" });
    var irDx = gsap.quickTo(dica, "x", { duration: 0.3 * desliza, ease: "power3.out" });
    var irDy = gsap.quickTo(dica, "y", { duration: 0.3 * desliza, ease: "power3.out" });
    var pontos = [], aberta = false, adiada = null;

    function maisPerto(xPct) {
      var melhor = pontos[0], d = Infinity;
      for (var i = 0; i < pontos.length; i++) {
        var di = Math.abs(pontos[i][0] - xPct);
        if (di < d) { d = di; melhor = pontos[i]; }
      }
      return melhor;
    }

    function mover(ev, ja) {
      if (!pontos.length) return;
      var b = area.getBoundingClientRect();
      if (!b.width) return;
      var p = maisPerto((ev.clientX - b.left) / b.width * 100);
      var px = p[0] / 100 * b.width, py = p[1] / 100 * b.height;
      forte.textContent = p[3];
      rotulo.textContent = p[2];
      det.textContent = p[4] || "";
      det.style.display = p[4] ? "" : "none";
      var meia = dica.offsetWidth / 2 + 4;
      var vira = py < 64;
      // a âncora da dica é reposta a cada passo: o applyReduce limpa o transform dos filhos do gráfico
      gsap.set(dica, { xPercent: -50, yPercent: vira ? 0 : -100 });
      var dx = Math.max(meia, Math.min(b.width - meia, px)), dy = vira ? py + 16 : py - 16;
      // na entrada a mira nasce no lugar (o 2º argumento é o ponto de partida); depois, ela desliza
      if (ja) { irX(px, px); irMx(px, px); irMy(py, py); irDx(dx, dx); irDy(dy, dy); }
      else { irX(px); irMx(px); irMy(py); irDx(dx); irDy(dy); }
    }

    function abrir(ev) {
      if (adiada) { adiada.kill(); adiada = null; }
      pontos = lerPontos(area);
      mover(ev, !aberta);
      if (!aberta) {
        aberta = true;
        gsap.to([linha, marca, dica], { autoAlpha: 1, duration: 0.2 * desliza, overwrite: "auto" });
      }
    }

    function fechar() {
      if (adiada) { adiada.kill(); adiada = null; }
      aberta = false;
      // a saída é mais rápida que a entrada
      gsap.to([linha, marca, dica], { autoAlpha: 0, duration: 0.15 * desliza, overwrite: "auto" });
    }

    area.addEventListener("pointerenter", function (ev) { if (ev.pointerType === "mouse") abrir(ev); });
    area.addEventListener("pointerdown", function (ev) { if (ev.pointerType !== "mouse") abrir(ev); });
    area.addEventListener("pointermove", function (ev) { if (aberta) mover(ev, false); });
    area.addEventListener("pointerleave", function (ev) {
      if (ev.pointerType === "mouse") fechar();
      else if (aberta && !adiada) adiada = gsap.delayedCall(2.5, fechar);
    });
  }

  /* ------------------------------------------------------------- núcleo */
  function scan(root) {
    root = root && root.querySelectorAll ? root : document;
    // no /dashboard interativo não há enhancedload: a raiz que o Blazor trocou solta o observador aqui
    soltaObservadoresOrfaos();
    // A mira da linha é RETORNO de gesto, não decoração: liga também com movimento reduzido (aí ela salta, sem
    // deslizar). Sem ela, o celular não tem como ler o valor de um dia — o <title> nativo não aparece com o dedo.
    ligaMiras(root);
    if (reduce.matches) return;
    // O pulso do "hoje" que o Blazor desenhou DEPOIS da entrada (o admin foi a um intervalo que terminou antes e voltou
    // aos 30 dias: o span é outro) pulsa também — senão o ponto vivo ficaria parado até recarregar a página.
    var pulsos = document.querySelectorAll('[data-um-motion="graf-linha"][data-um-entrou] .um-graf-pulso:not([data-um-pulsando])');
    for (var p = 0; p < pulsos.length; p++) pulsar(pulsos[p]);
    var nodes = root.querySelectorAll("[data-um-motion]:not([data-um-bound])");
    Array.prototype.forEach.call(nodes, function (el) {
      el.dataset.umBound = "1";
      var fn = presets[el.dataset.umMotion];
      if (fn) try { fn(el); } catch (e) { console.warn("[um-motion]", e); }
    });
  }

  function killOrphans() {
    soltaObservadoresOrfaos();
    if (!hasST) return;
    window.ScrollTrigger.getAll().forEach(function (st) {
      if (st.trigger && !document.contains(st.trigger)) st.kill();
    });
  }

  function kill(root) {
    root = root || document;
    if (hasST) window.ScrollTrigger.getAll().forEach(function (st) {
      if (st.trigger && root.contains(st.trigger)) st.kill();
    });
    gsap.killTweensOf(root.querySelectorAll ? root.querySelectorAll("[data-um-motion], [data-um-hero-item]") : root);
  }

  /* ----------------------------------------------------- helpers públicos */
  function cartPop(badgeEl) {
    if (!badgeEl) return;
    if (reduce.matches) return;
    gsap.fromTo(badgeEl, { scale: 0.5 }, { scale: 1, duration: 0.4, ease: "back.out(2)", overwrite: true });
  }

  function crossfade(fromEl, toEl) {
    if (reduce.matches) { if (fromEl) fromEl.hidden = true; if (toEl) { toEl.hidden = false; gsap.set(toEl, { autoAlpha: 1 }); } return; }
    if (fromEl) gsap.to(fromEl, { autoAlpha: 0, duration: 0.25, onComplete: function () { fromEl.hidden = true; } });
    if (toEl) { toEl.hidden = false; gsap.fromTo(toEl, { autoAlpha: 0 }, { autoAlpha: 1, duration: 0.25 }); }
  }

  function toast(el, lifeMs) {
    gsap.fromTo(el, { autoAlpha: 0, y: 16 }, { autoAlpha: 1, y: 0, duration: 0.25, ease: "power2.out" });
    return setTimeout(function () {
      gsap.to(el, { autoAlpha: 0, y: -8, duration: 0.2, ease: "power2.in", onComplete: function () { el.remove(); } });
    }, lifeMs || 4000);
  }

  function flipList(container, mutate) {
    if (!window.Flip || reduce.matches) { mutate(); return; }
    var state = window.Flip.getState(container.children);
    mutate();
    window.Flip.from(state, { duration: 0.4, ease: "power2.inOut", stagger: 0.02, absolute: true,
      onEnter: function (els) { return gsap.fromTo(els, { autoAlpha: 0 }, { autoAlpha: 1, duration: 0.25 }); },
      onLeave: function (els) { return gsap.to(els, { autoAlpha: 0, duration: 0.15 }); } });
  }

  /* --------------------------------------------------------------- faíscas
     Usadas pelo interruptor do NightDay. Ligar é gesto RARO (poucas vezes por
     dia), e é aí que a regra da frequência permite encantar — o mesmo lugar do
     "Pix confirmado". Duas doses: BURST no ato de ligar e uma faísca ocasional
     (0.4Hz) enquanto o modo está ligado, que é estado REAL, não decoração.
     As partículas moram num layer `fixed` posicionado pelo rect do gatilho:
     dentro da faixa da home o overflow do container as cortaria pela metade. */
  function sparkLayer() {
    var l = document.getElementById("um-spark-layer");
    if (!l) {
      l = document.createElement("div");
      l.id = "um-spark-layer";
      l.setAttribute("aria-hidden", "true");
      document.body.appendChild(l);
    }
    return l;
  }

  function emit(cx, cy, spread, life) {
    var p = document.createElement("i");
    var size = 3 + Math.random() * 3;
    p.className = "um-spark";
    p.style.width = p.style.height = size.toFixed(1) + "px";
    p.style.left = (cx - size / 2) + "px";
    p.style.top = (cy - size / 2) + "px";
    sparkLayer().appendChild(p);

    // Leque LARGO e deslocamento CURTO: subindo muito, a faísca sai do campo de
    // visão em ~150ms e o gesto passa despercebido — ela precisa acontecer PERTO
    // do interruptor, que é a origem lógica da ação.
    var ang = -Math.PI / 2 + (Math.random() - 0.5) * spread;
    var dist = 14 + Math.random() * 34;
    gsap.fromTo(p,
      { x: 0, y: 0, scale: 0.5, autoAlpha: 1 },
      {
        x: Math.cos(ang) * dist,
        y: Math.sin(ang) * dist - 4 - Math.random() * 10,
        scale: 0.2 + Math.random() * 0.5,
        autoAlpha: 0,
        // durações VARIADAS de propósito: uniformes achatam o burst num borrão
        duration: life * (0.7 + Math.random() * 0.6),
        ease: "power2.out",
        onComplete: function () { p.remove(); }
      });
  }

  function spark(el, count, spread, life) {
    if (!el || reduce.matches || document.hidden) return;
    var r = el.getBoundingClientRect();
    if (!r.width && !r.height) return;   // fora do layout: nada a medir
    var cx = r.left + r.width / 2, cy = r.top + r.height / 2;
    for (var i = 0; i < count; i++) emit(cx, cy, spread, life);
  }

  function sparkBurst(el) { spark(el, 16, 2.4, 0.85); }

  /** Faísca ocasional enquanto o modo está ligado. O handle mora no PRÓPRIO
   *  elemento: se o Blazor trocar o nó (navegação), o tick vê que ele saiu do
   *  documento e se mata — sem registro global pra vazar. */
  function sparkAmbient(el, on) {
    if (!el) return;
    if (el.__umSparkCall) { el.__umSparkCall.kill(); el.__umSparkCall = null; }
    if (!on || reduce.matches) return;
    var tick = function () {
      if (!document.contains(el)) { el.__umSparkCall = null; return; }
      spark(el, 1, 1.1, 1.1);   // `spark` já pula com a aba oculta
      el.__umSparkCall = gsap.delayedCall(1.9 + Math.random() * 1.3, tick);
    };
    // delayedCall entra na globalTimeline — pausa junto no reconnect do SignalR
    el.__umSparkCall = gsap.delayedCall(1.2, tick);
  }

  /* ------------------------------------------------- ambiente e ganchos */
  // reduce: estado final garantido (o CSS só esconde sob no-preference, então aqui é cinto)
  function applyReduce() {
    // Só com nó marcado: o seletor sem nó fazia o GSAP avisar "target not found" no console a cada página carregada
    // com movimento reduzido antes de ter o que soltar (o /dashboard é prerender:false — nasce vazio).
    var marcados = document.querySelectorAll("[data-um-motion], [data-um-motion='hero'] [data-um-hero-item], [data-um-motion='reveal-group'] > *");
    if (marcados.length) gsap.set(marcados, { clearProps: "transform,filter", autoAlpha: 1 });
    // O kit de gráficos esconde e encolhe os FILHOS até a entrada: um gráfico abaixo da dobra que ainda não entrou
    // ficaria pela metade para sempre, porque o ScrollTrigger dele morre logo abaixo. Tudo volta ao repouso do CSS,
    // que é o desenho do servidor (o clearProps de transform também tira o atributo transform do SVG).
    gsap.globalTimeline.getChildren(true, true, true).forEach(function (t) { if (t.data === "um-graf") t.kill(); });
    contando.forEach(function (c) { c.el.textContent = c.final; });
    contando = [];
    var filhos = document.querySelectorAll("[data-um-motion^='graf-'] *");
    if (filhos.length) gsap.set(filhos, { clearProps: "opacity,visibility,transform" });
    document.querySelectorAll(".um-graf-traco, .um-graf-spark svg").forEach(function (s) { s.style.clipPath = ""; });
    if (hasST) window.ScrollTrigger.getAll().forEach(function (st) { st.kill(); });
    gsap.globalTimeline.getChildren(true, true, true).forEach(function (t) { if (t.repeat && t.repeat() === -1) t.kill(); });
  }
  if (reduce.matches) applyReduce();
  reduce.addEventListener && reduce.addEventListener("change", function (e) { if (e.matches) applyReduce(); });

  // diff do SignalR insere nós → re-scan (barato: só nós não-bound)
  new MutationObserver(function (muts) {
    for (var i = 0; i < muts.length; i++) if (muts[i].addedNodes.length) { scan(); break; }
  }).observe(document.body, { childList: true, subtree: true });

  // enhanced navigation do Blazor: limpa órfãos, religa, re-mede
  if (window.Blazor && window.Blazor.addEventListener) {
    window.Blazor.addEventListener("enhancedload", function () {
      killOrphans(); scan();
      requestAnimationFrame(function () { hasST && window.ScrollTrigger.refresh(); });
    });
  }

  // pausa o mundo durante reconexão do SignalR
  new MutationObserver(function () {
    var modal = document.getElementById("components-reconnect-modal");
    if (!modal) return;
    var showing = modal.className.indexOf("components-reconnect-show") !== -1;
    showing ? gsap.globalTimeline.pause() : gsap.globalTimeline.resume();
  }).observe(document.documentElement, { attributes: true, subtree: true, attributeFilter: ["class"] });

  // fontes mudam medidas → refresh
  if (document.fonts && document.fonts.ready && hasST) {
    document.fonts.ready.then(function () { window.ScrollTrigger.refresh(); });
  }

  window.umMotion = { scan: scan, kill: kill, killOrphans: killOrphans,
    cartPop: cartPop, crossfade: crossfade, toast: toast, flipList: flipList,
    sparkBurst: sparkBurst, sparkAmbient: sparkAmbient };

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", function () { scan(); });
  else scan();
})();
