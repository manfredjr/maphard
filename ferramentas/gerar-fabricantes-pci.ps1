# Gera src\maphard.nucleo\tabelas\fabricantes-pci.csv com os fabricantes PCI do pci.ids do pciutils
# (repositorio pciutils/pciids), que pode ser distribuido pela GPL-2.0 ou posterior, ou pela licenca BSD
# de 3 clausulas. Uso, na pasta do projeto:
#   powershell -ExecutionPolicy Bypass -File ferramentas\gerar-fabricantes-pci.ps1
# O arquivo baixado fica em .superpowers\rascunho, que o git ignora.

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$raiz = Split-Path -Parent $PSScriptRoot
$rascunho = Join-Path $raiz ".superpowers\rascunho"
$destino = Join-Path $raiz "src\maphard.nucleo\tabelas\fabricantes-pci.csv"
$endereco = "https://raw.githubusercontent.com/pciutils/pciids/master/pci.ids"

New-Item -ItemType Directory -Force $rascunho | Out-Null
$arquivo = Join-Path $rascunho "pci.ids"
Invoke-WebRequest $endereco -OutFile $arquivo -UseBasicParsing
$texto = [System.IO.File]::ReadAllLines($arquivo, [System.Text.Encoding]::UTF8)

$versao = ($texto | Where-Object { $_ -match '^#\s+Version:\s+(\S+)' } | Select-Object -First 1) -replace '^#\s+Version:\s+', ''
if (-not $versao) { throw "versao do pci.ids nao encontrada" }

$linhas = New-Object System.Collections.Generic.List[string]
$linhas.Add("# Fabricantes PCI pelo codigo do fabricante. Gerada por ferramentas\gerar-fabricantes-pci.ps1.")
$linhas.Add("# Fonte: pci.ids versao $versao, $endereco. Distribuido pela GPL-2.0 ou posterior, ou pela licenca BSD de 3 clausulas.")
$linhas.Add("fabricante;nome;fonte")

foreach ($linha in $texto) {
    if ($linha -match '^C ') { break }
    if ($linha -match '^([0-9a-f]{4})\s+(.+)$') {
        $codigo = $Matches[1].ToUpper()
        $nome = $Matches[2].Trim()
        # Nome fora do ASCII imprimivel ou com ponto e virgula nao entra: a tabela embutida fica so com texto seguro.
        if ($nome -match ';' -or $nome -match '[^\x20-\x7E]') { continue }
        $linhas.Add("$codigo;$nome;pci.ids $versao")
    }
}

if ($linhas.Count -lt 1000) { throw "poucos fabricantes: $($linhas.Count)" }
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($destino, ($linhas -join "`r`n") + "`r`n", $utf8)
Write-Output "Gravado: $destino ($($linhas.Count - 3) fabricantes, pci.ids $versao)"
