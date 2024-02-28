namespace xFences
{
    partial class SpaceForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.shellView1 = new GongSolutions.Shell.ShellView();
            this.panel1 = new System.Windows.Forms.Panel();
            this.bnMenu = new System.Windows.Forms.Button();
            this.bnClose = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // shellView1
            // 
            this.shellView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.shellView1.Location = new System.Drawing.Point(3, 6);
            this.shellView1.Name = "shellView1";
            this.shellView1.ShowWebView = true;
            this.shellView1.Size = new System.Drawing.Size(794, 441);
            this.shellView1.StatusBar = null;
            this.shellView1.TabIndex = 0;
            this.shellView1.Text = "shellView1";
            this.shellView1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.shellView1_MouseDown);
            this.shellView1.MouseEnter += new System.EventHandler(this.SpaceForm_MouseEnter);
            this.shellView1.MouseLeave += new System.EventHandler(this.SpaceForm_MouseLeave);
            this.shellView1.MouseHover += new System.EventHandler(this.SpaceForm_MouseEnter);
            this.shellView1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.shellView1_MouseMove);
            this.shellView1.MouseUp += new System.Windows.Forms.MouseEventHandler(this.shellView1_MouseUp);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.WhiteSmoke;
            this.panel1.Controls.Add(this.bnMenu);
            this.panel1.Controls.Add(this.bnClose);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(800, 29);
            this.panel1.TabIndex = 1;
            // 
            // bnMenu
            // 
            this.bnMenu.FlatAppearance.BorderSize = 0;
            this.bnMenu.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.bnMenu.Image = global::xFences.Properties.Resources.icons8_menu_24;
            this.bnMenu.Location = new System.Drawing.Point(3, 2);
            this.bnMenu.Name = "bnMenu";
            this.bnMenu.Size = new System.Drawing.Size(24, 24);
            this.bnMenu.TabIndex = 0;
            this.bnMenu.UseVisualStyleBackColor = true;
            this.bnMenu.Click += new System.EventHandler(this.bnMenu_Click);
            // 
            // bnClose
            // 
            this.bnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bnClose.FlatAppearance.BorderSize = 0;
            this.bnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.bnClose.Image = global::xFences.Properties.Resources.icons8_close_24;
            this.bnClose.Location = new System.Drawing.Point(773, 3);
            this.bnClose.Name = "bnClose";
            this.bnClose.Size = new System.Drawing.Size(24, 24);
            this.bnClose.TabIndex = 0;
            this.bnClose.UseVisualStyleBackColor = true;
            this.bnClose.Click += new System.EventHandler(this.button1_Click);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Font = new System.Drawing.Font("Arial Rounded MT Bold", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(41, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(719, 23);
            this.label1.TabIndex = 1;
            this.label1.Text = "label1";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 50;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // SpaceForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.shellView1);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SpaceForm";
            this.Opacity = 0.45D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "SpaceForm";
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.SpaceForm_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.SpaceForm_DragEnter);
            this.MouseEnter += new System.EventHandler(this.SpaceForm_MouseEnter);
            this.MouseLeave += new System.EventHandler(this.SpaceForm_MouseLeave);
            this.MouseHover += new System.EventHandler(this.SpaceForm_MouseEnter);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.SpaceForm_MouseMove);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private GongSolutions.Shell.ShellView shellView1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button bnClose;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button bnMenu;
        private System.Windows.Forms.Timer timer1;
    }
}