using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DiscordVPNManager {
 internal sealed class RoundedButton : Button {
  public bool IsSelected {get;set;}
  public int NavigationIndex=-1;
  public bool Dashed;
  public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);UseVisualStyleBackColor=false;}
  protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>20&&Height>20){var old=Region;Region=RoundedShape.Create(Width,Height,10);if(old!=null)old.Dispose();}}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   var color=Enabled?(IsSelected?Color.FromArgb(28,31,51):BackColor):Color.FromArgb(35,37,51);
   using(var path=RoundedShape.Path(new RectangleF(.5f,.5f,Width-1,Height-1),10)){
    using(var brush=new LinearGradientBrush(ClientRectangle,color,color,LinearGradientMode.ForwardDiagonal))e.Graphics.FillPath(brush,path);
    if(IsSelected){using(var accent=new SolidBrush(Color.FromArgb(139,124,255)))e.Graphics.FillRectangle(accent,3,Height/3,3,Height/3);}
    if(NavigationIndex<0)using(var pen=new Pen(Color.FromArgb(42,48,71))){if(Dashed)pen.DashStyle=DashStyle.Dash;e.Graphics.DrawPath(pen,path);}
   }
   if(Focused&&ShowFocusCues){using(var p=RoundedShape.Path(new RectangleF(1,1,Width-3,Height-3),9))using(var pen=new Pen(Color.FromArgb(201,181,255),1.5f))e.Graphics.DrawPath(pen,p);}
   int inset=10;
   if(NavigationIndex>=0){float d=e.Graphics.DpiX/96f;inset=(int)(43*d);float x=14*d,y=(Height-16*d)/2;using(var pen=new Pen(IsSelected?Color.White:Color.FromArgb(180,188,207),1.5f*d)){
    if(NavigationIndex==0){e.Graphics.DrawRectangle(pen,x,y,5*d,7*d);e.Graphics.DrawRectangle(pen,x+9*d,y,5*d,4*d);e.Graphics.DrawRectangle(pen,x,y+11*d,5*d,4*d);e.Graphics.DrawRectangle(pen,x+9*d,y+8*d,5*d,7*d);}
    else if(NavigationIndex==1){e.Graphics.DrawRectangle(pen,x,y,15*d,5*d);e.Graphics.DrawRectangle(pen,x,y+10*d,15*d,5*d);}
    else e.Graphics.DrawLines(pen,new[]{new PointF(x,y+8*d),new PointF(x+4*d,y+8*d),new PointF(x+7*d,y),new PointF(x+10*d,y+16*d),new PointF(x+13*d,y+8*d),new PointF(x+16*d,y+8*d)});
   }}
   var alignment=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
   TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(inset,0,Width-inset-10,Height),Enabled?(NavigationIndex>=0&&!IsSelected?Color.FromArgb(180,188,207):ForeColor):Color.FromArgb(112,117,138),alignment|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  }
 }
}
