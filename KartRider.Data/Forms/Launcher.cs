using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Set_Data;
using System.Xml;
using ExcData;
using Launcher.Properties;
using KartRider.Common.Data;
using static KartRider.Common.Data.PINFile;

namespace KartRider
{
	public class Launcher : Form
	{
		public static bool GetKart = true;
		public string kartRiderDirectory = null;
		public string profilePath = null;
		public static string KartRider = "KartRider.exe";
		public static string pinFile = "KartRider.pin";
		private Button Start_Button;
		private PlayButton playButton;
		private bool startupDone, gameRunning;
		private int procCheckTick;
		private DateTime launchingUntil = DateTime.MinValue;
		private Button GetKart_Button;
		private Label label_DeveloperName;
		private Label lblRegionCaption;
		private Panel headerRightPanel, headerSep;
		private Label lblRegion;
		private LogoBox logoBox;
		private PillLabel versionPill;
		private ToolTip headerTip;
		private Label Speed_label;
		private ThemedComboBox Speed_comboBox;
		private Label MinorVersion;
		private Label lblTitle;
		private Label lblSubTitle;
		private Label lblServer;
		private Label lblGame;
		private Label lblData;
		private Label lblLogFile;
		private MainView mainView;
		private LogPanel logPanel;
		private LogSettingsPanel logSettingsPanel;
		private ItemPicker itemPicker;
		private ModalLayer modalLayer;
		private Panel modalCard;
		private bool cardShown;                         // 실제 카드가 화면에 보이는 중 (페이드 이미지가 아니라)
		private System.Windows.Forms.Timer modalTimer;
		private bool modalActive;                       // 열려 있거나 열리는 중
		private double modalProgress, modalFrom, modalTo, modalDur;   // 0 = 숨김 ~ 1 = 완전히 표시
		private DateTime modalStart;
		private Rectangle cardBounds;
		private Control modalContent;                   // 지금 카드 안에 보이는 내용 (아이템 추가 / 로그 설정)
		private Size modalLogical = new Size(720, 500); // 그 내용의 기본 크기 (96dpi 기준)
		private System.Windows.Forms.Timer statusTimer;
		private bool dataShown;
		private KartRhoFile.GameDataSource dataSource;   // 게임 데이터를 읽는 방식 (Data 폴더 / datapackOff 폴더)

		public Launcher()
		{
			this.InitializeComponent();
		}

