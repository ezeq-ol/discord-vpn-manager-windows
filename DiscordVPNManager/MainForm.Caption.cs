using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DiscordVPNManager {
 internal sealed partial class MainForm {
  private CaptionButton _captionMaximize;
  [DllImport("user32.dll")] private static extern bool ReleaseCapture();
  [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);

  private Control BuildCaption() {
   var bar=new Panel{Dock=DockStyle.Fill,BackColor=ColorBg,Margin=Padding.Empty};
   var title=new Label{Text="DISCORD VPN MANAGER",AutoSize=false,Bounds=new Rectangle(44,0,300,38),Font=new Font("Segoe UI",8,FontStyle.Bold),ForeColor=ColorMuted,TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.Transparent};bar.Controls.Add(title);
   var mark=new PictureBox{Bounds=new Rectangle(18,9,20,20),SizeMode=PictureBoxSizeMode.Zoom};using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("logo-transparent.png")){if(stream!=null)using(var image=Image.FromStream(stream))mark.Image=new Bitmap(image);}bar.Controls.Add(mark);
   MouseEventHandler drag=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0x00A1,new IntPtr(2),IntPtr.Zero);}};
   MouseEventHandler maximize=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left)ToggleMaximize();};
   bar.MouseDown+=drag;title.MouseDown+=drag;mark.MouseDown+=drag;bar.MouseDoubleClick+=maximize;title.MouseDoubleClick+=maximize;
   var actions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=144,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty,Padding=Padding.Empty};bar.Controls.Add(actions);
   var min=new CaptionButton(0){AccessibleName="Minimizar"};_captionMaximize=new CaptionButton(1){AccessibleName="Maximizar ou restaurar"};var close=new CaptionButton(2){AccessibleName="Ocultar na bandeja"};
   actions.Controls.Add(min);actions.Controls.Add(_captionMaximize);actions.Controls.Add(close);
   min.Click+=delegate{WindowState=FormWindowState.Minimized;};_captionMaximize.Click+=delegate{ToggleMaximize();};close.Click+=delegate{Close();};
   Resize+=delegate{_captionMaximize.Restore=WindowState==FormWindowState.Maximized;_captionMaximize.Invalidate();};
   bar.Paint+=delegate(object sender,PaintEventArgs e){using(var pen=new Pen(Color.FromArgb(34,37,52)))e.Graphics.DrawLine(pen,0,bar.Height-1,bar.Width,bar.Height-1);};
   return bar;
  }
  private void ToggleMaximize(){if(WindowState==FormWindowState.Normal)MaximizedBounds=Screen.FromControl(this).WorkingArea;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);MaximizedBounds=Screen.FromControl(this).WorkingArea;}
  protected override void WndProc(ref Message m){
   const int HitTest=0x84;
   base.WndProc(ref m);
   if(m.Msg!=HitTest||WindowState!=FormWindowState.Normal||m.Result.ToInt32()!=1)return;
   var point=PointToClient(new Point((short)(m.LParam.ToInt64()&0xffff),(short)((m.LParam.ToInt64()>>16)&0xffff)));
   int border=Logical(6);bool left=point.X<border,right=point.X>=ClientSize.Width-border,top=point.Y<border,bottom=point.Y>=ClientSize.Height-border;
   int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1;
   m.Result=new IntPtr(hit);
  }
 }
 internal sealed class CaptionButton:Button {
  private readonly int kind;private bool hovered;
  public bool Restore;
  public CaptionButton(int kind){this.kind=kind;Size=new Size(48,38);Margin=Padding.Empty;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.FromArgb(11,13,20);ForeColor=Color.FromArgb(184,190,211);TabStop=true;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(hovered?(kind==2?Color.FromArgb(190,49,75):Color.FromArgb(39,43,60)):BackColor);
   float scale=e.Graphics.DpiX/96f;int size=(int)(10*scale),x=(Width-size)/2,y=(Height-size)/2;
   using(var pen=new Pen(hovered?Color.White:ForeColor,Math.Max(1f,scale))){
    if(kind==0)e.Graphics.DrawLine(pen,x,y+size/2,x+size,y+size/2);
    else if(kind==2){e.Graphics.DrawLine(pen,x,y,x+size,y+size);e.Graphics.DrawLine(pen,x+size,y,x,y+size);}
    else if(Restore){e.Graphics.DrawRectangle(pen,x+3,y,size-3,size-3);using(var brush=new SolidBrush(hovered?Color.FromArgb(39,43,60):BackColor))e.Graphics.FillRectangle(brush,x,y+3,size-3,size-3);e.Graphics.DrawRectangle(pen,x,y+3,size-3,size-3);}
    else e.Graphics.DrawRectangle(pen,x,y,size,size);
    if(Focused&&ShowFocusCues)e.Graphics.DrawRectangle(pen,4,4,Width-9,Height-9);
   }
  }
 }
}
