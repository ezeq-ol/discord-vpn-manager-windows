using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace DiscordVPNManager {
 public sealed class ReleaseInfo {
  public Version Version;
  public string DownloadUrl;
  public string ChecksumUrl;
  public string PageUrl;
 }
 public static class UpdateClient {
  public const string Repository="ezeq-ol/discord-vpn-manager-windows";
  public static Version CurrentVersion {get{return Assembly.GetExecutingAssembly().GetName().Version;}}
  private sealed class Client : WebClient {
   public Client(){Headers[HttpRequestHeader.UserAgent]="DiscordVPNManager/"+CurrentVersion.ToString(3);Headers[HttpRequestHeader.Accept]="application/vnd.github+json";Encoding=System.Text.Encoding.UTF8;}
   protected override WebRequest GetWebRequest(Uri address){var r=base.GetWebRequest(address);r.Timeout=20000;var h=r as HttpWebRequest;if(h!=null)h.ReadWriteTimeout=20000;return r;}
  }
  public static Version ParseVersion(string text){Version v;if(string.IsNullOrWhiteSpace(text)||!Version.TryParse(text.Trim().TrimStart('v','V'),out v))throw new FormatException("Versão inválida na resposta do GitHub.");return new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));}
  private static string TrustedUrl(string text,string prefix){Uri u;if(!Uri.TryCreate(text,UriKind.Absolute,out u)||u.Scheme!="https"||!u.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||!u.AbsolutePath.StartsWith("/"+Repository+prefix,StringComparison.Ordinal))throw new InvalidDataException("O endereço da atualização não pertence ao projeto.");return u.AbsoluteUri;}
  public static ReleaseInfo ParseRelease(string json){
   var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
   if(data==null||!data.ContainsKey("tag_name")||!data.ContainsKey("assets"))throw new InvalidDataException("Resposta de atualização incompleta.");
   if(data.ContainsKey("draft")&&Convert.ToBoolean(data["draft"])||data.ContainsKey("prerelease")&&Convert.ToBoolean(data["prerelease"]))throw new InvalidDataException("A versão encontrada ainda não é estável.");
   var info=new ReleaseInfo{Version=ParseVersion(Convert.ToString(data["tag_name"])),PageUrl=TrustedUrl(Convert.ToString(data["html_url"]),"/releases/tag/")};
   foreach(var item in (IEnumerable)data["assets"]){var asset=item as Dictionary<string,object>;if(asset==null)continue;string name=Convert.ToString(asset["name"]);if(name=="DiscordVPNManager.exe")info.DownloadUrl=TrustedUrl(Convert.ToString(asset["browser_download_url"]),"/releases/download/");if(name=="DiscordVPNManager-Windows.sha256")info.ChecksumUrl=TrustedUrl(Convert.ToString(asset["browser_download_url"]),"/releases/download/");}
   if(info.DownloadUrl==null||info.ChecksumUrl==null)throw new InvalidDataException("A release não contém o executável e sua verificação de integridade.");
   return info;
  }
  public static ReleaseInfo FetchLatest(){ServicePointManager.SecurityProtocol=(SecurityProtocolType)3072;using(var c=new Client())return ParseRelease(c.DownloadString("https://api.github.com/repos/"+Repository+"/releases/latest"));}
  public static string ParseChecksum(string text){foreach(string line in text.TrimStart('\uFEFF').Replace("\r","").Split('\n')){var m=Regex.Match(line.Trim(),@"^([a-fA-F0-9]{64})\s+\*?DiscordVPNManager\.exe$",RegexOptions.IgnoreCase);if(m.Success)return m.Groups[1].Value.ToLowerInvariant();}throw new InvalidDataException("A soma SHA-256 do executável não foi encontrada.");}
  public static string Hash(string path){using(var s=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
  public static string Download(ReleaseInfo release,string folder,out string expectedHash){
   Directory.CreateDirectory(folder);string path=Path.Combine(folder,"DiscordVPNManager.exe");
   ServicePointManager.SecurityProtocol=(SecurityProtocolType)3072;
   using(var c=new Client()){expectedHash=ParseChecksum(c.DownloadString(release.ChecksumUrl));c.DownloadFile(release.DownloadUrl,path);}
   if(!Hash(path).Equals(expectedHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("O arquivo baixado falhou na verificação SHA-256. Nada foi instalado.");
   return path;
  }
  private static string Quote(string s){return "'"+s.Replace("'","''")+"'";}
  public static string InstallScript(string source,string installed,string current,int pid,string hash){
   if(!Regex.IsMatch(hash,@"\A[a-fA-F0-9]{64}\z"))throw new ArgumentException("Invalid SHA-256");
   return "$ErrorActionPreference = 'Stop'\r\n"+
    "$source = "+Quote(Path.GetFullPath(source))+"\r\n$installed = "+Quote(Path.GetFullPath(installed))+"\r\n$current = "+Quote(Path.GetFullPath(current))+"\r\n"+
    "$result = Join-Path (Split-Path -Parent $installed) 'update-result.txt'\r\n"+
    "try {\r\n"+
    " $deadline = (Get-Date).AddSeconds(60)\r\n"+
    " while ((Get-Process -Id "+pid+" -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }\r\n"+
    " if (Get-Process -Id "+pid+" -ErrorAction SilentlyContinue) { throw 'O Manager não foi encerrado para atualização.' }\r\n"+
    " if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne '"+hash+"') { throw 'SHA-256 inválido.' }\r\n"+
    " New-Item -ItemType Directory -Path (Split-Path -Parent $installed) -Force | Out-Null\r\n"+
    " $staged = $installed + '.new'\r\n $backup = $installed + '.previous'\r\n"+
    " Copy-Item -LiteralPath $source -Destination $staged -Force\r\n"+
    " if ((Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash -ne '"+hash+"') { throw 'SHA-256 do arquivo preparado invalido.' }\r\n"+
    " $copied = $false\r\n for ($i=0; $i -lt 20; $i++) { try { if ([IO.File]::Exists($installed)) { [IO.File]::Replace($staged,$installed,$backup,$true) } else { [IO.File]::Move($staged,$installed) }; $copied=$true; break } catch { Start-Sleep -Milliseconds 500 } }\r\n"+
    " if (-not $copied) { throw 'Nao foi possivel substituir o executavel instalado.' }\r\n"+
    " if ($current -ne $installed) { try { Copy-Item -LiteralPath $source -Destination $current -Force } catch { } }\r\n"+
    " 'SUCCESS: atualização instalada.' | Set-Content -LiteralPath $result -Encoding UTF8\r\n"+
    " Start-Process -FilePath $installed -ArgumentList '--updated' -WorkingDirectory (Split-Path -Parent $installed) -WindowStyle Normal\r\n"+
    "} catch {\r\n ('FAILED: ' + $_.Exception.Message) | Set-Content -LiteralPath $result -Encoding UTF8\r\n"+
    " if (Test-Path -LiteralPath $installed) { Start-Process -FilePath $installed -ArgumentList '--update-failed' -WindowStyle Normal }\r\n}\r\n";
  }
 }
}
