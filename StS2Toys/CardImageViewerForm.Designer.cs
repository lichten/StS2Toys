namespace StS2Toys
{
    partial class CardImageViewerForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pictureBox.Image?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            pictureBox = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
            SuspendLayout();
            //
            // pictureBox
            //
            pictureBox.BackColor = SystemColors.ControlLight;
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.Name = "pictureBox";
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 0;
            pictureBox.TabStop = false;
            pictureBox.Paint += PictureBox_Paint;
            //
            // CardImageViewerForm
            //
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(300, 370);
            Controls.Add(pictureBox);
            MinimumSize = new Size(200, 240);
            Name = "CardImageViewerForm";
            Text = "画像ビューア";
            ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
            ResumeLayout(false);
        }

        private PictureBox pictureBox;
    }
}
