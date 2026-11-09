using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DiscordVPNManager
{
	internal static class Program
	{
		[DllImport("user32.dll")]
		private static extern bool SetProcessDPIAware();

		[STAThread]
		private static void Main()
		{
			if (StartupSupport.BeforeMain())
			{
				try
				{
					SetProcessDPIAware();
				}
				catch
				{
				}
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new MainForm());
			}
		}
	}
}
