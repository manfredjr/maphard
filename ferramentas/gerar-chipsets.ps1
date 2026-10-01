# Gera src\maphard.nucleo\tabelas\chipsets.csv a partir do pci.ids do pciutils (repositorio pciutils/pciids),
# que pode ser distribuido pela GPL-2.0 ou posterior, ou pela licenca BSD de 3 clausulas.
# Uso, na pasta do projeto:
#   powershell -ExecutionPolicy Bypass -File ferramentas\gerar-chipsets.ps1
# Entram so os dispositivos da Intel (8086) e da AMD (1022) cujo nome indica ponte do processador,
# controlador LPC ou eSPI, ou ponte ISA: sao os que identificam o chipset.
# O arquivo baixado fica em .superpowers\rascunho, que o git ignora.

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$raiz = Split-Path -Parent $PSScriptRoot
$rascunho = Join-Path $raiz ".superpowers\rascunho"
$destino = Join-Path $raiz "src\maphard.nucleo\tabelas\chipsets.csv"
$endereco = "https://raw.githubusercontent.com/pciutils/pciids/master/pci.ids"

New-Item -ItemType Directory -Force $rascunho | Out-Null
$arquivo = Join-Path $rascunho "pci.ids"
Invoke-WebRequest $endereco -OutFile $arquivo -UseBasicParsing
$texto = [System.IO.File]::ReadAllLines($arquivo, [System.Text.Encoding]::UTF8)

$versao = ($texto | Where-Object { $_ -match '^#\s+Version:\s+(\S+)' } | Select-Object -First 1) -replace '^#\s+Version:\s+', ''
if (-not $versao) { throw "versao do pci.ids nao encontrada" }

$fabricantes = @{ "8086" = "Intel"; "1022" = "AMD" }
$chave = '(Host Bridge|Host bridge|LPC|eSPI|ISA Bridge|ISA bridge)'
$linhas = New-Object System.Collections.Generic.List[string]
$linhas.Add("# Dispositivos PCI que identificam o chipset: ponte do processador, LPC, eSPI e ponte ISA. Gerada por ferramentas\gerar-chipsets.ps1.")
$linhas.Add("# Fonte: pci.ids versao $versao, $endereco. Distribuido pela GPL-2.0 ou posterior, ou pela licenca BSD de 3 clausulas.")
$linhas.Add("fabricante;dispositivo;nome;fonte")

$atual = $null
foreach ($linha in $texto) {
    if ($linha -match '^([0-9a-f]{4})\s+') { $atual = $Matches[1]; continue }
    if ($linha -match '^C ') { break }
    if ($null -eq $atual -or -not $fabricantes.ContainsKey($atual)) { continue }
    if ($linha -match "^\t([0-9a-f]{4})\s+(.+)$") {
        $dispositivo = $Matches[1]
        $nome = $Matches[2].Trim()
        if ($nome -notmatch $chave) { continue }
        if ($nome -match ';' -or $nome -match '[^\x20-\x7E]') { throw "nome com caractere invalido: $nome" }
        $linhas.Add("$($atual.ToUpper());$($dispositivo.ToUpper());$nome;pci.ids $versao")
    }
}

if ($linhas.Count -lt 100) { throw "poucos dispositivos: $($linhas.Count)" }
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($destino, ($linhas -join "`r`n") + "`r`n", $utf8)
Write-Output "Gravado: $destino ($($linhas.Count - 3) dispositivos, pci.ids $versao)"
