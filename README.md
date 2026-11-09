# Discord VPN Manager — Windows

Gerenciador de perfis WireGuard que encaminha o Discord pela VPN e mantém os outros aplicativos na conexão normal.

## Download

Baixe o **DiscordVPNManager.exe** em [Releases](https://github.com/ezeq-ol/discord-vpn-manager-windows/releases). Ele contém o motor e os scripts necessários. Nenhum perfil ou chave de usuário é incluído.

## Recursos

- Importação e seleção de perfis `.conf`.
- Interface escura, ícone na bandeja e opção de desconectar e sair.
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

- `DiscordVPNManager/MainForm.cs`: interface, perfis e operações.
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

A inicialização usa um caminho instalado fixo. Ao atualizar esse arquivo, encerre o Manager pela bandeja antes de substituí-lo. Preserve a pasta de dados para manter os perfis.

Os testes em CI cobrem compilação, integridade dos recursos, leitura da opção de startup e rejeição de marcadores de prontidão inválidos. A ordem de inicialização foi simulada localmente; o PC não foi reiniciado para o teste.

## Dependências

Motor sing-box: [SagerNet/sing-box](https://github.com/SagerNet/sing-box), GPL-3.0. Driver/API Wintun: [site oficial](https://www.wintun.net/). Veja `THIRD_PARTY_NOTICES.md`.

App pessoal independente, sem vínculo com Discord ou Proton. O nome e o ícone do Discord identificam a aplicação à qual o túnel se destina.
