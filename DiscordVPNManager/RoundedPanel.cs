using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DiscordVPNManager {
 internal sealed class RoundedPanel : Panel {
  public Color GradientEnd {get;set;}
  public Color BorderColor {get;set;}
  public RoundedPanel(){DoubleBuffered=true;GradientEnd=Color.Empty;BorderColor=Color.FromArgb(37,41,58);}
  protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>32&&Height>32){var old=Region;Region=RoundedShape.Create(Width,Height,16);if(old!=null)old.Dispose();}Invalidate();}
  protected override void OnPaintBackground(PaintEventArgs e){if(Width<2||Height<2)return;using(var brush=new LinearGradientBrush(ClientRectangle,BackColor,GradientEnd.IsEmpty?BackColor:GradientEnd,LinearGradientMode.ForwardDiagonal))e.Graphics.FillRectangle(brush,ClientRectangle);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Width<32||Height<32)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=RoundedShape.Path(new RectangleF(.5f,.5f,Width-1,Height-1),16))using(var pen=new Pen(BorderColor))e.Graphics.DrawPath(pen,p);}
 }
}
