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
	internal sealed class MainForm : Form
	{
		private static readonly Color ColorBg = Color.FromArgb(15, 17, 26);

		private static readonly Color ColorPanel = Color.FromArgb(26, 29, 43);

		private static readonly Color ColorPanelSoft = Color.FromArgb(20, 23, 35);

		private static readonly Color ColorText = Color.FromArgb(242, 243, 245);

		private static readonly Color ColorMuted = Color.FromArgb(181, 186, 193);

		private static readonly Color ColorAccent = Color.FromArgb(126, 65, 238);

		private static readonly Color ColorSecondary = Color.FromArgb(44, 48, 68);

		private static readonly Color ColorDanger = Color.FromArgb(218, 55, 60);

		private static readonly Color ColorGreen = Color.FromArgb(35, 165, 90);

		private static readonly Color ColorRed = Color.FromArgb(242, 63, 67);

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
			base.ClientSize = new Size(760, 700);
			DoubleBuffered = true;
			BackColor = ColorBg;
			ForeColor = ColorText;
			Font = new Font("Segoe UI", 9.75f);
			base.FormBorderStyle = FormBorderStyle.FixedSingle;
			base.MaximizeBox = false;
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

		private void BuildUi()
		{
			Label label = new Label();
			label.Text = "Discord VPN";
			label.Font = new Font("Segoe UI", 20f, FontStyle.Bold);
			label.ForeColor = ColorText;
			label.AutoSize = true;
			label.Location = new Point(76, 12);
			base.Controls.Add(label);
			PictureBox pictureBox = new PictureBox();
			pictureBox.Location = new Point(20, 16);
			pictureBox.Size = new Size(46, 46);
			pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo-transparent.png"))
			{
				if (stream != null)
				{
					using (Image original = Image.FromStream(stream))
					{
						pictureBox.Image = new Bitmap(original);
					}
				}
				else
				{
					pictureBox.Image = base.Icon.ToBitmap();
				}
			}
			base.Controls.Add(pictureBox);
			Label label2 = new Label();
			label2.Text = "Sua conexão. Seu controle. Escolha um perfil para conectar.";
			label2.Font = new Font("Segoe UI", 9.5f);
			label2.ForeColor = ColorMuted;
			label2.AutoSize = true;
			label2.Location = new Point(79, 50);
			base.Controls.Add(label2);
			Panel panel = new RoundedPanel();
			panel.Location = new Point(18, 78);
			panel.Size = new Size(724, 92);
			panel.BackColor = ColorPanel;
			base.Controls.Add(panel);
			_lblTunnel = MakeStatusLabel("Túnel inativo", 14, 10, 360, 22, 11f, FontStyle.Bold, ColorRed);
			_lblSingbox = MakeStatusLabel("sing-box: parado", 14, 36, 360, 18, 9.25f, FontStyle.Regular, ColorMuted);
			_lblActive = MakeStatusLabel("Perfil ativo: -", 14, 60, 696, 18, 9.25f, FontStyle.Regular, ColorText);
			_lblEndpoint = MakeStatusLabel("Endpoint: -", 380, 10, 330, 18, 9.25f, FontStyle.Regular, ColorMuted);
			_lblDiscord = MakeStatusLabel("Discord: fechado", 380, 36, 330, 18, 9.25f, FontStyle.Regular, ColorMuted);
			_lblEndpoint.TextAlign = ContentAlignment.MiddleRight;
			_lblDiscord.TextAlign = ContentAlignment.MiddleRight;
			panel.Controls.Add(_lblTunnel);
			panel.Controls.Add(_lblSingbox);
			panel.Controls.Add(_lblActive);
			panel.Controls.Add(_lblEndpoint);
			panel.Controls.Add(_lblDiscord);
			Label label3 = new Label();
			label3.Text = "SEUS PERFIS";
			label3.Font = new Font("Segoe UI", 9.75f, FontStyle.Bold);
			label3.ForeColor = ColorText;
			label3.AutoSize = true;
			label3.Location = new Point(18, 184);
			base.Controls.Add(label3);
			LinkLabel linkLabel = new LinkLabel();
			linkLabel.Text = "Baixar perfis .conf no Proton VPN";
			linkLabel.Location = new Point(412, 180);
			linkLabel.Size = new Size(330, 26);
			linkLabel.TextAlign = ContentAlignment.MiddleRight;
			linkLabel.LinkColor = Color.FromArgb(184, 151, 255);
			linkLabel.ActiveLinkColor = Color.White;
			linkLabel.VisitedLinkColor = linkLabel.LinkColor;
			linkLabel.LinkBehavior = LinkBehavior.HoverUnderline;
			linkLabel.AccessibleDescription = "Abrir a página de downloads do Proton VPN no navegador para obter arquivos .conf.";
			linkLabel.LinkClicked += delegate
			{
				try
				{
					Process.Start(new ProcessStartInfo("https://account.protonvpn.com/downloads")
					{
						UseShellExecute = true
					});
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "Não foi possível abrir o navegador. Acesse https://account.protonvpn.com/downloads\r\n\r\n" + ex.Message, "Baixar perfis", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				}
			};
			base.Controls.Add(linkLabel);
			_list = new ListView();
			_list.Location = new Point(18, 206);
			_list.Size = new Size(724, 170);
			_list.View = View.Details;
			_list.FullRowSelect = true;
			_list.MultiSelect = false;
			_list.HideSelection = false;
			_list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
			_list.BorderStyle = BorderStyle.None;
			_list.BackColor = ColorPanel;
			_list.ForeColor = ColorText;
			_list.Font = new Font("Segoe UI", 9.25f);
			_list.OwnerDraw = true;
			_headerFont = new Font("Segoe UI", 9f, FontStyle.Bold);
			_list.Columns.Add("Perfil", 205);
			_list.Columns.Add("Endpoint", 285);
			_list.Columns.Add("Modificado", 145);
			_list.Columns.Add("Ativo", 64);
			_list.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e)
			{
				using (SolidBrush brush = new SolidBrush(ColorPanelSoft))
				{
					e.Graphics.FillRectangle(brush, e.Bounds);
				}
				using (SolidBrush brush2 = new SolidBrush(ColorMuted))
				{
					e.Graphics.DrawString(e.Header.Text, _headerFont, brush2, e.Bounds.X + 8, e.Bounds.Y + 6);
				}
			};
			_list.DrawItem += delegate(object sender, DrawListViewItemEventArgs e)
			{
				e.DrawDefault = false;
			};
			_list.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
			{
				Color color = (e.Item.Selected ? Color.FromArgb(67, 47, 108) : ColorPanel);
				using (SolidBrush brush = new SolidBrush(color))
				{
					e.Graphics.FillRectangle(brush, e.Bounds);
				}
				Rectangle bounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
				TextRenderer.DrawText(e.Graphics, e.SubItem.Text, _list.Font, bounds, ColorText, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			};
			_list.DoubleClick += delegate
			{
				if (_list.SelectedItems.Count > 0)
				{
					ApplySelected();
				}
			};
			base.Controls.Add(_list);
			_btnAdd = MakeButton("Adicionar .conf", 18, 384, 150, 32, ColorSecondary);
			_btnAdd.Click += delegate
			{
				AddProfile();
			};
			base.Controls.Add(_btnAdd);
			_btnOpenFolder = MakeButton("Abrir pasta", 176, 384, 120, 32, ColorSecondary);
			_btnOpenFolder.Click += delegate
			{
				try
				{
					Directory.CreateDirectory(_profilesDir);
					Process.Start("explorer.exe", _profilesDir);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "Discord VPN", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				}
			};
			base.Controls.Add(_btnOpenFolder);
			_btnRefresh = MakeButton("Atualizar", 304, 384, 100, 32, ColorSecondary);
			_btnRefresh.Click += delegate
			{
				RefreshProfiles();
				AppendLog("Lista de perfis atualizada.");
			};
			base.Controls.Add(_btnRefresh);
			Label label4 = new Label();
			label4.Text = "Dica: clique duas vezes em um perfil para aplicar.";
			label4.ForeColor = ColorMuted;
			label4.Font = new Font("Segoe UI", 9f);
			label4.AutoSize = true;
			label4.Location = new Point(424, 391);
			base.Controls.Add(label4);
			_btnApply = MakeButton("Conectar VPN", 18, 426, 220, 40, ColorAccent);
			_btnApply.Click += delegate
			{
				ApplySelected();
			};
			base.Controls.Add(_btnApply);
			_btnStop = MakeButton("Desconectar", 246, 426, 150, 40, ColorDanger);
			_btnStop.Click += delegate
			{
				StopVpn();
			};
			base.Controls.Add(_btnStop);
			_btnDiscord = MakeButton("Abrir Discord", 404, 426, 160, 40, ColorSecondary);
			_btnDiscord.Click += delegate
			{
				OpenDiscord();
			};
			base.Controls.Add(_btnDiscord);
			_lblOperation = new Label();
			_lblOperation.Text = "Pronto";
			_lblOperation.ForeColor = ColorMuted;
			_lblOperation.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
			_lblOperation.TextAlign = ContentAlignment.MiddleRight;
			_lblOperation.Location = new Point(570, 426);
			_lblOperation.Size = new Size(172, 20);
			base.Controls.Add(_lblOperation);
			_progress = new ProgressBar();
			_progress.Location = new Point(572, 452);
			_progress.Size = new Size(170, 8);
			_progress.Style = ProgressBarStyle.Marquee;
			_progress.MarqueeAnimationSpeed = 0;
			_progress.Visible = false;
			base.Controls.Add(_progress);
			Panel panel2 = new RoundedPanel();
			panel2.Location = new Point(18, 480);
			panel2.Size = new Size(724, 88);
			panel2.BackColor = ColorPanel;
			base.Controls.Add(panel2);
			_startup = new CheckBox();
			_startup.Text = "Iniciar VPN com o Windows";
			_startup.Location = new Point(16, 12);
			_startup.Size = new Size(460, 26);
			_startup.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
			_startup.Checked = File.Exists(Path.Combine(_appDir, "start-with-windows.flag"));
			panel2.Controls.Add(_startup);
			Label value = MakeStatusLabel("Ao entrar no Windows, conectar usando o último perfil aplicado.", 16, 44, 535, 26, 9f, FontStyle.Regular, ColorMuted);
			panel2.Controls.Add(value);
			_btnStartup = MakeButton("Salvar opção", 558, 24, 150, 38, ColorSecondary);
			_btnStartup.Click += delegate
			{
				StartupSupport.SaveStartup(this);
			};
			panel2.Controls.Add(_btnStartup);
			Label label5 = new Label();
			label5.Text = "Atividade";
			label5.Font = new Font("Segoe UI", 9.75f, FontStyle.Bold);
			label5.ForeColor = ColorText;
			label5.AutoSize = true;
			label5.Location = new Point(18, 584);
			base.Controls.Add(label5);
			_log = new TextBox();
			_log.Location = new Point(18, 608);
			_log.Size = new Size(724, 80);
			_log.Multiline = true;
			_log.ReadOnly = true;
			_log.ScrollBars = ScrollBars.Vertical;
			_log.BorderStyle = BorderStyle.None;
			_log.BackColor = ColorPanelSoft;
			_log.ForeColor = ColorMuted;
			_log.Font = new Font("Consolas", 8.5f);
			_log.TabStop = false;
			base.Controls.Add(_log);
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
			b.TabStop = false;
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
			_lblTunnel.Text = (flag ? "Túnel ativo" : "Túnel inativo");
			_lblTunnel.ForeColor = (flag ? ColorGreen : ColorRed);
			_lblTunnel.Text = (flag ? "● " : "● ") + _lblTunnel.Text;
			_lblSingbox.Text = ((array.Length > 0) ? ("sing-box: em execução (PID " + array[0].Id + ")") : "sing-box: parado");
			_lblSingbox.ForeColor = ((array.Length > 0) ? ColorGreen : ColorMuted);
			_lblDiscord.Text = ((array2.Length > 0) ? "Discord: aberto" : "Discord: fechado");
			_lblDiscord.ForeColor = ((array2.Length > 0) ? ColorText : ColorMuted);
			string configEndpoint = GetConfigEndpoint();
			_lblEndpoint.Text = ((configEndpoint != null) ? ("Endpoint: " + configEndpoint) : "Endpoint: -");
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
			bool flag = _scriptPath != null;
			_btnApply.Enabled = !busy && flag;
			_btnStop.Enabled = !busy && flag;
			_btnAdd.Enabled = !busy;
			_btnOpenFolder.Enabled = !busy;
			_btnRefresh.Enabled = !busy;
			_btnDiscord.Enabled = !busy;
			_btnStartup.Enabled = !busy && flag;
			_startup.Enabled = !busy && flag;
			base.UseWaitCursor = busy;
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
