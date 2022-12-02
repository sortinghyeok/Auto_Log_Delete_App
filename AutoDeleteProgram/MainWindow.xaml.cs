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
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainVM(this);
            Closing += WindowClosing;
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            //Test Code
            File.AppendAllText(@"type your path", "Log Start"+Environment.NewLine);
            while (((MainVM)(DataContext)).DeleteByDaysWorker.IsBusy)
            {
                System.Windows.Forms.Application.DoEvents();//내 시간..
            }
            File.AppendAllText(@"type your path", "Background Finished, Program Exit" + Environment.NewLine);
        
        }
    }
}