		private void InitializeComponent()
		{
			SuspendLayout();

			// ===== 폼 기본 =====
			AutoScaleDimensions = new SizeF(96F, 96F);
			AutoScaleMode = AutoScaleMode.Dpi;
			BackColor = UiTheme.Bg;
			ForeColor = UiTheme.Text;
			Font = UiTheme.UiFont(9F);
			ClientSize = new Size(940, 640);
			MinimumSize = new Size(820, 540);
			Icon = AppLogo.LoadIcon();   // 로고를 바꾸면 창 아이콘도 같이 바뀜
			Name = "Launcher";
			StartPosition = FormStartPosition.CenterScreen;

			// ===== 맨 위: 헤더 (왼쪽 로고 + 제목 + 프로그램 버전 / 오른쪽 국가 리전 · 클라이언트 버전 + 화면 전환 버튼) =====
			var header = new Panel();
			header.Dock = DockStyle.Top;
			header.Height = 58;
			header.BackColor = UiTheme.Panel;

			var accentBar = new Panel();
			accentBar.Dock = DockStyle.Left;
			accentBar.Width = 5;
			accentBar.BackColor = UiTheme.Accent;

			headerTip = new ToolTip();

			// 로고: 프로그램 아이콘(Properties\favicon.ico) 또는 Profile\logo.png 와 같은 이미지 -> 로고를 바꾸면 헤더도 같이 바뀜
			logoBox = new LogoBox();
			logoBox.Size = new Size(40, 40);
			logoBox.Location = new Point(20, 9);
			logoBox.Image = AppLogo.LoadBitmap();

			lblTitle = new Label();
			lblTitle.AutoSize = true;
			lblTitle.Text = "KartRider Launcher";
			lblTitle.Font = UiTheme.UiFont(14F, FontStyle.Bold);
			lblTitle.ForeColor = UiTheme.Text;
			lblTitle.BackColor = Color.Transparent;
			lblTitle.Location = new Point(72, 5);   // 실제 위치는 LayoutHeader 에서 로고 오른쪽에 맞춤

			// 프로그램 버전 (제목 오른쪽 알약)
			versionPill = new PillLabel("v" + AppInfo.Version);
			versionPill.Location = new Point(260, 10);
			headerTip.SetToolTip(versionPill, "프로그램 버전 " + AppInfo.Version + " (빌드 " + AppInfo.Build + (AppInfo.BuildTime.Length > 0 ? ", " + AppInfo.BuildTime : "") + ")");

			lblSubTitle = new Label();
			lblSubTitle.AutoSize = true;
			lblSubTitle.Font = UiTheme.UiFont(8.5F);
			lblSubTitle.ForeColor = UiTheme.Muted;
			lblSubTitle.BackColor = Color.Transparent;
			lblSubTitle.Location = new Point(74, 35);
			lblSubTitle.Text = "로컬 서버 모드";

			// 오른쪽 끝에 붙는 영역 (Dock = Right 라서 창 크기/DPI 와 상관없이 항상 보임): 국가 리전 | 클라이언트 버전, 둘 다 오른쪽 정렬
			var headerRight = new Panel();
			headerRight.Dock = DockStyle.Right;
			headerRight.Width = 320;
			headerRight.BackColor = Color.Transparent;
			headerRightPanel = headerRight;

			lblRegionCaption = new Label();
			lblRegionCaption.AutoSize = false;
			lblRegionCaption.Size = new Size(100, 16);
			lblRegionCaption.TextAlign = ContentAlignment.MiddleRight;
			lblRegionCaption.Text = "국가 리전";
			lblRegionCaption.Font = UiTheme.UiFont(8F);
			lblRegionCaption.ForeColor = UiTheme.Muted;
			lblRegionCaption.BackColor = Color.Transparent;
			lblRegionCaption.Location = new Point(10, 8);

			lblRegion = new Label();
			lblRegion.AutoSize = false;
			lblRegion.Size = new Size(100, 26);
			lblRegion.TextAlign = ContentAlignment.MiddleRight;
			lblRegion.Text = "-";
			lblRegion.Font = UiTheme.UiFont(13F, FontStyle.Bold);
			lblRegion.ForeColor = UiTheme.Text;
			lblRegion.BackColor = Color.Transparent;
			lblRegion.Location = new Point(10, 24);

			var sep = new Panel();
			sep.BackColor = UiTheme.Border;
			sep.Size = new Size(1, 30);
			sep.Location = new Point(122, 14);
			headerSep = sep;

			label_DeveloperName = new Label();
			label_DeveloperName.AutoSize = false;
			label_DeveloperName.Size = new Size(150, 16);
			label_DeveloperName.TextAlign = ContentAlignment.MiddleRight;
			label_DeveloperName.Text = "클라이언트 버전";
			label_DeveloperName.Font = UiTheme.UiFont(8F);
			label_DeveloperName.ForeColor = UiTheme.Muted;
			label_DeveloperName.BackColor = Color.Transparent;
			label_DeveloperName.Location = new Point(134, 8);

			// 주의: OnLoad 에서 MinorVersion.Text 를 숫자로 읽어 쓰므로 숫자만 넣을 것
			MinorVersion = new Label();
			MinorVersion.AutoSize = false;
			MinorVersion.Size = new Size(150, 26);
			MinorVersion.TextAlign = ContentAlignment.MiddleRight;
			MinorVersion.Text = "-";
			MinorVersion.Font = UiTheme.UiFont(13F, FontStyle.Bold);
			MinorVersion.ForeColor = UiTheme.Accent;
			MinorVersion.BackColor = Color.Transparent;
			MinorVersion.Location = new Point(134, 24);

			headerRight.Controls.Add(lblRegionCaption);
			headerRight.Controls.Add(lblRegion);
			headerRight.Controls.Add(sep);
			headerRight.Controls.Add(label_DeveloperName);
			headerRight.Controls.Add(MinorVersion);

			header.Controls.Add(logoBox);
			header.Controls.Add(lblTitle);
			header.Controls.Add(versionPill);
			header.Controls.Add(lblSubTitle);
			header.Controls.Add(headerRight);
			header.Controls.Add(accentBar);

			// ===== 가운데: 왼쪽 로그 (6) + 오른쪽 설정 (4) =====
			var body = new Panel();
			body.Dock = DockStyle.Fill;
			body.Padding = new Padding(16, 12, 16, 10);
			body.BackColor = UiTheme.Bg;

			mainView = new MainView();
			mainView.Dock = DockStyle.Fill;
			logPanel = mainView.Log;
			body.Controls.Add(mainView);

			// ===== 아래: 왼쪽 속도 선택 / 오른쪽 아이템 추가 · 플레이 =====
			var controlPanel = new Panel();
			controlPanel.Dock = DockStyle.Bottom;
			controlPanel.Height = 86;
			controlPanel.BackColor = UiTheme.Panel;

			Speed_label = new Label();
			Speed_label.AutoSize = true;
			Speed_label.Text = "속도";
			Speed_label.Font = UiTheme.UiFont(8.5F, FontStyle.Bold);
			Speed_label.ForeColor = UiTheme.Muted;
			Speed_label.BackColor = Color.Transparent;
			Speed_label.Location = new Point(22, 10);

			Speed_comboBox = new ThemedComboBox();
			Speed_comboBox.Name = "Speed_comboBox";
			Speed_comboBox.Size = new Size(250, 36);
			Speed_comboBox.Location = new Point(20, 32);
			foreach (string key in SpeedType.speedNames.Keys)
			{
				Speed_comboBox.Items.Add(key);
			}
			int defaultSpeed = Speed_comboBox.Items.IndexOf("표준");
			if (defaultSpeed >= 0) Speed_comboBox.SelectedIndex = defaultSpeed;
			Speed_comboBox.SelectedIndexChanged += Speed_comboBox_SelectedIndexChanged;

			controlPanel.Controls.Add(Speed_label);
			controlPanel.Controls.Add(Speed_comboBox);

			// 오른쪽 끝에 붙는 버튼 영역 (Dock = Right): [아이템 추가] [플레이]
			var controlRight = new Panel();
			controlRight.Dock = DockStyle.Right;
			controlRight.Width = 380;
			controlRight.BackColor = Color.Transparent;

			GetKart_Button = UiTheme.MakeButton("아이템 추가", false, 150, 58);
			GetKart_Button.Name = "GetKart_Button";
			GetKart_Button.Font = UiTheme.UiFont(10F);
			GetKart_Button.Location = new Point(20, 14);
			GetKart_Button.Click += GetKart_Button_Click;

			playButton = new PlayButton();
			playButton.Size = new Size(190, 58);
			Start_Button = playButton;
			Start_Button.Name = "Start_Button";
			Start_Button.Location = new Point(176, 14);
			Start_Button.Click += Start_Button_Click;

			controlRight.Controls.Add(GetKart_Button);
			controlRight.Controls.Add(Start_Button);
			controlPanel.Controls.Add(controlRight);

			// ===== 맨 아래: 상태줄 (서버/게임/데이터 상태 + 로그 파일) =====
			var statusStrip = new Panel();
			statusStrip.Dock = DockStyle.Bottom;
			statusStrip.Height = 30;
			statusStrip.BackColor = Color.FromArgb(18, 22, 28);

			lblLogFile = new Label();
			lblLogFile.Dock = DockStyle.Left;
			lblLogFile.Width = 300;
			lblLogFile.AutoSize = false;
			lblLogFile.AutoEllipsis = true;
			lblLogFile.TextAlign = ContentAlignment.MiddleLeft;
			lblLogFile.Padding = new Padding(16, 0, 0, 0);
			lblLogFile.Font = UiTheme.UiFont(8F);
			lblLogFile.ForeColor = UiTheme.Muted;
			string logName = AppLog.LogPath != null ? System.IO.Path.GetFileName(AppLog.LogPath) : "(없음)";
			lblLogFile.Text = "로그: " + logName;
			var logTip = new ToolTip();
			logTip.SetToolTip(lblLogFile, "로그 파일: " + (AppLog.LogPath ?? "(없음)"));

			var statusFlow = new FlowLayoutPanel();
			statusFlow.Dock = DockStyle.Fill;
			statusFlow.WrapContents = false;
			statusFlow.FlowDirection = FlowDirection.RightToLeft;   // 오른쪽 끝부터 채움
			statusFlow.BackColor = Color.Transparent;
			statusFlow.Padding = new Padding(0, 0, 14, 0);

			lblServer = MakeStatusLabel("● 서버 시작 전", UiTheme.Muted);
			lblGame = MakeStatusLabel("● 게임 실행 전", UiTheme.Muted);
			lblData = MakeStatusLabel("● 게임 데이터 대기", UiTheme.Muted);
			lblData.Margin = new Padding(0, 7, 0, 0);          // 맨 오른쪽 항목은 오른쪽 여백 없이
			// 오른쪽부터 채우므로 거꾸로 추가: 화면에는 [서버] [게임] [데이터] 순서로 보임
			statusFlow.Controls.Add(lblData);
			statusFlow.Controls.Add(lblGame);
			statusFlow.Controls.Add(lblServer);

			statusStrip.Controls.Add(statusFlow);   // Fill 먼저
			statusStrip.Controls.Add(lblLogFile);   // 그 다음 Right

			// ===== 런처 안에서 겹쳐 뜨는 아이템 추가 카드 (화면 중앙, 어두운 배경) =====
			modalLayer = new ModalLayer();
			modalLayer.Visible = false;
			modalLayer.Click += (s, e) => CloseModal();        // 어두운 바깥 영역을 누르면 닫기
			modalTimer = new System.Windows.Forms.Timer();
			modalTimer.Interval = 15;
			modalTimer.Tick += ModalTick;
			modalCard = new Panel();
			modalCard.BackColor = UiTheme.Border;               // 1px 테두리
			modalCard.Padding = new Padding(1);
			modalCard.Size = new Size(720, 500);
			itemPicker = new ItemPicker();
			itemPicker.Dock = DockStyle.Fill;
			itemPicker.CloseRequested += (s, e) => CloseModal();
			modalCard.Controls.Add(itemPicker);
			logSettingsPanel = new LogSettingsPanel();
			logSettingsPanel.Dock = DockStyle.Fill;
			logSettingsPanel.CloseRequested += (s, e) => CloseModal();
			modalCard.Controls.Add(logSettingsPanel);
			logPanel.SettingsRequested += (s, e) => OpenModal(logSettingsPanel, 640, 560);   // 로그 창의 ⚙ 버튼
			modalLayer.Controls.Add(modalCard);

			// 추가 순서: Fill(본문) → Bottom(옵션) → Bottom(상태줄, 가장 아래) → Top(헤더)
			Controls.Add(body);
			Controls.Add(controlPanel);
			Controls.Add(statusStrip);
			Controls.Add(header);
			Controls.Add(modalLayer);

			Text = "Launcher";
			FormClosing += OnFormClosing;
			Load += OnLoad;
			KeyPreview = true;
			KeyDown += OnOverlayKeyDown;
			SizeChanged += (s, e) => { LayoutOverlays(); LayoutHeader(); };
			UpdateSubTitle();

			statusTimer = new System.Windows.Forms.Timer();
			statusTimer.Interval = 400;
			statusTimer.Tick += StatusTick;
			statusTimer.Start();

			ResumeLayout(false);
			PerformLayout();
		}

