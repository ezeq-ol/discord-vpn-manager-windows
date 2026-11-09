using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DiscordVPNManager {
 internal sealed partial class MainForm {
  private Button _btnUpdate;
  private Label _updateStatus;
  private bool _updateBusy;
  private Panel _page;
  private float _layoutScale=1f;
  private Action _resizeContent;
  private bool _layoutReady;
  private Action<int> _navigate;
  private int _activeSection;
  private Button[] _navButtons;
  private int Logical(int pixels){return (int)Math.Round(pixels*_layoutScale);}

  private Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size,Color color,FontStyle style=FontStyle.Regular) {
   var l=MakeStatusLabel(text,x,y,w,h,size,style,color);l.BackColor=Color.Transparent;parent.Controls.Add(l);return l;
  }
  private RoundedPanel Card(Control parent,int y,int h,int width) {
   var c=new RoundedPanel{Location=new Point(24,y),Size=new Size(width,h),BackColor=ColorPanel,Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};parent.Controls.Add(c);return c;
  }
  private void Link(Control parent,string title,string url,Rectangle bounds) {
   var l=new LinkLabel{Text=title,Bounds=bounds,LinkColor=Color.FromArgb(191,166,255),ActiveLinkColor=Color.White,VisitedLinkColor=Color.FromArgb(191,166,255),LinkBehavior=LinkBehavior.HoverUnderline,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleRight};
   l.LinkClicked+=delegate{try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){AppendLog(ex.Message);}};parent.Controls.Add(l);
  }

  protected override void OnShown(EventArgs e) {
   base.OnShown(e);
   if(WindowState==FormWindowState.Normal){var area=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));Location=new Point(area.X+(area.Width-Width)/2,area.Y+(area.Height-Height)/2);}
   _layoutReady=true;if(_navigate!=null)_navigate(_activeSection);if(_resizeContent!=null)_resizeContent();
  }

  private void BuildUi() {
   SuspendLayout();
   var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Padding=Padding.Empty,BackColor=ColorBg};
   shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,224));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,Padding=Padding.Empty,BackColor=ColorBg};
   frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,38));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(frame);
   frame.Controls.Add(BuildCaption(),0,0);frame.Controls.Add(shell,0,1);
   var side=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(18,20,32),Margin=Padding.Empty};shell.Controls.Add(side,0,0);
   var logo=new PictureBox{Location=new Point(24,30),Size=new Size(48,48),SizeMode=PictureBoxSizeMode.Zoom};
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("logo-transparent.png")){if(stream!=null){using(var im=Image.FromStream(stream))logo.Image=new Bitmap(im);}}side.Controls.Add(logo);
   LabelAt(side,"Discord VPN",24,90,190,44,19,ColorText,FontStyle.Bold);LabelAt(side,"CONEXÃO SÓ PARA O DISCORD",26,140,190,24,7.7f,ColorMuted);
   LabelAt(side,"SEU ESPAÇO",26,200,184,18,8,ColorMuted,FontStyle.Bold);
   var navConnect=MakeButton("  Visão geral",16,232,192,46,side.BackColor);navConnect.TextAlign=ContentAlignment.MiddleLeft;side.Controls.Add(navConnect);
   var navProfiles=MakeButton("  Perfis WireGuard",16,286,192,46,side.BackColor);navProfiles.TextAlign=ContentAlignment.MiddleLeft;side.Controls.Add(navProfiles);
   var navActivity=MakeButton("  Atividade",16,340,192,46,side.BackColor);navActivity.TextAlign=ContentAlignment.MiddleLeft;side.Controls.Add(navActivity);
   var privacy=new RoundedPanel{Location=new Point(16,430),Size=new Size(192,114),BackColor=Color.FromArgb(28,30,45)};side.Controls.Add(privacy);
   LabelAt(privacy,"SÓ O DISCORD",16,14,160,20,8.7f,Color.FromArgb(185,163,241),FontStyle.Bold);LabelAt(privacy,"Seus outros apps usam\na conexão normal.",16,43,162,48,9.2f,ColorMuted);
   side.Resize+=delegate{privacy.Visible=side.ClientSize.Height>Logical(740);};
   var bottom=new Panel{Dock=DockStyle.Bottom,Height=184,BackColor=side.BackColor};side.Controls.Add(bottom);
   LabelAt(bottom,"Versão "+UpdateClient.CurrentVersion.ToString(3),24,4,180,24,9,ColorMuted);
   _btnUpdate=MakeButton("Buscar atualizações",16,39,192,42,ColorSecondary);_btnUpdate.Click+=delegate{CheckForUpdates();};bottom.Controls.Add(_btnUpdate);
   _updateStatus=LabelAt(bottom,"Atualizações pelo GitHub",23,89,180,32,8.2f,ColorMuted);
   Link(bottom,"Projeto no GitHub  ↗","https://github.com/ezeq-ol/discord-vpn-manager-windows",new Rectangle(16,132,188,30));

   _page=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty,BackColor=ColorBg};shell.Controls.Add(_page,1,0);
   var canvas=new Panel{Dock=DockStyle.Top,Size=new Size(856,818),BackColor=ColorBg};_page.Controls.Add(canvas);
   int cw=808;
   var pageTitle=LabelAt(canvas,"Sua conexão, sob controle.",24,16,700,52,24,ColorText,FontStyle.Bold);
   var pageSubtitle=LabelAt(canvas,"Escolha um perfil. Conecte. Abra o Discord.",26,72,700,26,10,ColorMuted);
   var hero=Card(canvas,110,188,cw);hero.GradientEnd=Color.FromArgb(36,29,57);hero.BorderColor=Color.FromArgb(64,52,91);
   _lblTunnel=LabelAt(hero,"Desconectado",22,12,440,50,23,ColorRed,FontStyle.Bold);
   _lblSingbox=LabelAt(hero,"Conecte um perfil para começar.",24,64,420,26,10,ColorMuted);
   _lblActive=LabelAt(hero,"Perfil ativo: —",24,94,480,26,9.3f,Color.FromArgb(207,198,228));
   var serverTitle=LabelAt(hero,"SERVIDOR",500,22,282,18,8,ColorMuted,FontStyle.Bold);serverTitle.TextAlign=ContentAlignment.MiddleRight;serverTitle.Anchor=AnchorStyles.Top|AnchorStyles.Right;
   _lblEndpoint=LabelAt(hero,"Nenhum servidor selecionado",480,46,302,25,10,ColorText);_lblEndpoint.TextAlign=ContentAlignment.MiddleRight;_lblEndpoint.Anchor=AnchorStyles.Top|AnchorStyles.Right;
   _lblDiscord=LabelAt(hero,"Discord: fechado",500,77,282,24,9,ColorMuted);_lblDiscord.TextAlign=ContentAlignment.MiddleRight;_lblDiscord.Anchor=AnchorStyles.Top|AnchorStyles.Right;
   _btnApply=MakeButton("Conectar VPN",22,132,195,42,ColorAccent);_btnApply.Click+=delegate{ApplySelected();};hero.Controls.Add(_btnApply);
   _btnStop=MakeButton("Desconectar",227,132,136,42,ColorDanger);_btnStop.Click+=delegate{StopVpn();};hero.Controls.Add(_btnStop);
   _btnDiscord=MakeButton("Abrir Discord  ↗",373,132,175,42,ColorSecondary);_btnDiscord.Click+=delegate{OpenDiscord();};hero.Controls.Add(_btnDiscord);
   _lblOperation=LabelAt(hero,"Pronto",551,132,231,23,8.5f,ColorMuted);_lblOperation.TextAlign=ContentAlignment.MiddleRight;_lblOperation.Anchor=AnchorStyles.Top|AnchorStyles.Right;
   _progress=new ProgressBar{Location=new Point(565,163),Size=new Size(217,5),Style=ProgressBarStyle.Marquee,Visible=false,Anchor=AnchorStyles.Top|AnchorStyles.Right};hero.Controls.Add(_progress);

   var profiles=Card(canvas,316,246,cw);LabelAt(profiles,"Perfis WireGuard",20,13,310,32,14,ColorText,FontStyle.Bold);
   Link(profiles,"Baixar .conf no Proton VPN  ↗","https://account.protonvpn.com/downloads",new Rectangle(470,14,312,30));profiles.Controls[profiles.Controls.Count-1].Anchor=AnchorStyles.Top|AnchorStyles.Right;
   _list=new ListView{Location=new Point(16,56),Size=new Size(776,124),View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,HeaderStyle=ColumnHeaderStyle.Nonclickable,BorderStyle=BorderStyle.None,BackColor=ColorPanel,ForeColor=ColorText,Font=new Font("Segoe UI",10),OwnerDraw=true,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
   var rowSize=new ImageList{ImageSize=new Size(1,34)};rowSize.Images.Add(new Bitmap(1,34));_list.SmallImageList=rowSize;_list.Disposed+=delegate{rowSize.Dispose();};
   _headerFont=new Font("Segoe UI",8.5f,FontStyle.Bold);_list.Columns.Add("PERFIL",218);_list.Columns.Add("SERVIDOR",276);_list.Columns.Add("MODIFICADO",169);_list.Columns.Add("EM USO",80);
   _list.DrawColumnHeader+=delegate(object sender,DrawListViewColumnHeaderEventArgs e){using(var b=new SolidBrush(Color.FromArgb(21,24,37)))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,_headerFont,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-16,e.Bounds.Height),ColorMuted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
   _list.DrawItem+=delegate(object sender,DrawListViewItemEventArgs e){e.DrawDefault=false;};
   _list.DrawSubItem+=delegate(object sender,DrawListViewSubItemEventArgs e){Color bg=e.Item.Selected?Color.FromArgb(48,37,72):ColorPanel;using(var b=new SolidBrush(bg))e.Graphics.FillRectangle(b,e.Bounds);if(e.Item.Selected&&e.ColumnIndex==0){using(var b=new SolidBrush(ColorAccent))e.Graphics.FillRectangle(b,new Rectangle(e.Bounds.X,e.Bounds.Y+5,3,e.Bounds.Height-10));}Color color=e.ColumnIndex==3&&e.SubItem.Text=="Sim"?ColorGreen:ColorText;TextRenderer.DrawText(e.Graphics,e.SubItem.Text,_list.Font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-17,e.Bounds.Height),color,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);};
   _list.DoubleClick+=delegate{if(_list.SelectedItems.Count>0)ApplySelected();};profiles.Controls.Add(_list);
   _btnAdd=MakeButton("＋ Adicionar .conf",18,194,178,36,ColorSecondary);_btnAdd.Click+=delegate{AddProfile();};profiles.Controls.Add(_btnAdd);
   _btnOpenFolder=MakeButton("Abrir pasta",206,194,126,36,ColorSecondary);_btnOpenFolder.Click+=delegate{try{Directory.CreateDirectory(_profilesDir);Process.Start("explorer.exe",_profilesDir);}catch(Exception ex){AppendLog(ex.Message);}};profiles.Controls.Add(_btnOpenFolder);
   _btnRefresh=MakeButton("Recarregar lista",342,194,156,36,ColorSecondary);_btnRefresh.Click+=delegate{RefreshProfiles();AppendLog("Lista de perfis atualizada.");};profiles.Controls.Add(_btnRefresh);
   var hint=LabelAt(profiles,"Duplo clique para conectar",540,197,240,28,8.5f,ColorMuted);hint.TextAlign=ContentAlignment.MiddleRight;hint.Anchor=AnchorStyles.Top|AnchorStyles.Right;

   profiles.Resize+=delegate{hint.Visible=profiles.Width>=Logical(780);int available=_list.ClientSize.Width;_list.Columns[0].Width=(int)(available*0.28);_list.Columns[1].Width=(int)(available*0.35);_list.Columns[3].Width=Logical(80);_list.Columns[2].Width=Math.Max(Logical(70),available-_list.Columns[0].Width-_list.Columns[1].Width-Logical(82));};
   hero.Resize+=delegate{_lblOperation.Visible=hero.Width>=Logical(830);if(hero.Width<Logical(830))_progress.Visible=false;};
   var startup=Card(canvas,580,94,cw);
   _startup=new CheckBox{Text="Iniciar VPN com o Windows",Location=new Point(20,14),Size=new Size(430,26),BackColor=Color.Transparent,ForeColor=ColorText,Font=new Font("Segoe UI",10.5f,FontStyle.Bold),Checked=File.Exists(Path.Combine(_appDir,"start-with-windows.flag"))};startup.Controls.Add(_startup);
   LabelAt(startup,"Ao entrar: bandeja → VPN pronta → Discord.",21,46,440,25,9,ColorMuted);
   _btnStartup=MakeButton("Salvar opção",625,24,163,42,ColorSecondary);_btnStartup.Anchor=AnchorStyles.Top|AnchorStyles.Right;_btnStartup.Click+=delegate{StartupSupport.SaveStartup(this);};startup.Controls.Add(_btnStartup);
   var activity=Card(canvas,692,110,cw);LabelAt(activity,"Atividade recente",18,9,360,25,10,ColorText,FontStyle.Bold);
   _log=new TextBox{Location=new Point(18,39),Size=new Size(772,55),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=ColorPanel,ForeColor=ColorMuted,Font=new Font("Consolas",8.3f),TabStop=false,Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right};activity.Controls.Add(_log);
   _navButtons=new Button[]{navConnect,navProfiles,navActivity};
   var shortcut=Card(canvas,428,112,cw);
   LabelAt(shortcut,"Seu próximo servidor",20,13,420,32,14,ColorText,FontStyle.Bold);
   LabelAt(shortcut,"Importe e escolha seus perfis .conf do Proton VPN.",21,55,470,26,9,ColorMuted);
   var manage=MakeButton("Escolher perfil  ↗",603,35,185,42,ColorSecondary);manage.Anchor=AnchorStyles.Top|AnchorStyles.Right;shortcut.Controls.Add(manage);
   manage.Click+=delegate{_navigate(1);};
   Action layoutSection=delegate{
    int width=Math.Max(Logical(650),canvas.ClientSize.Width-Logical(48));
    hero.Width=profiles.Width=startup.Width=activity.Width=shortcut.Width=width;
    if(_activeSection==0){
     hero.Top=Logical(110);startup.Top=Logical(316);shortcut.Top=Logical(428);
     canvas.Height=Logical(560);
    }else if(_activeSection==1){
     profiles.Top=Logical(110);profiles.Height=Logical(350);
     _list.Height=Logical(228);
     _btnAdd.Top=_btnOpenFolder.Top=_btnRefresh.Top=Logical(298);hint.Top=Logical(301);
     hero.Top=Logical(478);canvas.Height=Logical(686);
    }else{
     activity.Top=Logical(110);activity.Height=Math.Max(Logical(300),_page.ClientSize.Height-Logical(134));
     _log.Height=activity.Height-Logical(55);canvas.Height=activity.Bottom+Logical(24);
    }
   };
   _navigate=delegate(int section){
    if(section<0||section>2)return;
    _activeSection=section;
    _page.AutoScrollPosition=Point.Empty;
    canvas.SuspendLayout();
    hero.Visible=section!=2;profiles.Visible=section==1;startup.Visible=section==0;
    activity.Visible=section==2;shortcut.Visible=section==0;
    pageTitle.Text=section==0?"Sua conexão, sob controle.":section==1?"Seus perfis WireGuard":"Atividade da conexão";
    pageSubtitle.Text=section==0?"Conexão e inicialização do Discord.":section==1?"Importe um .conf e selecione o perfil para conectar.":"Acompanhe os eventos e mensagens do Manager.";
    for(int i=0;i<_navButtons.Length;i++){
     ((RoundedButton)_navButtons[i]).IsSelected=i==section;
     _navButtons[i].AccessibleDescription=i==section?"Página atual":"Abrir página";
     _navButtons[i].Invalidate();
    }
    layoutSection();canvas.ResumeLayout(true);
    _page.AutoScrollPosition=Point.Empty;
   };
   navConnect.Click+=delegate{_navigate(0);};navProfiles.Click+=delegate{_navigate(1);};navActivity.Click+=delegate{_navigate(2);};
   _resizeContent=layoutSection;
   _page.SizeChanged+=delegate{if(_layoutReady)_resizeContent();};
   canvas.SizeChanged+=delegate{if(_layoutReady)_resizeContent();};
   // Controls are authored in logical 96-DPI pixels. Set the baseline only
   // after the complete tree exists, so WinForms scales bounds once together.
   using(var graphics=CreateGraphics())_layoutScale=graphics.DpiX/96f;
   AutoScaleMode=AutoScaleMode.Dpi;
   AutoScaleDimensions=new SizeF(96f,96f);
   ResumeLayout(true);
  }
 }
}
