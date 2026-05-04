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
            btnCardDetail = new Button();
            btnImageViewer = new Button();
            btnToggleAuto = new Button();
            panelFileControls = new Panel();
            txtFilePath = new TextBox();
            btnOpen = new Button();
            panelInfo = new Panel();
            lblInfo = new Label();
            splitContainer = new SplitContainer();
            listViewDeck = new ListView();
            colCardName = new ColumnHeader();
            colCardNameJa = new ColumnHeader();
            colCardType = new ColumnHeader();
            colCardCount = new ColumnHeader();
            lblDeckTitle = new Label();
            listViewRelics = new ListView();
            colRelicName = new ColumnHeader();
            colRelicNameJa = new ColumnHeader();
            lblRelicsTitle = new Label();
            panelTop.SuspendLayout();
            panelFileControls.SuspendLayout();
            panelInfo.SuspendLayout();
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
            panelTop.Controls.Add(btnCardDetail);
            panelTop.Controls.Add(btnImageViewer);
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
            lblUpdateFlash.Location = new Point(463, 8);
            lblUpdateFlash.Name = "lblUpdateFlash";
            lblUpdateFlash.Size = new Size(0, 32);
            lblUpdateFlash.TabIndex = 0;
            lblUpdateFlash.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblLastUpdated
            // 
            lblLastUpdated.Dock = DockStyle.Left;
            lblLastUpdated.Location = new Point(308, 8);
            lblLastUpdated.Name = "lblLastUpdated";
            lblLastUpdated.Padding = new Padding(8, 0, 0, 0);
            lblLastUpdated.Size = new Size(155, 32);
            lblLastUpdated.TabIndex = 1;
            lblLastUpdated.Text = "最終更新: --:--:--";
            lblLastUpdated.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnCardDetail
            // 
            btnCardDetail.Dock = DockStyle.Left;
            btnCardDetail.Location = new Point(208, 8);
            btnCardDetail.Name = "btnCardDetail";
            btnCardDetail.Size = new Size(100, 32);
            btnCardDetail.TabIndex = 4;
            btnCardDetail.Text = "○ カード詳細";
            btnCardDetail.Click += BtnCardDetail_Click;
            // 
            // btnImageViewer
            // 
            btnImageViewer.Dock = DockStyle.Left;
            btnImageViewer.Location = new Point(98, 8);
            btnImageViewer.Name = "btnImageViewer";
            btnImageViewer.Size = new Size(110, 32);
            btnImageViewer.TabIndex = 3;
            btnImageViewer.Text = "○ 画像ビューア";
            btnImageViewer.Click += BtnImageViewer_Click;
            // 
            // btnToggleAuto
            // 
            btnToggleAuto.Dock = DockStyle.Left;
            btnToggleAuto.Location = new Point(8, 8);
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
            panelFileControls.Location = new Point(432, 8);
            panelFileControls.Name = "panelFileControls";
            panelFileControls.Padding = new Padding(4, 0, 0, 0);
            panelFileControls.Size = new Size(360, 32);
            panelFileControls.TabIndex = 3;
            // 
            // txtFilePath
            // 
            txtFilePath.Dock = DockStyle.Fill;
            txtFilePath.Location = new Point(4, 0);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.ReadOnly = true;
            txtFilePath.Size = new Size(246, 31);
            txtFilePath.TabIndex = 0;
            // 
            // btnOpen
            // 
            btnOpen.Dock = DockStyle.Right;
            btnOpen.Location = new Point(250, 0);
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
            lblInfo.Location = new Point(10, 6);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(782, 42);
            lblInfo.TabIndex = 0;
            lblInfo.Text = "ファイルを開くと、ランの情報を表示します。";
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(0, 96);
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
            splitContainer.Size = new Size(800, 424);
            splitContainer.SplitterDistance = 645;
            splitContainer.TabIndex = 0;
            // 
            // listViewDeck
            // 
            listViewDeck.Columns.AddRange(new ColumnHeader[] { colCardName, colCardNameJa, colCardType, colCardCount });
            listViewDeck.Dock = DockStyle.Fill;
            listViewDeck.FullRowSelect = true;
            listViewDeck.GridLines = true;
            listViewDeck.Location = new Point(0, 26);
            listViewDeck.Name = "listViewDeck";
            listViewDeck.Size = new Size(645, 398);
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
            // colCardType
            // 
            colCardType.Text = "種別";
            colCardType.Width = 65;
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
            lblDeckTitle.Location = new Point(0, 0);
            lblDeckTitle.Name = "lblDeckTitle";
            lblDeckTitle.Padding = new Padding(4, 4, 0, 0);
            lblDeckTitle.Size = new Size(645, 26);
            lblDeckTitle.TabIndex = 1;
            lblDeckTitle.Text = "デッキ";
            // 
            // listViewRelics
            // 
            listViewRelics.Columns.AddRange(new ColumnHeader[] { colRelicName, colRelicNameJa });
            listViewRelics.Dock = DockStyle.Fill;
            listViewRelics.FullRowSelect = true;
            listViewRelics.GridLines = true;
            listViewRelics.Location = new Point(0, 26);
            listViewRelics.Name = "listViewRelics";
            listViewRelics.Size = new Size(151, 398);
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
            lblRelicsTitle.Location = new Point(0, 0);
            lblRelicsTitle.Name = "lblRelicsTitle";
            lblRelicsTitle.Padding = new Padding(4, 4, 0, 0);
            lblRelicsTitle.Size = new Size(151, 26);
            lblRelicsTitle.TabIndex = 1;
            lblRelicsTitle.Text = "レリック";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 520);
            Controls.Add(splitContainer);
            Controls.Add(panelInfo);
            Controls.Add(panelTop);
            Name = "Form1";
            Text = "StS2 Deck Viewer";
            panelTop.ResumeLayout(false);
            panelFileControls.ResumeLayout(false);
            panelFileControls.PerformLayout();
            panelInfo.ResumeLayout(false);
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ResumeLayout(false);
        }

        private Panel panelTop;
        private Panel panelFileControls;
        private Button btnCardDetail;
        private Button btnImageViewer;
        private Button btnToggleAuto;
        private Label lblLastUpdated;
        private Label lblUpdateFlash;
        private Button btnOpen;
        private TextBox txtFilePath;
        private Panel panelInfo;
        private Label lblInfo;
        private SplitContainer splitContainer;
        private Label lblDeckTitle;
        private ListView listViewDeck;
        private ColumnHeader colCardName;
        private ColumnHeader colCardNameJa;
        private ColumnHeader colCardType;
        private ColumnHeader colCardCount;
        private Label lblRelicsTitle;
        private ListView listViewRelics;
        private ColumnHeader colRelicName;
        private ColumnHeader colRelicNameJa;
    }
}
