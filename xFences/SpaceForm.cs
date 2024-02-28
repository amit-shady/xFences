using GongSolutions.Shell;
using GongSolutions.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace xFences
{
    public partial class SpaceForm : Form
    {
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        private const int WM_NCHITTEST = 0x84;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private void MakeAllwaysOnBack()
        {
            this.Load += (s, e) => { SendToBack(); CreateFolder(); };
            this.VisibleChanged += (s, e) => SendToBack();
            this.Shown += (s, e) => SendToBack();
            this.SizeChanged += (s, e) => { SendToBack(); WindowState = FormWindowState.Normal; };
        }

        private void CreateFolder()
        {
            if (!Directory.Exists(SpaceFolder))
                Directory.CreateDirectory(SpaceFolder);
            if (SpaceFolder != null)
            {
                var path =Path.Combine(Directory.GetCurrentDirectory(), SpaceFolder);
                shellView1.CurrentFolder = new GongSolutions.Shell.ShellItem(path);
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            SendToBack();
        }

        public string SpaceFolder { get; set; }

        public SpaceForm()
        {
            InitializeComponent();
            MakeAllwaysOnBack();
            panel1.MouseDown += new MouseEventHandler(shellView1_MouseDown);
            panel1.MouseMove += new MouseEventHandler(shellView1_MouseMove);
            panel1.MouseUp += new MouseEventHandler(shellView1_MouseUp);
            label1.MouseDown += new MouseEventHandler(shellView1_MouseDown);
            label1.MouseMove += new MouseEventHandler(shellView1_MouseMove);
            label1.MouseUp += new MouseEventHandler(shellView1_MouseUp);
            label1.Text = Text;
        }

        private void SpaceForm_DragEnter(object sender, DragEventArgs e)
        {
            // Check if the data being dragged is a file
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy; // Show the copy cursor
            }
            else
            {
                e.Effect = DragDropEffects.None; // Show the no-drop cursor
            }
        }

        private void SpaceForm_DragDrop(object sender, DragEventArgs e)
        {
            // Retrieve the file names being dragged
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            
            // Process the files, for example, list them in a ListBox
            foreach (string file in files)
            {
                File.Copy(file, Path.Combine(SpaceFolder, Path.GetFileName(file)), true);
            }
        }

        private void shellView1_MouseDown(object sender, MouseEventArgs e)
        {
            dragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = this.Location;
        }

        private void shellView1_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        private void shellView1_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(diff));
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void SpaceForm_MouseEnter(object sender, EventArgs e)
        {
            Opacity+=0.01;
        }

        private void SpaceForm_MouseLeave(object sender, EventArgs e)
        {
            Opacity = 0.4;
        }
        protected override CreateParams CreateParams
        {
            get
            {
                var Params = base.CreateParams;
                Params.ExStyle |= WS_EX_TOOLWINDOW;
                return Params;
            }
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST)
            {
                Point cursor = PointToClient(Cursor.Position);

                if (cursor.X < 5 && cursor.Y < 5)
                {
                    m.Result = (IntPtr)HTTOPLEFT;
                }
                else if (cursor.X < 5 && cursor.Y > Height - 5)
                {
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                }
                else if (cursor.X > Width - 5 && cursor.Y > Height - 5)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                }
                else if (cursor.X > Width - 5 && cursor.Y < 5)
                {
                    m.Result = (IntPtr)HTTOPRIGHT;
                }
                else if (cursor.X < 5)
                {
                    m.Result = (IntPtr)HTLEFT;
                }
                else if (cursor.X > Width - 5)
                {
                    m.Result = (IntPtr)HTRIGHT;
                }
                else if (cursor.Y < 5)
                {
                    m.Result = (IntPtr)HTTOP;
                }
                else if (cursor.Y > Height - 5)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                }
            }
        }

        private void SpaceForm_MouseMove(object sender, MouseEventArgs e)
        {
            SpaceForm_MouseEnter(sender, e);
        }
        const int WM_COMMAND = 0x111;
        const int MIN_ALL = 419;
        const int MIN_ALL_UNDO = 416;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        private void bnMenu_Click(object sender, EventArgs e)
        {
            IntPtr hWnd = FindWindow("Progman", null);
            var toggleDesktopCommand = new IntPtr(0x7402);
            if (hWnd != IntPtr.Zero)
            {
                SendMessage(hWnd, WM_COMMAND, toggleDesktopCommand, IntPtr.Zero);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (Bounds.Contains(Cursor.Position))
            {
                Opacity += 0.2;
            }
            else
                Opacity = 0.4;
        }
    }
}
