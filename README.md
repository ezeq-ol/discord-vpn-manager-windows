# Discord VPN Manager — Windows

Gerenciador de perfis WireGuard que encaminha o Discord pela VPN e mantém os outros aplicativos na conexão normal.

## Download

Baixe o **DiscordVPNManager.exe** em [Releases](https://github.com/ezeq-ol/discord-vpn-manager-windows/releases). Ele contém o motor e os scripts necessários. Nenhum perfil ou chave de usuário é incluído.

## Recursos

- Importação e seleção de perfis `.conf`.
- Interface redesenhada com navegação lateral, cartões de conexão, lista de perfis e janela redimensionável.
- Ícone na bandeja e opção de desconectar e sair.
- **Buscar atualizações** consulta a última release estável deste repositório e oferece baixar e instalar quando houver uma versão nova.
- Link direto para baixar os perfis `.conf` no Proton VPN.
- **Iniciar VPN com o Windows** registra **Discord VPN Manager** na aba Inicializar.
- Ao entrar no Windows: Manager na bandeja → túnel pronto → abertura do Discord.
- Sem túnel pronto, a abertura automática do Discord é cancelada após o prazo.
- Ao desativar essa opção, a entrada de inicialização anterior do Discord é restaurada.

## Compilar

Windows com .NET Framework 4.x (já presente no Windows 10/11) e PowerShell. Não é necessário instalar um SDK .NET para o script principal.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\smoke.ps1
```

O build baixa os motores binários da release fixa indicada em `runtime.json`, verifica SHA-256 e os incorpora ao executável. O resultado fica em `dist/DiscordVPNManager.exe`.

O arquivo `.csproj` também está disponível para IDEs. Execute `restore-runtime.ps1` antes de compilar pela IDE.

## Estrutura para manutenção

- `DiscordVPNManager/MainForm.cs`: perfis e operações.
- `DiscordVPNManager/MainForm.Design.cs`: interface e layout.
- `DiscordVPNManager/MainForm.Updates.cs` e `UpdateClient.cs`: consulta, download e instalação de releases.
- `StartupSupport.cs`: instância única, bandeja no logon e espera pela VPN antes do Discord.
- `TraySupport.cs`: ícone, restauração da janela e desconectar/sair.
- `Aplicar-VPN-Discord.ps1`: geração da configuração, tarefas e ativação/desativação do início automático.
- `tunnel-launcher.ps1`: supervisor do túnel e sinal de prontidão.
- `discord-waiter.ps1`: abertura do Discord condicionada à prontidão.
- `runtime.json`: versões de download e hashes dos motores incorporados.
- `tests/`: testes sem reiniciar o PC ou alterar a preferência real de inicialização.

O código da interface foi recuperado do executável da versão 1.2.0 e validado por recompilação em C#. A partir deste repositório, as alterações podem ser feitas diretamente no fonte; não é necessário repetir patches binários.

## Dados locais e privilégios

O executável instala os componentes em `%LOCALAPPDATA%/Programs/Discord Single-Tunneling`. Perfis e seleção ficam em `%LOCALAPPDATA%/DiscordVPNManager` e nos arquivos importados pelo usuário. Ativar o túnel e configurar sua tarefa exigem a autorização de administrador pelo UAC. A tarefa já configurada inicia no logon sem precisar abrir o Manager como administrador.

A inicialização usa um caminho instalado fixo. O botão **Buscar atualizações** baixa `DiscordVPNManager.exe` e `DiscordVPNManager-Windows.sha256` da release pública, exige HTTPS e URLs deste repositório, verifica SHA-256 antes de instalar e novamente no arquivo preparado. Após sua confirmação, fecha somente o Manager, substitui o executável de forma atômica com cópia `.previous` e reabre a interface. O túnel em execução, os perfis e a preferência de inicialização são preservados. Uma instalação manual também pode substituir o executável com o Manager fechado.

O hash detecta downloads corrompidos; ele não é uma assinatura digital independente do GitHub. O computador precisa conseguir acessar a API e os downloads públicos do GitHub. A consulta é manual, sem exigir login ou token. Para publicar novas versões, inclua os dois arquivos na release estável e aumente a versão em `Properties/AssemblyInfo.cs`.

Os testes em CI cobrem compilação, leitura da opção de startup, rejeição de marcadores de prontidão inválidos, versões de releases, URLs de download e checksums. Localmente também foram testados o download real com SHA-256 e a instalação em pasta de teste, incluindo a recusa de um arquivo adulterado. O teste desta atualização não exige reiniciar o PC.

## Dependências

Motor sing-box: [SagerNet/sing-box](https://github.com/SagerNet/sing-box), GPL-3.0. Driver/API Wintun: [site oficial](https://www.wintun.net/). Veja `THIRD_PARTY_NOTICES.md`.

App pessoal independente, sem vínculo com Discord ou Proton. O nome e o ícone do Discord identificam a aplicação à qual o túnel se destina.

### Correção de escala — 1.3.1

A interface usa coordenadas lógicas de 96 DPI e dimensiona seus controles conforme a escala do Windows. Os testes de layout medem a altura dos textos, largura dos botões, sobreposições e barras horizontais nos tamanhos normal, mínimo e maximizado. A correção foi validada localmente em 96 DPI (100%) e 120 DPI (125%, escala real do computador). Não é necessário alterar a escala do Windows.

### Navegação — 1.3.2

Os botões laterais alternam entre Visão geral (conexão e inicialização), Perfis WireGuard (lista e controles da VPN) e Atividade (histórico ampliado). A página selecionada fica destacada. A navegação mantém o perfil selecionado e as mensagens de atividade.

### Interface escura completa — 1.4.0

O topo agora usa uma barra escura própria. Arraste pelo título para mover, dê duplo clique para maximizar/restaurar e use os botões à direita para minimizar, maximizar ou ocultar na bandeja. A janela continua redimensionável pelas bordas. A Visão geral ganhou um cartão de acesso aos perfis; botões e seleção lateral receberam novos contornos e cores. Os testes também verificam os controles da janela e o redimensionamento.

### Interface de referência — 1.5.0

A interface foi adaptada do HTML de referência do usuário para os controles nativos do aplicativo, com as logos do Discord preservadas. A Visão geral inclui botão circular de energia, painel de informações e cartões responsivos. A chave de inicialização salva ao clicar; se a autorização for cancelada, o estado salvo é restaurado. A página Atividade possui contador, Copiar e Limpar (somente o conteúdo exibido). Os dados de perfis e estados vêm do aplicativo, sem incorporar os exemplos do HTML. O aplicativo continua sem dependência de navegador ou fontes remotas.
