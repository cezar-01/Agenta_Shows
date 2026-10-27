using Agenda_Shows.Entities;

namespace Agenda_Shows.Services.Demo;

/// <summary>
/// Os dados de demonstração: os nichos, as contas e os 45 eventos (5 por nicho, cada um com a sua ilustração em
/// <c>wwwroot/img/eventos</c>, gerada em <c>design/ilustracoes</c>). Os lugares são reais de Curitiba; os eventos,
/// os organizadores e as pessoas são fictícios.
/// </summary>
/// <remarks>
/// As datas são RELATIVAS: cada evento diz "hoje + N dias às HH:mm" (ou "começou há X min", para os ao vivo). A
/// <see cref="Semeadura"/> as resolve contra o relógio quando semeia e, em dev, de novo a cada boot — a demonstração
/// sempre tem evento ao vivo, hoje e em breve, não importa o dia em que for aberta.
/// </remarks>
public static class DadosDaDemo
{
    /// <summary>
    /// A chave de configuração da senha das contas de demonstração. A senha NÃO mora no código: vem dos user-secrets
    /// (dev). Sem ela a semeadura cria os nichos e os eventos, mas não cria nenhuma conta com senha conhecida.
    /// </summary>
    public const string ChaveDaSenha = "Demo:Senha";

    /// <summary>
    /// A senha SÓ do admin. A senha da demo é feita para ser compartilhada (avaliadores, a turma); se ela abrisse o admin,
    /// qualquer um mudaria papéis e veria o e-mail de todos. Em Development, sem esta chave, o admin usa a da demo.
    /// </summary>
    public const string ChaveDaSenhaDoAdmin = "Demo:SenhaAdmin";

    public sealed record Conta(string Email, string Nome, Papel Papel, string? NomeDoOrganizador = null);

    public static readonly Conta[] Contas =
    [
        new("admin@umura.dev", "Equipe UMURA", Papel.Admin, "UMURA Curadoria"),
        new("cena@umura.dev", "Marina Duarte", Papel.Organizador, "Coletivo Cena Livre"),
        new("palco@umura.dev", "Rafael Moreira", Papel.Organizador, "Produtora Palco Aberto"),
        new("feira@umura.dev", "Júlia Tanaka", Papel.Organizador, "Feira Viva Curitiba"),
        new("ana@umura.dev", "Ana Souza", Papel.Usuario),
        new("bruno@umura.dev", "Bruno Lima", Papel.Usuario),
        new("carla@umura.dev", "Carla Nunes", Papel.Usuario),
    ];

    public sealed record Nicho(string Slug, string Nome, string Descricao, int Ordem);

    public static readonly Nicho[] Nichos =
    [
        new("shows", "Shows", "Bandas, MPB, rock e música ao vivo", 1),
        new("festas", "Festas", "Baladas, DJs e pista cheia", 2),
        new("teatro", "Teatro e humor", "Peças, stand-up e improviso", 3),
        new("arte", "Arte", "Exposições, museus e galerias", 4),
        new("feiras", "Feiras", "Gastronomia, food trucks e artesanato", 5),
        new("oficinas", "Oficinas", "Cursos e workshops pra pôr a mão na massa", 6),
        new("cinema", "Cinema", "Cineclubes e sessões ao ar livre", 7),
        new("rua", "Cultura de rua", "Hip hop, batalhas, grafite e skate", 8),
        new("sarau", "Sarau", "Poesia, leitura e microfone aberto", 9),
    ];

    /// <summary>Um lote de ingresso: nome, preço em centavos, quantidade.</summary>
    public sealed record Lote(string Nome, int PrecoCentavos, int Quantidade, string? Descricao = null);

    /// <summary>
    /// Um evento de demonstração. <paramref name="Dia"/> é o deslocamento em dias a partir de hoje (Brasília) e
    /// <paramref name="Hora"/> a hora local; <paramref name="ComecouHaMin"/> (quando dado) ignora os dois e faz o evento
    /// estar AO VIVO: começou há tantos minutos.
    /// </summary>
    public sealed record EventoDemo(
        string Slug, string Nicho, string Organizador, string Foto, string Titulo, string Resumo, string Descricao,
        string Local, string Endereco, string Bairro, double Lat, double Lng,
        int Dia, string Hora, int DuracaoMin, Lote[] Lotes,
        int? ComecouHaMin = null, bool Destaque = false, int Classificacao = 0);

