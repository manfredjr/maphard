# Verificação jurídica: página do MapHard - MT, relatórios e LGPD

Data: 02/10/2026. Objeto: a página do MapHard - MT (`public/index.html` e `public/privacidade.html`, para `maphard.manfred.com.br`) e os relatórios que o programa grava (HTML, JSON e CSV). O MapHard é software livre (GPL-3.0), desenvolvido e distribuído pela MANFRED TECNOLOGIA LTDA (MT - Manfred Tecnologia). Situação: fatias 1 a 6 juntadas ao `main`, fatia 7 em código (`docs/superpowers/plans/2026-10-02-maphard-fatia-7-relatorio.md`).

Esta análise segue o formato da verificação do MapDisk - MT (`docs/legal/verificacao-distribuicao-e-lgpd-2026-09-28.md` do repositório `manfredjr/mapdisk`, lida em 02/10/2026). A forma de distribuição é a mesma. O que muda no MapHard: o programa **só lê**, não apaga nem move nada, e os relatórios trazem dados de identificação da máquina.

## 1. Contexto e fatos

- **O que o programa faz:** lê o hardware do computador em que roda (processador, memória, discos com o SMART, placa-mãe e firmware, vídeo, monitores, bateria, rede, Windows) e os eventos do log Sistema do Windows, e aponta o que pede atenção. Não altera configuração, driver, firmware, registro nem arquivo do usuário (regra 1 do produto, `AGENTS.md`).
- **O que sai da máquina:** nada. O programa não tem código de rede: a busca por `HttpClient`, `WebClient`, `WebRequest`, `Socket` e `System.Net.Http` no código, feita em 02/10/2026, não achou nenhum uso.
- **Relatórios:** o técnico grava, só quando pede, o relatório HTML para o cliente, a coleta completa em JSON e uma linha em CSV para planilha, na pasta que escolher. A linha de comando pode gravar numa pasta de rede, por roteiro de logon, em todas as estações de um cliente (R41 do desenho).
- **Dados que aparecem nos relatórios:** nome do computador (que muitas vezes traz o nome da pessoa, como "NOTE-MARIA"), fabricante, modelo, números de série do equipamento, da placa-mãe, dos discos, dos módulos de memória e do monitor, e o identificador UUID do equipamento. O programa não lê o nome do usuário do Windows nem a chave de produto (regra 9 do produto).
- **Distribuição:** de graça, pela página e pelo GitHub, para qualquer pessoa. Finalidade: divulgar a MT, como no MapNet e no MapDisk.
- **Uso pela MT:** a equipe técnica roda o programa nas máquinas de clientes, no atendimento e no inventário das estações.

## 2. Fontes e verificação

A biblioteca local da `legal-br` (clonada de `manfredjr/legal-br` em 02/10/2026) tem a última coleta em 21/08/2026 e não traz o relatório `STATUS-FONTES.md`. A checagem dela está vencida. Por isso todo texto legal abaixo foi conferido no Planalto, na data desta análise.

| Norma | Onde foi verificada | Data |
|---|---|---|
| LGPD (Lei nº 13.709/2018), arts. 4º, I, 5º, I, VI, VII e X, 6º, VI, 37, 39 e 46 | planalto.gov.br, texto compilado (`https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm`) | 02/10/2026 |
| CDC (Lei nº 8.078/1990), arts. 2º, 3º, § 2º, 6º, III, 25, 30, 37, § 1º, e 51, I | planalto.gov.br, texto compilado (`https://www.planalto.gov.br/ccivil_03/leis/l8078compilado.htm`) | 02/10/2026 |
| GPL-3.0, seção 0 (Appropriate Legal Notices) | Arquivo `LICENSE` deste repositório, texto oficial | 02/10/2026 |
| Verificação do MapDisk - MT, seções 4.6, 7 e 11 | `manfredjr/mapdisk`, `docs/legal/verificacao-distribuicao-e-lgpd-2026-09-28.md` | Lida em 02/10/2026 |

## 3. Premissas de incidência

