# Ilustrações dos eventos e nichos

As imagens do site (`src/Agenda_Shows.UI/wwwroot/img/eventos` e `img/nichos`) saem daqui. São flat, com os bonequinhos do conceito aprovado, e não usam fotos.

## Como regerar

Rode os comandos a partir desta pasta.

**1. Gere os SVGs** (vão para `svg/eventos` e `svg/nichos`):

```bash
python gerar.py
```

**2. Abra um Chrome headless** com porta de depuração (uma vez só):

```bash
"/c/Program Files/Google/Chrome/Application/chrome.exe" --headless=new --remote-debugging-port=9333 --user-data-dir="$TEMP/umura-chrome" about:blank &
```

**3. Renderize os PNGs** (vão para `png/`, que é descartável):

```bash
node render.mjs 9333
```

**4. Grave os `.webp` no site.** Os nomes são os mesmos que o seed usa:

```bash
python webp.py
```

**Revisar tudo de uma vez:** `python folha.py folha.html` monta uma folha de contato. Para virar imagem:

```bash
node render.mjs 9333 png folha.html 1600 1900 folha.png
```

Depois de trocar as imagens, faça rebuild e restart do site. O `MapStaticAssets` muda a impressão digital dos arquivos, então o navegador não fica com a imagem velha no cache.

## Onde mexer

- **Uma cena:** cada evento tem uma função em `gerar.py` (`shows_1`, `festas_3`, …). O nome do arquivo é a chave em `EVENTOS`.
- **Um bonequinho:** `pessoa` (de pé), `busto` (plateia), `sentado`, `deitado`, `breaker`. As poses são `pe`, `bracos`, `braco`, `aponta`, `mic`, `cintura` e `mesa`.
- **A paleta:** fica no topo do arquivo. Deep Green, Coral, Off White, Worker Green e Premium Grey são da marca. O resto é apoio.
