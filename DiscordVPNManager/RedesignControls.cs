using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DiscordVPNManager {
 internal sealed class FlowSteps:Control {
  public FlowSteps(){Font=new Font("Segoe UI",9);DoubleBuffered=true;}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent.BackColor);float scale=g.DpiX/96f;int x=0;string[] steps={"Bandeja","VPN pronta","Discord"};foreach(string text in steps){int width=TextRenderer.MeasureText(text,Font).Width+(int)(16*scale);using(var path=RoundedShape.Path(new RectangleF(x,0,width,Height-1),7*scale))using(var brush=new SolidBrush(Color.FromArgb(22,26,40)))g.FillPath(brush,path);TextRenderer.DrawText(g,text,Font,new Rectangle(x,0,width,Height),Color.FromArgb(201,207,224),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);x+=width;if(text!="Discord"){TextRenderer.DrawText(g,"→",Font,new Rectangle(x,0,(int)(28*scale),Height),Color.FromArgb(154,163,184),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);x+=(int)(28*scale);}}}
 }
 internal sealed class PowerButton:Button {
  public bool Connected;
  public PowerButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.FromArgb(11,13,20);AccessibleName="Conectar ou desconectar VPN";SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent.BackColor);Color c=Enabled?(Connected?Color.FromArgb(74,222,159):Color.FromArgb(241,139,154)):Color.FromArgb(90,96,114);float pad=Width*.07f;using(var glow=new SolidBrush(Color.FromArgb(22,c)))g.FillEllipse(glow,0,0,Width-1,Height-1);using(var fill=new SolidBrush(BackColor))g.FillEllipse(fill,pad,pad,Width-pad*2,Height-pad*2);using(var pen=new Pen(c,2)){g.DrawEllipse(pen,pad,pad,Width-pad*2,Height-pad*2);pen.Width=Width*.026f;pen.StartCap=pen.EndCap=LineCap.Round;float d=Width*.23f;g.DrawArc(pen,Width/2-d/2,Height*.44f,d,d,-42,264);g.DrawLine(pen,Width/2,Height*.36f,Width/2,Height*.49f);}if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-3,-3));}
 }
 internal sealed class ToggleCheckBox:CheckBox {
  public ToggleCheckBox(){AutoSize=false;AccessibleName="Iniciar VPN com o Windows";SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent.BackColor);var bounds=new RectangleF(0,0,Width-1,Height-1);using(var path=RoundedShape.Path(bounds,(Height-1)/2f))using(var brush=new SolidBrush(!Enabled?Color.FromArgb(47,51,69):Checked?Color.FromArgb(139,124,255):Color.FromArgb(42,48,71)))g.FillPath(brush,path);float pad=Height*.1f,diameter=Height-pad*2;using(var brush=new SolidBrush(Enabled?Color.White:Color.FromArgb(140,145,158)))g.FillEllipse(brush,Checked?Width-pad-diameter:pad,pad,diameter,diameter);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-2,-2));}
 }
}
