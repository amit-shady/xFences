using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GongSolutions.Shell;
using static ExtremeControls.Controls.ExtremeBarPlot;

namespace xFences
{
    public partial class ParentForm : Form
    {
        Settings settings;
        List<SpaceForm> spaceForms = new List<SpaceForm>();
        #region InterOP Code
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "SendMessageA", SetLastError = true)]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] 
        static extern IntPtr GetWindow(IntPtr hWnd, GetWindow_Cmd uCmd);
        enum GetWindow_Cmd : uint
        {
            GW_HWNDFIRST = 0,
            GW_HWNDLAST = 1,
            GW_HWNDNEXT = 2,
            GW_HWNDPREV = 3,
            GW_OWNER = 4,
            GW_CHILD = 5,
            GW_ENABLEDPOPUP = 6
        }
        
        const int WM_COMMAND = 0x111;
        const int MIN_ALL = 419;
        const int MIN_ALL_UNDO = 416;
        #endregion

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            SendToBack();
        }

        public void ToggleDesktopIcons()
        {
            IntPtr hWnd = FindWindow("Progman", null);
            var toggleDesktopCommand = new IntPtr(0x7402);            
            if (hWnd != IntPtr.Zero)
            {
                SendMessage(hWnd, WM_COMMAND, toggleDesktopCommand, IntPtr.Zero);
            }  
        }
        private string CurrentFolder { get; set; }
        public ParentForm()
        {
            InitializeComponent();
            MakeAllwaysOnBack();
            Visible = false;
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path = Path.Combine(path, Application.ProductName);
            Directory.CreateDirectory(path);
            Directory.SetCurrentDirectory(path);            

            settings = Settings.Load();
            foreach (var formSettings in settings.SpaceFormSettings)
            {
                AddSpaceForm(formSettings);
            }
            /*
            var folders = Directory.GetDirectories(Directory.GetCurrentDirectory());
            foreach (var folder in folders)
            {
                AddSpaceForm(folder);
            }*/            
        }

        private void AddSpaceForm(SpaceFormSettings setting)
        {            
            SpaceForm s = setting.GetSpaceForm();
            spaceForms.Add(s);            
            s.FormClosed += SpaceForm_FormClosed;
            s.Show();
        }

        private void AddSpaceForm(string folder = null)
        {
            if (folder == null)
            {
                int i = 1;
                while (Directory.Exists(i.ToString()))
                    i++;
                folder = i.ToString();
            }
            SpaceForm s = new SpaceForm();
            spaceForms.Add(s);
            s.SpaceFolder = folder;
            s.FormClosed += SpaceForm_FormClosed;
            s.Show();
        }

        private void SpaceForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            spaceForms.Remove(sender as SpaceForm);
        }

        private void MakeAllwaysOnBack()
        {            
            this.Load += (s, e) => SendToBack();
            this.VisibleChanged += (s, e) => SendToBack();
            this.Shown += (s, e) => { SendToBack(); Hide(); };
            this.SizeChanged += (s, e) => { SendToBack(); };            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var files = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            var path = @"H:\ComfyUI\run.bat";
            var shellItem = new ShellItem(path);// (Environment.SpecialFolder.Desktop));
            var shellMenu = new ShellContextMenu(shellItem);
            Point screenLocation = MousePosition;//this.PointToScreen(location);
            shellMenu.ShowContextMenu(this, screenLocation);
        }

        private void addSpaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddSpaceForm();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            settings.SpaceFormSettings.Clear();
            settings.SpaceFormSettings = spaceForms.Select(x => new SpaceFormSettings(x)).ToList();
            settings.Save();
        }
    }
}
