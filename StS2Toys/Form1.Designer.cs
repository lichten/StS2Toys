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
            panelTop = new Panel();
            lblUpdateFlash = new Label();
            lblLastUpdated = new Label();
            btnToggleAuto = new Button();
            panelFileControls = new Panel();
            txtFilePath = new TextBox();
            btnOpen = new Button();
            panelInfo = new Panel();
            lblInfo = new Label();
            splitContainerOuter = new SplitContainer();
            panelSideButtons = new Panel();
            btnImageViewer = new Button();
            btnCardDetail = new Button();
            btnDeckOverview = new Button();
            btnBlockOverview = new Button();
            btnDrawOverview = new Button();
            btnHpHistory = new Button();
            btnEncounterOverview = new Button();
            btnFilterBlock = new Button();
            splitContainer = new SplitContainer();
            listViewDeck = new ListView();
            colCardName = new ColumnHeader();
            colCardNameJa = new ColumnHeader();
            colCardCost = new ColumnHeader();
            colCardType = new ColumnHeader();
            colCardEnchant = new ColumnHeader();
            colCardCount = new ColumnHeader();
            lblDeckTitle = new Label();
            listViewRelics = new ListView();
            colRelicName = new ColumnHeader();
            colRelicNameJa = new ColumnHeader();
            lblRelicsTitle = new Label();
            panelTop.SuspendLayout();
            panelFileControls.SuspendLayout();
            panelInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerOuter).BeginInit();
            splitContainerOuter.Panel1.SuspendLayout();
            splitContainerOuter.Panel2.SuspendLayout();
            splitContainerOuter.SuspendLayout();
            panelSideButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            SuspendLayout();
            //
            // panelTop
            //
            panelTop.Controls.Add(lblUpdateFlash);
            panelTop.Controls.Add(lblLastUpdated);
            panelTop.Controls.Add(btnToggleAuto);
            panelTop.Controls.Add(panelFileControls);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Padding = new Padding(8, 8, 8, 4);
            panelTop.Size = new Size(800, 44);
            panelTop.TabIndex = 2;
            //
            // lblUpdateFlash
            //
            lblUpdateFlash.Dock = DockStyle.Fill;
            lblUpdateFlash.ForeColor = Color.ForestGreen;
            lblUpdateFlash.Name = "lblUpdateFlash";
            lblUpdateFlash.TabIndex = 0;
            lblUpdateFlash.TextAlign = ContentAlignment.MiddleCenter;
            //
            // lblLastUpdated
            //
            lblLastUpdated.Dock = DockStyle.Left;
            lblLastUpdated.Name = "lblLastUpdated";
            lblLastUpdated.Padding = new Padding(8, 0, 0, 0);
            lblLastUpdated.Size = new Size(155, 32);
            lblLastUpdated.TabIndex = 1;
            lblLastUpdated.Text = "最終更新: --:--:--";
            lblLastUpdated.TextAlign = ContentAlignment.MiddleLeft;
            //
            // btnToggleAuto
            //
            btnToggleAuto.Dock = DockStyle.Left;
            btnToggleAuto.Name = "btnToggleAuto";
            btnToggleAuto.Size = new Size(90, 32);
            btnToggleAuto.TabIndex = 2;
            btnToggleAuto.Text = "○ 自動更新";
            btnToggleAuto.Click += BtnToggleAuto_Click;
            //
            // panelFileControls
            //
            panelFileControls.Controls.Add(txtFilePath);
            panelFileControls.Controls.Add(btnOpen);
            panelFileControls.Dock = DockStyle.Right;
            panelFileControls.Name = "panelFileControls";
            panelFileControls.Padding = new Padding(4, 0, 0, 0);
            panelFileControls.Size = new Size(360, 32);
            panelFileControls.TabIndex = 3;
            //
            // txtFilePath
            //
            txtFilePath.Dock = DockStyle.Fill;
            txtFilePath.Name = "txtFilePath";
            txtFilePath.ReadOnly = true;
            txtFilePath.TabIndex = 0;
            //
            // btnOpen
            //
            btnOpen.Dock = DockStyle.Right;
            btnOpen.Name = "btnOpen";
            btnOpen.Size = new Size(110, 32);
            btnOpen.TabIndex = 1;
            btnOpen.Text = "ファイルを開く";
            btnOpen.Click += BtnOpen_Click;
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
            lblInfo.Name = "lblInfo";
            lblInfo.TabIndex = 0;
            lblInfo.Text = "ファイルを開くと、ランの情報を表示します。";
            //
            // splitContainerOuter
            //
            splitContainerOuter.Dock = DockStyle.Fill;
            splitContainerOuter.FixedPanel = FixedPanel.Panel1;
            splitContainerOuter.Location = new Point(0, 96);
            splitContainerOuter.Name = "splitContainerOuter";
            splitContainerOuter.Orientation = Orientation.Vertical;
            splitContainerOuter.Panel1.Controls.Add(panelSideButtons);
            splitContainerOuter.Panel2.Controls.Add(splitContainer);
            splitContainerOuter.Panel1MinSize = 60;
            splitContainerOuter.Panel2MinSize = 200;
            splitContainerOuter.Size = new Size(800, 424);
            splitContainerOuter.SplitterDistance = 150;
            splitContainerOuter.TabIndex = 0;
            //
            // panelSideButtons — buttons added bottom-first so top-docked controls appear in order
            //
            panelSideButtons.Controls.Add(btnFilterBlock);
            panelSideButtons.Controls.Add(btnEncounterOverview);
            panelSideButtons.Controls.Add(btnHpHistory);
            panelSideButtons.Controls.Add(btnDrawOverview);
            panelSideButtons.Controls.Add(btnBlockOverview);
            panelSideButtons.Controls.Add(btnDeckOverview);
            panelSideButtons.Controls.Add(btnCardDetail);
            panelSideButtons.Controls.Add(btnImageViewer);
            panelSideButtons.Dock = DockStyle.Fill;
            panelSideButtons.Name = "panelSideButtons";
            panelSideButtons.TabIndex = 0;
            //
            // btnImageViewer
            //
            btnImageViewer.Dock = DockStyle.Top;
            btnImageViewer.Height = 30;
            btnImageViewer.Name = "btnImageViewer";
            btnImageViewer.TabIndex = 0;
            btnImageViewer.Text = "○ 画像ビューア";
            btnImageViewer.Click += BtnImageViewer_Click;
            //
            // btnCardDetail
            //
            btnCardDetail.Dock = DockStyle.Top;
            btnCardDetail.Height = 30;
            btnCardDetail.Name = "btnCardDetail";
            btnCardDetail.TabIndex = 1;
            btnCardDetail.Text = "○ カード詳細";
            btnCardDetail.Click += BtnCardDetail_Click;
            //
            // btnDeckOverview
            //
            btnDeckOverview.Dock = DockStyle.Top;
            btnDeckOverview.Height = 30;
            btnDeckOverview.Name = "btnDeckOverview";
            btnDeckOverview.TabIndex = 2;
            btnDeckOverview.Text = "○ デッキ概観";
            btnDeckOverview.Click += BtnDeckOverview_Click;
            //
            // btnBlockOverview
            //
            btnBlockOverview.Dock = DockStyle.Top;
            btnBlockOverview.Height = 30;
            btnBlockOverview.Name = "btnBlockOverview";
            btnBlockOverview.TabIndex = 3;
            btnBlockOverview.Text = "○ ブロック関連概観";
            btnBlockOverview.Click += BtnBlockOverview_Click;
            //
            // btnDrawOverview
            //
            btnDrawOverview.Dock = DockStyle.Top;
            btnDrawOverview.Height = 30;
            btnDrawOverview.Name = "btnDrawOverview";
            btnDrawOverview.TabIndex = 4;
            btnDrawOverview.Text = "○ ドロー関連概観";
            btnDrawOverview.Click += BtnDrawOverview_Click;
            //
            // btnHpHistory
            //
            btnHpHistory.Dock = DockStyle.Top;
            btnHpHistory.Height = 30;
            btnHpHistory.Name = "btnHpHistory";
            btnHpHistory.TabIndex = 5;
            btnHpHistory.Text = "○ HP変動";
            btnHpHistory.Click += BtnHpHistory_Click;
            //
            // btnEncounterOverview
            //
            btnEncounterOverview.Dock = DockStyle.Top;
            btnEncounterOverview.Height = 30;
            btnEncounterOverview.Name = "btnEncounterOverview";
            btnEncounterOverview.TabIndex = 6;
            btnEncounterOverview.Text = "○ 敵情報";
            btnEncounterOverview.Click += BtnEncounterOverview_Click;
            //
            // btnFilterBlock
            //
            btnFilterBlock.Dock = DockStyle.Top;
            btnFilterBlock.Height = 30;
            btnFilterBlock.Name = "btnFilterBlock";
            btnFilterBlock.TabIndex = 7;
            btnFilterBlock.Text = "○ ブロック関連絞り込み";
            btnFilterBlock.Click += BtnFilterBlock_Click;
            //
            // splitContainer
            //
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(0, 0);
            splitContainer.Name = "splitContainer";
            //
            // splitContainer.Panel1
            //
            splitContainer.Panel1.Controls.Add(listViewDeck);
            splitContainer.Panel1.Controls.Add(lblDeckTitle);
            //
            // splitContainer.Panel2
            //
            splitContainer.Panel2.Controls.Add(listViewRelics);
            splitContainer.Panel2.Controls.Add(lblRelicsTitle);
            splitContainer.Size = new Size(646, 424);
            splitContainer.SplitterDistance = 515;
            splitContainer.TabIndex = 0;
            //
            // listViewDeck
            //
            listViewDeck.Columns.AddRange(new ColumnHeader[] { colCardName, colCardNameJa, colCardCost, colCardType, colCardEnchant, colCardCount });
            listViewDeck.Dock = DockStyle.Fill;
            listViewDeck.FullRowSelect = true;
            listViewDeck.GridLines = true;
            listViewDeck.Location = new Point(0, 26);
            listViewDeck.Name = "listViewDeck";
            listViewDeck.TabIndex = 0;
            listViewDeck.UseCompatibleStateImageBehavior = false;
            listViewDeck.View = View.Details;
            listViewDeck.ColumnClick += ListViewDeck_ColumnClick;
            listViewDeck.SelectedIndexChanged += ListViewDeck_SelectedIndexChanged;
            //
            // colCardName
            //
            colCardName.Text = "カード名 (EN)";
            colCardName.Width = 180;
            //
            // colCardNameJa
            //
            colCardNameJa.Text = "カード名 (JP)";
            colCardNameJa.Width = 160;
            //
            // colCardCost
            //
            colCardCost.Text = "コスト";
            colCardCost.TextAlign = HorizontalAlignment.Center;
            colCardCost.Width = 52;
            //
            // colCardType
            //
            colCardType.Text = "種別";
            colCardType.Width = 65;
            //
            // colCardEnchant
            //
            colCardEnchant.Text = "エンチャント";
            colCardEnchant.Width = 110;
            //
            // colCardCount
            //
            colCardCount.Text = "枚数";
            colCardCount.TextAlign = HorizontalAlignment.Right;
            colCardCount.Width = 55;
            //
            // lblDeckTitle
            //
            lblDeckTitle.Dock = DockStyle.Top;
            lblDeckTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblDeckTitle.Name = "lblDeckTitle";
            lblDeckTitle.Padding = new Padding(4, 4, 0, 0);
            lblDeckTitle.Size = new Size(515, 26);
            lblDeckTitle.TabIndex = 1;
            lblDeckTitle.Text = "デッキ";
            //
            // listViewRelics
            //
            listViewRelics.Columns.AddRange(new ColumnHeader[] { colRelicName, colRelicNameJa });
            listViewRelics.Dock = DockStyle.Fill;
            listViewRelics.FullRowSelect = true;
            listViewRelics.GridLines = true;
            listViewRelics.Name = "listViewRelics";
            listViewRelics.TabIndex = 0;
            listViewRelics.UseCompatibleStateImageBehavior = false;
            listViewRelics.View = View.Details;
            listViewRelics.SelectedIndexChanged += ListViewRelics_SelectedIndexChanged;
            //
            // colRelicName
            //
            colRelicName.Text = "レリック名 (EN)";
            colRelicName.Width = 160;
            //
            // colRelicNameJa
            //
            colRelicNameJa.Text = "レリック名 (JP)";
            colRelicNameJa.Width = 140;
            //
            // lblRelicsTitle
            //
            lblRelicsTitle.Dock = DockStyle.Top;
            lblRelicsTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblRelicsTitle.Name = "lblRelicsTitle";
            lblRelicsTitle.Padding = new Padding(4, 4, 0, 0);
            lblRelicsTitle.Size = new Size(127, 26);
            lblRelicsTitle.TabIndex = 1;
            lblRelicsTitle.Text = "レリック";
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 520);
            Controls.Add(splitContainerOuter);
            Controls.Add(panelInfo);
            Controls.Add(panelTop);
            Name = "Form1";
            Text = "StS2 Deck Viewer";
            panelTop.ResumeLayout(false);
            panelFileControls.ResumeLayout(false);
            panelFileControls.PerformLayout();
            panelInfo.ResumeLayout(false);
            panelSideButtons.ResumeLayout(false);
            splitContainerOuter.Panel1.ResumeLayout(false);
            splitContainerOuter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerOuter).EndInit();
            splitContainerOuter.ResumeLayout(false);
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ResumeLayout(false);
        }

        private Panel panelTop;
        private Panel panelFileControls;
        private Button btnToggleAuto;
        private Label lblLastUpdated;
        private Label lblUpdateFlash;
        private Button btnOpen;
        private TextBox txtFilePath;
        private Panel panelInfo;
        private Label lblInfo;
        private SplitContainer splitContainerOuter;
        private Panel panelSideButtons;
        private Button btnImageViewer;
        private Button btnCardDetail;
        private Button btnDeckOverview;
        private Button btnBlockOverview;
        private Button btnDrawOverview;
        private Button btnHpHistory;
        private Button btnEncounterOverview;
        private Button btnFilterBlock;
        private SplitContainer splitContainer;
        private Label lblDeckTitle;
        private ListView listViewDeck;
        private ColumnHeader colCardName;
        private ColumnHeader colCardNameJa;
        private ColumnHeader colCardCost;
        private ColumnHeader colCardType;
        private ColumnHeader colCardEnchant;
        private ColumnHeader colCardCount;
        private Label lblRelicsTitle;
        private ListView listViewRelics;
        private ColumnHeader colRelicName;
        private ColumnHeader colRelicNameJa;
    }
}