		// ---------- 아이템 추가 / 로그 설정 카드 (겹쳐 뜨는 패널) ----------
		// 열기: 배경이 어두워지면서 카드가 제자리에서 페이드 인 / 닫기: 페이드 아웃.
		// 카드(안의 컨트롤 포함)를 화면 밖에서 한 번 그려 이미지로 만들고, 그 이미지의 투명도를 바꿔 가며 그림.
		// 열기가 끝나면 실제 카드로 바꿔치기 (모양이 같아서 이어져 보임). 이미지를 만들지 못하면 페이드 없이 바로 표시.
		private void LayoutOverlays()
		{
			if (modalLayer == null || !modalLayer.Visible) return;
			modalLayer.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
			ComputeCardBounds();
			modalLayer.CardBounds = cardBounds;
			if (cardShown) modalCard.Bounds = cardBounds;
		}

		private void OpenModal(Control content, int logicalWidth, int logicalHeight)
		{
			if (modalActive && !modalLayer.Visible) modalActive = false;   // 카드가 안 보이는데 열린 상태로 남은 경우 복구
			if (modalActive || modalLayer.Visible) return;                  // 열려 있거나 열리는/닫히는 중이면 무시
			try
			{
				modalActive = true;
				modalContent = content;
				modalLogical = new Size(logicalWidth, logicalHeight);
				foreach (Control c in modalCard.Controls) c.Visible = (c == content);

				modalLayer.Backdrop = CaptureBackdrop();      // 어둡게 하기 전의 런처 화면
				modalLayer.DimAlpha = 0;
				modalLayer.CardAlpha = 0;
				modalLayer.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
				if (content == itemPicker) itemPicker.Reload();   // 시간이 걸리는 준비는 화면이 바뀌기 전에 먼저 끝냄
				ComputeCardBounds();
				modalLayer.CardBounds = cardBounds;
				modalLayer.Visible = true;                    // 부모가 보여야 카드를 그릴 수 있음 (배경은 화면과 같아서 눈에 띄는 변화 없음)
				modalLayer.BringToFront();
				modalLayer.CardImage = SnapshotCard(true);    // 화면 밖에서 카드를 그려 이미지로 저장
				cardShown = false;
				modalProgress = 0;
				WheelRouter.Suspended = true;                 // 카드 뒤의 화면이 휠로 스크롤되지 않게
				StartModalAnimation(1.0, 220);
			}
			catch (Exception ex)
			{
				// 열다가 예외가 나도 '열린 상태'로 남지 않게 정리 (그러면 다른 카드도 못 여는 문제가 생김)
				AppLog.Error("카드 열기 실패", ex);
				modalTimer.Stop();
				modalActive = false;
				FinishClose();
			}
		}

		private void CloseModal()
		{
			if (!modalActive) return;
			modalActive = false;
			try
			{
				if (cardShown)
				{
					Bitmap snap = SnapshotCard(false);        // 지금 카드 모습을 이미지로 저장해서 그 이미지를 페이드 아웃
					if (snap != null)
					{
						modalLayer.CardBounds = cardBounds;
						modalLayer.CardAlpha = 255;
						modalLayer.CardImage = snap;
						modalCard.Visible = false;
					}
					cardShown = false;
				}
			}
			catch (Exception ex) { AppLog.Error("카드 닫기 준비 실패", ex); }
			StartModalAnimation(0.0, 160);
		}

		/// <summary>카드(안의 컨트롤 포함)를 이미지로 만듦. offscreen=true 면 화면 밖에 잠깐 배치해서 그림 (사용자에게 보이지 않음)</summary>
		private Bitmap SnapshotCard(bool offscreen)
		{
			try
			{
				if (offscreen)
				{
					modalCard.SetBounds(-10000, -10000, cardBounds.Width, cardBounds.Height);
					modalCard.Visible = true;
				}
				var bmp = new Bitmap(cardBounds.Width, cardBounds.Height);
				using (Graphics g = Graphics.FromImage(bmp)) g.Clear(UiTheme.Bg);
				modalCard.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
				return bmp;
			}
			catch (Exception ex)
			{
				AppLog.Error("카드 이미지 만들기 실패 (페이드 없이 표시)", ex);
				return null;
			}
			finally
			{
				if (offscreen) { modalCard.Visible = false; modalCard.Bounds = cardBounds; }
			}
		}

		private void StartModalAnimation(double to, double durationMs)
		{
			modalFrom = modalProgress;
			modalTo = to;
			modalDur = durationMs;
			modalStart = DateTime.UtcNow;
			modalTimer.Start();
		}