    private static Lote[] Gratis(int vagas = 300) => [new("Entrada gratuita", 0, vagas, "Retire o seu ingresso grátis pra garantir lugar")];

    public static readonly EventoDemo[] Eventos =
    [
        // ── Shows ──────────────────────────────────────────────────────────────────────────────────────────────────
        new("noite-pop-rock-opera-de-arame", "shows", "cena@umura.dev", "shows-1", "Noite de Pop Rock na Ópera de Arame",
            "Três bandas autorais e o palco mais bonito da cidade", "Uma noite inteira de pop rock autoral curitibano dentro da Ópera de Arame. Abertura com a banda Vento Sul, seguida de Lume e do show principal da Maré Alta. Chegue cedo: o lago em volta fica lindo no fim de tarde.",
            "Ópera de Arame", "R. João Gava, 920", "Abranches", -25.3846, -49.2768, 0, "", 210,
            [new("Pista", 6000, 400), new("Meia-entrada", 3000, 200, "Estudantes, professores e maiores de 60"), new("Camarote", 12000, 60)],
            ComecouHaMin: 50, Destaque: true, Classificacao: 14),
        new("rock-autoral-largo-da-ordem", "shows", "cena@umura.dev", "shows-2", "Rock Autoral no Largo da Ordem",
            "Banda nova, som pesado e porão cheio", "O porão do Largo recebe a turnê de lançamento do primeiro disco da Ferrugem. Show com 1h30 de repertório autoral e participação especial de músicos da cena local.",
            "Porão do Largo", "Largo da Ordem, 112", "São Francisco", -25.4273, -49.2721, 0, "21:30", 150,
            [new("Antecipado", 3500, 150), new("Na porta", 5000, 50)], Classificacao: 18),
        new("festival-pop-pedreira", "shows", "cena@umura.dev", "shows-3", "Festival Pop na Pedreira Paulo Leminski",
            "Um dia inteiro de pop brasileiro ao ar livre", "Seis atrações nacionais e locais num palco gigante na Pedreira Paulo Leminski. Praça de alimentação, área coberta e transporte especial saindo da Rodoferroviária.",
            "Pedreira Paulo Leminski", "R. João Gava, s/n", "Abranches", -25.3856, -49.2779, 5, "15:00", 480,
            [new("Pista", 18000, 3000), new("Meia-entrada", 9000, 1000), new("Front stage", 32000, 400)], Destaque: true, Classificacao: 16),
        new("som-no-terraco", "shows", "palco@umura.dev", "shows-4", "Som no Terraço: bandas independentes",
            "Fim de tarde com vista e música ao vivo", "Sessão de bandas independentes no terraço de um prédio histórico do Centro. Repertório acústico, pôr do sol e cerveja artesanal local.",
            "Terraço do Edifício Garcez", "Av. Luiz Xavier, 11", "Centro", -25.4317, -49.2706, 2, "17:00", 180,
            [new("Ingresso", 4000, 120)]),
        new("mpb-no-barigui", "shows", "palco@umura.dev", "shows-5", "MPB no Parque Barigui ao entardecer",
            "Clássicos da MPB no gramado, de graça", "Quarteto de MPB tocando Gil, Caetano, Elis e Djavan no gramado do Barigui. Leve sua canga — o evento é gratuito, mas a retirada de ingresso garante lugar na área em frente ao palco.",
            "Parque Barigui", "Av. Cândido Hartmann, s/n", "Santo Inácio", -25.4227, -49.3090, 1, "17:30", 150,
            Gratis(800)),

        // ── Festas ─────────────────────────────────────────────────────────────────────────────────────────────────
        new("baile-de-confete", "festas", "cena@umura.dev", "festas-1", "Baile de Confete: Noite Eletrônica",
            "Chuva de confete e house até o sol nascer", "A festa mais colorida do São Francisco volta com três DJs residentes, chuva de confete à meia-noite e pista até as 5h.",
            "Casarão do São Francisco", "R. Trajano Reis, 326", "São Francisco", -25.4250, -49.2740, 0, "", 360,
            [new("1º lote", 4000, 200), new("2º lote", 5500, 200)], ComecouHaMin: 30, Destaque: true, Classificacao: 18),
        new("galpao-sessions", "festas", "cena@umura.dev", "festas-2", "Galpão Sessions: DJ set",
            "Techno num galpão industrial do Rebouças", "Line-up de techno e house num galpão industrial, com sistema de som de alta potência e projeções mapeadas.",
            "Galpão 1700", "R. Brasílio Itiberê, 1700", "Rebouças", -25.4430, -49.2620, 3, "23:00", 360,
            [new("Pista", 5000, 500)], Classificacao: 18),
        new("noite-das-divas", "festas", "palco@umura.dev", "festas-3", "Noite das Divas – Flashback 2000",
            "Só hino dos anos 2000, do começo ao fim", "Britney, Beyoncé, Kelly Key e Rouge numa noite inteira de flashback. Concurso de fantasia à 1h.",
            "Clube do Batel", "Av. do Batel, 1550", "Batel", -25.4410, -49.2860, 0, "22:00", 300,
            [new("Antecipado", 3000, 300), new("VIP com open bar de soft", 8000, 80)], Classificacao: 18),
        new("festa-neon", "festas", "cena@umura.dev", "festas-4", "Festa Neon Universitária",
            "Tinta neon, balões e pop no Prado Velho", "A festa neon dos calouros, com kit de tinta fluorescente na entrada e DJ tocando pop e funk.",
            "Espaço Prado", "R. Imaculada Conceição, 1100", "Prado Velho", -25.4500, -49.2560, 6, "22:00", 300,
            [new("Estudante", 2500, 400), new("Geral", 4000, 200)], Classificacao: 18),
        new("baile-disco-groove", "festas", "palco@umura.dev", "festas-5", "Baile Disco Groove",
            "Disco, funk e soul em vinil", "Só vinil: disco, funk e soul dos anos 70 e 80, com pista de luz e espelho no teto.",
            "Bar do Largo", "Largo da Ordem, 48", "São Francisco", -25.4276, -49.2725, 1, "21:00", 300,
            [new("Ingresso", 3500, 180)], Classificacao: 18),

        // ── Teatro e humor ─────────────────────────────────────────────────────────────────────────────────────────
        new("stand-up-largo-da-ordem", "teatro", "palco@umura.dev", "teatro-1", "Noite de Stand-up no Largo",
            "Quatro comediantes da cena curitibana", "Show de stand-up com quatro comediantes locais, apresentação de 90 minutos e plateia em formato cabaré.",
            "Teatro Lala Schneider", "R. Treze de Maio, 629", "São Francisco", -25.4267, -49.2732, 0, "20:00", 90,
            [new("Inteira", 5000, 150), new("Meia-entrada", 2500, 80)], Destaque: true, Classificacao: 16),
        new("mostra-teatro-popular", "teatro", "palco@umura.dev", "teatro-2", "Mostra de Teatro Popular no Guaíra",
            "Grupos de teatro de rua no palco do Guaíra", "Uma semana de mostra com grupos de teatro popular de Curitiba e região. Hoje: \"A Rua É Nossa\", do Grupo Ventania.",
            "Teatro Guaíra – Auditório Glauco Flores", "R. XV de Novembro, 971", "Centro", -25.4330, -49.2652, 2, "19:30", 100,
            [new("Inteira", 4000, 400), new("Meia-entrada", 2000, 200)], Classificacao: 12),
        new("estreia-comedia-paiol", "teatro", "palco@umura.dev", "teatro-3", "Estreia de Comédia no Teatro Paiol",
            "Uma família, um casamento e tudo dando errado", "Estreia da nova comédia da Cia. Três Atos no charmoso Teatro Paiol, o antigo depósito de pólvora da cidade.",
            "Teatro Paiol", "Largo Guido Viaro, s/n", "Prado Velho", -25.4485, -49.2622, 4, "20:00", 80,
            [new("Inteira", 6000, 200), new("Meia-entrada", 3000, 100)], Classificacao: 12),
        new("festival-palhacaria", "teatro", "palco@umura.dev", "teatro-4", "Festival de Palhaçaria e Humor",
            "Palhaços, esquetes e risada pra família toda", "Festival com cinco números de palhaçaria e esquetes de humor físico. Livre para todas as idades.",
            "Praça Santos Andrade", "Praça Santos Andrade, s/n", "Centro", -25.4290, -49.2618, 0, "", 180,
            Gratis(500), ComecouHaMin: 40),
        new("sessao-de-improviso", "teatro", "palco@umura.dev", "teatro-5", "Sessão de Improviso: Plateia no Comando",
            "A plateia sugere, o elenco inventa na hora", "Espetáculo de teatro de improviso em que cada cena nasce de uma sugestão do público.",
            "Espaço Cênico Rebouças", "R. Engenheiros Rebouças, 1420", "Rebouças", -25.4455, -49.2640, 3, "21:00", 75,
            [new("Ingresso", 3000, 90)], Classificacao: 14),

        // ── Arte ───────────────────────────────────────────────────────────────────────────────────────────────────
        new("arte-que-conversa-mon", "arte", "feira@umura.dev", "arte-1", "Arte que Conversa: instalações no MON",
            "Instalações interativas no Museu do Olho", "Exposição de instalações interativas de artistas paranaenses, com mediação e percurso sensorial.",
            "Museu Oscar Niemeyer", "R. Mal. Hermes, 999", "Centro Cívico", -25.4101, -49.2671, 0, "", 480,
            [new("Inteira", 3000, 600), new("Meia-entrada", 1500, 300)], ComecouHaMin: 120, Destaque: true),
        new("visita-guiada-contemporanea", "arte", "feira@umura.dev", "arte-2", "Visita guiada: Arte Contemporânea",
            "Um percurso de uma hora com mediadora", "Visita guiada pelas salas de arte contemporânea com a mediadora Luiza Prates. Grupo de até 25 pessoas.",
            "Museu Oscar Niemeyer", "R. Mal. Hermes, 999", "Centro Cívico", -25.4101, -49.2671, 1, "15:00", 60,
            [new("Vaga na visita", 2000, 25)]),
        new("exposicao-azul-profundo", "arte", "feira@umura.dev", "arte-3", "Exposição Azul Profundo",
            "Pintura abstrata em grande formato", "Mostra coletiva de pintura abstrata em grandes telas, explorando o azul em todas as suas camadas.",
            "Solar do Barão", "R. Pres. Carlos Cavalcanti, 533", "Centro", -25.4295, -49.2755, 0, "10:00", 600,
            Gratis(400)),
        new("aquarelas-do-litoral", "arte", "feira@umura.dev", "arte-4", "Aquarelas do Litoral",
            "O litoral do Paraná em 40 aquarelas", "Exposição individual com 40 aquarelas que percorrem Paranaguá, Antonina e a Ilha do Mel.",
            "Museu Alfredo Andersen", "R. Mateus Leme, 336", "São Francisco", -25.4280, -49.2780, 3, "10:00", 480,
            Gratis(300)),
        new("arte-digital-imersiva", "arte", "feira@umura.dev", "arte-5", "Arte Digital Imersiva",
            "Projeções 360° no Paço da Liberdade", "Experiência imersiva com projeções 360° e trilha sonora original, em sessões de 40 minutos.",
            "Paço da Liberdade", "Praça Generoso Marques, 189", "Centro", -25.4316, -49.2717, 5, "14:00", 300,
            [new("Sessão", 3500, 200), new("Meia-entrada", 1750, 100)]),

        // ── Feiras ─────────────────────────────────────────────────────────────────────────────────────────────────
        new("food-truck-day-jardim-botanico", "feiras", "feira@umura.dev", "feiras-1", "Food Truck Day no Jardim Botânico",
            "20 food trucks e música ao vivo", "Vinte food trucks, mesas no gramado e música ao vivo ao lado do cartão-postal da cidade. Entrada gratuita.",
            "Jardim Botânico", "R. Eng. Ostoja Roguski, s/n", "Jardim Botânico", -25.4433, -49.2399, 0, "", 420,
            Gratis(1000), ComecouHaMin: 90, Destaque: true),
        new("rota-dos-food-trucks", "feiras", "feira@umura.dev", "feiras-2", "Rota dos Food Trucks no São Francisco",
            "Comida de rua e cerveja local", "Edição noturna da rota de food trucks, com foco em comida de rua latina e cervejarias de Curitiba.",
            "Praça João Cândido", "Praça João Cândido, s/n", "São Francisco", -25.4262, -49.2760, 1, "18:00", 300,
            Gratis(800)),
        new("feira-organica-passeio-publico", "feiras", "feira@umura.dev", "feiras-3", "Feira Orgânica do Passeio Público",
            "Produtor direto, sem agrotóxico", "Feira de produtores orgânicos da região metropolitana, com café da manhã colonial e oficina de compostagem.",
            "Passeio Público", "R. Pres. Faria, s/n", "Centro", -25.4253, -49.2672, 2, "08:00", 300,
            Gratis(600)),
        new("feira-artesanato-barigui", "feiras", "feira@umura.dev", "feiras-4", "Feira de Artesanato do Barigui",
            "120 artesãos e cozinha afetiva", "Cento e vinte expositores de artesanato, cerâmica, moda autoral e cozinha afetiva às margens do lago.",
            "Parque Barigui", "Av. Cândido Hartmann, s/n", "Santo Inácio", -25.4210, -49.3075, 6, "10:00", 420,
            Gratis(1500)),
        new("feira-do-largo-primavera", "feiras", "feira@umura.dev", "feiras-5", "Feira do Largo: especial de primavera",
            "A feira mais tradicional da cidade, florida", "Edição especial de primavera da Feira do Largo da Ordem, com flores, música e barracas de comida típica.",
            "Largo da Ordem", "Largo da Ordem, s/n", "São Francisco", -25.4270, -49.2718, 7, "09:00", 300,
            Gratis(2000)),

        // ── Oficinas ───────────────────────────────────────────────────────────────────────────────────────────────
        new("oficina-ceramica-iniciantes", "oficinas", "feira@umura.dev", "oficinas-1", "Oficina de Cerâmica para Iniciantes",
            "Modelagem manual e queima inclusa", "Aprenda as técnicas de modelagem manual (rolinho, placa e beliscão) e leve sua peça depois da queima. Material incluso.",
            "Ateliê Barro Vivo", "R. Paula Gomes, 410", "São Francisco", -25.4240, -49.2752, 2, "14:00", 180,
            [new("Inscrição", 12000, 12)]),
        new("workshop-pintura-solar", "oficinas", "feira@umura.dev", "oficinas-2", "Workshop de Pintura em Tela",
            "Acrílica do zero em uma tarde", "Workshop de pintura acrílica para quem nunca pegou num pincel. Tela, tintas e avental inclusos.",
            "Solar do Barão", "R. Pres. Carlos Cavalcanti, 533", "Centro", -25.4297, -49.2753, 4, "15:00", 180,
            [new("Inscrição", 9000, 20)]),
        new("aulao-danca-praca-do-japao", "oficinas", "palco@umura.dev", "oficinas-3", "Aulão Aberto de Dança de Salão",
            "Forró, samba de gafieira e salsa ao ar livre", "Aulão gratuito de dança de salão na Praça do Japão, para todos os níveis. Não precisa levar par.",
            "Praça do Japão", "Av. Sete de Setembro, s/n", "Água Verde", -25.4440, -49.2890, 0, "", 120,
            Gratis(200), ComecouHaMin: 20),
        new("torno-ceramico-intensivo", "oficinas", "feira@umura.dev", "oficinas-4", "Curso Intensivo de Torno Cerâmico",
            "Um fim de semana inteiro no torno", "Curso intensivo de torno cerâmico em dois dias, com 8 horas de prática e acompanhamento individual.",
            "Ateliê Torno & Forno", "Al. Dom Pedro II, 255", "Batel", -25.4425, -49.2848, 5, "09:00", 480,
            [new("Inscrição", 38000, 8)]),
        new("oficina-arte-em-resina", "oficinas", "feira@umura.dev", "oficinas-5", "Oficina de Arte em Resina",
            "Bandejas e porta-copos em resina", "Oficina prática de arte em resina epóxi: cada participante faz uma bandeja e um jogo de porta-copos.",
            "Memorial de Curitiba", "R. Claudino dos Santos, 79", "São Francisco", -25.4271, -49.2716, 9, "14:00", 180,
            [new("Inscrição", 15000, 15)]),

        // ── Cinema ─────────────────────────────────────────────────────────────────────────────────────────────────
        new("cineclube-suspense-cinemateca", "cinema", "palco@umura.dev", "cinema-1", "Cineclube de Suspense",
            "Hitchcock na tela grande e debate depois", "Sessão de cineclube com \"Janela Indiscreta\" e debate mediado por críticos da cidade.",
            "Cinemateca de Curitiba", "R. Carlos Cavalcanti, 1174", "São Francisco", -25.4307, -49.2810, 1, "19:30", 150,
            [new("Ingresso", 1000, 120), new("Meia-entrada", 500, 60)], Classificacao: 14),
        new("cinema-ao-ar-livre-barigui", "cinema", "palco@umura.dev", "cinema-2", "Cinema ao Ar Livre no Barigui",
            "Telão no gramado e pipoca de graça", "Sessão gratuita de cinema ao ar livre com telão inflável. Hoje: animação brasileira para toda a família.",
            "Parque Barigui", "Av. Cândido Hartmann, s/n", "Santo Inácio", -25.4235, -49.3085, 0, "19:00", 120,
            Gratis(600), Destaque: true),
        new("sessao-na-grama-praca-da-espanha", "cinema", "palco@umura.dev", "cinema-3", "Sessão na Grama da Praça da Espanha",
            "Curtas paranaenses sob as estrelas", "Mostra de curtas-metragens paranaenses projetados na Praça da Espanha. Leve sua canga.",
            "Praça da Espanha", "Praça da Espanha, s/n", "Batel", -25.4386, -49.2856, 3, "19:30", 120,
            Gratis(400)),
        new("cineclube-no-terraco", "cinema", "palco@umura.dev", "cinema-4", "Cineclube no Terraço: clássicos",
            "Clássicos do cinema ao anoitecer", "Sessão de clássicos no terraço do Centro, com cadeiras de praia e vinho da casa.",
            "Terraço Cultural XV", "R. XV de Novembro, 300", "Centro", -25.4320, -49.2700, 6, "19:00", 150,
            [new("Ingresso", 2500, 60)], Classificacao: 12),
        new("mostra-comedia-cine-passeio", "cinema", "palco@umura.dev", "cinema-5", "Mostra de Comédia Brasileira",
            "Uma semana de comédias nacionais", "Mostra com as comédias brasileiras mais queridas das últimas décadas, em sessões diárias.",
            "Cine Passeio", "R. Riachuelo, 410", "Centro", -25.4258, -49.2694, 2, "18:00", 120,
            [new("Ingresso", 1200, 150), new("Meia-entrada", 600, 80)], Classificacao: 12),

        // ── Cultura de rua ─────────────────────────────────────────────────────────────────────────────────────────
        new("roda-de-breaking-santos-andrade", "rua", "cena@umura.dev", "rua-1", "Roda de Breaking na Santos Andrade",
            "Crews da cidade numa roda aberta", "Roda aberta de breaking com DJ e crews convidadas. Quem quiser entra na roda.",
            "Praça Santos Andrade", "Praça Santos Andrade, s/n", "Centro", -25.4292, -49.2615, 0, "", 180,
            Gratis(300), ComecouHaMin: 25),
        new("batalha-de-rima-largo", "rua", "cena@umura.dev", "rua-2", "Batalha de Rima do Largo",
            "A batalha mais antiga da cidade", "Batalha de rima semanal no Largo da Ordem, com inscrição de MCs na hora e júri popular.",
            "Largo da Ordem", "Largo da Ordem, s/n", "São Francisco", -25.4275, -49.2715, 0, "20:00", 180,
            Gratis(500), Destaque: true),
        new("mutirao-de-grafite", "rua", "cena@umura.dev", "rua-3", "Mutirão de Grafite no Rebouças",
            "Um muro de 80 metros, 20 artistas", "Mutirão de grafite com vinte artistas pintando um muro de 80 metros. Aberto para assistir e conversar.",
            "Muro da Brasílio Itiberê", "R. Brasílio Itiberê, 2100", "Rebouças", -25.4460, -49.2600, 4, "10:00", 420,
            Gratis(1000)),
        new("sessao-de-skate-barigui", "rua", "cena@umura.dev", "rua-4", "Sessão de Skate no Barigui",
            "Campeonato amador e sessão livre", "Campeonato amador de skate street com categorias iniciante e amador, seguido de sessão livre.",
            "Pista de Skate do Barigui", "Parque Barigui", "Santo Inácio", -25.4195, -49.3070, 5, "13:00", 300,
            [new("Inscrição para competir", 2000, 60), new("Público", 0, 500)]),
        new("street-dance-rua-xv", "rua", "cena@umura.dev", "rua-5", "Street Dance na Rua XV",
            "Apresentação aberta de crews", "Apresentações de crews de street dance na Boca Maldita, com batalha final entre as três melhores.",
            "Boca Maldita", "R. XV de Novembro, s/n", "Centro", -25.4325, -49.2705, 1, "16:00", 150,
            Gratis(800)),

        // ── Sarau ──────────────────────────────────────────────────────────────────────────────────────────────────
        new("microfone-aberto-no-quintal", "sarau", "palco@umura.dev", "sarau-1", "Microfone Aberto no Quintal",
            "Sarau de poesia autoral, entra quem quiser", "Sarau de poesia autoral no quintal de uma casa do São Francisco. Inscrição para ler na hora.",
            "Quintal da Casa Amarela", "R. Inácio Lustosa, 280", "São Francisco", -25.4235, -49.2730, 0, "19:30", 150,
            [new("Contribuição", 1000, 80), new("Gratuito", 0, 40)]),
        new("roda-de-leitura-entre-elas", "sarau", "palco@umura.dev", "sarau-2", "Roda de Leitura Entre Elas",
            "Autoras latino-americanas em voz alta", "Roda de leitura dedicada a autoras latino-americanas. Este mês: Clarice Lispector e Gabriela Mistral.",
            "Biblioteca Pública do Paraná", "R. Cândido Lopes, 133", "Centro", -25.4300, -49.2680, 3, "18:30", 120,
            Gratis(60)),
        new("feira-do-livro-santos-andrade", "sarau", "palco@umura.dev", "sarau-3", "Feira do Livro da Santos Andrade",
            "Sebos, editoras e autógrafos", "Feira com sebos, editoras independentes e sessões de autógrafo ao longo do dia.",
            "Praça Santos Andrade", "Praça Santos Andrade, s/n", "Centro", -25.4288, -49.2620, 2, "10:00", 540,
            Gratis(2000)),
        new("cafe-com-autor", "sarau", "palco@umura.dev", "sarau-4", "Café com Autor",
            "Bate-papo e leitura na livraria", "Bate-papo com o autor do romance do mês, leitura de trechos e sessão de autógrafos.",
            "Livraria do Batel", "Av. do Batel, 1868", "Batel", -25.4420, -49.2870, 6, "17:00", 90,
            [new("Lugar garantido", 1500, 40)]),
        new("sarau-noturno-no-porao", "sarau", "palco@umura.dev", "sarau-5", "Sarau Noturno no Porão",
            "Poesia de bolso e microfone livre", "Sarau noturno no porão de um bar do Largo, com poesia de bolso e microfone livre até a meia-noite.",
            "Porão do Largo", "Largo da Ordem, 112", "São Francisco", -25.4274, -49.2723, 4, "21:00", 150,
            [new("Ingresso", 1500, 70)], Classificacao: 18),
    ];
}
