using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;

namespace AutoDeleteProgram
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Forms.NotifyIcon noti;
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainVM(this);
            Closing += WindowClosing;
            noti = new NotifyIcon();
            noti.Icon = new System.Drawing.Icon("../../../Asset/TempIcon.ico");
            noti.Visible = true;
            noti.DoubleClick += delegate (object sender, EventArgs eventArgs)
            {
                Show();
                WindowState = WindowState.Normal;
            };
            noti.ContextMenuStrip = SetMenuStrip(noti);
            noti.Text = "AutoDelete";
        }

        private ContextMenuStrip SetMenuStrip(NotifyIcon ni)
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem openItem = new ToolStripMenuItem("Open");
            openItem.Click += delegate (object click, EventArgs eventArgs)
            {
                Show();
                WindowState = WindowState.Normal;
            };
            menu.Items.Add(openItem);

            ToolStripMenuItem closeItem = new ToolStripMenuItem("Close");
            closeItem.Click += delegate (object click, EventArgs eventArgs)
            {
                System.Windows.Application.Current.Shutdown();
                ni.Dispose();
            };
            menu.Items.Add(closeItem);

            return menu;
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            //Test Code
            File.AppendAllText(@"C:\Users\이종혁\Pictures\Desktop\log.txt", "Log Start"+Environment.NewLine);
            while (((MainVM)DataContext).DeleteByDaysWorker.IsBusy)
            {
                System.Windows.Forms.Application.DoEvents();//내 시간..
            }
            File.AppendAllText(@"C:\Users\이종혁\Pictures\Desktop\log.txt", "Background Finished, Program Exit" + Environment.NewLine);       
        }
    }
}
