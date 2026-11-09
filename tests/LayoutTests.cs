using System;using System.Drawing;using System.IO;using System.Reflection;using System.Runtime.InteropServices;using System.Windows.Forms;
class LayoutTests {
[DllImport("user32.dll")]static extern bool SetProcessDPIAware();
static int assertions;
static void Require(bool ok,string message){assertions++;if(!ok)throw new Exception(message);}
static void Check(Control c){
if(c.Visible&&(c is Label||c is Button)&&!c.Text.Contains("\n")&&c.Text.Length>0){var measured=TextRenderer.MeasureText(c.Text,c.Font,new Size(10000,10000),TextFormatFlags.SingleLine);Require(c.Height>=measured.Height,"Text height: "+c.Text+" "+c.Height+" < "+measured.Height);if(c is Button)Require(c.Width-20>=measured.Width,"Button width: "+c.Text);}
if(c.Visible&&c is Panel&&c.Parent is Panel&&c.GetType().Name=="RoundedPanel"&&c.Parent.Parent is Panel)Require(c.Right<=c.Parent.ClientSize.Width,"Card outside canvas");
foreach(Control child in c.Controls){if(child.Visible&&child is Button)foreach(Control peer in c.Controls)if(peer.Visible&&peer is Label&&!(peer is LinkLabel)){var overlap=Rectangle.Intersect(child.Bounds,peer.Bounds);Require(overlap.Width<2||overlap.Height<2,"Overlapping label/button: "+child.Text+" / "+peer.Text); }Check(child);}
if(c is Panel&&((Panel)c).AutoScroll)Require(!((Panel)c).HorizontalScroll.Visible,"Unexpected horizontal scrollbar");
}
static void Save(Form f,string name){using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(Point.Empty,f.Size));image.Save(name);}}
[STAThread]static int Main(string[]a){if(a[1]=="system")SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var asm=Assembly.LoadFile(Path.GetFullPath(a[0]));var f=(Form)Activator.CreateInstance(asm.GetType("DiscordVPNManager.MainForm"));f.Opacity=0;f.ShowInTaskbar=false;f.Show();Application.DoEvents();using(var g=f.CreateGraphics())Console.WriteLine("Layout DPI: "+g.DpiX);Check(f);if(a.Length>2)Save(f,a[2]+"-normal.png");f.WindowState=FormWindowState.Maximized;Application.DoEvents();Check(f);if(a.Length>2)Save(f,a[2]+"-max.png");f.WindowState=FormWindowState.Normal;f.Size=f.MinimumSize;Application.DoEvents();Check(f);if(a.Length>2)Save(f,a[2]+"-min.png");f.Dispose();Console.WriteLine("PASS: "+assertions+" layout assertions at normal, maximized and minimum sizes.");return 0;}
}
