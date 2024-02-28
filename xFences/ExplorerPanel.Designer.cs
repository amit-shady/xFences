namespace xFences
{
    partial class ExplorerPanel
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.smallImageList = new System.Windows.Forms.ImageList(this.components);
            this.LargeImageList = new System.Windows.Forms.ImageList(this.components);
            this.listView1 = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // smallImageList
            // 
            this.smallImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            this.smallImageList.ImageSize = new System.Drawing.Size(16, 16);
            this.smallImageList.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // LargeImageList
            // 
            this.LargeImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            this.LargeImageList.ImageSize = new System.Drawing.Size(16, 16);
            this.LargeImageList.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // listView1
            // 
            this.listView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listView1.HideSelection = false;
            this.listView1.LargeImageList = this.LargeImageList;
            this.listView1.Location = new System.Drawing.Point(0, 0);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(575, 344);
            this.listView1.SmallImageList = this.smallImageList;
            this.listView1.TabIndex = 0;
            this.listView1.UseCompatibleStateImageBehavior = false;
            // 
            // ExplorerPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.listView1);
            this.Name = "ExplorerPanel";
            this.Size = new System.Drawing.Size(575, 344);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ImageList smallImageList;
        private System.Windows.Forms.ImageList LargeImageList;
        private System.Windows.Forms.ListView listView1;
    }
}
