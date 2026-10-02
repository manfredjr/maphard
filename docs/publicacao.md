# Publicação do MapHard - MT

São duas publicações, que não se misturam. As duas só acontecem com a autorização do Manfred, dada no chat.

| O quê | Para onde | Como |
|---|---|---|
| O programa (`maphard.exe`) | GitHub Releases | Marca de versão `vX.Y.Z` no `main` |
| A página (pasta `public/`) | `maphard.manfred.com.br` | Git Version Control do cPanel, com o `.cpanel.yml` da raiz |

Os dados do servidor (conta, endereço, chaves) não ficam neste repositório. Estão no roteiro de publicação do método da MT, fora daqui.

## Programa: nova versão

1. Trocar o número em `Directory.Build.props` (`<Version>`) num Pull Request.
2. Escrever as notas da versão em `docs/versoes/vX.Y.Z.md`, em português. Sem esse arquivo, o CI para a publicação.
3. Depois do merge, com autorização, criar e enviar a marca no `main`:

```bat
git tag -a vX.Y.Z origin/main -m "Versao X.Y.Z do MapHard - MT"
git push origin vX.Y.Z
```

4. O CI testa, gera o `maphard.exe`, calcula o SHA-256 e publica a Release com os dois arquivos.
5. Conferir: baixar a Release, comparar o SHA-256 e rodar `maphard.exe --versao`.

## Página: primeira publicação

Site estático, na variante que não tira nada do ar: o cPanel clona o repositório em `repositories/maphard`, e o domínio passa a apontar para a pasta `public` dele. Cada passo no servidor é um comando no Terminal do cPanel, um por vez.

1. Conferir a raiz atual do domínio `maphard.manfred.com.br` e o que já existe na pasta de chaves.
2. Criar a chave de leitura do projeto, sem senha, e o apelido dela, como manda o roteiro.
3. Cadastrar a chave pública no GitHub como chave de implantação **somente leitura** do repositório `manfredjr/maphard`, com autorização.
4. Testar a chave contra o GitHub.
5. No cPanel, Git Version Control, Criar: clonar o repositório em `repositories/maphard`.
6. Na aba Pull or Deploy: Update from Remote, F5 e Deploy HEAD Commit. Ler o log do deploy.
7. Em Domínios, trocar a raiz de `maphard.manfred.com.br` para `repositories/maphard/public`.
8. Purgar o cache da Cloudflare e conferir a página de fora: `index.html`, `privacidade.html`, imagens e cabeçalhos do `.htaccess`.

O botão "Baixar para Windows" aponta para a última Release. Enquanto o repositório for privado, só quem tem acesso a ele consegue baixar.

## Página: versão nova

1. Merge do Pull Request no `main`, com CI verde e autorização.
2. cPanel, Git Version Control, Gerenciar, aba Pull or Deploy.
3. Update from Remote, F5 e Deploy HEAD Commit.
4. Purgar o cache da Cloudflare e conferir a página.
