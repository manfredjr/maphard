# Gera src\maphard.nucleo\tabelas\atributos-smart.csv a partir da entrada DEFAULT do drivedb.h do
# smartmontools (repositorio oficial smartmontools/smartmontools, GPL-2.0). Uso, na pasta do projeto:
#   powershell -ExecutionPolicy Bypass -File ferramentas\gerar-atributos-smart.ps1
# A coluna "nome" (portugues) e preenchida a mao; o roteiro preserva o que ja estiver traduzido.
# O arquivo baixado fica em .superpowers\rascunho, que o git ignora.

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$raiz = Split-Path -Parent $PSScriptRoot
$rascunho = Join-Path $raiz ".superpowers\rascunho"
$destino = Join-Path $raiz "src\maphard.nucleo\tabelas\atributos-smart.csv"
$endereco = "https://api.github.com/repos/smartmontools/smartmontools/contents/drivedb/drivedb.h"
$fonte = "smartmontools drivedb/drivedb.h entrada DEFAULT"

New-Item -ItemType Directory -Force $rascunho | Out-Null
$resposta = Invoke-RestMethod $endereco -Headers @{ "User-Agent" = "maphard-ferramentas" }
$texto = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($resposta.content))
[System.IO.File]::WriteAllText((Join-Path $rascunho "drivedb.h"), $texto)

$inicio = $texto.IndexOf('{ "DEFAULT",')
if ($inicio -lt 0) { throw "entrada DEFAULT nao encontrada" }
$fim = $texto.IndexOf("},", $inicio)
$bloco = $texto.Substring($inicio, $fim - $inicio)

# Traducoes ja feitas: id -> nome em portugues.
$traduzidos = @{}
if (Test-Path $destino) {
    foreach ($linha in [System.IO.File]::ReadAllLines($destino)) {
        if ($linha.StartsWith("#") -or $linha.StartsWith("id;")) { continue }
        $campos = $linha.Split(";")
        if ($campos.Count -ge 3 -and $campos[2].Trim().Length -gt 0) { $traduzidos[[int]$campos[0]] = $campos[2] }
    }
}

$linhas = New-Object System.Collections.Generic.List[string]
$linhas.Add("# Nomes dos atributos SMART ATA. Gerada por ferramentas\gerar-atributos-smart.ps1.")
$linhas.Add("# Fonte: $fonte (GPL-2.0), https://github.com/smartmontools/smartmontools. O nome em portugues e traducao da MT.")
$linhas.Add("# tipo_disco: HDD, SSD ou vazio quando vale para os dois.")
$linhas.Add("id;nome_original;nome;tipo_disco;fonte")

$vistos = @{}
foreach ($m in [regex]::Matches($bloco, '"-v (\d+),[^,]+,([^," ]+)(?:,(HDD|SSD))? ?"')) {
    $id = [int]$m.Groups[1].Value
    if ($vistos.ContainsKey($id)) { continue }
    $vistos[$id] = $true
    $nome = if ($traduzidos.ContainsKey($id)) { $traduzidos[$id] } else { "" }
    $linhas.Add("$id;$($m.Groups[2].Value);$nome;$($m.Groups[3].Value);drivedb.h DEFAULT")
}

if ($vistos.Count -lt 50) { throw "poucos atributos na entrada DEFAULT: $($vistos.Count)" }
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($destino, ($linhas -join "`r`n") + "`r`n", $utf8)
Write-Output "Gravado: $destino ($($vistos.Count) atributos, $($traduzidos.Count) ja traduzidos)"
