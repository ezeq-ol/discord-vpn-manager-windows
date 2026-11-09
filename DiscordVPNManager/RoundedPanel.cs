using System;
using System.Drawing;
using System.Windows.Forms;

namespace DiscordVPNManager
{
	internal sealed class RoundedPanel : Panel
	{
		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			if (base.Width >= 24 && base.Height >= 24)
			{
				Region region = base.Region;
				base.Region = RoundedShape.Create(base.Width, base.Height, 12);
				if (region != null)
				{
					region.Dispose();
				}
			}
		}
	}
}