| Premissa | O que aciona | Fundamento |
|---|---|---|
| Os relatórios podem trazer dados pessoais | LGPD, para quem grava e guarda os relatórios | O nome do computador e os números de série de um equipamento usado por uma pessoa tornam essa pessoa identificável (art. 5º, I). Gravar e guardar o relatório são operações de tratamento (art. 5º, X) |
| No atendimento e no inventário, a MT age em nome do cliente | LGPD, com a MT como operadora e o cliente como controlador | O cliente decide quais máquinas são lidas e para quê. A MT executa (art. 5º, VI e VII) |
| Na simples distribuição, a MT não trata os dados de quem baixa | A MT fica fora dos deveres de agente de tratamento quanto a esses usos | O programa não envia nada à MT. Mesma premissa das análises do MapNet e do MapDisk |
| Quem baixa de graça pode ser consumidor | CDC, se a distribuição com finalidade de divulgação for considerada remunerada de forma indireta | Premissa adotada por cautela, como no MapDisk (seção 3 da verificação dele). A questão foi respondida pelo parecer do MapNet, segundo a seção 7 da verificação do MapDisk |
| A página é controlada pela MT | LGPD, com a MT como controladora dos registros de acesso da página | A MT decide sobre a hospedagem e o Cloudflare, que registram o endereço IP de cada acesso (art. 5º, I e VI) |

## 4. Análise

### 4.1 LGPD: os relatórios e quem os guarda

**FATO LEGAL.** LGPD, art. 5º: "I - dado pessoal: informação relacionada a pessoa natural identificada ou identificável"; "X - tratamento: toda operação realizada com dados pessoais, como as que se referem a coleta, produção, recepção, classificação, utilização, acesso, reprodução, transmissão, distribuição, processamento, arquivamento, armazenamento, eliminação, avaliação ou controle da informação, modificação, comunicação, transferência, difusão ou extração". Verificado no Planalto em 02/10/2026. Incidência: o relatório de uma estação usada por uma pessoa liga o nome do computador e os números de série a ela; gravar e guardar o relatório é armazenamento.

**FATO LEGAL.** LGPD, art. 4º: "Esta Lei não se aplica ao tratamento de dados pessoais: I - realizado por pessoa natural para fins exclusivamente particulares e não econômicos". Verificado no Planalto em 02/10/2026. Incidência: alcança quem usa o programa no próprio computador de casa. Não alcança empresa nem técnico que atende cliente.

**INTERPRETAÇÃO.** O dado é pouco sensível: identifica o equipamento e, por ele, quem o usa. O cuidado que ele pede é o de qualquer documento interno do cliente: guardar onde só quem precisa vê e apagar quando não precisar mais. O programa ajuda ao não ler o nome do usuário do Windows nem a chave de produto, e ao gravar só quando o técnico pede.

### 4.2 LGPD: a MT no atendimento e no inventário por roteiro de logon

**FATO LEGAL.** LGPD, art. 5º, "VI - controlador: pessoa natural ou jurídica, de direito público ou privado, a quem competem as decisões referentes ao tratamento de dados pessoais; VII - operador: pessoa natural ou jurídica, de direito público ou privado, que realiza o tratamento de dados pessoais em nome do controlador". Art. 39: "O operador deverá realizar o tratamento segundo as instruções fornecidas pelo controlador, que verificará a observância das próprias instruções e das normas sobre a matéria." Art. 37: "O controlador e o operador devem manter registro das operações de tratamento de dados pessoais que realizarem, especialmente quando baseado no legítimo interesse." Verificados no Planalto em 02/10/2026. Incidência: a MT, ao rodar o programa nas estações do cliente e gravar os relatórios a pedido dele, trata dados em nome do cliente.

**FATO LEGAL.** LGPD, art. 46: "Os agentes de tratamento devem adotar medidas de segurança, técnicas e administrativas aptas a proteger os dados pessoais de acessos não autorizados e de situações acidentais ou ilícitas de destruição, perda, alteração, comunicação ou qualquer forma de tratamento inadequado ou ilícito." Verificado no Planalto em 02/10/2026. Incidência: vale para a MT como operadora e para o cliente como controlador.

**INTERPRETAÇÃO.** O inventário por roteiro de logon lê todas as estações do cliente de uma vez e junta os arquivos numa pasta de rede. É o caso de maior volume. Pede instrução do cliente antes (art. 39) e uma pasta com acesso restrito (art. 46). O programa grava um arquivo por máquina, com o nome do computador e a data (decisão do Manfred no plano da fatia 7), o que serve de registro do que foi lido e quando (art. 37).

