using System;using System.IO;using DiscordVPNManager;
class UpdateTests {
static void Assert(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS: "+name);}
static void Reject(Action action,string name){try{action();}catch(InvalidDataException){Console.WriteLine("PASS: "+name);return;}throw new Exception(name);}
static int Main(string[]a){
Assert(UpdateClient.ParseVersion("v1.3.0")==new Version(1,3,0,0),"version normalization");
string root="https://github.com/"+UpdateClient.Repository;
string json="{\"tag_name\":\"v1.4.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\""+root+"/releases/tag/v1.4.0\",\"assets\":[{\"name\":\"DiscordVPNManager.exe\",\"browser_download_url\":\""+root+"/releases/download/v1.4.0/DiscordVPNManager.exe\"},{\"name\":\"DiscordVPNManager-Windows.sha256\",\"browser_download_url\":\""+root+"/releases/download/v1.4.0/DiscordVPNManager-Windows.sha256\"}]}";
Assert(UpdateClient.ParseRelease(json).Version>UpdateClient.CurrentVersion,"new release recognized");
Reject(()=>UpdateClient.ParseRelease(json.Replace("github.com","evil.invalid")),"external download rejected");
Reject(()=>UpdateClient.ParseRelease(json.Replace("Windows.sha256","Windows.txt")),"missing checksum rejected");
Reject(()=>UpdateClient.ParseRelease(json.Replace("\"prerelease\":false","\"prerelease\":true")),"prerelease rejected");
Assert(UpdateClient.ParseChecksum(new string('a',64)+"  DiscordVPNManager.exe")==new string('a',64),"checksum exact asset");
Reject(()=>UpdateClient.ParseChecksum(new string('a',64)+"  Another.exe"),"unrelated checksum rejected");
string temp=Path.Combine(Path.GetTempPath(),"Discord updater tests ' "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
string source=Path.Combine(temp,"source.exe"), target=Path.Combine(temp,"installed.exe");File.Copy(a[0],source);File.WriteAllText(target,"old exe");
string hash=UpdateClient.Hash(source);string script=UpdateClient.InstallScript(source,target,target,99999999,hash);
Assert(script.Contains("tests '' ")&&script.Contains("[IO.File]::Replace")&&!script.Contains("Stop-Process"),"quoted paths and atomic replacement without stopping tunnel");
File.WriteAllText(Path.Combine(temp,"install.ps1"),script,new System.Text.UTF8Encoding(true));
File.WriteAllText(Path.Combine(Path.GetDirectoryName(a[0]),"updater-test-folder.txt"),temp);
if(a.Length>1&&a[1]=="--online"){var latest=UpdateClient.FetchLatest();Console.WriteLine("Live GitHub release: "+latest.Version);string expected;var download=UpdateClient.Download(latest,Path.Combine(temp,"download"),out expected);Assert(UpdateClient.Hash(download)==expected,"live release download and SHA-256");}
return 0;
}}
