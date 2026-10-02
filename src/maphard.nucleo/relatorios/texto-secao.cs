using System.Text;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Painel;

namespace MapHard.Nucleo.Relatorios;

/// <summary>
/// Uma seção da tela como texto, para colar em chamado ou e-mail (R37). Sai das mesmas linhas da tela, com os
/// mesmos textos de estado. Quebra de linha do Windows, porque o texto vai para o Bloco de Notas e o e-mail.
/// </summary>
public static class TextoSecao
{
    public static string Gerar(SecaoTela secao, ColetaMaquina coleta)
    {
        var texto = new StringBuilder();
        texto.Append($"MapHard - MT: {secao.Titulo}\r\n");
        texto.Append($"{coleta.Identificacao.Computador.Valor ?? "computador"}, coletado em {Formatador.DataHora(coleta.ColetadoEm)}\r\n");
        foreach (var cartao in secao.Cartoes)
        {
            texto.Append("\r\n");
            texto.Append(cartao.Selo is null ? $"{cartao.Titulo}\r\n" : $"{cartao.Titulo} [{cartao.Selo}]\r\n");
            foreach (var linha in cartao.Linhas)
            {
                texto.Append(linha.Rotulo.Length == 0 ? $"  {linha.Texto}\r\n" : $"  {linha.Rotulo}: {linha.Texto}\r\n");
            }
        }

        return texto.ToString();
    }
}
