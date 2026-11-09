using System.Drawing;
using System.Drawing.Drawing2D;

namespace DiscordVPNManager
{
	internal static class RoundedShape
 {
        public static GraphicsPath Path(RectangleF r,float radius) {
            var p=new GraphicsPath();float d=radius*2;
            p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
		public static Region Create(int width, int height, int radius)
		{
			using (GraphicsPath graphicsPath = new GraphicsPath())
			{
				int num = radius * 2;
				graphicsPath.AddArc(0, 0, num, num, 180f, 90f);
				graphicsPath.AddArc(width - num, 0, num, num, 270f, 90f);
				graphicsPath.AddArc(width - num, height - num, num, num, 0f, 90f);
				graphicsPath.AddArc(0, height - num, num, num, 90f, 90f);
				graphicsPath.CloseFigure();
				return new Region(graphicsPath);
			}
		}
	}
}
