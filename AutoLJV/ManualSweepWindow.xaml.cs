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
using System.Windows.Shapes;
using AutoLJV.Instrument_Control;
using AutoLJV.ViewModels;
using AsyncCommands;
using System.Windows.Forms;
namespace AutoLJV
{
    /// <summary>
    /// Interaction logic for ManualSweepWindow.xaml
    /// </summary>
    public partial class ManualSweepWindow : Window
    {
        public ManualSweepWindow(SingleDeviceScanVM sdsvm)
        {
            InitializeComponent();
            _sdsvm = sdsvm;
            DataContext = _sdsvm;
        }
        SingleDeviceScanVM _sdsvm;
        private void ThaGrid_Loaded(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.Integration.WindowsFormsHost host = new System.Windows.Forms.Integration.WindowsFormsHost();
            host.Child = InstrumentService.LJVScanCoordinator.TheImagingControl;
            ThaGrid.Children.Add(host);
        }
    }
}