		private void ModalTick(object sender, EventArgs e)
		{
			double t = Math.Min(1.0, (DateTime.UtcNow - modalStart).TotalMilliseconds / Math.Max(1.0, modalDur));
			double eased = modalTo > modalFrom ? 1 - Math.Pow(1 - t, 3) : t * t * (3 - 2 * t);   // 열기: 감속 / 닫기: 부드러운 가속
			ApplyModalProgress(modalFrom + (modalTo - modalFrom) * eased);
			if (t < 1.0) return;
			modalTimer.Stop();
			if (!modalActive) { FinishClose(); return; }      // 닫기 애니메이션이 끝났을 때만 숨김
			// 열기 완료: 페이드용 이미지를 걷어내고 실제 카드로 바꿔치기
			modalCard.Bounds = cardBounds;
			modalCard.Visible = true;
			cardShown = true;
			modalLayer.CardImage = null;
			if (modalContent == itemPicker) itemPicker.FocusSearch();
		}

		/// <summary>p: 0 = 숨김 ~ 1 = 완전히 표시. 배경 어둡기와 카드 이미지 투명도가 함께 변함 (위치 이동 없음)</summary>
		private void ApplyModalProgress(double p)
		{
			modalProgress = p;
			modalLayer.DimAlpha = (int)(175 * p);
			if (modalLayer.CardImage != null)
			{
				modalLayer.CardAlpha = (int)(255 * p);
				return;
			}
			// 이미지를 만들지 못한 경우: 페이드 없이 어느 정도 어두워진 뒤 바로 표시/숨김
			bool show = p >= 0.35;
			if (modalCard.Visible != show)
			{
				if (show) modalCard.Bounds = cardBounds;
				modalCard.Visible = show;
			}
		}

		private void FinishClose()
		{
			modalLayer.Visible = false;
			modalCard.Visible = false;
			cardShown = false;
			modalLayer.CardImage = null;
			Bitmap old = modalLayer.Backdrop;
			modalLayer.Backdrop = null;
			if (old != null) old.Dispose();
			WheelRouter.Suspended = false;
		}

		private void ComputeCardBounds()
		{
			float scale = DeviceDpi / 96f;
			int margin = Math.Max(24, ClientSize.Width / 24);
			int w = Math.Min((int)(modalLogical.Width * scale), ClientSize.Width - margin * 2);
			int h = Math.Min((int)(modalLogical.Height * scale), ClientSize.Height - margin * 2);
			cardBounds = new Rectangle((ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
		}

		/// <summary>현재 런처 화면(클라이언트 영역)을 있는 그대로 캡처 (실패하면 null -> 단색 배경)</summary>
		private Bitmap CaptureBackdrop()
		{
			try
			{
				Point origin = PointToScreen(Point.Empty);
				var bmp = new Bitmap(ClientSize.Width, ClientSize.Height);
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.CopyFromScreen(origin, Point.Empty, ClientSize);
				}
				return bmp;
			}
			catch { return null; }
		}

		private void OnOverlayKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape && modalActive)
			{
				CloseModal();
				e.Handled = true;
			}
		}

		/// <summary>헤더 왼쪽: 로고 → 제목 → 프로그램 버전 순서로 오른쪽으로 이어 붙임 (실제 크기 기준이라 DPI 와 무관)</summary>
		private void LayoutHeader()
		{
			if (logoBox == null || lblTitle == null || versionPill == null || lblSubTitle == null) return;
			int gap = (int)(12 * DeviceDpi / 96f);
			lblTitle.Left = logoBox.Right + gap;
			versionPill.Size = versionPill.GetPreferredSize(Size.Empty);
			versionPill.Left = lblTitle.Right + gap;
			versionPill.Top = Math.Max(0, (versionPill.Parent.ClientSize.Height - versionPill.Height) / 2);   // 제목이 아니라 헤더 전체 기준 가운데
			lblSubTitle.Left = lblTitle.Left + 2;
			LayoutHeaderRight();
		}

		/// <summary>설정에서 읽기 방식을 바꿔 데이터를 다시 읽기 시작할 때: 로그, 리전, 하단 상태줄을 새 방식에 맞게 갱신</summary>
		private void OnDataSourceChanged(KartRhoFile.GameDataSource src)
		{
			dataSource = src;
			dataShown = false;
			LogDataSource(src);
			DetectRegion(src);
			lblData.Text = "● 게임 데이터(" + DataSourceName() + ") 읽는 중…";
			lblData.ForeColor = UiTheme.Warn;
		}

		/// <summary>하단 상태줄에 표시할 데이터 읽기 방식 이름: 기존 방식이면 "Data", datapackOff 폴더 방식이면 그 폴더 이름(예: DataRaw)</summary>
		private string DataSourceName()
		{
			if (dataSource != null && dataSource.Kind == KartRhoFile.DataSourceKind.Folder)
			{
				try
				{
					string n = Path.GetFileName(dataSource.Location.TrimEnd('\\', '/'));
					if (!string.IsNullOrEmpty(n)) return n;
				}
				catch { }
				return "DataRaw";
			}
			return "Data";
		}

		/// <summary>게임 데이터를 어느 방식으로 읽는지 로그에 표시 (기존 Data 폴더 / KartRider.xml 의 datapackOff 폴더)</summary>
		private void LogDataSource(KartRhoFile.GameDataSource src)
		{
			if (src.Kind == KartRhoFile.DataSourceKind.Folder)
				Console.WriteLine("[런처] 게임 데이터 방식: 폴더에서 읽기 (KartRider.xml {0})", src.Message);
			else
				Console.WriteLine("[런처] 게임 데이터 방식: 기존 Data 폴더 (aaa.pk){0}", string.IsNullOrEmpty(src.Message) ? "" : " — " + src.Message);
		}

		/// <summary>게임 데이터에서 국가 리전을 판별해 헤더에 표시 (읽는 방식에 맞게 aaa.pk 또는 폴더에서)</summary>
		private void DetectRegion(KartRhoFile.GameDataSource src)
		{
			string code = null;
			try
			{
				if (src.Kind == KartRhoFile.DataSourceKind.Folder) code = KartRhoFile.DetectRegionRaw(src.Location);
				else if (File.Exists(src.Location)) code = KartRhoFile.DetectRegion(src.Location);
			}
			catch { }
			UpdateRegion(code);
			Console.WriteLine("[런처] 국가 리전: {0}", code ?? "(알 수 없음)");
		}

		private void UpdateRegion(string code)
		{
			lblRegion.Text = string.IsNullOrEmpty(code) ? "-" : code.Trim().ToUpperInvariant();
			LayoutHeaderRight();
		}

