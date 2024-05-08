using ExtensionManagerLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

namespace SBExtensionManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Visibility = Visibility.Hidden;

            try
            {
                string tempPath = System.IO.Path.GetTempPath();
                string[] strings = Directory.GetFiles(tempPath, "*.sbprime");
                foreach (string file in strings)
                {
                    File.Delete(file);
                }
            }
            catch
            {

            }

            EMWindow windowEM = new EMWindow();
            windowEM.ShowDialog();

            Close();
        }
    }
}
