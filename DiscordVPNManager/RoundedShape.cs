using System.Drawing;
using System.Drawing.Drawing2D;

namespace DiscordVPNManager
{
	internal static class RoundedShape
	{
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
