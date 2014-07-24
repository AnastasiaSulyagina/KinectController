using System;
using System.Collections.Generic;
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
using System.Threading;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            var mvvm = new GameMode.MVVM();
            this.DataContext = mvvm;
            InitializeComponent();
        }

        private void modeChooser_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }

        private void conectionButton_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
