using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace YMixer
{
    internal static class Program
    {
        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "YMixer_SingleInstanceMutex", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("The application is already running.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FMain());
            }
        }
    }
}
