using System;
using System.Drawing;
using System.Windows.Forms;
namespace DiscordVPNManager {
 internal sealed class DarkScrollPanel:Panel {
  private readonly DarkScrollRail rail;
  private Control content;
  private int offset;private bool arranging;
  public DarkScrollPanel(){base.AutoScroll=false;DoubleBuffered=true;TabStop=true;rail=new DarkScrollRail(this);Controls.Add(rail);}
  public new Point AutoScrollPosition {get{return new Point(0,-offset);}set{ScrollTo(value.Y);}}
  internal int Offset {get{return offset;}}
  internal int Maximum {get{return content==null?0:Math.Max(0,content.Height-ClientSize.Height);}}
  internal int ContentHeight {get{return content==null?0:content.Height;}}
  internal void ScrollTo(int value){offset=Math.Max(0,Math.Min(Maximum,value));if(content!=null)content.Top=-offset;rail.Invalidate();}
  internal void Wheel(int delta){ScrollTo(offset-(int)Math.Round(delta/120.0*80*ScaleFactor));}
  internal float ScaleFactor {get{using(var graphics=CreateGraphics())return graphics.DpiX/96f;}}
  protected override void OnControlAdded(ControlEventArgs e){base.OnControlAdded(e);if(e.Control is DarkScrollRail)return;content=e.Control;ObserveFocus(content);content.SizeChanged+=ContentSizeChanged;PerformLayout();}
  protected override void OnControlRemoved(ControlEventArgs e){if(e.Control==content){content.SizeChanged-=ContentSizeChanged;content=null;}base.OnControlRemoved(e);}
  private void ObserveFocus(Control control){control.Enter+=delegate{if(!control.Visible||control is Panel)return;var y=PointToClient(control.PointToScreen(Point.Empty)).Y;if(y<0)ScrollTo(offset+y);else if(y+control.Height>ClientSize.Height)ScrollTo(offset+y+control.Height-ClientSize.Height);};control.ControlAdded+=delegate(object sender,ControlEventArgs e){ObserveFocus(e.Control);};foreach(Control child in control.Controls)ObserveFocus(child);}
  private void ContentSizeChanged(object sender,EventArgs e){PerformLayout();}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(arranging||content==null||rail==null)return;arranging=true;try{bool needed=Maximum>0;int width=Math.Max(12,(int)(12*ScaleFactor));rail.Visible=needed;rail.Bounds=new Rectangle(ClientSize.Width-width,0,width,ClientSize.Height);content.Bounds=new Rectangle(0,-Math.Min(offset,Maximum),Math.Max(0,ClientSize.Width-(needed?width:0)),content.Height);offset=Math.Min(offset,Maximum);rail.BringToFront();rail.Invalidate();}finally{arranging=false;}}
  protected override void OnMouseWheel(MouseEventArgs e){Wheel(e.Delta);var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;}
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){switch(keyData){case Keys.PageDown:ScrollTo(offset+ClientSize.Height*4/5);return true;case Keys.PageUp:ScrollTo(offset-ClientSize.Height*4/5);return true;case Keys.Control|Keys.Home:ScrollTo(0);return true;case Keys.Control|Keys.End:ScrollTo(Maximum);return true;}return base.ProcessCmdKey(ref msg,keyData);}
  internal sealed class DarkScrollRail:Control {
   private readonly DarkScrollPanel owner;private bool dragging,hover;private int startY,startOffset;
   internal DarkScrollRail(DarkScrollPanel owner){this.owner=owner;TabStop=true;AccessibleName="Rolagem da página";AccessibleRole=AccessibleRole.ScrollBar;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
   private Rectangle Thumb {get{int margin=3;int h=Math.Max(36,(int)((long)Height*Height/Math.Max(1,owner.ContentHeight)));h=Math.Min(Height,h);int y=owner.Maximum==0?0:(int)((long)owner.Offset*(Height-h)/owner.Maximum);return new Rectangle(margin,y,Math.Max(2,Width-margin*2),h);}}
   protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(owner.BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;var thumb=Thumb;if(thumb.Height<4)return;using(var path=RoundedShape.Path(thumb,Math.Min(thumb.Width/2f,4)))using(var brush=new SolidBrush(dragging||hover?Color.FromArgb(89,78,125):Color.FromArgb(48,55,76)))e.Graphics.FillPath(brush,path);}
   protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
   protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
   protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();if(Thumb.Contains(e.Location)){dragging=true;startY=e.Y;startOffset=owner.Offset;Capture=true;}else owner.ScrollTo(owner.Offset+(e.Y<Thumb.Top?-1:1)*owner.ClientSize.Height*4/5);Invalidate();}
   protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){int travel=Height-Thumb.Height;if(travel>0)owner.ScrollTo(startOffset+(int)((long)(e.Y-startY)*owner.Maximum/travel));}}
   protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture){dragging=false;Invalidate();}base.OnMouseCaptureChanged(e);}
   protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Invalidate();base.OnMouseUp(e);}
   protected override void OnMouseWheel(MouseEventArgs e){owner.Wheel(e.Delta);}
   protected override bool IsInputKey(Keys keyData){return keyData==Keys.Up||keyData==Keys.Down||keyData==Keys.Home||keyData==Keys.End||base.IsInputKey(keyData);}
   protected override void OnKeyDown(KeyEventArgs e){switch(e.KeyCode){case Keys.Up:owner.ScrollTo(owner.Offset-(int)(40*owner.ScaleFactor));break;case Keys.Down:owner.ScrollTo(owner.Offset+(int)(40*owner.ScaleFactor));break;case Keys.Home:owner.ScrollTo(0);break;case Keys.End:owner.ScrollTo(owner.Maximum);break;default:base.OnKeyDown(e);return;}e.Handled=true;}
  }
 }
}
