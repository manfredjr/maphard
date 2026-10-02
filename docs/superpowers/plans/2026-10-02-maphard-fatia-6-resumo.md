# Plano da fatia 6: Resumo com os cartões de saúde

> **Para quem executa:** siga tarefa por tarefa, na ordem. Cada passo tem caixa de seleção (`- [ ]`). Cada tarefa termina com teste verde e commit. Leia o `AGENTS.md` antes de começar.

**Objetivo:** a primeira tela do MapHard responde de uma vez o que o técnico quer saber ao abrir o programa: o que nesta máquina pede atenção. Um cartão por área, com Bom, Atenção, Ruim ou Desconhecido e uma frase curta com o motivo. O clique no cartão abre a seção com o detalhe (R1).

**Arquitetura:** as regras de saúde já existem e estão espalhadas pelas seções: `RegrasDisco` (um disco), `RegrasEstabilidade` (Estabilidade, Memória e Dispositivos) e `RegrasWindows` (Windows 11 e Bateria). Esta fatia acrescenta as duas que faltam (o conjunto dos discos e o Processador) e um montador do Resumo, `ResumoSaude`, que junta tudo numa lista de cartões, na ordem do R1. A tela, a linha de comando e a demonstração passam a usar essa lista. A janela ganha o clique no cartão.

**Tecnologia:** C# com .NET 8, WPF e xUnit. Nenhum pacote NuGet novo. Nenhuma leitura nova da máquina, a não ser a da tarefa 2 (Hyper-V pedido), se o Manfred aprovar.

Desenho: `docs/superpowers/specs/2026-09-30-maphard-design.md`. Requisito desta fatia: R1, com as linhas da seção 8 que ainda não têm regra (Processador e o conjunto dos discos).

## Restrições globais

- As mesmas das fatias anteriores. Nesta fatia pesa a regra do produto 2: cartão sem leitura fica Desconhecido, com o motivo, nunca Bom.
- **Nenhuma regra nova inventada.** Os limites são os da seção 8 do desenho e os que o Manfred já aprovou nas fatias 3 a 5.
- **O Resumo não lê nada.** Ele só junta o que as seções já calcularam. Assim o cartão e a seção nunca discordam.
- Textos da tela em português, sem os caracteres proibidos, e passados pela `humanizar-ptbr`.

## O que já existe e o que falta

| Cartão do R1 | Regra | Situação |
|---|---|---|
| Discos | `RegrasDisco.Ata` e `RegrasDisco.Nvme`, um disco por vez | Falta juntar: o pior disco manda |
| Memória | `RegrasEstabilidade.Memoria` | Pronta. O erro de memória do WHEA continua fora (pendência da fatia 4) |
| Processador | Nenhuma | Falta. Ver a tarefa 2 |
| Estabilidade | `RegrasEstabilidade.Estabilidade` | Pronta |
| Dispositivos | `RegrasEstabilidade.Dispositivos` | Pronta |
| Windows 11 | `RegrasWindows.Windows11` | Pronta |
| Bateria | `RegrasWindows.Bateria` | Pronta |

## Decisões que o plano pede ao Manfred

O PR deste plano é o lugar de responder. Até a resposta, vale a recomendação.

1. **Processador, Atenção "quando o Hyper-V é pedido".** O Windows não diz, sem administrador, se alguém pediu o Hyper-V. Recomendação: considerar pedido quando o serviço do Hyper-V (`vmms`) ou o do WSL (`WslService`) existir no registro, em `HKLM\SYSTEM\CurrentControlSet\Services`, que o usuário comum lê. Os dois nomes ficam [CONFERIR] até serem vistos numa máquina com o recurso ligado. Sem nenhum dos dois, virtualização desligada no firmware não muda o cartão. A outra opção é avisar sempre que a virtualização estiver desligada no firmware, mesmo sem o Hyper-V.
2. **Processador, Ruim com "erro de processador no WHEA".** O MapHard ainda não separa o componente do erro do WHEA (pendência da fatia 4). Até separar, o cartão Processador não fica Ruim por esse motivo. O erro não corrigido continua deixando a Estabilidade em Ruim.
3. **Bateria no desktop.** Recomendação: no desktop sem bateria, o cartão não aparece. Um cartão "Bom" ali diria que a bateria está boa, e um "Desconhecido" diria que faltou ler, e nenhum dos dois é verdade. A outra opção é mostrar o cartão sem selo, com "não disponível neste equipamento".

## Ordem dos estados

