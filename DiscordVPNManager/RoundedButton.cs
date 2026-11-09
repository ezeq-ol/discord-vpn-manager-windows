using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DiscordVPNManager {
 internal sealed class RoundedButton : Button {
  public bool IsSelected {get;set;}
  public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);UseVisualStyleBackColor=false;}
  protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>20&&Height>20){var old=Region;Region=RoundedShape.Create(Width,Height,10);if(old!=null)old.Dispose();}}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   var color=Enabled?(IsSelected?Color.FromArgb(49,36,77):BackColor):Color.FromArgb(35,37,51);
   using(var path=RoundedShape.Path(new RectangleF(.5f,.5f,Width-1,Height-1),10)){
    using(var brush=new LinearGradientBrush(ClientRectangle,color,IsSelected?Color.FromArgb(41,34,65):color,LinearGradientMode.ForwardDiagonal))e.Graphics.FillPath(brush,path);
    if(IsSelected){using(var accent=new SolidBrush(Color.FromArgb(166,125,255)))e.Graphics.FillRectangle(accent,3,Height/3,3,Height/3);}
    using(var pen=new Pen(IsSelected?Color.FromArgb(102,75,149):Color.FromArgb(52,57,77)))e.Graphics.DrawPath(pen,path);
   }
   if(Focused&&ShowFocusCues){using(var p=RoundedShape.Path(new RectangleF(1,1,Width-3,Height-3),9))using(var pen=new Pen(Color.FromArgb(201,181,255),1.5f))e.Graphics.DrawPath(pen,p);}
   var alignment=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
   TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(10,0,Width-20,Height),Enabled?ForeColor:Color.FromArgb(112,117,138),alignment|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  }
 }
}
