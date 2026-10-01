# Gera src\maphard.nucleo\tabelas\fabricantes-memoria.csv a partir da lista @vendors do decode-dimms,
# do pacote oficial do i2c-tools (GPL-2.0 ou posterior). Uso, na pasta do projeto:
#   powershell -ExecutionPolicy Bypass -File ferramentas\gerar-fabricantes-memoria.ps1
# Os arquivos baixados ficam em .superpowers\rascunho, que o git ignora.

param(
    [string]$Versao = "4.4"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$rascunho = Join-Path $raiz ".superpowers\rascunho\i2c-tools"
$destino = Join-Path $raiz "src\maphard.nucleo\tabelas\fabricantes-memoria.csv"
$endereco = "https://mirrors.edge.kernel.org/pub/software/utils/i2c-tools/i2c-tools-$Versao.tar.xz"
$fonte = "i2c-tools $Versao eeprom/decode-dimms @vendors $endereco"

New-Item -ItemType Directory -Force $rascunho | Out-Null
$pacote = Join-Path $rascunho "i2c-tools-$Versao.tar.xz"
Invoke-WebRequest $endereco -OutFile $pacote -UseBasicParsing
tar -xf $pacote -C $rascunho "i2c-tools-$Versao/eeprom/decode-dimms"
if ($LASTEXITCODE -ne 0) { throw "tar nao conseguiu extrair o decode-dimms" }

$texto = [System.IO.File]::ReadAllText((Join-Path $rascunho "i2c-tools-$Versao\eeprom\decode-dimms"), [System.Text.Encoding]::UTF8)
$inicio = $texto.IndexOf("@vendors = (")
$fim = $texto.IndexOf("`n);", $inicio)
if ($inicio -lt 0 -or $fim -lt 0) { throw "lista @vendors nao encontrada" }
$bloco = $texto.Substring($inicio, $fim - $inicio)

# Cada pagina e um banco do JEP106, entre colchetes. Cada nome e uma cadeia entre aspas duplas.
$paginas = [regex]::Matches($bloco, '\[(.*?)\]', [System.Text.RegularExpressions.RegexOptions]::Singleline)
if ($paginas.Count -lt 10) { throw "numero de bancos inesperado: $($paginas.Count)" }

# Paridade impar no bit 7: o codigo gravado no modulo tem um numero impar de bits ligados.
function Com-Paridade([int]$numero) {
    $bits = 0
    for ($n = $numero; $n -gt 0; $n = $n -shr 1) { $bits += $n -band 1 }
    if ($bits % 2 -eq 0) { return $numero -bor 0x80 }
    return $numero
}

$linhas = New-Object System.Collections.Generic.List[string]
$linhas.Add("# Fabricantes de memoria pelo codigo JEDEC (JEP106). Gerada por ferramentas\gerar-fabricantes-memoria.ps1.")
$linhas.Add("# Fonte: $fonte (GPL-2.0 ou posterior).")
$linhas.Add("# banco comeca em 1. codigo e o byte do fabricante com a paridade impar no bit 7, em hexadecimal, como gravado no modulo.")
$linhas.Add("banco;codigo;nome;fonte")

for ($banco = 0; $banco -lt $paginas.Count; $banco++) {
    $nomes = [regex]::Matches($paginas[$banco].Groups[1].Value, '"((?:[^"\\]|\\.)*)"')
    if ($banco -lt $paginas.Count - 1 -and $nomes.Count -ne 126) { throw "banco $($banco + 1) com $($nomes.Count) nomes; o esperado e 126" }
    for ($i = 0; $i -lt $nomes.Count; $i++) {
        $nome = $nomes[$i].Groups[1].Value.Replace([string][char]0x2019, "'").Replace('\"', '"').Trim()
        if ($nome -match ';' -or $nome -match '[^\x20-\x7E]') { throw "nome com caractere invalido no banco $($banco + 1): $nome" }
        $codigo = '{0:X2}' -f (Com-Paridade ($i + 1))
        $linhas.Add("$($banco + 1);$codigo;$nome;decode-dimms")
    }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($destino, ($linhas -join "`r`n") + "`r`n", $utf8)
Write-Output "Gravado: $destino ($($linhas.Count - 4) fabricantes em $($paginas.Count) bancos)"