Quando um cartão junta várias coisas (vários discos, vários motivos), vale o pior, nesta ordem: Ruim, Atenção, Desconhecido, Bom. É a mesma ordem que `RegrasWindows.Windows11` já usa.

## Tarefas

### Tarefa 1: saúde do conjunto dos discos

**Arquivos:**
- Modificar: `src/maphard.nucleo/saude/regras-disco.cs`
- Teste: `testes/maphard.testes/saude-resumo-testes.cs` (novo)

**Interfaces:**
- Consome: `SecaoDiscos`, `DiscoTela.Saude` (`SaudeDisco`), `DiscoTela.Numero`.
- Produz: `public static SaudeArea RegrasDisco.Conjunto(SecaoDiscos discos)`.

- [ ] **Passo 1:** escrever os testes.

```csharp
[Fact]
public void Conjunto_dos_discos_fica_com_o_pior()
{
    var s = RegrasDisco.Conjunto(Discos((0, EstadoSaude.Bom, []), (1, EstadoSaude.Atencao, ["3 setores realocados"])));

    Assert.Equal(EstadoSaude.Atencao, s.Estado);
    Assert.Equal(["Disco 1: 3 setores realocados"], s.Motivos);
}

[Fact]
public void Disco_sem_smart_deixa_o_conjunto_desconhecido_so_quando_nenhum_esta_pior()
{
    Assert.Equal(EstadoSaude.Desconhecido, RegrasDisco.Conjunto(Discos((0, EstadoSaude.Bom, []), (1, EstadoSaude.Desconhecido, ["requer administrador"]))).Estado);
    Assert.Equal(EstadoSaude.Ruim, RegrasDisco.Conjunto(Discos((0, EstadoSaude.Ruim, ["falha prevista pelo disco"]), (1, EstadoSaude.Desconhecido, ["requer administrador"]))).Estado);
}

[Fact]
public void Lista_de_discos_nao_lida_fica_desconhecida()
{
    var s = RegrasDisco.Conjunto(new SecaoDiscos(Campo<IReadOnlyList<DiscoTela>>.Erro(FonteDado.Armazenamento, "lista de discos indisponível"), []));

    Assert.Equal(EstadoSaude.Desconhecido, s.Estado);
    Assert.Equal(["lista de discos indisponível"], s.Motivos);
}
```

O auxiliar `Discos` monta uma `SecaoDiscos` com um `DiscoTela` por tupla, tirado do disco da `DadosDemonstracao` com `with { Numero = n, Saude = new SaudeDisco(estado, motivos) }`.

- [ ] **Passo 2:** rodar e ver falhar (`RegrasDisco.Conjunto` não existe).
- [ ] **Passo 3:** implementar.

```csharp
/// <summary>Saúde de todos os discos: o pior manda, e cada motivo leva o número do disco.</summary>
public static SaudeArea Conjunto(SecaoDiscos discos)
{
    if (!discos.Discos.FoiLido)
    {
        return new SaudeArea(EstadoSaude.Desconhecido, [discos.Discos.Motivo ?? "discos não lidos"]);
    }

    var lista = discos.Discos.Valor!;
    var pior = lista.Select(d => d.Saude.Estado).DefaultIfEmpty(EstadoSaude.Desconhecido).MaxBy(OrdemSaude.Peso);
    var motivos = lista
        .Where(d => d.Saude.Estado != EstadoSaude.Bom)
        .OrderByDescending(d => OrdemSaude.Peso(d.Saude.Estado))
        .SelectMany(d => d.Saude.Motivos.Select(m => $"Disco {d.Numero}: {m}"))
        .ToList();
    return new SaudeArea(pior, lista.Count == 0 ? ["nenhum disco encontrado"] : motivos);
}
```

`OrdemSaude.Peso` vai num arquivo novo, `src/maphard.nucleo/saude/ordem-saude.cs`: Bom 0, Desconhecido 1, Atenção 2, Ruim 3.

- [ ] **Passo 4:** testes verdes, commit "Junta a saude de todos os discos".

### Tarefa 2: saúde do processador

**Arquivos:**
- Criar: `src/maphard.nucleo/saude/regras-processador.cs`
- Modificar: `src/maphard.nucleo/coleta/fontes.cs`, `src/maphard.nucleo/coleta/coleta.cs` (campo novo `Campo<bool> HyperVPedido` em `SecaoProcessador`), `src/maphard.nucleo/coleta/coletor.cs`, `src/maphard.nucleo/painel/demonstracao.cs`
- Teste: `testes/maphard.testes/saude-resumo-testes.cs`

