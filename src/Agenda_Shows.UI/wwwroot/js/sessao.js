// As abas do mesmo navegador combinam a sessão: cada página publica a sua marca (<meta name="um-sessao">, embaralhada no
// servidor) no localStorage. Quando OUTRA aba publica uma marca diferente — saiu, entrou com outra conta, mudou de papel —
// esta recarrega: o cookie é um só para todas, mas o circuito do Blazor de uma aba esquecida aberta continuaria com a sessão
// velha (num computador de laboratório, o admin que saiu na aba B seguia agindo na aba A).
(function () {
  'use strict';
  var CHAVE = 'umura:sessao';
  var minha = marca();
  if (!minha) return;

  function marca() {
    var meta = document.querySelector('meta[name="um-sessao"]');
    return meta && meta.content ? meta.content : null;
  }

  function publica(valor) {
    try { localStorage.setItem(CHAVE, valor); } catch (e) { /* modo privado / armazenamento bloqueado: segue sem */ }
  }

  publica(minha);

  window.addEventListener('storage', function (e) {
    if (e.key !== CHAVE || !e.newValue || e.newValue === minha) return;
    // Aba escondida também recarrega (é ela que ficou para trás); o recarregamento reconfere a sessão no servidor.
    location.reload();
  });

  // A navegação aprimorada troca o <head> (o meta muda) sem rodar este script de novo. Se a sessão mudou no caminho (papel
  // novo, conta apagada, cookie vencido), avisa as outras abas e recarrega esta — o circuito que veio junto ainda é o velho.
  // Depois do reload a marca da página e a desta aba batem, então não entra em laço.
  if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
    window.Blazor.addEventListener('enhancedload', function () {
      var nova = marca();
      if (!nova || nova === minha) return;
      minha = nova;
      publica(nova);
      location.reload();
    });
  }
})();
