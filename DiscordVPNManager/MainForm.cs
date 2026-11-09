using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace DiscordVPNManager
{
	internal sealed partial class MainForm : Form
	{
		private static readonly Color ColorBg = Color.FromArgb(15, 17, 26);

		private static readonly Color ColorPanel = Color.FromArgb(26, 29, 43);

		private static readonly Color ColorPanelSoft = Color.FromArgb(20, 23, 35);

		private static readonly Color ColorText = Color.FromArgb(242, 243, 245);

		private static readonly Color ColorMuted = Color.FromArgb(151, 157, 180);

		private static readonly Color ColorAccent = Color.FromArgb(139, 92, 246);

		private static readonly Color ColorSecondary = Color.FromArgb(44, 48, 68);

		private static readonly Color ColorDanger = Color.FromArgb(125, 48, 70);

		private static readonly Color ColorGreen = Color.FromArgb(94, 218, 174);

		private static readonly Color ColorRed = Color.FromArgb(241, 139, 154);

		private static readonly Color ColorYellow = Color.FromArgb(240, 178, 50);

		private readonly string _exeDir;

		private readonly string _appDir;

		private readonly string _profilesDir;

		private readonly string _configPath;

		private readonly string _scriptPath;

		private readonly string _settingsPath;

		private ListView _list;

		private Label _lblTunnel;

		private Label _lblSingbox;

		private Label _lblActive;

		private Label _lblEndpoint;

		private Label _lblDiscord;

		private Label _lblOperation;

		private TextBox _log;

		private Button _btnApply;

		private Button _btnStop;

		private Button _btnAdd;

		private Button _btnOpenFolder;

		private Button _btnRefresh;

		private Button _btnDiscord;

		private CheckBox _startup;

		private Button _btnStartup;

		private bool _startupOperation;

		private ProgressBar _progress;

		private Font _headerFont;

		private System.Windows.Forms.Timer _timer;

		private bool _busy;

		private int _stopAttempts;

		private string _lastProfilePath;

		private string _runtimeError;

		public MainForm()
		{
			_exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
			_appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Discord Single-Tunneling");
			_profilesDir = Path.Combine(_exeDir, "Perfis");
			_configPath = Path.Combine(_appDir, "config.json");
			_settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiscordVPNManager", "last-profile.txt");
			EnsureRuntime();
			_scriptPath = FindApplyScript();
			_lastProfilePath = LoadLastProfile();
			Text = "Discord VPN Manager";
			base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			base.ShowIcon = true;
			base.ShowInTaskbar = true;
			base.ClientSize = new Size(1080, 820);
			DoubleBuffered = true;
			BackColor = ColorBg;
			ForeColor = ColorText;
			Font = new Font("Segoe UI", 9.75f);
			base.FormBorderStyle = FormBorderStyle.Sizable;
			base.MaximizeBox = true;
            MinimumSize = new Size(970, 700);
			base.StartPosition = FormStartPosition.CenterScreen;
			base.AutoScaleMode = AutoScaleMode.Dpi;
			try
			{
				Directory.CreateDirectory(_profilesDir);
			}
			catch
			{
			}
			BuildUi();
			RefreshProfiles();
			AppendLog("Discord VPN Manager iniciado.");
			if (!string.IsNullOrEmpty(_runtimeError))
			{
				AppendLog("AVISO: falha ao preparar componentes internos: " + _runtimeError);
			}
			if (_scriptPath == null)
			{
				AppendLog("ERRO: Aplicar-VPN-Discord.ps1 não foi encontrado.");
			}
			else
			{
				AppendLog("Script: " + _scriptPath);
			}
			AppendLog("Perfis encontrados: " + _list.Items.Count + ".");
			SetBusy(false, null);
			UpdateStatus();
			_timer = new System.Windows.Forms.Timer();
			_timer.Interval = 2000;
			_timer.Tick += delegate
			{
				UpdateStatus();
			};
			_timer.Start();
			base.FormClosing += OnFormClosing;
			TraySupport.Attach(this);
		}


		private static Label MakeStatusLabel(string text, int x, int y, int w, int h, float size, FontStyle style, Color color)
		{
			Label label = new Label();
			label.Text = text;
			label.Location = new Point(x, y);
			label.Size = new Size(w, h);
			label.Font = new Font("Segoe UI", size, style);
			label.ForeColor = color;
			label.TextAlign = ContentAlignment.MiddleLeft;
			return label;
		}

		private static Button MakeButton(string text, int x, int y, int w, int h, Color back)
		{
			Button b = new RoundedButton();
			b.Text = text;
			b.Location = new Point(x, y);
			b.Size = new Size(w, h);
			b.FlatStyle = FlatStyle.Flat;
			b.FlatAppearance.BorderSize = 0;
			b.BackColor = back;
			b.ForeColor = Color.White;
			b.Font = new Font("Segoe UI", 9.75f, FontStyle.Bold);
			b.Cursor = Cursors.Hand;
			b.TabStop = true;
			b.MouseEnter += delegate
			{
				b.BackColor = Shift(back, 14);
			};
			b.MouseLeave += delegate
			{
				b.BackColor = back;
			};
			return b;
		}

		private static Color Shift(Color c, int delta)
		{
			return Color.FromArgb(c.A, Math.Min(255, Math.Max(0, c.R + delta)), Math.Min(255, Math.Max(0, c.G + delta)), Math.Min(255, Math.Max(0, c.B + delta)));
		}

		private void EnsureRuntime()
		{
			try
			{
				Directory.CreateDirectory(_appDir);
			}
			catch (Exception ex)
			{
				_runtimeError = ex.Message;
				return;
			}
			ExtractEmbedded("Aplicar-VPN-Discord.ps1", Path.Combine(_appDir, "Aplicar-VPN-Discord.ps1"));
			ExtractEmbedded("tunnel-launcher.ps1", Path.Combine(_appDir, "tunnel-launcher.ps1"));
			ExtractEmbedded("discord-waiter.ps1", Path.Combine(_appDir, "discord-waiter.ps1"));
			ExtractEmbedded("wintun.dll", Path.Combine(_appDir, "wintun.dll"));
			ExtractEmbedded("sing-box.exe", Path.Combine(_appDir, "sing-box.exe"));
		}

		private void ExtractEmbedded(string fileName, string destination)
		{
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string text = null;
				string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
				foreach (string text2 in manifestResourceNames)
				{
					if (string.Equals(text2, fileName, StringComparison.OrdinalIgnoreCase) || text2.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
					{
						text = text2;
						break;
					}
				}
				if (text == null)
				{
					return;
				}
				using (Stream stream = executingAssembly.GetManifestResourceStream(text))
				{
					if (stream == null || (File.Exists(destination) && new FileInfo(destination).Length == stream.Length))
					{
						return;
					}
					string text3 = destination + ".new";
					try
					{
						using (FileStream fileStream = new FileStream(text3, FileMode.Create, FileAccess.Write, FileShare.None))
						{
							byte[] array = new byte[81920];
							int count;
							while ((count = stream.Read(array, 0, array.Length)) > 0)
							{
								fileStream.Write(array, 0, count);
							}
						}
						if (File.Exists(destination))
						{
							File.Delete(destination);
						}
						File.Move(text3, destination);
					}
					finally
					{
						try
						{
							if (File.Exists(text3))
							{
								File.Delete(text3);
							}
						}
						catch
						{
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (string.IsNullOrEmpty(_runtimeError))
				{
					_runtimeError = fileName + ": " + ex.Message;
				}
			}
		}

		private string FindApplyScript()
		{
			string[] array = new string[3]
			{
				Path.Combine(_appDir, "Aplicar-VPN-Discord.ps1"),
				Path.Combine(_exeDir, "Aplicar-VPN-Discord.ps1"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Discord VPN", "Aplicar-VPN-Discord.ps1")
			};
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (File.Exists(text))
				{
					return text;
				}
			}
			return null;
		}

		private List<ProfileInfo> FindProfiles()
		{
			List<string> list = new List<string>();
			list.Add(_profilesDir);
			list.Add(_exeDir);
			list.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			list.Add(text);
			list.Add(Path.Combine(text, "Discord VPN"));
			List<ProfileInfo> list2 = new List<ProfileInfo>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string item in list)
			{
				if (!Directory.Exists(item))
				{
					continue;
				}
				string[] files;
				try
				{
					files = Directory.GetFiles(item, "*.conf", SearchOption.TopDirectoryOnly);
				}
				catch
				{
					continue;
				}
				string[] array = files;
				foreach (string path in array)
				{
					string fileName = Path.GetFileName(path);
					if (hashSet.Add(fileName))
					{
						ProfileInfo profileInfo = new ProfileInfo();
						profileInfo.Path = Path.GetFullPath(path);
						profileInfo.Name = Path.GetFileNameWithoutExtension(path);
						profileInfo.Endpoint = ReadProfileEndpoint(path);
						try
						{
							profileInfo.Modified = File.GetLastWriteTime(path);
						}
						catch
						{
							profileInfo.Modified = DateTime.MinValue;
						}
						list2.Add(profileInfo);
					}
				}
			}
			list2.Sort((ProfileInfo a, ProfileInfo b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
			return list2;
		}

		private static string ReadProfileEndpoint(string path)
		{
			try
			{
				string input = File.ReadAllText(path);
				Match match = Regex.Match(input, "(?im)^\\s*Endpoint\\s*=\\s*([^\\r\\n#]+)");
				if (match.Success)
				{
					return match.Groups[1].Value.Trim();
				}
			}
			catch
			{
			}
			return "-";
		}

		private void RefreshProfiles()
		{
			string text = GetSelectedPath();
			if (text == null)
			{
				text = _lastProfilePath;
			}
			_list.BeginUpdate();
			_list.Items.Clear();
			foreach (ProfileInfo item in FindProfiles())
			{
				ListViewItem listViewItem = new ListViewItem(item.Name);
				listViewItem.SubItems.Add(item.Endpoint);
				listViewItem.SubItems.Add((item.Modified == DateTime.MinValue) ? "-" : item.Modified.ToString("dd/MM/yyyy HH:mm"));
				listViewItem.SubItems.Add("");
				listViewItem.Tag = item.Path;
				_list.Items.Add(listViewItem);
			}
			_list.EndUpdate();
			if (text != null)
			{
				SelectByPath(text);
			}
			if (_list.SelectedItems.Count == 0 && _list.Items.Count > 0)
			{
				_list.Items[0].Selected = true;
				_list.Items[0].Focused = true;
			}
			UpdateStatus();
		}

		private void SelectByPath(string path)
		{
			if (path == null)
			{
				return;
			}
			string fileName = Path.GetFileName(path);
			foreach (ListViewItem item in _list.Items)
			{
				string text = item.Tag as string;
				if (text != null && string.Equals(text, path, StringComparison.OrdinalIgnoreCase))
				{
					item.Selected = true;
					item.Focused = true;
					_list.EnsureVisible(item.Index);
					return;
				}
			}
			foreach (ListViewItem item2 in _list.Items)
			{
				string text2 = item2.Tag as string;
				if (text2 != null && string.Equals(Path.GetFileName(text2), fileName, StringComparison.OrdinalIgnoreCase))
				{
					item2.Selected = true;
					item2.Focused = true;
					_list.EnsureVisible(item2.Index);
					break;
				}
			}
		}

		private string GetSelectedPath()
		{
			if (_list.SelectedItems.Count == 0)
			{
				return null;
			}
			return _list.SelectedItems[0].Tag as string;
		}

		private string LoadLastProfile()
		{
			try
			{
				if (File.Exists(_settingsPath))
				{
					string text = File.ReadAllText(_settingsPath, Encoding.UTF8).Trim();
					if (text.Length > 0)
					{
						return text;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private void SaveLastProfile(string path)
		{
			_lastProfilePath = path;
			try
			{
				string directoryName = Path.GetDirectoryName(_settingsPath);
				if (!Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				File.WriteAllText(_settingsPath, path, new UTF8Encoding(false));
			}
			catch
			{
			}
		}

		private void UpdateStatus()
		{
			bool flag = IsTunnelUp();
			Process[] array = SafeGetProcesses("sing-box");
			Process[] array2 = SafeGetProcesses("Discord");
			_lblTunnel.Text = (flag ? "Conectado" : "Desconectado");
			_lblTunnel.ForeColor = (flag ? ColorGreen : ColorRed);
			_lblTunnel.Text = (flag ? "● " : "● ") + _lblTunnel.Text;
			_lblSingbox.Text = flag ? "Somente o Discord usa a VPN." : "Conecte um perfil para começar.";
			_lblSingbox.ForeColor = ColorMuted;
			_lblDiscord.Text = ((array2.Length > 0) ? "Discord: aberto" : "Discord: fechado");
			_lblDiscord.ForeColor = ((array2.Length > 0) ? ColorText : ColorMuted);
			string configEndpoint = GetConfigEndpoint();
			_lblEndpoint.Text = ((configEndpoint != null) ? (configEndpoint) : "Nenhum servidor selecionado");
			UpdateActiveMarkers(configEndpoint);
		}

		private void UpdateActiveMarkers(string configEndpoint)
		{
			string text = NormalizeEndpoint(configEndpoint);
			string text2 = null;
			foreach (ListViewItem item in _list.Items)
			{
				string text3 = NormalizeEndpoint(item.SubItems[1].Text);
				bool flag = text != null && text3.Length > 0 && text3 == text;
				item.SubItems[3].Text = (flag ? "Sim" : "");
				item.SubItems[3].ForeColor = (flag ? ColorGreen : ColorMuted);
				if (flag && text2 == null)
				{
					text2 = item.Text;
				}
			}
			_lblActive.Text = ((text2 != null) ? ("Perfil ativo: " + text2) : "Perfil ativo: -");
		}

		private static Process[] SafeGetProcesses(string name)
		{
			try
			{
				return Process.GetProcessesByName(name);
			}
			catch
			{
				return new Process[0];
			}
		}

		private static bool IsTunnelUp()
		{
			try
			{
				NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				foreach (NetworkInterface networkInterface in allNetworkInterfaces)
				{
					string text = networkInterface.Name.ToLowerInvariant();
					string text2 = networkInterface.Description.ToLowerInvariant();
					if ((text.StartsWith("tun") || text2.Contains("sing-tun")) && networkInterface.OperationalStatus == OperationalStatus.Up)
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		private string GetConfigEndpoint()
		{
			try
			{
				string text = ReadSharedText(_configPath);
				if (text == null)
				{
					return null;
				}
				Match match = Regex.Match(text, "\"peers\"\\s*:\\s*\\[\\s*\\{.*?\"address\"\\s*:\\s*\"([^\"]+)\".*?\"port\"\\s*:\\s*(\\d+)", RegexOptions.Singleline);
				if (match.Success)
				{
					return match.Groups[1].Value.Trim() + ":" + match.Groups[2].Value.Trim();
				}
			}
			catch
			{
			}
			return null;
		}

		private static string ReadSharedText(string path)
		{
			try
			{
				if (!File.Exists(path))
				{
					return null;
				}
				using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8, true))
					{
						return streamReader.ReadToEnd();
					}
				}
			}
			catch
			{
				return null;
			}
		}

		private static string NormalizeEndpoint(string endpoint)
		{
			if (endpoint == null)
			{
				return "";
			}
			string text = endpoint.Trim().ToLowerInvariant();
			if (text == "-")
			{
				return "";
			}
			if (text.StartsWith("["))
			{
				int num = text.IndexOf(']');
				if (num > 0)
				{
					string text2 = text.Substring(1, num - 1);
					string text3 = text.Substring(num + 1);
					return text2 + text3;
				}
			}
			return text;
		}

		private void AddProfile()
		{
			if (_busy)
			{
				return;
			}
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				openFileDialog.Title = "Adicionar perfil .conf";
				openFileDialog.Filter = "WireGuard (*.conf)|*.conf|Todos os arquivos (*.*)|*.*";
				openFileDialog.Multiselect = false;
				if (openFileDialog.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}
				try
				{
					Directory.CreateDirectory(_profilesDir);
					string text = Path.Combine(_profilesDir, Path.GetFileName(openFileDialog.FileName));
					if (File.Exists(text) && !string.Equals(Path.GetFullPath(text), Path.GetFullPath(openFileDialog.FileName), StringComparison.OrdinalIgnoreCase))
					{
						DialogResult dialogResult = MessageBox.Show(this, "Já existe um perfil com esse nome. Substituir?", "Discord VPN", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
						if (dialogResult != DialogResult.Yes)
						{
							return;
						}
					}
					if (!string.Equals(Path.GetFullPath(text), Path.GetFullPath(openFileDialog.FileName), StringComparison.OrdinalIgnoreCase))
					{
						File.Copy(openFileDialog.FileName, text, true);
					}
					_lastProfilePath = text;
					SaveLastProfile(text);
					RefreshProfiles();
					AppendLog("Perfil adicionado: " + Path.GetFileName(text));
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				}
			}
		}

		private void ApplySelected()
		{
			if (_busy)
			{
				return;
			}
			if (_scriptPath == null)
			{
				MessageBox.Show(this, "Não foi possível preparar o script interno de aplicação.\n\nVerifique o log de atividade e execute o aplicativo novamente.", "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			string selectedPath = GetSelectedPath();
			if (selectedPath == null)
			{
				MessageBox.Show(this, "Selecione um perfil .conf na lista.", "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			SaveLastProfile(selectedPath);
			_stopAttempts = 0;
			RunScriptElevated("-ConfPath \"" + selectedPath + "\"", "Aplicando " + Path.GetFileName(selectedPath) + "...", true);
		}

		private void StopVpn()
		{
			if (!_busy)
			{
				if (_scriptPath == null)
				{
					MessageBox.Show(this, "O arquivo Aplicar-VPN-Discord.ps1 não foi encontrado.", "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
					return;
				}
				_stopAttempts = 0;
				RunScriptElevated("-Stop", "Parando a VPN...", false);
			}
		}

		private void RunScriptElevated(string arguments, string operation, bool isApply)
		{
			_busy = true;
			SetBusy(true, operation);
			AppendLog(operation);
			string script = _scriptPath;
			string logPath = Path.Combine(Path.GetDirectoryName(script), "Discord-VPN.log");
			long offset = 0L;
			try
			{
				if (File.Exists(logPath))
				{
					offset = new FileInfo(logPath).Length;
				}
			}
			catch
			{
			}
			Thread thread = new Thread((ThreadStart)delegate
			{
				int exitCode = -1;
				string error = null;
				try
				{
					using (Process process = Process.Start(new ProcessStartInfo
					{
						FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe"),
						Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" " + arguments,
						WorkingDirectory = Path.GetDirectoryName(script),
						Verb = "runas",
						UseShellExecute = true,
						WindowStyle = ProcessWindowStyle.Hidden
					}))
					{
						if (process == null)
						{
							throw new Exception("Não foi possível iniciar o PowerShell elevado.");
						}
						process.WaitForExit();
						try
						{
							exitCode = process.ExitCode;
						}
						catch
						{
							exitCode = -1;
						}
					}
				}
				catch (Win32Exception ex)
				{
					error = ((ex.NativeErrorCode == 1223) ? "cancel" : ex.Message);
				}
				catch (Exception ex2)
				{
					error = ex2.Message;
				}
				string appended = ReadLogSince(logPath, offset);
				try
				{
					BeginInvoke((Action)delegate
					{
						OnOperationDone(operation, isApply, exitCode, error, appended);
					});
				}
				catch
				{
				}
			});
			thread.IsBackground = true;
			thread.Start();
		}

		private void OnOperationDone(string operation, bool isApply, int exitCode, string error, string appended)
		{
			_busy = false;
			SetBusy(false, null);
			UpdateStatus();
			if (_startupOperation)
			{
				_startupOperation = false;
				_startup.Checked = File.Exists(Path.Combine(_appDir, "start-with-windows.flag"));
				if (error == null && exitCode == 0)
				{
					AppendLog(_startup.Checked ? "Inicialização com o Windows ativada." : "Inicialização com o Windows desativada.");
					return;
				}
				string text = ((error == "cancel") ? "Alteração cancelada. A opção anterior foi mantida." : ("Não foi possível salvar a opção. " + error));
				AppendLog(text);
				if (!string.IsNullOrEmpty(appended))
				{
					AppendLogTail(appended);
				}
				MessageBox.Show(this, text, "Inicialização", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			if (appended != null && appended.Trim().Length > 0)
			{
				AppendLogTail(appended);
			}
			if (error == "cancel")
			{
				AppendLog("Elevação cancelada pelo usuário.");
				return;
			}
			if (error != null)
			{
				AppendLog("ERRO: " + error);
				MessageBox.Show(this, error, "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			bool flag;
			string text2;
			if (isApply)
			{
				if (appended != null && appended.IndexOf("SUCCESS: tunnel is up", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					flag = true;
					text2 = "VPN aplicada e túnel ativo.";
				}
				else if (appended != null && (appended.IndexOf("FAILED:", StringComparison.OrdinalIgnoreCase) >= 0 || appended.IndexOf("ERROR:", StringComparison.OrdinalIgnoreCase) >= 0))
				{
					flag = false;
					text2 = "Falha ao aplicar a VPN. Veja o log de atividade.";
				}
				else
				{
					flag = exitCode == 0;
					text2 = (flag ? "VPN aplicada." : ("Falha ao aplicar a VPN (código " + exitCode + ")."));
				}
			}
			else if (SafeGetProcesses("sing-box").Length > 0 || IsTunnelUp())
			{
				if (_stopAttempts < 1)
				{
					_stopAttempts++;
					AppendLog("AVISO: a VPN ainda está ativa. Tentando parar novamente...");
					RunScriptElevated("-Stop", "Parando a VPN (tentativa 2)...", false);
					return;
				}
				flag = false;
				text2 = "A VPN ainda está em execução e não pôde ser encerrada. Feche o aplicativo e execute-o como administrador.";
			}
			else if (appended != null && appended.IndexOf("Tunnel stopped", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				flag = true;
				text2 = "VPN parada. O Discord voltou a conexão normal.";
			}
			else if (appended != null && appended.IndexOf("ERROR:", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				flag = false;
				text2 = "Falha ao parar a VPN. Veja o log de atividade.";
			}
			else
			{
				flag = exitCode == 0;
				text2 = (flag ? "VPN parada." : ("Falha ao parar a VPN (código " + exitCode + ")."));
			}
			AppendLog(text2);
			if (!flag)
			{
				MessageBox.Show(this, text2, "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			bool flag2 = SafeGetProcesses("Discord").Length > 0;
			if (isApply && flag2)
			{
				DialogResult dialogResult = MessageBox.Show(this, text2 + "\r\n\r\nO Discord está aberto. Reiniciá-lo agora para que todas as conexões usem a nova VPN?", "Discord VPN", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
				if (dialogResult == DialogResult.Yes)
				{
					RestartDiscord();
				}
			}
			else if (isApply)
			{
				DialogResult dialogResult2 = MessageBox.Show(this, text2 + "\r\n\r\nAbrir o Discord agora?", "Discord VPN", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
				if (dialogResult2 == DialogResult.Yes)
				{
					OpenDiscord();
				}
			}
			else
			{
				MessageBox.Show(this, text2, "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}

		private void OpenDiscord()
		{
			try
			{
				bool flag = SafeGetProcesses("Discord").Length > 0;
				string text = Path.Combine(_appDir, "discord-waiter.ps1");
				string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord", "Update.exe");
				if (!flag && File.Exists(text))
				{
					ProcessStartInfo processStartInfo = new ProcessStartInfo();
					processStartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
					processStartInfo.Arguments = "-WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + text + "\"";
					processStartInfo.UseShellExecute = false;
					processStartInfo.CreateNoWindow = true;
					Process.Start(processStartInfo);
					AppendLog("Abrindo o Discord depois que o túnel estiver pronto...");
				}
				else if (File.Exists(text2))
				{
					ProcessStartInfo processStartInfo2 = new ProcessStartInfo();
					processStartInfo2.FileName = text2;
					processStartInfo2.Arguments = "--processStart Discord.exe";
					processStartInfo2.WorkingDirectory = Path.GetDirectoryName(text2);
					processStartInfo2.UseShellExecute = true;
					Process.Start(processStartInfo2);
					AppendLog("Discord iniciado/ativado.");
				}
				else
				{
					MessageBox.Show(this, "Não encontrei a instalação do Discord.", "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private void RestartDiscord()
		{
			try
			{
				Process[] array = SafeGetProcesses("Discord");
				foreach (Process process in array)
				{
					try
					{
						process.Kill();
					}
					catch
					{
					}
				}
				Thread.Sleep(900);
				OpenDiscord();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private void SetBusy(bool busy, string operation)
		{
            _busy = busy;
			bool flag = _scriptPath != null;
			_btnApply.Enabled = !busy && flag;
			_btnStop.Enabled = !busy && flag;
			_btnAdd.Enabled = !busy;
			_btnOpenFolder.Enabled = !busy;
			_btnRefresh.Enabled = !busy;
			_btnDiscord.Enabled = !busy;
			_btnStartup.Enabled = !busy && flag;
            if (_btnUpdate != null) _btnUpdate.Enabled = !busy && !_updateBusy;
			_startup.Enabled = !busy && flag;
			base.UseWaitCursor = busy;
			_progress.Style = ProgressBarStyle.Marquee;
            _progress.Visible = busy;
			_progress.MarqueeAnimationSpeed = (busy ? 30 : 0);
			_lblOperation.Text = ((!busy) ? "Pronto" : ((operation == null) ? "Trabalhando..." : operation));
			_lblOperation.ForeColor = (busy ? ColorYellow : ColorMuted);
		}

		private void AppendLog(string text)
		{
			if (text != null)
			{
				if (_log.TextLength > 120000)
				{
					_log.Text = _log.Text.Substring(_log.TextLength - 60000);
				}
				_log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
			}
		}

		private void AppendLogTail(string text)
		{
			string text2 = text.Replace("\r\n", "\n").Replace("\r", "\n");
			string[] array = text2.Split('\n');
			int num = Math.Max(0, array.Length - 14);
			for (int i = num; i < array.Length; i++)
			{
				string text3 = array[i].TrimEnd();
				if (text3.Length != 0)
				{
					_log.AppendText("    " + text3 + Environment.NewLine);
				}
			}
		}

		private static string ReadLogSince(string path, long offset)
		{
			try
			{
				if (!File.Exists(path))
				{
					return "";
				}
				using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					if (fileStream.Length <= offset)
					{
						return "";
					}
					fileStream.Seek(offset, SeekOrigin.Begin);
					using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8))
					{
						return streamReader.ReadToEnd();
					}
				}
			}
			catch
			{
				return "";
			}
		}

		private void OnFormClosing(object sender, FormClosingEventArgs e)
		{
			if (TraySupport.HideOnClose(this, e))
			{
				return;
			}
			if (_busy)
			{
				DialogResult dialogResult = MessageBox.Show(this, "Uma operação está em andamento. Fechar mesmo assim?", "Discord VPN", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
				if (dialogResult != DialogResult.Yes)
				{
					e.Cancel = true;
					return;
				}
			}
			string selectedPath = GetSelectedPath();
			if (selectedPath != null)
			{
				SaveLastProfile(selectedPath);
			}
			if (_timer != null)
			{
				_timer.Stop();
			}
		}
	}
}
