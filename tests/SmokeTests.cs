using System;using System.IO;using System.Reflection;using System.Windows.Forms;
class VerifyStartup {
 [STAThread] static int Main(string[]a){
  var asm=Assembly.LoadFile(Path.GetFullPath(a[0]));var s=asm.GetType("StartupSupport",true);
  var ready=s.GetMethod("IsTunnelReady");string temp=Path.Combine(Path.GetTempPath(),"discord-vpn-readiness-test.txt");
  if((bool)ready.Invoke(null,new object[]{temp+".missing"}))throw new Exception("Missing marker accepted");
  File.WriteAllText(temp,"99999999\n1\n");if((bool)ready.Invoke(null,new object[]{temp}))throw new Exception("Dead PID accepted");File.Delete(temp);
  Console.WriteLine("PASS: missing marker and dead PID cannot release Discord.");
  var f=(Form)Activator.CreateInstance(asm.GetType("DiscordVPNManager.MainForm",true));
  var check=(CheckBox)f.GetType().GetField("_startup",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);
  Console.WriteLine("Startup checkbox: "+check.Checked);if(check.Checked!=(bool)s.GetMethod("IsStartupEnabled").Invoke(null,null))throw new Exception("Startup state not reflected in UI");
  f.Dispose();Console.WriteLine("PASS: Manager initializes and reflects the actual startup setting.");
  return 0;
 }
}
