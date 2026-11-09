using System;
using System.Drawing;
using System.Windows.Forms;

namespace DiscordVPNManager
{
	internal sealed class RoundedButton : Button
	{
		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			if (base.Width >= 16 && base.Height >= 16)
			{
				Region region = base.Region;
				base.Region = RoundedShape.Create(base.Width, base.Height, 8);
				if (region != null)
				{
					region.Dispose();
				}
			}
		}
	}
}