		/// <summary>헤더 오른쪽: 국가 리전 | 클라이언트 버전 을 오른쪽 끝에 촘촘하게, 세로로는 헤더 높이의 가운데에 배치</summary>
		private void LayoutHeaderRight()
		{
			if (headerRightPanel == null || headerSep == null || lblRegion == null || lblRegionCaption == null || MinorVersion == null || label_DeveloperName == null) return;
			float sc = DeviceDpi / 96f;
			int padRight = (int)(24 * sc), gap = (int)(14 * sc), slack = (int)(6 * sc);

			// 가로: 실제로 그려지는 글자 폭 기준 (잘리지 않게), 영역이 모자라면 왼쪽으로 넓힘
			int regW = Math.Max(lblRegionCaption.GetPreferredSize(Size.Empty).Width, lblRegion.GetPreferredSize(Size.Empty).Width) + slack;
			int verW = Math.Max(label_DeveloperName.GetPreferredSize(Size.Empty).Width, MinorVersion.GetPreferredSize(Size.Empty).Width) + slack;
			int need = padRight + verW + gap + headerSep.Width + gap + regW + (int)(8 * sc);
			if (headerRightPanel.Width < need) headerRightPanel.Width = need;
			int verLeft = headerRightPanel.ClientSize.Width - padRight - verW;
			int sepLeft = verLeft - gap - headerSep.Width;
			int regLeft = sepLeft - gap - regW;

			// 세로: 글자 높이(위 작은 글자 + 아래 큰 글자) 기준으로 두 줄을 헤더 높이의 정확히 가운데에
			int capH = Math.Max(lblRegionCaption.Font.Height, label_DeveloperName.Font.Height) + 2;
			int valH = Math.Max(lblRegion.Font.Height, MinorVersion.Font.Height) + 2;
			int lineGap = Math.Max(1, (int)(2 * sc));
			int totalH = capH + lineGap + valH;
			int top0 = Math.Max(0, (headerRightPanel.ClientSize.Height - totalH) / 2);
			int valTop = top0 + capH + lineGap;

			lblRegionCaption.SetBounds(regLeft, top0, regW, capH);
			lblRegion.SetBounds(regLeft, valTop, regW, valH);
			label_DeveloperName.SetBounds(verLeft, top0, verW, capH);
			MinorVersion.SetBounds(verLeft, valTop, verW, valH);
			headerSep.Height = (int)(totalH * 0.85f);
			headerSep.Location = new Point(sepLeft, Math.Max(0, (headerRightPanel.ClientSize.Height - headerSep.Height) / 2));
		}

		private static Label MakeStatusLabel(string text, Color color)
		{
			var l = new Label();
			l.AutoSize = true;
			l.Text = text;
			l.Font = UiTheme.UiFont(9F);
			l.ForeColor = color;
			l.BackColor = Color.Transparent;
			l.Margin = new Padding(0, 7, 22, 0);
			return l;
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			UiTheme.DarkTitleBar(this);
		}

		// 창 제목(Text)은 config.cs 에서 "속도 패치" 여부에 따라 바뀜 -> 헤더 부제목에 반영
		protected override void OnTextChanged(EventArgs e)
		{
			base.OnTextChanged(e);
			UpdateSubTitle();
		}

		private void UpdateSubTitle()
		{
			if (lblSubTitle == null || lblSubTitle.IsDisposed) return;
			if (lblSubTitle.InvokeRequired)
			{
				try { lblSubTitle.BeginInvoke(new Action(UpdateSubTitle)); } catch { }
				return;
			}
			bool patch = Text != null && Text.Contains("속도 패치");
			lblSubTitle.Text = patch ? "속도 패치 사용 중" : "로컬 서버 모드";
			lblSubTitle.ForeColor = patch ? UiTheme.Warn : UiTheme.Muted;
		}

		/// <summary>게임 상태 표시 (다른 스레드에서 호출해도 됨)</summary>
		private void SetGameStatus(string text, Color color)
		{
			if (lblGame == null || lblGame.IsDisposed) return;
			if (lblGame.InvokeRequired)
			{
				try { lblGame.BeginInvoke(new Action(() => SetGameStatus(text, color))); } catch { }
				return;
			}
			lblGame.Text = "● " + text;
			lblGame.ForeColor = color;
		}

		private void UpdateServerStatus()
		{
			if (InvokeRequired) { try { BeginInvoke(new Action(UpdateServerStatus)); } catch { } return; }
			if (RouterListener.Running)
			{
				lblServer.Text = "● 서버 " + RouterListener.sIP + ":" + RouterListener.port + " 대기 중";
				lblServer.ForeColor = UiTheme.Ok;
			}
			else
			{
				lblServer.Text = "● 서버 시작 실패" + (string.IsNullOrEmpty(RouterListener.LastError) ? "" : " (" + RouterListener.LastError + ")");
				lblServer.ForeColor = UiTheme.Err;
			}
		}

		/// <summary>플레이를 막아야 하는 이유 (null 이면 실행 가능)</summary>
		private string PlayBlockReason()
		{
			if (!startupDone) return "준비 중…";
			if (!File.Exists(Launcher.KartRider) && !File.Exists(@"KartRider.pin")) return "게임 파일 없음";
			if (KartRhoFile.Status == "읽는 중") return "게임 데이터 읽는 중…";
			if (!RouterListener.Running) return "서버 시작 실패";
			if (gameRunning) return "게임 실행 중";
			if (DateTime.UtcNow < launchingUntil) return "게임 시작 중…";
			return null;
		}

		private void UpdatePlayState()
		{
			if (playButton == null) return;
			if (++procCheckTick >= 3)
			{
				procCheckTick = 0;
				try { gameRunning = Process.GetProcessesByName("KartRider").Length != 0; } catch { }
			}
			string reason = PlayBlockReason();
			playButton.Reason = reason;
			bool canPlay = reason == null;
			if (playButton.Enabled != canPlay) playButton.Enabled = canPlay;
		}

		private void StatusTick(object sender, EventArgs e)
		{
			UpdatePlayState();
			string st = KartRhoFile.Status;
			if (st == "읽는 중")
			{
				lblData.Text = "● 게임 데이터(" + DataSourceName() + ") 읽는 중…";
				lblData.ForeColor = UiTheme.Warn;
			}
			else if (st == "완료" && !dataShown)
			{
				dataShown = true;
				int items = 0;
				foreach (var v in RiderData.NewRider.items.Values) items += v.Count;
				lblData.Text = "● 게임 데이터(" + DataSourceName() + "): 카트 " + Kart.kartName.Count + " · 펫 " + FlyingPet.flyingName.Count + " · 아이템 " + items;
				lblData.ForeColor = (KartRhoFile.FromFolder ? UiTheme.Accent : UiTheme.Ok);
			}
			else if (st == "실패")
			{
				lblData.Text = "● 게임 데이터(" + DataSourceName() + ") 읽기 실패 (기본 성능 사용)";
				lblData.ForeColor = UiTheme.Err;
			}
			else if (st == "꺼짐")
			{
				lblData.Text = "● 게임 데이터(" + DataSourceName() + ") 읽기 꺼짐";
				lblData.ForeColor = UiTheme.Muted;
			}
			else if (st == "없음")
			{
				lblData.Text = "● 게임 데이터(" + DataSourceName() + ") 없음 (기본 성능 사용)";
				lblData.ForeColor = UiTheme.Muted;
			}
		}

