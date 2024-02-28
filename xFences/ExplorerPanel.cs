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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace xFences
{
    public partial class ExplorerPanel : UserControl
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        public View View { get { return listView1.View; } set { listView1.View = value; } }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_LARGEICON = 0x0;
        private string path = null;
        public string Folder
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
                LoadFilesAndFolders(path);
            }
        }
        public ExplorerPanel()
        {
            InitializeComponent();            
        }
        private void LoadFilesAndFolders(string path)
        {
            listView1.Items.Clear();
            if (path == null)
                return;
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            foreach (var folder in dirInfo.GetDirectories())
            {
                AddItemWithIcon(folder.FullName, true);
            }

            foreach (var file in dirInfo.GetFiles())
            {
                AddItemWithIcon(file.FullName, false);
            }
        }

        private void AddItemWithIcon(string path, bool isFolder)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            SHFILEINFO shfi_big = new SHFILEINFO();
            
            

            IntPtr resLittle = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_SMALLICON);
            IntPtr resBig = SHGetFileInfo(path, 0, ref shfi_big, (uint)Marshal.SizeOf(shfi_big), SHGFI_ICON | SHGFI_LARGEICON);

            if (resBig != IntPtr.Zero && resLittle != IntPtr.Zero)
            {
                //System.Drawing.Icon.FromHandle(shfi.hIcon); // Load the icon from handle
                //int index = listView1.SmallImageList.Images.Count; // Get next index
                //listView1.SmallImageList.Images.Add(System.Drawing.Icon.FromHandle(shfi.hIcon)); // Add icon to ImageList
                System.Drawing.Icon.FromHandle(shfi_big.hIcon); // Load the icon from handle
                int index = listView1.LargeImageList.Images.Count;
                 listView1.LargeImageList.Images.Add(System.Drawing.Icon.FromHandle(shfi_big.hIcon)); // Add icon to ImageList
                ListViewItem item = new ListViewItem(Path.GetFileName(path), index);
                listView1.Items.Add(item);
            }
        }
    }
}