**RECOMENDAÇÃO de processo, fora do programa.** No atendimento da MT: registrar na ordem de serviço o pedido do cliente para o inventário, com as máquinas ou o grupo de máquinas; gravar na pasta de rede que o cliente indicar, com acesso só da equipe de TI dele e da MT; e entregar o relatório só ao contato que pediu o serviço.

### 4.3 CDC: frases da página que vinculam

**FATO LEGAL.** CDC, art. 30: "Toda informação ou publicidade, suficientemente precisa, veiculada por qualquer forma ou meio de comunicação com relação a produtos e serviços oferecidos ou apresentados, obriga o fornecedor que a fizer veicular ou dela se utilizar e integra o contrato que vier a ser celebrado." Art. 37, § 1º: "É enganosa qualquer modalidade de informação ou comunicação de caráter publicitário, inteira ou parcialmente falsa, ou, por qualquer outro modo, mesmo por omissão, capaz de induzir em erro o consumidor a respeito da natureza, características, qualidade, quantidade, propriedades, origem, preço e quaisquer outros dados sobre produtos e serviços." Art. 6º, III, direito básico à "informação adequada e clara sobre os diferentes produtos e serviços, com especificação correta de quantidade, características, composição, qualidade, tributos incidentes e preço, bem como sobre os riscos que apresentem". Verificados no Planalto em 02/10/2026. Incidência: sob a premissa de relação de consumo na distribuição (seção 3), cada frase precisa da página sobre o que o programa faz vale como oferta.

**INTERPRETAÇÃO.** Cada frase da página precisa corresponder ao que o programa faz e, quando possível, a um teste:

| Frase | Situação |
|---|---|
| "O MapHard só lê: não altera nada no computador" | Fica. Regra 1 do produto. Os únicos arquivos gravados são os relatórios que o técnico pede |
| "Não envia nenhuma informação pela internet" | Fica, com um teste que confira a ausência de código de rede, como o `SemRedeTestes` do MapDisk (seção 11 da verificação dele) |
| "Não instala nada: roda de um pendrive ou de uma pasta de rede" | Fica. Regra 5 do produto |
| "Campo que não pôde ser lido aparece com o motivo, nunca como zero" | Fica. Regra 2 do produto, conferida por testes |
| Verificação do Windows 11 | Fica com o alcance exato: confere os requisitos pela lista de processadores publicada pela Microsoft para a versão indicada, e o processador fora da lista aparece como "não consta na lista do MapHard", não como "não aceita". Precisa dizer que a ferramenta oficial da Microsoft é o PC Integridade do Computador, para não prometer mais que ela |
| Saúde dos discos | Fica com o alcance exato: as regras leem o que o próprio disco informa no SMART. Não é previsão de falha. Um disco em "Bom" pode falhar, e a página não pode sugerir o contrário (art. 37, § 1º, alcança a omissão) |
| Desgaste da bateria | Fica com o alcance exato: estimativa a partir das capacidades que a bateria informa |
| Requisitos (Windows 10 e 11, 64 bits) | Fica. Regra 8 do produto |

**RISCO.** Uma frase como "descobre se o disco vai falhar" ou "garante que a máquina aceita o Windows 11" seria oferta que o programa não cumpre. Fica fora da página.

### 4.4 CDC e a ausência de garantia da GPL-3.0

**FATO LEGAL.** CDC, art. 25: "É vedada a estipulação contratual de cláusula que impossibilite, exonere ou atenue a obrigação de indenizar prevista nesta e nas seções anteriores." Art. 51, I: são nulas as cláusulas que "impossibilitem, exonerem ou atenuem a responsabilidade do fornecedor por vícios de qualquer natureza dos produtos e serviços ou impliquem renúncia ou disposição de direitos. Nas relações de consumo entre o fornecedor e o consumidor pessoa jurídica, a indenização poderá ser limitada, em situações justificáveis". Verificados no Planalto em 02/10/2026.

**INTERPRETAÇÃO.** Vale para o MapHard a solução das verificações do MapNet e do MapDisk: manter a GPL-3.0 íntegra e pôr, separado e em português, um texto que diga os limites do programa e ressalve os direitos garantidos por lei. No MapHard não há risco de perda de arquivo. O limite que importa é o da leitura: o que o programa mostra depende do que o hardware, o firmware e o Windows informam.

### 4.5 Privacidade da página

