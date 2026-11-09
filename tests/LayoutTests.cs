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
static object Field(Form f,string name){return f.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
static void CheckViews(Form f){
var buttons=(Button[])Field(f,"_navButtons");var list=(ListView)Field(f,"_list");var log=(TextBox)Field(f,"_log");var apply=(Button)Field(f,"_btnApply");var startup=(CheckBox)Field(f,"_startup");
string history=log.Text;int selected=list.SelectedItems.Count;
for(int round=0;round<2;round++)for(int page=0;page<3;page++){
buttons[page].PerformClick();Application.DoEvents();
Require(list.Visible==(page==1),"Profiles page visibility");Require(log.Visible==(page==2),"Activity page visibility");Require(startup.Visible==(page==0),"Overview startup visibility");Require(apply.Visible==(page!=2),"Connection controls on overview and profiles");
for(int i=0;i<3;i++)Require((bool)buttons[i].GetType().GetProperty("IsSelected").GetValue(buttons[i],null)==(i==page),"Selected navigation indicator");
Require(log.Text==history,"Activity history preserved when navigating");Require(list.SelectedItems.Count==selected,"Profile selection preserved when navigating");Check(f);
}buttons[0].PerformClick();Application.DoEvents();
}
static void Save(Form f,string name){using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(Point.Empty,f.Size));image.Save(name);}}
[STAThread]static int Main(string[]a){if(a[1]=="system")SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var asm=Assembly.LoadFile(Path.GetFullPath(a[0]));var f=(Form)Activator.CreateInstance(asm.GetType("DiscordVPNManager.MainForm"));f.Opacity=0;f.ShowInTaskbar=false;f.Show();Application.DoEvents();using(var g=f.CreateGraphics())Console.WriteLine("Layout DPI: "+g.DpiX);CheckViews(f);if(a.Length>2)Save(f,a[2]+"-normal.png");f.WindowState=FormWindowState.Maximized;Application.DoEvents();CheckViews(f);if(a.Length>2)Save(f,a[2]+"-max.png");f.WindowState=FormWindowState.Normal;f.Size=f.MinimumSize;Application.DoEvents();CheckViews(f);if(a.Length>2)Save(f,a[2]+"-min.png");f.Dispose();Console.WriteLine("PASS: "+assertions+" layout assertions at normal, maximized and minimum sizes.");return 0;}
}
