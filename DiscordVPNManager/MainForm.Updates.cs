using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
namespace DiscordVPNManager {
 internal sealed partial class MainForm {
  private void UpdateUi(Action action){if(!IsDisposed&&IsHandleCreated){try{BeginInvoke(action);}catch(InvalidOperationException){}}}
  private void FinishUpdate(string status){_updateBusy=false;SetBusy(false,null);_updateStatus.Text=status;}
  private void CheckForUpdates(){
   if(_busy||_updateBusy)return;
   _updateBusy=true;SetBusy(true,"Consultando GitHub...");_updateStatus.Text="Buscando uma nova versão...";
   new Thread(delegate(){
    try{var release=UpdateClient.FetchLatest();UpdateUi(delegate{
     FinishUpdate("Última consulta: "+DateTime.Now.ToString("HH:mm"));
     if(release.Version<=UpdateClient.CurrentVersion){MessageBox.Show(this,"Você já está na versão mais recente: "+UpdateClient.CurrentVersion.ToString(3)+".","Tudo atualizado",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
     if(MessageBox.Show(this,"A versão "+release.Version.ToString(3)+" está disponível no GitHub.\r\n\r\nBaixar e instalar agora? Seus perfis e a configuração de inicialização serão mantidos. O Manager reabre ao terminar.","Nova atualização",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)DownloadUpdate(release);
    });}catch(Exception ex){UpdateUi(delegate{FinishUpdate("Não foi possível consultar");AppendLog("Atualização: "+ex.Message);MessageBox.Show(this,"Não foi possível buscar atualizações agora. Verifique sua conexão e tente novamente.\r\n\r\n"+ex.Message,"Atualizações",MessageBoxButtons.OK,MessageBoxIcon.Warning);});}
   }){IsBackground=true}.Start();
  }
  private void DownloadUpdate(ReleaseInfo release){
   if(_busy)return;_updateBusy=true;SetBusy(true,"Baixando atualização...");_updateStatus.Text="Baixando versão "+release.Version.ToString(3);
   new Thread(delegate(){
    try{
     string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DiscordVPNManager","updates",release.Version.ToString(3));
     string hash;string downloaded=UpdateClient.Download(release,folder,out hash);
     string target=Path.Combine(_appDir,"DiscordVPNManager.exe");string script=Path.Combine(folder,"install-update.ps1");
     File.WriteAllText(script,UpdateClient.InstallScript(downloaded,target,Application.ExecutablePath,Process.GetCurrentProcess().Id,hash),new System.Text.UTF8Encoding(true));
     UpdateUi(delegate{
      FinishUpdate("Arquivo verificado. Instalando...");AppendLog("Atualização verificada por SHA-256. Reiniciando somente o Manager.");
      try{
       Process.Start(new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+script+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
       TraySupport.CloseForUpdate(this);
      }catch(Exception e){MessageBox.Show(this,e.Message,"Não foi possível instalar",MessageBoxButtons.OK,MessageBoxIcon.Error);}
     });
    }catch(Exception ex){UpdateUi(delegate{FinishUpdate("A atualização não foi instalada");AppendLog("Atualização: "+ex.Message);MessageBox.Show(this,ex.Message,"Atualização interrompida",MessageBoxButtons.OK,MessageBoxIcon.Warning);});}
   }){IsBackground=true}.Start();
  }
 }
}
