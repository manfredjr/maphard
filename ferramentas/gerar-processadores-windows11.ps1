# Gera src\maphard.nucleo\tabelas\processadores-windows11.csv com as listas de processadores aceitos pelo
# Windows 11 publicadas pela Microsoft no learn.microsoft.com (Intel, AMD e Qualcomm), da versao pedida.
# Uso, na pasta do projeto:
#   powershell -ExecutionPolicy Bypass -File ferramentas\gerar-processadores-windows11.ps1 -Versao 25h2
# As paginas baixadas ficam em .superpowers\rascunho, que o git ignora.

param([string]$Versao = "25h2")

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$raiz = Split-Path -Parent $PSScriptRoot
$rascunho = Join-Path $raiz ".superpowers\rascunho"
$destino = Join-Path $raiz "src\maphard.nucleo\tabelas\processadores-windows11.csv"
$versaoTela = $Versao.ToUpper()

New-Item -ItemType Directory -Force $rascunho | Out-Null
$cliente = New-Object System.Net.WebClient
$cliente.Encoding = [System.Text.Encoding]::UTF8

$linhas = New-Object System.Collections.Generic.List[string]
$linhas.Add("# Processadores aceitos pelo Windows 11 $versaoTela, pelas listas da Microsoft. Gerada por ferramentas\gerar-processadores-windows11.ps1.")
$linhas.Add("fabricante;produto;serie;versao;fonte")

foreach ($fabricante in "intel", "amd", "qualcomm") {
    $endereco = "https://learn.microsoft.com/en-us/windows-hardware/design/minimum/supported/windows-11-$Versao-supported-$fabricante-processors"
    $pagina = $cliente.DownloadString($endereco)
    [System.IO.File]::WriteAllText((Join-Path $rascunho "windows11-$Versao-$fabricante.html"), $pagina, (New-Object System.Text.UTF8Encoding($false)))
    $achadas = [regex]::Matches($pagina, '<tr>\s*<td>(.*?)</td>\s*<td>(.*?)</td>\s*<td>(.*?)</td>', 'Singleline')
    if ($achadas.Count -eq 0) { throw "nenhuma linha na pagina de $fabricante" }
    foreach ($achada in $achadas) {
        $campos = @()
        foreach ($i in 1..3) {
            $texto = $achada.Groups[$i].Value -replace '<[^>]+>', ''
            $texto = [System.Net.WebUtility]::HtmlDecode($texto) -replace '[^\x20-\x7E]', '' -replace '\s+', ' '
            $campos += $texto.Trim()
        }
        if ($campos -match ';' -or ($campos | Where-Object { $_ -eq '' })) { throw "linha fora do formato: $($campos -join ' | ')" }
        $linhas.Add("$($campos[0]);$($campos[1]);$($campos[2]);$versaoTela;$endereco")
    }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($destino, ($linhas -join "`r`n") + "`r`n", $utf8)
Write-Output "Gravado: $destino ($($linhas.Count - 2) linhas, Windows 11 $versaoTela)"
