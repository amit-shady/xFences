using ExtremeNiosConsole;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace xFences
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        [STAThread]
        static void Main()
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!SingleInstance())
            {
                MessageBox.Show("An instance of the application is already running.", Application.ProductName + " Running", MessageBoxButtons.OK);
                return;
            }
            Application.Run(new ParentForm());
        }

        static Mutex mutex;
        public static bool SingleInstance()
        {
            bool isNewInstance;
            mutex = new Mutex(true, "xFencesSingleton", out isNewInstance);
            return isNewInstance;
        }
    }
}
