using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
namespace DiscordVPNManager {
 internal sealed partial class MainForm {
  private Button _btnUpdate;private Label _updateStatus;private bool _updateBusy;
  private DarkScrollPanel _page;private float _layoutScale=1f;private Action _resizeContent;private bool _layoutReady;
  private Action<int> _navigate;private int _activeSection;private Button[] _navButtons;private PowerButton _power;
  private int Logical(int value){return (int)Math.Round(value*_layoutScale);}
  private Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size,Color color,FontStyle style=FontStyle.Regular){var label=MakeStatusLabel(text,x,y,w,h,size,style,color);label.BackColor=Color.Transparent;parent.Controls.Add(label);return label;}
  private RoundedPanel Card(Control parent,int y,int h,int width){var card=new RoundedPanel{Location=new Point(40,y),Size=new Size(width,h),BackColor=ColorPanel,BorderColor=Color.FromArgb(35,40,56)};parent.Controls.Add(card);return card;}
  private LinkLabel Link(Control parent,string title,string url,Rectangle bounds){var link=new LinkLabel{Text=title,Bounds=bounds,LinkColor=Color.FromArgb(182,172,255),ActiveLinkColor=Color.White,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleRight,LinkBehavior=LinkBehavior.HoverUnderline};link.LinkClicked+=delegate{try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){AppendLog(e.Message);}};parent.Controls.Add(link);return link;}
  protected override void OnShown(EventArgs e){base.OnShown(e);if(WindowState==FormWindowState.Normal){var area=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));Location=new Point(area.X+(area.Width-Width)/2,area.Y+(area.Height-Height)/2);}_layoutReady=true;_navigate(_activeSection);}
  private void BuildUi(){
   SuspendLayout();
   var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,Padding=Padding.Empty};frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,40));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(frame);frame.Controls.Add(BuildCaption(),0,0);
   var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Padding=Padding.Empty};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,248));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.Controls.Add(shell,0,1);
   var side=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(13,16,24),Margin=Padding.Empty};shell.Controls.Add(side,0,0);
   side.Paint+=delegate(object sender,PaintEventArgs e){using(var pen=new Pen(Color.FromArgb(26,30,44)))e.Graphics.DrawLine(pen,side.Width-1,0,side.Width-1,side.Height);};
   var logo=new PictureBox{Bounds=new Rectangle(24,24,40,40),SizeMode=PictureBoxSizeMode.Zoom};using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("logo-transparent.png")){if(stream!=null)using(var image=Image.FromStream(stream))logo.Image=new Bitmap(image);}side.Controls.Add(logo);
   LabelAt(side,"Discord VPN",76,22,160,29,15,ColorText,FontStyle.Bold);LabelAt(side,"Túnel só para o Discord",76,50,160,22,8.1f,ColorMuted);
   var nav0=MakeButton("Visão geral",16,92,216,44,side.BackColor);var nav1=MakeButton("Perfis WireGuard",16,142,216,44,side.BackColor);var nav2=MakeButton("Atividade",16,192,216,44,side.BackColor);
   _navButtons=new Button[]{nav0,nav1,nav2};for(int i=0;i<3;i++){_navButtons[i].TextAlign=ContentAlignment.MiddleLeft;((RoundedButton)_navButtons[i]).NavigationIndex=i;side.Controls.Add(_navButtons[i]);}
   var note=new RoundedPanel{Bounds=new Rectangle(16,264,216,104),BackColor=ColorPanel,BorderColor=Color.FromArgb(31,36,51)};side.Controls.Add(note);LabelAt(note,"ⓘ",14,14,22,24,12,Color.FromArgb(182,172,255));LabelAt(note,"Só o Discord",43,12,155,24,9,ColorText,FontStyle.Bold);LabelAt(note,"Seus outros apps\ncontinuam na conexão\nnormal.",43,38,158,59,9,ColorMuted);
   var bottom=new Panel{Dock=DockStyle.Bottom,Height=136,BackColor=side.BackColor};side.Controls.Add(bottom);LabelAt(bottom,"Versão "+UpdateClient.CurrentVersion.ToString(3),24,3,200,24,9,ColorMuted);
   _btnUpdate=MakeButton("Buscar atualizações",24,38,200,40,ColorSecondary);bottom.Controls.Add(_btnUpdate);_btnUpdate.Click+=delegate{CheckForUpdates();};_updateStatus=LabelAt(bottom,"Atualizações pelo GitHub",24,90,156,25,8,ColorMuted);Link(bottom,"Projeto ↗","https://github.com/ezeq-ol/discord-vpn-manager-windows",new Rectangle(175,90,60,25));side.Resize+=delegate{note.Visible=side.Height-bottom.Height>Logical(390);};
   _page=new DarkScrollPanel{Dock=DockStyle.Fill,BackColor=ColorBg,Margin=Padding.Empty};shell.Controls.Add(_page,1,0);var canvas=new Panel{Dock=DockStyle.None,Size=new Size(832,700),BackColor=ColorBg};_page.Controls.Add(canvas);
   var heading=LabelAt(canvas,"Sua conexão, sob controle.",40,30,1000,54,27,ColorText,FontStyle.Bold);var subtitle=LabelAt(canvas,"Conexão e inicialização do Discord.",40,85,1000,28,10.5f,ColorMuted);
   var hero=Card(canvas,140,188,752);_power=new PowerButton{Bounds=new Rectangle(22,30,128,128)};hero.Controls.Add(_power);_power.Click+=delegate{if(_power.Connected)StopVpn();else ApplySelected();};
   _lblTunnel=LabelAt(hero,"Desligado",172,23,450,48,23,ColorGreen,FontStyle.Bold);_lblSingbox=LabelAt(hero,"Somente o Discord usa a VPN.",172,73,440,27,10.5f,ColorMuted);
   _btnApply=MakeButton("Conectar VPN",172,112,145,44,ColorAccent);_btnApply.Click+=delegate{ApplySelected();};_btnApply.ForeColor=ColorBg;hero.Controls.Add(_btnApply);
   _btnStop=MakeButton("Desconectar",172,112,124,44,ColorDanger);_btnStop.ForeColor=Color.FromArgb(255,154,171);_btnStop.Click+=delegate{StopVpn();};hero.Controls.Add(_btnStop);
   _btnDiscord=MakeButton("Abrir Discord  ↗",306,112,151,44,ColorSecondary);_btnDiscord.Click+=delegate{OpenDiscord();};hero.Controls.Add(_btnDiscord);
   var info=new RoundedPanel{Bounds=new Rectangle(430,28,300,132),BackColor=Color.FromArgb(13,16,24),BorderColor=Color.FromArgb(31,36,51)};hero.Controls.Add(info);
   LabelAt(info,"Servidor",16,4,78,40,9,ColorMuted);_lblEndpoint=LabelAt(info,"—",98,4,186,40,9.4f,ColorText);_lblEndpoint.TextAlign=ContentAlignment.MiddleRight;
   LabelAt(info,"Perfil ativo",16,46,90,40,9,ColorMuted);_lblActive=LabelAt(info,"—",108,46,176,40,9.4f,ColorText);_lblActive.TextAlign=ContentAlignment.MiddleRight;
   LabelAt(info,"Discord",16,88,90,40,9,ColorMuted);_lblDiscord=LabelAt(info,"fechado",108,88,176,40,9.4f,ColorText);_lblDiscord.TextAlign=ContentAlignment.MiddleRight;
   info.Paint+=delegate(object sender,PaintEventArgs e){using(var pen=new Pen(Color.FromArgb(31,36,51))){e.Graphics.DrawLine(pen,0,Logical(44),info.Width,Logical(44));e.Graphics.DrawLine(pen,0,Logical(87),info.Width,Logical(87));}};
   _lblOperation=LabelAt(hero,"Pronto",172,164,460,20,8,ColorMuted);_progress=new ProgressBar{Bounds=new Rectangle(24,180,400,4),Style=ProgressBarStyle.Marquee,Visible=false};hero.Controls.Add(_progress);
   var startup=Card(canvas,348,154,752);var startupTitle=LabelAt(startup,"Iniciar com o Windows",22,18,400,34,16,ColorText,FontStyle.Bold);var startupDescription=LabelAt(startup,"Ao entrar, a bandeja abre, a VPN conecta e o Discord é aberto.",22,56,680,42,9.5f,ColorMuted);
   _startup=new ToggleCheckBox{Bounds=new Rectangle(678,22,52,30),Checked=StartupSupport.IsStartupEnabled()};startup.Controls.Add(_startup);_startup.Click+=delegate{StartupSupport.SaveStartup(this);};
   _btnStartup=MakeButton("Salvar opção",0,0,150,38,ColorSecondary);_btnStartup.Visible=false;startup.Controls.Add(_btnStartup);_btnStartup.Click+=delegate{StartupSupport.SaveStartup(this);};
   var flow=new FlowSteps{Bounds=new Rectangle(22,110,600,27)};startup.Controls.Add(flow);
   var shortcut=Card(canvas,522,154,752);var nextTitle=LabelAt(shortcut,"Seu próximo servidor",22,18,600,34,16,ColorText,FontStyle.Bold);var nextDescription=LabelAt(shortcut,"Importe e escolha seus perfis .conf do Proton VPN.",22,56,680,42,9.5f,ColorMuted);var manage=MakeButton("＋ Escolher perfil",22,108,178,42,ColorPanel);((RoundedButton)manage).Dashed=true;shortcut.Controls.Add(manage);manage.Click+=delegate{_navigate(1);};
   var profiles=Card(canvas,140,350,752);LabelAt(profiles,"Perfis WireGuard",22,12,320,34,16,ColorText,FontStyle.Bold);var proton=Link(profiles,"Baixar .conf no Proton VPN ↗","https://account.protonvpn.com/downloads",new Rectangle(400,14,330,30));
   _list=new ListView{Bounds=new Rectangle(20,58,712,224),View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,HeaderStyle=ColumnHeaderStyle.Nonclickable,BorderStyle=BorderStyle.None,BackColor=ColorPanel,ForeColor=ColorText,Font=new Font("Segoe UI",10),OwnerDraw=true};profiles.Controls.Add(_list);
   var rows=new ImageList{ImageSize=new Size(1,42)};rows.Images.Add(new Bitmap(1,42));_list.SmallImageList=rows;_list.Disposed+=delegate{rows.Dispose();};_headerFont=new Font("Segoe UI",8.5f,FontStyle.Bold);_list.Columns.Add("PERFIL",200);_list.Columns.Add("SERVIDOR",230);_list.Columns.Add("MODIFICADO",182);_list.Columns.Add("EM USO",100);
   _list.DrawColumnHeader+=delegate(object sender,DrawListViewColumnHeaderEventArgs e){using(var brush=new SolidBrush(ColorPanel))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,_headerFont,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-16,e.Bounds.Height),ColorMuted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
   _list.DrawItem+=delegate(object sender,DrawListViewItemEventArgs e){e.DrawDefault=false;};_list.DrawSubItem+=delegate(object sender,DrawListViewSubItemEventArgs e){using(var brush=new SolidBrush(e.Item.Selected?Color.FromArgb(30,32,54):ColorPanel))e.Graphics.FillRectangle(brush,e.Bounds);if(e.Item.Selected&&e.ColumnIndex==0)using(var brush=new SolidBrush(ColorAccent))e.Graphics.FillRectangle(brush,e.Bounds.X,e.Bounds.Y+5,3,e.Bounds.Height-10);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,_list.Font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-17,e.Bounds.Height),e.ColumnIndex==3&&e.SubItem.Text=="Sim"?ColorGreen:ColorText,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};_list.DoubleClick+=delegate{if(_list.SelectedItems.Count>0)ApplySelected();};
   _btnAdd=MakeButton("＋ Adicionar .conf",20,298,178,40,ColorSecondary);_btnAdd.Click+=delegate{AddProfile();};profiles.Controls.Add(_btnAdd);_btnOpenFolder=MakeButton("Abrir pasta",208,298,126,40,ColorSecondary);_btnOpenFolder.Click+=delegate{try{Directory.CreateDirectory(_profilesDir);Process.Start("explorer.exe",_profilesDir);}catch(Exception e){AppendLog(e.Message);}};profiles.Controls.Add(_btnOpenFolder);_btnRefresh=MakeButton("Recarregar lista",344,298,156,40,ColorSecondary);_btnRefresh.Click+=delegate{RefreshProfiles();AppendLog("Lista de perfis atualizada.");};profiles.Controls.Add(_btnRefresh);var hint=LabelAt(profiles,"Duplo clique para conectar",510,302,220,30,9,ColorMuted);hint.TextAlign=ContentAlignment.MiddleRight;
   var activity=Card(canvas,140,400,752);LabelAt(activity,"Atividade recente",22,14,330,34,16,ColorText,FontStyle.Bold);var count=LabelAt(activity,"0 eventos",350,18,140,28,9,ColorMuted);var copy=MakeButton("Copiar",514,14,98,36,ColorSecondary);var clear=MakeButton("Limpar",622,14,98,36,ColorSecondary);activity.Controls.Add(copy);activity.Controls.Add(clear);
   _log=new TextBox{Bounds=new Rectangle(20,66,712,310),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=ColorBg,ForeColor=Color.FromArgb(213,218,232),Font=new Font("Consolas",9.5f),TabStop=false};activity.Controls.Add(_log);_log.TextChanged+=delegate{count.Text=Array.FindAll(_log.Lines,line=>!string.IsNullOrWhiteSpace(line)).Length+" eventos";};copy.Click+=delegate{if(_log.TextLength>0)Clipboard.SetText(_log.Text);};clear.Click+=delegate{_log.Clear();};
   var profileServer=LabelAt(hero,"Servidor",0,0,190,24,8,ColorMuted);var profileEndpoint=LabelAt(hero,"—",0,0,190,30,9.3f,ColorText);_lblEndpoint.TextChanged+=delegate{profileEndpoint.Text=_lblEndpoint.Text;};
   Action layout=delegate{
    int width=Math.Max(Logical(620),canvas.ClientSize.Width-Logical(80));bool wide=width>=Logical(1000),compact=width>=Logical(1150);int margin=Logical(40);
    hero.Left=profiles.Left=activity.Left=margin;hero.Width=profiles.Width=activity.Width=width;heading.Width=subtitle.Width=width;
    _btnApply.Visible=_activeSection!=2&&(_activeSection==1||!_power.Connected);_btnStop.Visible=_activeSection!=2&&(_activeSection==1||_power.Connected);
    _power.Visible=_activeSection==0;info.Visible=_activeSection==0;profileServer.Visible=profileEndpoint.Visible=_activeSection==1&&compact;
    if(_activeSection==0){
     hero.Top=Logical(140);hero.Height=Logical(wide?188:330);_power.Bounds=new Rectangle(Logical(22),Logical(30),Logical(128),Logical(128));
     _lblTunnel.Location=new Point(Logical(172),Logical(23));_lblTunnel.Width=width-Logical(wide?530:195);_lblSingbox.Location=new Point(Logical(172),Logical(73));_lblSingbox.Width=width-Logical(wide?530:195);
     _btnApply.Location=_btnStop.Location=new Point(Logical(172),Logical(112));_btnDiscord.Location=new Point(Logical(_power.Connected?306:327),Logical(112));
     info.Bounds=wide?new Rectangle(width-Logical(328),Logical(28),Logical(300),Logical(132)):new Rectangle(Logical(22),Logical(180),width-Logical(44),Logical(132));
     _lblEndpoint.Width=info.Width-Logical(114);_lblActive.Width=_lblDiscord.Width=info.Width-Logical(124);
     int top=hero.Bottom+Logical(20),half=(width-Logical(22))/2;
     startup.Bounds=new Rectangle(margin,top,wide?half:width,Logical(154));shortcut.Bounds=new Rectangle(wide?margin+half+Logical(22):margin,wide?top:top+Logical(174),wide?half:width,Logical(154));
     startupTitle.Width=startup.Width-Logical(110);startupDescription.Width=startup.Width-Logical(44);_startup.Location=new Point(startup.Width-Logical(74),Logical(22));flow.Width=startup.Width-Logical(44);
     nextTitle.Width=nextDescription.Width=shortcut.Width-Logical(44);canvas.Height=shortcut.Bottom+Logical(24);
    }else if(_activeSection==1){
     profiles.Top=Logical(140);profiles.Height=Logical(350);hero.Top=profiles.Bottom+Logical(20);hero.Height=Logical(compact?112:170);
     _lblTunnel.Location=new Point(Logical(22),Logical(12));_lblTunnel.Width=width-Logical(44);_lblSingbox.Location=new Point(Logical(24),Logical(62));_lblSingbox.Width=width-Logical(48);
     _btnApply.Location=new Point(Logical(22),Logical(104));_btnStop.Location=new Point(Logical(177),Logical(104));_btnDiscord.Location=new Point(Logical(311),Logical(104));
     if(compact){_lblTunnel.Width=_lblSingbox.Width=width-Logical(770);profileServer.Location=new Point(width-Logical(720),Logical(24));profileEndpoint.Location=new Point(width-Logical(720),Logical(49));_btnApply.Location=new Point(width-Logical(482),Logical(34));_btnStop.Location=new Point(width-Logical(327),Logical(34));_btnDiscord.Location=new Point(width-Logical(193),Logical(34));}
     canvas.Height=hero.Bottom+Logical(24);
    }else{activity.Top=Logical(140);activity.Height=Math.Max(Logical(300),_page.ClientSize.Height-Logical(164));canvas.Height=activity.Bottom+Logical(24);}
    _lblOperation.Visible=false;_progress.Location=new Point(Logical(22),hero.Height-Logical(8));_progress.Width=width-Logical(44);
    proton.Left=width-Logical(352);_list.Width=width-Logical(40);int available=_list.ClientSize.Width;_list.Columns[0].Width=(int)(available*.28);_list.Columns[1].Width=(int)(available*.32);_list.Columns[3].Width=Logical(90);_list.Columns[2].Width=available-_list.Columns[0].Width-_list.Columns[1].Width-Logical(92);hint.Left=width-Logical(242);hint.Visible=width>=Logical(850);
    _log.Size=new Size(width-Logical(40),activity.Height-Logical(86));copy.Left=width-Logical(238);clear.Left=width-Logical(130);count.Visible=width>=Logical(850);
   };
   _navigate=delegate(int section){if(section<0||section>2)return;_activeSection=section;_page.AutoScrollPosition=Point.Empty;hero.Visible=section!=2;startup.Visible=shortcut.Visible=section==0;profiles.Visible=section==1;activity.Visible=section==2;heading.Text=section==0?"Sua conexão, sob controle.":section==1?"Seus perfis WireGuard":"Atividade da conexão";subtitle.Text=section==0?"Conexão e inicialização do Discord.":section==1?"Importe um .conf e selecione o perfil para conectar.":"Acompanhe os eventos e mensagens do Manager.";for(int i=0;i<3;i++){((RoundedButton)_navButtons[i]).IsSelected=i==section;_navButtons[i].AccessibleDescription=i==section?"Página atual":"Abrir página";_navButtons[i].Invalidate();}layout();_page.AutoScrollPosition=Point.Empty;};
   nav0.Click+=delegate{_navigate(0);};nav1.Click+=delegate{_navigate(1);};nav2.Click+=delegate{_navigate(2);};_resizeContent=layout;canvas.SizeChanged+=delegate{if(_layoutReady)layout();};_page.SizeChanged+=delegate{if(_layoutReady)layout();};
   using(var graphics=CreateGraphics())_layoutScale=graphics.DpiX/96f;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96f,96f);ResumeLayout(true);
  }
 }
}
