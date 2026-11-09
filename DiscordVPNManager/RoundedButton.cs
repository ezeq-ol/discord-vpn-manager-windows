using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DiscordVPNManager {
 internal sealed class RoundedButton : Button {
  public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);UseVisualStyleBackColor=false;}
  protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>20&&Height>20){var old=Region;Region=RoundedShape.Create(Width,Height,10);if(old!=null)old.Dispose();}}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   var color=Enabled?BackColor:Color.FromArgb(35,37,51);
   using(var b=new SolidBrush(color))e.Graphics.FillRectangle(b,ClientRectangle);
   if(Focused){using(var p=RoundedShape.Path(new RectangleF(1,1,Width-3,Height-3),9))using(var pen=new Pen(Color.FromArgb(201,181,255),1.5f))e.Graphics.DrawPath(pen,p);}
   var alignment=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
   TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(10,0,Width-20,Height),Enabled?ForeColor:Color.FromArgb(112,117,138),alignment|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  }
 }
}