**FATO LEGAL.** LGPD, art. 6º, VI: "transparência: garantia, aos titulares, de informações claras, precisas e facilmente acessíveis sobre a realização do tratamento e os respectivos agentes de tratamento, observados os segredos comercial e industrial". Verificado no Planalto em 02/10/2026. Incidência: a MT é controladora dos registros de acesso da página (seção 3).

**INTERPRETAÇÃO.** Vale a solução da seção 11 da verificação do MapDisk: dizer o que a página não tem (formulário, propaganda, estatística, conteúdo de outros sites) e o que a hospedagem, o Cloudflare e o GitHub registram, sem afirmação absoluta sobre cookies de terceiros. O MapHard acrescenta um parágrafo sobre os relatórios, que ficam só onde o técnico grava.

### 4.6 Nomes de terceiros

**INTERPRETAÇÃO.** Microsoft, Windows, GitHub, Intel, AMD e Qualcomm aparecem só para dizer onde o programa roda, de onde vem a lista de processadores e onde estão o código e o download, sem logotipo e sem sugerir parceria. É o mesmo uso que a seção 11 da verificação do MapDisk aceitou para o GitHub. Os nomes dos dois programas de referência do desenho ficam fora da página, como manda o `AGENTS.md`, e um teste confere. Sem uso de marca alheia em comparação, não há o que analisar além disso.

## 5. Textos para a página

Os textos passam pela `humanizar-ptbr` antes de entrar na página, sem mudar o sentido.

**Perto do download:**

> Uso autorizado. O MapHard - MT lê o hardware e os registros do Windows do computador em que roda e não altera nada nele. Use o programa só em computadores que você tem autorização para administrar ou para atender. Os relatórios trazem o nome do computador e números de série, que podem identificar quem usa a máquina: guarde onde só quem precisa tem acesso e apague quando não precisar mais. O programa não envia nenhuma informação para fora do computador.

**Licença e garantias:**

> Licença e garantias. O MapHard - MT é distribuído gratuitamente sob a GPL-3.0. O que ele mostra depende do que o hardware, o firmware e o Windows informam, e alguns dados só aparecem com administrador. A saúde dos discos vem do que o próprio disco informa no SMART e não é previsão de falha. A verificação do Windows 11 segue a lista de processadores publicada pela Microsoft, e a ferramenta oficial da Microsoft para essa conferência é o PC Integridade do Computador. A licença não inclui promessa de funcionamento em todo computador nem serviço de suporte técnico. As disposições da GPL-3.0 sobre garantias e responsabilidade aplicam-se nos limites permitidos pela legislação brasileira e não restringem direitos assegurados ao consumidor por lei.

**Privacidade, parágrafo dos relatórios:**

> O MapHard roda no seu computador e não coleta nem envia dados. Não tem telemetria, não pede cadastro e não precisa de conta. Os relatórios ficam só onde você gravar. Eles trazem o nome do computador e números de série, que podem identificar quem usa a máquina. Guarde com cuidado e apague quando não precisar mais.

**Quem responde:** MANFRED TECNOLOGIA LTDA, com o CNPJ e o contato que a página de privacidade do MapDisk já publica (`manfredjr/mapdisk`, `public/privacidade.html`, lida em 02/10/2026).

## 6. Requisitos para implementação

- [ ] Textos "Uso autorizado" e "Licença e garantias" da seção 5 na página, perto do download, e no README.
- [ ] Teste que confira a ausência de código de rede no programa (`SemRedeTestes`), antes de a página dizer que nada sai da máquina.
- [ ] Teste que confira que a página não cita os nomes dos programas de referência.
- [ ] A página diz a versão da lista de processadores do Windows 11 e cita o PC Integridade do Computador.
- [ ] Nenhuma frase de previsão de falha de disco nem de garantia de aceitação do Windows 11.
- [ ] Imagens de tela só do modo `--demonstracao`.
- [ ] Fora do programa, no atendimento da MT: pedido do cliente para o inventário registrado na ordem de serviço, pasta de rede com acesso restrito e relatório entregue só ao contato que pediu (seção 4.2). Fica em `docs/superpowers/pendencias.md` como decisão de processo do Manfred.

## 7. Pendências de validação

Nenhuma. A premissa de relação de consumo na distribuição gratuita e o alcance das seções 15 e 16 da GPL-3.0 foram respondidos pelo parecer do MapNet, com fatos de distribuição iguais, segundo a seção 7 da verificação do MapDisk.