		private void OnFormClosing(object sender, FormClosingEventArgs e)
		{
			if (File.Exists(this.kartRiderDirectory + "KartRider-bak.pin"))
			{
				File.Delete(this.kartRiderDirectory + "KartRider.pin");
				File.Move(this.kartRiderDirectory + "KartRider-bak.pin", this.kartRiderDirectory + "KartRider.pin");
			}
			if (Process.GetProcessesByName("KartRider").Length != 0)
			{
				LauncherSystem.MessageBoxType1();
				//e.Cancel = true;
			}
		}

		private void OnLoad(object sender, EventArgs e)
		{
			this.kartRiderDirectory = Environment.CurrentDirectory + "\\";
			LayoutHeader();
			Console.WriteLine("[런처] 코드 버전 {0} · 프로그램 v{1} · 빌드 시각 {2}", AppInfo.Build, AppInfo.Version, AppInfo.BuildTime);
			KartRhoFile.GameDir = this.kartRiderDirectory;
			// 게임 폴더에 KartRider.xml 이 없으면 기본 파일을 만듦 (datapackOff 설정이 동작하려면 필요)
			if (File.Exists(Path.Combine(this.kartRiderDirectory, "KartRider.exe")) || File.Exists(Path.Combine(this.kartRiderDirectory, "KartRider.pin")))
				KartRhoFile.EnsureConfigFile(this.kartRiderDirectory);
			dataSource = KartRhoFile.ResolveSource(this.kartRiderDirectory);
			KartRhoFile.CurrentSource = dataSource;
			RouterListener.ServerChanged += UpdateServerStatus;   // 설정에서 서버 주소를 바꾸면 표시 갱신
			KartRhoFile.SourceChanged += OnDataSourceChanged;   // 설정에서 Data/DataRaw 를 바꾸면 화면 표시도 갱신
			LogDataSource(dataSource);
			lblData.Text = "● 게임 데이터(" + DataSourceName() + ") 대기";
			DetectRegion(dataSource);
			Console.WriteLine("[런처] 게임 폴더: {0} / KartRider.exe: {1}, KartRider.pin: {2}, Data 폴더: {3}", this.kartRiderDirectory, File.Exists(Launcher.KartRider), File.Exists("KartRider.pin"), Directory.Exists(Path.Combine(this.kartRiderDirectory, "Data")));
			string str = Path.Combine(this.kartRiderDirectory, "Profile", SessionGroup.Service);
			if (!Directory.Exists(str))
			{
				Directory.CreateDirectory(str);
			}
			if (File.Exists(Launcher.KartRider) || File.Exists(@"KartRider.pin"))
			{
				Load_KartExcData();
				StartingLoad_ALL.StartingLoad();
				SettingsHub.Reload();   // 불러온 닉네임 등을 설정 카드에 반영
				PINFile PINFile = new PINFile(pinFile);
				MinorVersion.Text = Convert.ToString(PINFile.Header.MinorVersion);
				SetGameOption.Version = ushort.Parse(MinorVersion.Text);
				SetGameOption.Save_SetGameOption();
				this.profilePath = Path.Combine(str, "launcher.xml");
				Console.WriteLine("Process: {0}", this.kartRiderDirectory + Launcher.KartRider);
				Console.WriteLine("Profile: {0}", this.profilePath);
				KartRhoFile.StartLoad(dataSource);
				RouterListener.sIP = LauncherSettings.Current.ServerIP;
				RouterListener.port = LauncherSettings.Current.ServerPort;
				Console.WriteLine("[설정] " + LauncherSettings.Current.Describe());
				RouterListener.Start();
				UpdateServerStatus();
				LayoutHeaderRight();   // 클라이언트 버전 숫자가 정해졌으니 헤더 오른쪽 다시 배치
			}
			else
			{
				LauncherSystem.MessageBoxType3();
			}
			startupDone = true;
		}

		private void Start_Button_Click(object sender, EventArgs e)
		{
			if (playButton != null && !playButton.Enabled) return;   // 실행할 수 없는 상태 (데이터 읽는 중 등)
			launchingUntil = DateTime.UtcNow.AddSeconds(5);          // 게임이 뜰 때까지 다시 누르지 못하게
			if (Process.GetProcessesByName("KartRider").Length != 0)
			{
				LauncherSystem.MessageBoxType2();
			}
			else
			{
				(new Thread(() =>
				{
					Console.WriteLine("Backing up old PinFile...");
					Console.WriteLine(this.kartRiderDirectory + "KartRider-bak.pin");
					if (File.Exists(this.kartRiderDirectory + "KartRider-bak.pin"))
					{
						File.Delete(this.kartRiderDirectory + "KartRider.pin");
						File.Move(this.kartRiderDirectory + "KartRider-bak.pin", this.kartRiderDirectory + "KartRider.pin");
					}
					File.Copy(this.kartRiderDirectory + "KartRider.pin", this.kartRiderDirectory + "KartRider-bak.pin");
					PINFile val = new PINFile(this.kartRiderDirectory + "KartRider.pin");
					if (LauncherSettings.Current.PinLog) DumpPin(val);
					bool removeSec = LauncherSettings.Current.RemoveSecModule;
					bool removeNgs = LauncherSettings.Current.RemoveNgsOn;
					foreach (AuthMethod authMethod in val.AuthMethods)
					{
						Console.WriteLine("Changing IP Addr to local... {0}", authMethod.Name);
						authMethod.LoginServers.Clear();
						authMethod.LoginServers.Add(new IPEndPoint
						{
							IP = RouterListener.sIP,
							Port = (ushort)RouterListener.port
						});
					}
					foreach (BmlObject bml in val.BmlObjects)
					{
						if (bml.Name == "extra")
						{
							for (int i = bml.SubObjects.Count - 1; i >= 0; i--)
							{
								Console.WriteLine("Removing {0}", bml.SubObjects[i].Item1);
								string subName = bml.SubObjects[i].Item1;
								if (subName == "NgsOn" && removeNgs)
								{
									Console.WriteLine("[PIN] NgsOn 제거");
									bml.SubObjects.RemoveAt(i);
									continue;
								}
								if (removeSec && subName == "secModule")
								{
									Console.WriteLine("[PIN] secModule 제거");
									bml.SubObjects.RemoveAt(i);
								}
							}
						}
					}
					File.WriteAllBytes(this.kartRiderDirectory + "KartRider.pin", val.GetEncryptedData());
					Start_Button.Enabled = true;
					Launcher.GetKart = false;
					string str = this.profilePath;
					string[] text = new string[] { "<?xml version='1.0' encoding='UTF-16'?>\r\n<profile>\r\n<username>", SetRider.UserID, "</username>\r\n</profile>" };
					File.WriteAllText(str, string.Concat(text));
					ProcessStartInfo startInfo = new ProcessStartInfo(Launcher.KartRider, LauncherSettings.Current.LaunchArgs)
					{
						WorkingDirectory = this.kartRiderDirectory,
						UseShellExecute = true
					};
					if (LauncherSettings.Current.RunAsAdmin) startInfo.Verb = "runas";
					Console.WriteLine("[런처] 실행 명령: {0} {1} (관리자 권한: {2})", Launcher.KartRider, LauncherSettings.Current.LaunchArgs, LauncherSettings.Current.RunAsAdmin);
					try
					{
						DateTime startedUtc = DateTime.UtcNow;
						Process game = Process.Start(startInfo);
						Console.WriteLine("[런처] 게임 실행: {0} (PID {1})", Launcher.KartRider, game != null ? game.Id : 0);
						SetGameStatus("게임 실행 중 (PID " + (game != null ? game.Id : 0) + ")", UiTheme.Ok);
						if (game != null)
						{
							(new Thread(() =>
							{
								try { game.WaitForExit(); int code = game.ExitCode; Console.WriteLine("[런처] 게임 종료: 종료 코드 {0} (0x{0:X})", code); SetGameStatus(code == 0 ? "게임 종료" : "게임 종료 (종료 코드 0x" + code.ToString("X") + ")", code == 0 ? UiTheme.Muted : UiTheme.Err); if (code != 0) LogCrashEvent(startedUtc); }
								catch (Exception ex2) { Console.WriteLine("[런처] 게임 종료 감시 실패: " + ex2.Message); }
							}) { IsBackground = true }).Start();
						}
						Thread.Sleep(1000);
						Start_Button.Enabled = true;
						Launcher.GetKart = true;
					}
					catch (System.ComponentModel.Win32Exception ex)
					{
						// 사용자가 UAC 창을 취소했거나 권한이 없음
						Console.WriteLine(ex.Message);
					}
				})).Start();
			}
		}

