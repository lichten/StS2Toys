namespace StS2Toys
{
    partial class EncounterOverviewForm
    {
        private System.ComponentModel.IContainer components = null;

        private void InitializeComponent()
        {
            _scrollPanel = new Panel();
            _pictureBox  = new PictureBox();

            _scrollPanel.SuspendLayout();
            SuspendLayout();

            // ---- _scrollPanel ----
            _scrollPanel.Dock       = DockStyle.Fill;
            _scrollPanel.AutoScroll = true;
            _scrollPanel.Controls.Add(_pictureBox);

            // ---- _pictureBox ----
            _pictureBox.Location = new Point(0, 0);
            _pictureBox.SizeMode = PictureBoxSizeMode.Normal;

            // ---- Form ----
            AutoScaleMode   = AutoScaleMode.Font;
            ClientSize      = new Size(380, 420);
            MinimumSize     = new Size(280, 180);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.Manual;
            ShowInTaskbar   = false;
            MaximizeBox     = true;
            MinimizeBox     = false;
            Text            = "エンカウンター情報";
            Controls.Add(_scrollPanel);

            _scrollPanel.ResumeLayout();
            ResumeLayout();
        }

        private Panel      _scrollPanel;
        private PictureBox _pictureBox;
    }
}