**Interfaces:**
- Consome: `SecaoProcessador.VirtualizacaoNoProcessador`, `SecaoProcessador.VirtualizacaoLigada`, `IFonteRegistro`.
- Produz: `public static SaudeArea RegrasProcessador.Processador(SecaoProcessador p)` e `SecaoProcessador.HyperVPedido`.

- [ ] **Passo 1:** conferir os nomes dos serviços `vmms` e `WslService` numa máquina com Hyper-V e com WSL e anotar no comentário da fonte. Se a decisão 1 for "avisar sempre", pular este passo e o campo `HyperVPedido`.
- [ ] **Passo 2:** escrever os testes.

```csharp
[Fact]
public void Virtualizacao_desligada_com_hyperv_pedido_e_atencao()
{
    var s = RegrasProcessador.Processador(Processador(suporte: true, ligada: false, hyperV: true));

    Assert.Equal(EstadoSaude.Atencao, s.Estado);
    Assert.Equal(["virtualização desligada no firmware, e o Hyper-V ou o WSL está instalado: ligar no firmware"], s.Motivos);
}

[Fact]
public void Virtualizacao_desligada_sem_hyperv_e_bom()
{
    Assert.Equal(EstadoSaude.Bom, RegrasProcessador.Processador(Processador(suporte: true, ligada: false, hyperV: false)).Estado);
}

[Fact]
public void Processador_nao_lido_fica_desconhecido()
{
    var p = Processador(suporte: true, ligada: true, hyperV: false) with { Nome = Campo<string>.Erro(FonteDado.Cpuid, "falhou") };

    Assert.Equal(EstadoSaude.Desconhecido, RegrasProcessador.Processador(p).Estado);
}
```

O auxiliar `Processador` parte do processador da `DadosDemonstracao` e troca os três campos.

- [ ] **Passo 3:** implementar a regra.

```csharp
public static SaudeArea Processador(SecaoProcessador p)
{
    if (!p.Nome.FoiLido)
    {
        return new SaudeArea(EstadoSaude.Desconhecido, [p.Nome.Motivo ?? "processador não lido"]);
    }

    var desligada = p.VirtualizacaoNoProcessador is { FoiLido: true, Valor: true } && p.VirtualizacaoLigada is { FoiLido: true, Valor: false };
    return desligada && p.HyperVPedido is { FoiLido: true, Valor: true }
        ? new SaudeArea(EstadoSaude.Atencao, ["virtualização desligada no firmware, e o Hyper-V ou o WSL está instalado: ligar no firmware"])
        : new SaudeArea(EstadoSaude.Bom, []);
}
```

- [ ] **Passo 4:** ler `HyperVPedido` no coletor pelo registro: `Lido(true)` se `registro.Ler(@"SYSTEM\CurrentControlSet\Services\vmms", "Start")` ou o mesmo em `WslService` devolver valor; `Lido(false)` se os dois faltarem. A `FontesSimuladas.Registro` ganha a chave só quando o teste pedir.
- [ ] **Passo 5:** testes verdes, commit "Cria a regra de saude do processador".

### Tarefa 3: montador do Resumo

**Arquivos:**
- Criar: `src/maphard.nucleo/saude/resumo-saude.cs`
- Teste: `testes/maphard.testes/saude-resumo-testes.cs`

**Interfaces:**
- Consome: `RegrasDisco.Conjunto`, `RegrasEstabilidade.Memoria`, `RegrasProcessador.Processador`, `RegrasEstabilidade.Estabilidade`, `RegrasEstabilidade.Dispositivos`, `RegrasWindows.Windows11`, `RegrasWindows.Bateria`, as constantes de `MontadorSecoes`.
- Produz:

```csharp
/// <summary>Um cartão do Resumo (R1): a área, a seção que o clique abre, o estado e a frase curta.</summary>
public sealed record CartaoSaude(string Area, string Secao, EstadoSaude Estado, string Frase, IReadOnlyList<string> Motivos);

public static class ResumoSaude
{
    public static IReadOnlyList<CartaoSaude> Montar(ColetaMaquina c);
    public static string Frase(SaudeArea s);
}
```