		/// <summary>게임이 비정상 종료되면 Windows 오류 이벤트(오류 모듈/예외 코드/오프셋)를 로그에 남김</summary>
		private static void LogCrashEvent(DateTime sinceUtc)
		{
			try
			{
				Thread.Sleep(3000); // Windows 가 오류 이벤트를 기록할 때까지 잠시 대기
				var psi = new ProcessStartInfo("wevtutil.exe", "qe Application \"/q:*[System[Provider[@Name='Application Error']]]\" /c:8 /rd:true /f:xml")
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				};
				using (Process p = Process.Start(psi))
				{
					string xml = p.StandardOutput.ReadToEnd();
					p.WaitForExit(5000);
					foreach (string ev in xml.Split(new[] { "</Event>" }, StringSplitOptions.RemoveEmptyEntries))
					{
						if (ev.IndexOf("KartRider", StringComparison.OrdinalIgnoreCase) < 0) continue;
						var vals = System.Text.RegularExpressions.Regex.Matches(ev, "<Data[^>]*>([^<]*)</Data>").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value);
						string time = System.Text.RegularExpressions.Regex.Match(ev, "SystemTime='([^']*)'").Groups[1].Value;
						DateTime evTime;
						// 이번 실행 이전(예: 어제 다른 클라이언트)의 오류 이벤트는 무시
						if (DateTime.TryParse(time, null, System.Globalization.DateTimeStyles.RoundtripKind, out evTime) && evTime.ToUniversalTime() < sinceUtc.AddSeconds(-5)) continue;
						Console.WriteLine("[런처] Windows 오류 이벤트 ({0}): {1}", time, string.Join(" | ", vals));
						return;
					}
					Console.WriteLine("[런처] 이번 실행에 해당하는 Windows 오류 이벤트 없음 -> 게임이 크래시가 아니라 스스로 종료 코드를 반환하며 종료했을 가능성");
				}
			}
			catch (Exception ex) { Console.WriteLine("[런처] 오류 이벤트 조회 실패: " + ex.Message); }
		}

		private static void DumpBml(BmlObject b, string indent)
		{
			string attrs = string.Join(", ", b.Values.Select(kv => kv.Key + "=" + kv.Value));
			Console.WriteLine("[PIN] {0}{1} value='{2}' attrs[{3}]", indent, b.Name, b.Value, attrs);
			foreach (var sub in b.SubObjects) DumpBml(sub.Item2, indent + "  ");
		}

		private static void DumpPin(PINFile pin)
		{
			try
			{
				var h = pin.Header;
				Console.WriteLine("[PIN] 헤더: LocaleID={0}, LocaleType={1}, MinorVersion={2}, LoginType={3}, URL={4}, PatchURL={5}", h.LocaleID, h.LocaleType, h.MinorVersion, h.LoginType, h.URL, h.PatchURL);
				foreach (AuthMethod m in pin.AuthMethods)
				{
					Console.WriteLine("[PIN] 인증방식 #{0} {1}: 서버 {2}", m.Index, m.Name, string.Join(", ", m.LoginServers.Select(x => x.ToString())));
					if (m.AccountConfig != null) DumpBml(m.AccountConfig, "  ");
					if (m.ExtraConfig != null) DumpBml(m.ExtraConfig, "  ");
				}
				foreach (BmlObject b in pin.BmlObjects) DumpBml(b, "");
			}
			catch (Exception ex) { Console.WriteLine("[PIN] 덤프 실패: " + ex.Message); }
		}

		private void GetKart_Button_Click(object sender, EventArgs e)
		{
			// 런처가 인식한 아이템을 분류별로 골라서 추가하는 카드 (런처 화면 중앙에 표시)
			OpenModal(itemPicker, 720, 500);
		}

		private void Speed_comboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (Speed_comboBox.SelectedItem != null)
			{
				string selectedSpeed = Speed_comboBox.SelectedItem.ToString();
				if (SpeedType.speedNames.ContainsKey(selectedSpeed))
				{
					config.SpeedType = SpeedType.speedNames[selectedSpeed];
					Console.WriteLine("[런처] 속도 선택: " + selectedSpeed);
				}
				else
				{
					Console.WriteLine("Invalid speed type selected.");
				}
			}
		}

		public void Load_KartExcData()
		{
			if (!(File.Exists(@"Profile\Item.xml")))
			{
				string Item = Resources.Item;
				using (StreamWriter streamWriter = new StreamWriter(@"Profile\Item.xml", false))
				{
					streamWriter.Write(Item);
				}
			}
			if (!(File.Exists(@"Profile\RandomTrack.xml")))
			{
				string RandomTrack = Resources.RandomTrack;
				using (StreamWriter streamWriter = new StreamWriter(@"Profile\RandomTrack.xml", false))
				{
					streamWriter.Write(RandomTrack);
				}
			}
			if (!(File.Exists(@"Profile\NewKart.xml")))
			{
				string NewKart = Resources.NewKart;
				using (StreamWriter streamWriter = new StreamWriter(@"Profile\NewKart.xml", false))
				{
					streamWriter.Write(NewKart);
				}
			}
			if (!(File.Exists(@"Profile\PartsData.xml")))
			{
				string PartsData = Resources.PartsData;
				using (StreamWriter streamWriter = new StreamWriter(@"Profile\PartsData.xml", false))
				{
					streamWriter.Write(PartsData);
				}
			}
			if (File.Exists(@"Profile\TuneData.xml"))
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(@"Profile\TuneData.xml");
				if (!(doc.GetElementsByTagName("Kart") == null))
				{
					XmlNodeList lis = doc.GetElementsByTagName("Kart");
					KartExcData.TuneList = new List<List<short>>();
					foreach (XmlNode xn in lis)
					{
						XmlElement xe = (XmlElement)xn;
						short i = short.Parse(xe.GetAttribute("id"));
						short sn = short.Parse(xe.GetAttribute("sn"));
						short tune1 = short.Parse(xe.GetAttribute("tune1"));
						short tune2 = short.Parse(xe.GetAttribute("tune2"));
						short tune3 = short.Parse(xe.GetAttribute("tune3"));
						short slot1 = short.Parse(xe.GetAttribute("slot1"));
						short count1 = short.Parse(xe.GetAttribute("count1"));
						short slot2 = short.Parse(xe.GetAttribute("slot2"));
						short count2 = short.Parse(xe.GetAttribute("count2"));
						List<short> AddList = new List<short> { i, sn, tune1, tune2, tune3, slot1, count1, slot2, count2 };
						KartExcData.TuneList.Add(AddList);
					}
				}
			}
			if (File.Exists(@"Profile\PlantData.xml"))
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(@"Profile\PlantData.xml");
				if (!(doc.GetElementsByTagName("Kart") == null))
				{
					XmlNodeList lis = doc.GetElementsByTagName("Kart");
					KartExcData.PlantList = new List<List<short>>();
					foreach (XmlNode xn in lis)
					{
						XmlElement xe = (XmlElement)xn;
						short i = short.Parse(xe.GetAttribute("id"));
						short sn = short.Parse(xe.GetAttribute("sn"));
						short item1 = short.Parse(xe.GetAttribute("item1"));
						short item_id1 = short.Parse(xe.GetAttribute("item_id1"));
						short item2 = short.Parse(xe.GetAttribute("item2"));
						short item_id2 = short.Parse(xe.GetAttribute("item_id2"));
						short item3 = short.Parse(xe.GetAttribute("item3"));
						short item_id3 = short.Parse(xe.GetAttribute("item_id3"));
						short item4 = short.Parse(xe.GetAttribute("item4"));
						short item_id4 = short.Parse(xe.GetAttribute("item_id4"));
						List<short> AddList = new List<short> { i, sn, item1, item_id1, item2, item_id2, item3, item_id3, item4, item_id4 };
						KartExcData.PlantList.Add(AddList);
					}
				}
			}
			if (File.Exists(@"Profile\LevelData.xml"))
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(@"Profile\LevelData.xml");
				if (!(doc.GetElementsByTagName("Kart") == null))
				{
					XmlNodeList lis = doc.GetElementsByTagName("Kart");
					KartExcData.LevelList = new List<List<short>>();
					foreach (XmlNode xn in lis)
					{
						XmlElement xe = (XmlElement)xn;
						short i = short.Parse(xe.GetAttribute("id"));
						short sn = short.Parse(xe.GetAttribute("sn"));
						short level = short.Parse(xe.GetAttribute("level"));
						short point = short.Parse(xe.GetAttribute("point"));
						short v1 = short.Parse(xe.GetAttribute("v1"));
						short v2 = short.Parse(xe.GetAttribute("v2"));
						short v3 = short.Parse(xe.GetAttribute("v3"));
						short v4 = short.Parse(xe.GetAttribute("v4"));
						short Effect = short.Parse(xe.GetAttribute("Effect"));
						List<short> AddList = new List<short> { i, sn, level, point, v1, v2, v3, v4, Effect };
						KartExcData.LevelList.Add(AddList);
					}
				}
			}
			if (File.Exists(@"Profile\PartsData.xml"))
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(@"Profile\PartsData.xml");
				if (!(doc.GetElementsByTagName("Kart") == null))
				{
					XmlNodeList lis = doc.GetElementsByTagName("Kart");
					KartExcData.PartsList = new List<List<short>>();
					foreach (XmlNode xn in lis)
					{
						XmlElement xe = (XmlElement)xn;
						short i = short.Parse(xe.GetAttribute("id"));
						short sn = short.Parse(xe.GetAttribute("sn"));
						short Item_Id1 = short.Parse(xe.GetAttribute("Item_Id1"));
						byte Grade1 = byte.Parse(xe.GetAttribute("Grade1"));
						short PartsValue1 = short.Parse(xe.GetAttribute("PartsValue1"));
						short Item_Id2 = short.Parse(xe.GetAttribute("Item_Id2"));
						byte Grade2 = byte.Parse(xe.GetAttribute("Grade2"));
						short PartsValue2 = short.Parse(xe.GetAttribute("PartsValue2"));
						short Item_Id3 = short.Parse(xe.GetAttribute("Item_Id3"));
						byte Grade3 = byte.Parse(xe.GetAttribute("Grade3"));
						short PartsValue3 = short.Parse(xe.GetAttribute("PartsValue3"));
						short Item_Id4 = short.Parse(xe.GetAttribute("Item_Id4"));
						byte Grade4 = byte.Parse(xe.GetAttribute("Grade4"));
						short PartsValue4 = short.Parse(xe.GetAttribute("PartsValue4"));
						short partsCoating = byte.Parse(xe.GetAttribute("partsCoating"));
						short partsTailLamp = short.Parse(xe.GetAttribute("partsTailLamp"));
						List<short> AddList = new List<short> { i, sn, Item_Id1, Grade1, PartsValue1, Item_Id2, Grade2, PartsValue2, Item_Id3, Grade3, PartsValue3, Item_Id4, Grade4, PartsValue4, partsCoating, partsTailLamp };
						KartExcData.PartsList.Add(AddList);
					}
				}
			}
		}
	}
}
