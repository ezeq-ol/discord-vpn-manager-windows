using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

public static class TraySupport
{
	private sealed class TrayState
	{
		public readonly NotifyIcon Icon;

		public bool Exiting;

		public bool Notified;

		private readonly Form form;

		private readonly ContextMenuStrip menu;

		private bool stopping;

		public TrayState(Form owner)
		{
			form = owner;
			menu = new ContextMenuStrip();
			ToolStripItemCollection items = menu.Items;
			EventHandler onClick = delegate
			{
				Restore();
			};
			items.Add("Abrir Discord VPN Manager", null, onClick);
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add("Desconectar VPN e sair", null, delegate
			{
				StopAndExit();
			});
			Icon = new NotifyIcon
			{
				Icon = (form.Icon ?? SystemIcons.Application),
				Text = "Discord VPN Manager",
				ContextMenuStrip = menu,
				Visible = true
			};
			Icon.MouseClick += delegate(object sender, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					Restore();
				}
			};
			form.FormClosed += delegate
			{
				Icon.Visible = false;
				Icon.Dispose();
				menu.Dispose();
				states.Remove(form);
			};
		}

		private void StopAndExit()
		{
			if (stopping)
			{
				return;
			}
			FieldInfo field = form.GetType().GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null && (bool)field.GetValue(form))
			{
				Restore();
				MessageBox.Show(form, "Aguarde a operação atual terminar antes de sair.", "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			string script = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\Discord Single-Tunneling\\Aplicar-VPN-Discord.ps1");
			if (!File.Exists(script))
			{
				MessageBox.Show("Script de desconexão não encontrado. O gerenciador continuará aberto.", "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			StartupSupport.CancelStartup();
			stopping = true;
			form.Enabled = false;
			menu.Items[2].Enabled = false;
			Icon.Text = "Discord VPN: desconectando...";
			Thread thread = new Thread((ThreadStart)delegate
			{
				string error = null;
				try
				{
					ProcessStartInfo startInfo = new ProcessStartInfo
					{
						FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe"),
						Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -Stop",
						UseShellExecute = true,
						Verb = "runas",
						WindowStyle = ProcessWindowStyle.Hidden
					};
					using (Process process = Process.Start(startInfo))
					{
						process.WaitForExit();
						if (process.ExitCode != 0)
						{
							throw new Exception("Falha ao desligar a VPN (código " + process.ExitCode + ").");
						}
					}
				}
				catch (Exception ex)
				{
					error = ex.Message;
				}
				if (!form.IsDisposed)
				{
					form.BeginInvoke((Action)delegate
					{
						stopping = false;
						form.Enabled = true;
						menu.Items[2].Enabled = true;
						Icon.Text = "Discord VPN Manager";
						if (error != null)
						{
							Restore();
							MessageBox.Show(form, error + "\nO gerenciador continuará aberto.", "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
						}
						else
						{
							Exiting = true;
							form.Close();
							if (!form.IsDisposed)
							{
								Exiting = false;
							}
						}
					});
				}
			});
			thread.IsBackground = true;
			thread.Start();
		}

		private void Restore()
		{
			form.Show();
			if (form.WindowState == FormWindowState.Minimized)
			{
				form.WindowState = FormWindowState.Normal;
			}
			form.Activate();
			form.BringToFront();
		}
	}

	private static readonly Dictionary<Form, TrayState> states = new Dictionary<Form, TrayState>();

	public static void Attach(Form form)
	{
		if (!states.ContainsKey(form))
		{
			TrayState value = new TrayState(form);
			states.Add(form, value);
			StartupSupport.Attach(form);
		}
	}

    public static void CloseForUpdate(Form form) {
        TrayState state;
        if(!states.TryGetValue(form,out state))throw new InvalidOperationException("Manager não encontrado na bandeja.");
        state.Exiting=true;StartupSupport.CancelStartup();form.Close();
        if(!form.IsDisposed)state.Exiting=false;
    }

	public static bool HideOnClose(Form form, FormClosingEventArgs e)
	{
		TrayState value;
		if (e.CloseReason != CloseReason.UserClosing || !states.TryGetValue(form, out value) || value.Exiting)
		{
			return false;
		}
		e.Cancel = true;
		form.Hide();
		if (!value.Notified)
		{
			value.Notified = true;
			value.Icon.ShowBalloonTip(3500, "Discord VPN Manager", "Continuo aberto na bandeja. Clique no ícone para abrir; botão direito para sair.", ToolTipIcon.Info);
		}
		return true;
	}
}