- [ ] **Passo 1:** escrever os testes.
  - Ordem do R1: Discos, Memória, Processador, Estabilidade, Dispositivos, Windows 11, Bateria.
  - Frase: Bom é "nenhum problema encontrado"; um motivo vira a frase; dois ou mais viram "o primeiro motivo, e mais N".
  - Cada cartão aponta a seção certa (`MontadorSecoes.Discos`, `Memoria`, `Processador`, `Estabilidade`, `Dispositivos`, `Windows`, `Bateria`).
  - Desktop sem bateria: sem o cartão Bateria (decisão 3).
  - Com a coleta da `DadosDemonstracao`: Discos Atenção, Memória Atenção, Estabilidade Atenção, Dispositivos Atenção, Windows 11 Atenção, Bateria Bom, Processador Bom.
- [ ] **Passo 2:** implementar. O motivo vem igual ao da seção, para cartão e seção nunca discordarem.
- [ ] **Passo 3:** testes verdes, commit "Monta os cartoes de saude do Resumo".

### Tarefa 4: Resumo na tela e clique no cartão

**Arquivos:**
- Modificar: `src/maphard.nucleo/painel/secoes.cs`, `src/maphard/janela-principal.xaml`, `src/maphard/janela-principal.xaml.cs`
- Teste: `testes/maphard.testes/painel-testes.cs`

**Interfaces:**
- Consome: `ResumoSaude.Montar`.
- Produz: `CartaoTela` ganha o parâmetro opcional `string? Abre = null`, o id da seção que o clique abre.

- [ ] **Passo 1:** testes do painel: a seção Resumo começa pelos cartões de saúde, na ordem do R1, cada um com o selo, o tom, a linha "Situação" com a frase e `Abre` com a seção; depois vêm "Este computador" e "Windows", sem `Abre`.
- [ ] **Passo 2:** `MontarResumo` monta os cartões de saúde a partir de `ResumoSaude.Montar(c)`.
- [ ] **Passo 3:** na janela, o cartão com `Abre` fica clicável (cursor de mão, dica "Abrir a seção"), e o clique faz:

```csharp
private void AoClicarCartao(object sender, MouseButtonEventArgs e)
{
    if ((sender as FrameworkElement)?.DataContext is CartaoTela { Abre: { } id })
    {
        Navegacao.SelectedItem = _painel.Secoes.FirstOrDefault(s => s.Id == id);
    }
}
```

O teclado também abre: o cartão clicável recebe foco pelo Tab e abre com Enter.
- [ ] **Passo 4:** testes verdes; rodar `maphard.exe --demonstracao` e clicar em cada cartão; commit "Mostra os cartoes de saude no Resumo".

### Tarefa 5: linha de comando

**Arquivos:**
- Modificar: `src/maphard.nucleo/linha-de-comando/executor-cli.cs`
- Teste: `testes/maphard.testes/linha-de-comando-testes.cs`

- [ ] **Passo 1:** testes: logo depois de "Coletando...", um bloco "Saúde", uma linha por cartão, como "Discos:       Atenção (Disco 1: 3 setores realocados)" e "Bateria:      Bom". As linhas "Atenção:" espalhadas pelo resumo de hoje saem, porque o bloco já diz tudo.
- [ ] **Passo 2:** implementar com `ResumoSaude.Montar`.
- [ ] **Passo 3:** testes verdes, commit "Mostra a saude no topo da linha de comando".

### Tarefa 6: fechamento

- [ ] **Passo 1:** README: o Resumo no "Uso" e a linha da fatia 6 na "Situação do projeto". A linha da fatia 5 passa a "Concluída em 02/10/2026".
- [ ] **Passo 2:** pendências: as decisões 1 a 3 com a resposta do Manfred, e o que ficar aberto.
- [ ] **Passo 3:** portões, envio, Pull Request do código e leitura do CI até ficar verde.

## Como o Manfred testa

1. Baixar o `maphard.exe` do PR, ou gerar com `ferramentas\publicar.cmd`.
2. Abrir o programa: a primeira tela mostra os cartões de saúde, com o selo e o motivo.
3. Clicar em cada cartão e ver a seção certa abrir, com o mesmo motivo no cartão da seção.
4. Abrir `maphard.exe --demonstracao` e conferir os estados descritos na tarefa 3.
5. Num desktop, conferir que o cartão Bateria não aparece.
6. `maphard coletar` e conferir o bloco "Saúde" no topo.

## Riscos

| Risco | O que fazer |
|---|---|
| Cartão e seção discordarem | O Resumo só chama as regras que as seções usam; o teste da tarefa 3 compara os dois |
| Hyper-V pedido sem fonte oficial | Decisão 1. Sem os nomes conferidos, o campo fica "não informado" e o cartão não muda |
| Cartão Bom escondendo leitura que falhou | Leitura que falha deixa o cartão Desconhecido, com o motivo, nunca Bom |
