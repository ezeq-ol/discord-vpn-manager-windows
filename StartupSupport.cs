using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public static class StartupSupport
{
	public const string EntryName = "Discord VPN Manager";

	private static Mutex mutex;

	private static EventWaitHandle activate;

	private static bool owns;

	private static volatile bool cancelled;

	private static Form owner;

	public static bool IsStartup
	{
		get
		{
			return Environment.GetCommandLineArgs().Any((string a) => a.Equals("--startup", StringComparison.OrdinalIgnoreCase));
		}
	}

	public static string AppDir
	{
		get
		{
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\Discord Single-Tunneling");
		}
	}

	public static bool BeforeMain()
	{
		try
		{
			string value = WindowsIdentity.GetCurrent().User.Value;
			mutex = new Mutex(false, "Local\\DiscordVPNManager-" + value);
			try
			{
				owns = mutex.WaitOne(0);
			}
			catch (AbandonedMutexException)
			{
				owns = true;
			}
			activate = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\DiscordVPNManager-Activate-" + value);
			if (!owns)
			{
				if (!IsStartup)
				{
					activate.Set();
				}
				activate.Dispose();
				mutex.Dispose();
				return false;
			}
			return true;
		}
		catch
		{
			return true;
		}
	}

	public static bool IsStartupEnabled()
	{
		if (!File.Exists(Path.Combine(AppDir, "start-with-windows.flag")))
		{
			return false;
		}
		using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
		{
			if (registryKey == null || !(registryKey.GetValue("Discord VPN Manager") is string))
			{
				return false;
			}
		}
		using (RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run"))
		{
			byte[] array = ((registryKey2 == null) ? null : (registryKey2.GetValue("Discord VPN Manager") as byte[]));
			return array == null || array.Length == 0 || (array[0] != 3 && array[0] != 7);
		}
	}

	private static FieldInfo Field(Form f, string name)
	{
		return f.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
	}

	private static void Call(Form f, string name, params object[] args)
	{
		f.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, args);
	}

	public static void SaveStartup(Form form)
	{
		try
		{
			if (!(bool)Field(form, "_busy").GetValue(form))
			{
				CheckBox checkBox = (CheckBox)Field(form, "_startup").GetValue(form);
				Field(form, "_startupOperation").SetValue(form, true);
				string fileName = Process.GetCurrentProcess().MainModule.FileName;
				Call(form, "RunScriptElevated", "-StartupMode " + (checkBox.Checked ? "On" : "Off") + " -ManagerPath \"" + fileName + "\"", "Salvando inicialização do Manager...", false);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(form, ex.GetBaseException().Message, "Inicialização", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	public static void Attach(Form form)
	{
		owner = form;
		try
		{
			((CheckBox)Field(form, "_startup").GetValue(form)).Checked = IsStartupEnabled();
		}
		catch
		{
		}
		if (activate != null)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				while (!cancelled)
				{
					try
					{
						if (activate.WaitOne(1000) && !form.IsDisposed && form.IsHandleCreated)
						{
							form.BeginInvoke((Action)delegate
							{
								Show(form);
							});
						}
					}
					catch
					{
						break;
					}
				}
			});
			thread.IsBackground = true;
			thread.Start();
		}
		form.FormClosed += delegate
		{
			cancelled = true;
			try
			{
				if (owns)
				{
					mutex.ReleaseMutex();
				}
			}
			catch
			{
			}
			try
			{
				if (activate != null)
				{
					activate.Dispose();
				}
				if (mutex != null)
				{
					mutex.Dispose();
				}
			}
			catch
			{
			}
		};
		if (!IsStartup)
		{
			return;
		}
		form.Opacity = 0.0;
		form.ShowInTaskbar = false;
		form.Shown += delegate
		{
			form.Hide();
			form.ShowInTaskbar = true;
			form.Opacity = 1.0;
			if (IsStartupEnabled())
			{
				BeginStartup(form);
			}
		};
	}

	private static void Show(Form form)
	{
		form.Show();
		if (form.WindowState == FormWindowState.Minimized)
		{
			form.WindowState = FormWindowState.Normal;
		}
		form.Activate();
	}

	public static void CancelStartup()
	{
		cancelled = true;
	}

	public static void Log(string text)
	{
		try
		{
			File.AppendAllText(Path.Combine(AppDir, "manager-startup.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine);
		}
		catch
		{
		}
	}

	public static bool IsTunnelReady(string path)
	{
		try
		{
			if (!File.Exists(path) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds > 90.0)
			{
				return false;
			}
			string[] array = File.ReadAllLines(path);
			int pid;
			if (array.Length < 2 || !int.TryParse(array[0], out pid))
			{
				return false;
			}
			if (!Process.GetProcessesByName("sing-box").Any((Process p) => p.Id == pid))
			{
				return false;
			}
			return NetworkInterface.GetAllNetworkInterfaces().Any((NetworkInterface n) => n.Name.Equals("tun-discord", StringComparison.OrdinalIgnoreCase) && n.OperationalStatus == OperationalStatus.Up);
		}
		catch
		{
			return false;
		}
	}

	private static void BeginStartup(Form form)
	{
		Log("STARTUP: Manager na bandeja; aguardando VPN.");
		Thread thread = new Thread((ThreadStart)delegate
		{
			string path = Path.Combine(AppDir, "vpn-ready.txt");
			DateTime dateTime = DateTime.UtcNow.AddSeconds(240.0);
			while (!cancelled && DateTime.UtcNow < dateTime)
			{
				if (!IsStartupEnabled())
				{
					Log("STARTUP: inicialização desativada; cancelada.");
					return;
				}
				if (IsTunnelReady(path))
				{
					Log("READY: túnel confirmado antes de abrir Discord.");
					if (cancelled)
					{
						return;
					}
					if (Process.GetProcessesByName("Discord").Length > 0)
					{
						Log("DISCORD: já estava aberto; nenhuma segunda instância iniciada.");
						return;
					}
					string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord\\Update.exe");
					if (!File.Exists(text))
					{
						Log("FAILED: Discord não instalado.");
						return;
					}
					if (IsTunnelReady(path))
					{
						try
						{
							Process.Start(new ProcessStartInfo(text, "--processStart Discord.exe")
							{
								UseShellExecute = true,
								WorkingDirectory = Path.GetDirectoryName(text),
								WindowStyle = ProcessWindowStyle.Hidden
							});
							Log("DISCORD: iniciado após VPN pronta.");
							return;
						}
						catch (Exception ex)
						{
							Log("FAILED: " + ex.Message);
							return;
						}
					}
				}
				else
				{
					Thread.Sleep(500);
				}
			}
			if (!cancelled)
			{
				Log("FAILED: VPN não ficou pronta em 240s. Discord não foi iniciado.");
				if (!form.IsDisposed && form.IsHandleCreated)
				{
					form.BeginInvoke((Action)delegate
					{
						Show(form);
						try
						{
							Call(form, "AppendLog", "A VPN não ficou pronta. O Discord não foi aberto automaticamente.");
						}
						catch
						{
						}
					});
				}
			}
		});
		thread.IsBackground = true;
		thread.Start();
	}
}
