namespace MapHard.Nucleo.Saude;

/// <summary>Quando um cartão junta várias coisas, vale o pior: Ruim, Atenção, Desconhecido, Bom.</summary>
public static class OrdemSaude
{
    public static int Peso(EstadoSaude estado) => estado switch
    {
        EstadoSaude.Ruim => 3,
        EstadoSaude.Atencao => 2,
        EstadoSaude.Desconhecido => 1,
        _ => 0,
    };
}
