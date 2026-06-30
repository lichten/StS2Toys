namespace StS2Toys
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            lblUpdateFlash = new Label();
            lblLastUpdated = new Label();
            btnLang = new Button();
            btnToggleAuto = new Button();
            btnOpen = new Button();
            lblGroupFile = new Label();
            panelInfo = new Panel();
            lblInfo = new Label();
            splitContainerOuter = new SplitContainer();
            panelSideButtons = new Panel();
            btnEncounterOverview = new Button();
            btnHpHistory = new Button();
            lblGroupOther = new Label();
            btnCharacterOverview = new Button();
            btnCombinedOverview = new Button();
            lblGroupOverview = new Label();
            _top = new FlowLayoutPanel();
            _radioGroup = new FlowLayoutPanel();
            _lblCapture = new Label();
            _rbWgc = new RadioButton();
            _rbGdi = new RadioButton();
            _cbAuto = new CheckBox();
            _btnCapture = new Button();
            _btnLinks = new Button();
            _lblCharacter = new Label();
            _cbCharacter = new ComboBox();
            _status = new Label();
            _outer = new SplitContainer();
            _left = new SplitContainer();
            _list = new ListView();
            _colCardId = new ColumnHeader();
            _colEn = new ColumnHeader();
            _colJp = new ColumnHeader();
            _colConf = new ColumnHeader();
            _colRecog = new ColumnHeader();
            _ocrHeaderPanel = new Panel();
            _ocrList = new ListView();
            _colOcrText = new ColumnHeader();
            _colOcrKind = new ColumnHeader();
            _colOcrMatch = new ColumnHeader();
            _colOcrDist = new ColumnHeader();
            _ocrHeaderLabel = new Label();
            _previewHeaderPanel = new Panel();
            _capturePreview = new PictureBox();
            _previewHeaderLabel = new Label();
            panelInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerOuter).BeginInit();
            splitContainerOuter.Panel1.SuspendLayout();
            splitContainerOuter.Panel2.SuspendLayout();
            splitContainerOuter.SuspendLayout();
            panelSideButtons.SuspendLayout();
            _top.SuspendLayout();
            _radioGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_outer).BeginInit();
            _outer.Panel1.SuspendLayout();
            _outer.Panel2.SuspendLayout();
            _outer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_left).BeginInit();
            _left.Panel1.SuspendLayout();
            _left.Panel2.SuspendLayout();
            _left.SuspendLayout();
            _ocrHeaderPanel.SuspendLayout();
            _previewHeaderPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_capturePreview).BeginInit();
            SuspendLayout();
            //
            // panelInfo
            //
            panelInfo.BackColor = SystemColors.ControlLight;
            panelInfo.Controls.Add(lblInfo);
            panelInfo.Dock = DockStyle.Top;
            panelInfo.Location = new Point(0, 44);
            panelInfo.Name = "panelInfo";
            panelInfo.Padding = new Padding(10, 6, 8, 4);
            panelInfo.Size = new Size(800, 52);
            panelInfo.TabIndex = 1;
            //
            // lblInfo
            //
            lblInfo.Dock = DockStyle.Fill;
            lblInfo.Font = new Font("Segoe UI", 10F);
            lblInfo.Location = new Point(10, 6);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(782, 42);
            lblInfo.TabIndex = 0;
            lblInfo.Text = "ファイルを開くと、ランの情報を表示します。";
            //
            // splitContainerOuter
            //
            splitContainerOuter.Dock = DockStyle.Fill;
            splitContainerOuter.FixedPanel = FixedPanel.Panel1;
            splitContainerOuter.Location = new Point(0, 96);
            splitContainerOuter.Name = "splitContainerOuter";
            //
            // splitContainerOuter.Panel1
            //
            splitContainerOuter.Panel1.Controls.Add(panelSideButtons);
            splitContainerOuter.Panel1MinSize = 60;
            //
            // splitContainerOuter.Panel2
            //
            splitContainerOuter.Panel2.Controls.Add(_outer);
            splitContainerOuter.Panel2.Controls.Add(_status);
            splitContainerOuter.Panel2.Controls.Add(_top);
            splitContainerOuter.Panel2MinSize = 200;
            splitContainerOuter.Size = new Size(800, 424);
            splitContainerOuter.SplitterDistance = 150;
            splitContainerOuter.TabIndex = 0;
            //
            // panelSideButtons
            //
            panelSideButtons.Controls.Add(btnEncounterOverview);
            panelSideButtons.Controls.Add(btnHpHistory);
            panelSideButtons.Controls.Add(lblGroupOther);
            panelSideButtons.Controls.Add(btnCharacterOverview);
            panelSideButtons.Controls.Add(btnCombinedOverview);
            panelSideButtons.Controls.Add(lblGroupOverview);
            panelSideButtons.Controls.Add(lblUpdateFlash);
            panelSideButtons.Controls.Add(lblLastUpdated);
            panelSideButtons.Controls.Add(btnLang);
            panelSideButtons.Controls.Add(btnToggleAuto);
            panelSideButtons.Controls.Add(btnOpen);
            panelSideButtons.Controls.Add(lblGroupFile);
            panelSideButtons.Dock = DockStyle.Fill;
            panelSideButtons.Location = new Point(0, 0);
            panelSideButtons.Name = "panelSideButtons";
            panelSideButtons.Size = new Size(150, 424);
            panelSideButtons.TabIndex = 0;
            //
            // btnEncounterOverview
            //
            btnEncounterOverview.Dock = DockStyle.Top;
            btnEncounterOverview.Location = new Point(0, 246);
            btnEncounterOverview.Name = "btnEncounterOverview";
            btnEncounterOverview.Size = new Size(150, 30);
            btnEncounterOverview.TabIndex = 6;
            btnEncounterOverview.Text = "○ 敵情報";
            btnEncounterOverview.Click += BtnEncounterOverview_Click;
            //
            // btnHpHistory
            //
            btnHpHistory.Dock = DockStyle.Top;
            btnHpHistory.Location = new Point(0, 216);
            btnHpHistory.Name = "btnHpHistory";
            btnHpHistory.Size = new Size(150, 30);
            btnHpHistory.TabIndex = 5;
            btnHpHistory.Text = "○ HP変動";
            btnHpHistory.Click += BtnHpHistory_Click;
            //
            // lblGroupOther
            //
            lblGroupOther.BackColor = SystemColors.ControlDark;
            lblGroupOther.Dock = DockStyle.Top;
            lblGroupOther.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            lblGroupOther.ForeColor = Color.White;
            lblGroupOther.Location = new Point(0, 198);
            lblGroupOther.Name = "lblGroupOther";
            lblGroupOther.Padding = new Padding(4, 0, 0, 0);
            lblGroupOther.Size = new Size(150, 18);
            lblGroupOther.TabIndex = 8;
            lblGroupOther.Text = "その他";
            lblGroupOther.TextAlign = ContentAlignment.MiddleLeft;
            //
            // btnCharacterOverview
            //
            btnCharacterOverview.Dock = DockStyle.Top;
            btnCharacterOverview.Location = new Point(0, 168);
            btnCharacterOverview.Name = "btnCharacterOverview";
            btnCharacterOverview.Size = new Size(150, 30);
            btnCharacterOverview.TabIndex = 8;
            btnCharacterOverview.Text = "○ キャラクター概観";
            btnCharacterOverview.Click += BtnCharacterOverview_Click;
            //
            // btnCombinedOverview
            //
            btnCombinedOverview.Dock = DockStyle.Top;
            btnCombinedOverview.Location = new Point(0, 108);
            btnCombinedOverview.Name = "btnCombinedOverview";
            btnCombinedOverview.Size = new Size(150, 30);
            btnCombinedOverview.TabIndex = 2;
            btnCombinedOverview.Text = "○ デッキ概観";
            btnCombinedOverview.Click += BtnCombinedOverview_Click;
            //
            // lblGroupOverview
            //
            lblGroupOverview.BackColor = SystemColors.ControlDark;
            lblGroupOverview.Dock = DockStyle.Top;
            lblGroupOverview.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            lblGroupOverview.ForeColor = Color.White;
            lblGroupOverview.Location = new Point(0, 90);
            lblGroupOverview.Name = "lblGroupOverview";
            lblGroupOverview.Padding = new Padding(4, 0, 0, 0);
            lblGroupOverview.Size = new Size(150, 18);
            lblGroupOverview.TabIndex = 14;
            lblGroupOverview.Text = "概観";
            lblGroupOverview.TextAlign = ContentAlignment.MiddleLeft;
            //
            // lblGroupFile
            //
            lblGroupFile.BackColor = SystemColors.ControlDark;
            lblGroupFile.Dock = DockStyle.Top;
            lblGroupFile.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            lblGroupFile.ForeColor = Color.White;
            lblGroupFile.Location = new Point(0, 0);
            lblGroupFile.Name = "lblGroupFile";
            lblGroupFile.Padding = new Padding(4, 0, 0, 0);
            lblGroupFile.Size = new Size(150, 18);
            lblGroupFile.TabIndex = 15;
            lblGroupFile.Text = "ファイル / 更新";
            lblGroupFile.TextAlign = ContentAlignment.MiddleLeft;
            //
            // btnOpen
            //
            btnOpen.Dock = DockStyle.Top;
            btnOpen.Location = new Point(0, 18);
            btnOpen.Name = "btnOpen";
            btnOpen.Size = new Size(150, 30);
            btnOpen.TabIndex = 16;
            btnOpen.Text = "ファイルを開く";
            btnOpen.Click += BtnOpen_Click;
            //
            // btnToggleAuto
            //
            btnToggleAuto.Dock = DockStyle.Top;
            btnToggleAuto.Location = new Point(0, 48);
            btnToggleAuto.Name = "btnToggleAuto";
            btnToggleAuto.Size = new Size(150, 30);
            btnToggleAuto.TabIndex = 17;
            btnToggleAuto.Text = "○ 自動更新";
            btnToggleAuto.Click += BtnToggleAuto_Click;
            //
            // btnLang
            //
            btnLang.Dock = DockStyle.Top;
            btnLang.Location = new Point(0, 78);
            btnLang.Name = "btnLang";
            btnLang.Size = new Size(150, 30);
            btnLang.TabIndex = 18;
            btnLang.Text = "JP";
            btnLang.Click += BtnLang_Click;
            //
            // lblLastUpdated
            //
            lblLastUpdated.Dock = DockStyle.Top;
            lblLastUpdated.Location = new Point(0, 108);
            lblLastUpdated.Name = "lblLastUpdated";
            lblLastUpdated.Padding = new Padding(8, 0, 0, 0);
            lblLastUpdated.Size = new Size(150, 24);
            lblLastUpdated.TabIndex = 19;
            lblLastUpdated.Text = "最終更新: --:--:--";
            lblLastUpdated.TextAlign = ContentAlignment.MiddleLeft;
            //
            // lblUpdateFlash
            //
            lblUpdateFlash.Dock = DockStyle.Top;
            lblUpdateFlash.ForeColor = Color.ForestGreen;
            lblUpdateFlash.Location = new Point(0, 132);
            lblUpdateFlash.Name = "lblUpdateFlash";
            lblUpdateFlash.Padding = new Padding(8, 0, 0, 0);
            lblUpdateFlash.Size = new Size(150, 24);
            lblUpdateFlash.TabIndex = 20;
            lblUpdateFlash.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _top
            //
            _top.AutoSize = true;
            _top.Controls.Add(_radioGroup);
            _top.Controls.Add(_cbAuto);
            _top.Controls.Add(_btnCapture);
            _top.Controls.Add(_btnLinks);
            _top.Controls.Add(_lblCharacter);
            _top.Controls.Add(_cbCharacter);
            _top.Dock = DockStyle.Top;
            _top.Location = new Point(0, 0);
            _top.Name = "_top";
            _top.Padding = new Padding(8, 8, 8, 4);
            _top.Size = new Size(646, 53);
            _top.TabIndex = 2;
            //
            // _radioGroup
            //
            _radioGroup.AutoSize = true;
            _radioGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _radioGroup.Controls.Add(_lblCapture);
            _radioGroup.Controls.Add(_rbWgc);
            _radioGroup.Controls.Add(_rbGdi);
            _radioGroup.Location = new Point(8, 8);
            _radioGroup.Margin = new Padding(0);
            _radioGroup.Name = "_radioGroup";
            _radioGroup.Size = new Size(210, 35);
            _radioGroup.TabIndex = 0;
            _radioGroup.WrapContents = false;
            //
            // _lblCapture
            //
            _lblCapture.AutoSize = true;
            _lblCapture.Location = new Point(0, 6);
            _lblCapture.Margin = new Padding(0, 6, 2, 0);
            _lblCapture.Name = "_lblCapture";
            _lblCapture.Size = new Size(52, 25);
            _lblCapture.TabIndex = 0;
            _lblCapture.Text = "取得:";
            //
            // _rbWgc
            //
            _rbWgc.AutoSize = true;
            _rbWgc.Location = new Point(57, 3);
            _rbWgc.Name = "_rbWgc";
            _rbWgc.Size = new Size(77, 29);
            _rbWgc.TabIndex = 1;
            _rbWgc.Text = "WGC";
            //
            // _rbGdi
            //
            _rbGdi.AutoSize = true;
            _rbGdi.Location = new Point(140, 3);
            _rbGdi.Name = "_rbGdi";
            _rbGdi.Size = new Size(67, 29);
            _rbGdi.TabIndex = 2;
            _rbGdi.Text = "GDI";
            //
            // _cbAuto
            //
            _cbAuto.AutoSize = true;
            _cbAuto.Location = new Point(221, 11);
            _cbAuto.Name = "_cbAuto";
            _cbAuto.Size = new Size(110, 29);
            _cbAuto.TabIndex = 1;
            _cbAuto.Text = "自動監視";
            //
            // _btnCapture
            //
            _btnCapture.AutoSize = true;
            _btnCapture.Location = new Point(337, 11);
            _btnCapture.Name = "_btnCapture";
            _btnCapture.Size = new Size(123, 35);
            _btnCapture.TabIndex = 2;
            _btnCapture.Text = "手動キャプチャ";
            //
            // _btnLinks
            //
            _btnLinks.AutoSize = true;
            _btnLinks.Location = new Point(466, 11);
            _btnLinks.Name = "_btnLinks";
            _btnLinks.Size = new Size(98, 35);
            _btnLinks.TabIndex = 3;
            _btnLinks.Text = "リンク設定";
            //
            // _lblCharacter
            //
            _lblCharacter.AutoSize = true;
            _lblCharacter.Location = new Point(575, 14);
            _lblCharacter.Margin = new Padding(8, 6, 2, 0);
            _lblCharacter.Name = "_lblCharacter";
            _lblCharacter.Size = new Size(82, 25);
            _lblCharacter.TabIndex = 4;
            _lblCharacter.Text = "  枠キャラ:";
            //
            // _cbCharacter
            //
            _cbCharacter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cbCharacter.Location = new Point(667, 11);
            _cbCharacter.Margin = new Padding(8, 3, 0, 0);
            _cbCharacter.Name = "_cbCharacter";
            _cbCharacter.Size = new Size(150, 33);
            _cbCharacter.TabIndex = 5;
            //
            // _status
            //
            _status.Dock = DockStyle.Top;
            _status.Location = new Point(0, 53);
            _status.Name = "_status";
            _status.Padding = new Padding(8, 4, 8, 4);
            _status.Size = new Size(646, 26);
            _status.TabIndex = 1;
            _status.Text = "初期化中...";
            //
            // _outer
            //
            _outer.Dock = DockStyle.Fill;
            _outer.Location = new Point(0, 79);
            _outer.Name = "_outer";
            //
            // _outer.Panel1
            //
            _outer.Panel1.Controls.Add(_left);
            //
            // _outer.Panel2
            //
            _outer.Panel2.Controls.Add(_previewHeaderPanel);
            _outer.Size = new Size(646, 345);
            _outer.SplitterDistance = 247;
            _outer.TabIndex = 0;
            //
            // _left
            //
            _left.Dock = DockStyle.Fill;
            _left.Location = new Point(0, 0);
            _left.Name = "_left";
            _left.Orientation = Orientation.Horizontal;
            //
            // _left.Panel1
            //
            _left.Panel1.Controls.Add(_list);
            //
            // _left.Panel2
            //
            _left.Panel2.Controls.Add(_ocrHeaderPanel);
            _left.Size = new Size(247, 345);
            _left.SplitterDistance = 172;
            _left.TabIndex = 0;
            //
            // _list
            //
            _list.Columns.AddRange(new ColumnHeader[] { _colCardId, _colEn, _colJp, _colConf, _colRecog });
            _list.Dock = DockStyle.Fill;
            _list.FullRowSelect = true;
            _list.Location = new Point(0, 0);
            _list.Name = "_list";
            _list.Size = new Size(247, 172);
            _list.TabIndex = 0;
            _list.UseCompatibleStateImageBehavior = false;
            _list.View = View.Details;
            //
            // _colCardId
            //
            _colCardId.Text = "CardId";
            _colCardId.Width = 180;
            //
            // _colEn
            //
            _colEn.Text = "EN";
            _colEn.Width = 130;
            //
            // _colJp
            //
            _colJp.Text = "JP";
            _colJp.Width = 120;
            //
            // _colConf
            //
            _colConf.Text = "確信度";
            _colConf.Width = 60;
            //
            // _colRecog
            //
            _colRecog.Text = "認識器";
            _colRecog.Width = 70;
            //
            // _ocrHeaderPanel
            //
            _ocrHeaderPanel.Controls.Add(_ocrList);
            _ocrHeaderPanel.Controls.Add(_ocrHeaderLabel);
            _ocrHeaderPanel.Dock = DockStyle.Fill;
            _ocrHeaderPanel.Location = new Point(0, 0);
            _ocrHeaderPanel.Name = "_ocrHeaderPanel";
            _ocrHeaderPanel.Size = new Size(247, 169);
            _ocrHeaderPanel.TabIndex = 0;
            //
            // _ocrList
            //
            _ocrList.Columns.AddRange(new ColumnHeader[] { _colOcrText, _colOcrKind, _colOcrMatch, _colOcrDist });
            _ocrList.Dock = DockStyle.Fill;
            _ocrList.FullRowSelect = true;
            _ocrList.Location = new Point(0, 18);
            _ocrList.Name = "_ocrList";
            _ocrList.Size = new Size(247, 151);
            _ocrList.TabIndex = 0;
            _ocrList.UseCompatibleStateImageBehavior = false;
            _ocrList.View = View.Details;
            //
            // _colOcrText
            //
            _colOcrText.Text = "検出テキスト／候補";
            _colOcrText.Width = 240;
            //
            // _colOcrKind
            //
            _colOcrKind.Text = "種別";
            _colOcrKind.Width = 45;
            //
            // _colOcrMatch
            //
            _colOcrMatch.Text = "一致";
            _colOcrMatch.Width = 80;
            //
            // _colOcrDist
            //
            _colOcrDist.Text = "距離";
            _colOcrDist.Width = 90;
            //
            // _ocrHeaderLabel
            //
            _ocrHeaderLabel.BackColor = SystemColors.ControlDark;
            _ocrHeaderLabel.Dock = DockStyle.Top;
            _ocrHeaderLabel.ForeColor = SystemColors.ControlLightLight;
            _ocrHeaderLabel.Location = new Point(0, 0);
            _ocrHeaderLabel.Name = "_ocrHeaderLabel";
            _ocrHeaderLabel.Padding = new Padding(4, 0, 0, 0);
            _ocrHeaderLabel.Size = new Size(247, 18);
            _ocrHeaderLabel.TabIndex = 1;
            _ocrHeaderLabel.Text = "ショップ候補（レリック／ポーション）";
            _ocrHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _previewHeaderPanel
            //
            _previewHeaderPanel.Controls.Add(_capturePreview);
            _previewHeaderPanel.Controls.Add(_previewHeaderLabel);
            _previewHeaderPanel.Dock = DockStyle.Fill;
            _previewHeaderPanel.Location = new Point(0, 0);
            _previewHeaderPanel.Name = "_previewHeaderPanel";
            _previewHeaderPanel.Size = new Size(395, 345);
            _previewHeaderPanel.TabIndex = 0;
            //
            // _capturePreview
            //
            _capturePreview.BackColor = SystemColors.ControlDarkDark;
            _capturePreview.Dock = DockStyle.Fill;
            _capturePreview.Location = new Point(0, 18);
            _capturePreview.Name = "_capturePreview";
            _capturePreview.Size = new Size(395, 327);
            _capturePreview.SizeMode = PictureBoxSizeMode.Zoom;
            _capturePreview.TabIndex = 0;
            _capturePreview.TabStop = false;
            //
            // _previewHeaderLabel
            //
            _previewHeaderLabel.BackColor = SystemColors.ControlDark;
            _previewHeaderLabel.Dock = DockStyle.Top;
            _previewHeaderLabel.ForeColor = SystemColors.ControlLightLight;
            _previewHeaderLabel.Location = new Point(0, 0);
            _previewHeaderLabel.Name = "_previewHeaderLabel";
            _previewHeaderLabel.Padding = new Padding(4, 0, 0, 0);
            _previewHeaderLabel.Size = new Size(395, 18);
            _previewHeaderLabel.TabIndex = 1;
            _previewHeaderLabel.Text = "キャプチャ画像（縮小プレビュー）";
            _previewHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 520);
            Controls.Add(splitContainerOuter);
            Controls.Add(panelInfo);
            Name = "Form1";
            Text = "StS2 Deck Viewer";
            panelInfo.ResumeLayout(false);
            splitContainerOuter.Panel1.ResumeLayout(false);
            splitContainerOuter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerOuter).EndInit();
            splitContainerOuter.ResumeLayout(false);
            panelSideButtons.ResumeLayout(false);
            _top.ResumeLayout(false);
            _top.PerformLayout();
            _radioGroup.ResumeLayout(false);
            _radioGroup.PerformLayout();
            _outer.Panel1.ResumeLayout(false);
            _outer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_outer).EndInit();
            _outer.ResumeLayout(false);
            _left.Panel1.ResumeLayout(false);
            _left.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_left).EndInit();
            _left.ResumeLayout(false);
            _ocrHeaderPanel.ResumeLayout(false);
            _previewHeaderPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_capturePreview).EndInit();
            ResumeLayout(false);
        }

        private Button btnToggleAuto;
        private Button btnLang;
        private Label lblLastUpdated;
        private Label lblUpdateFlash;
        private Button btnOpen;
        private Label lblGroupFile;
        private Panel panelInfo;
        private Label lblInfo;
        private SplitContainer splitContainerOuter;
        private Panel panelSideButtons;
        private Button btnCombinedOverview;
        private Button btnHpHistory;
        private Button btnEncounterOverview;
        private Button btnCharacterOverview;
        private Label lblGroupOverview;
        private Label lblGroupOther;

        private FlowLayoutPanel _top;
        private FlowLayoutPanel _radioGroup;
        private Label _lblCapture;
        private RadioButton _rbWgc;
        private RadioButton _rbGdi;
        private CheckBox _cbAuto;
        private Button _btnCapture;
        private Button _btnLinks;
        private Label _lblCharacter;
        private ComboBox _cbCharacter;
        private Label _status;
        private SplitContainer _outer;
        private SplitContainer _left;
        private ListView _list;
        private ColumnHeader _colCardId;
        private ColumnHeader _colEn;
        private ColumnHeader _colJp;
        private ColumnHeader _colConf;
        private ColumnHeader _colRecog;
        private Panel _ocrHeaderPanel;
        private ListView _ocrList;
        private ColumnHeader _colOcrText;
        private ColumnHeader _colOcrKind;
        private ColumnHeader _colOcrMatch;
        private ColumnHeader _colOcrDist;
        private Label _ocrHeaderLabel;
        private Panel _previewHeaderPanel;
        private PictureBox _capturePreview;
        private Label _previewHeaderLabel;
    }
}
