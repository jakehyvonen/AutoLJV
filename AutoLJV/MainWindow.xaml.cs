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
using AutoLJV.ViewModels;
using DeviceBatchWPF.Windows;
using DeviceBatchWPF.ViewModels;
using AutoLJV.Instrument_Control;
using System.Windows.Forms;
using System.Configuration;

namespace AutoLJV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(() =>
            {
                System.Diagnostics.Debug.WriteLine("opening MainWindow");
            }).ContinueWith(async r =>
            {
                MWVM = await MainWindowViewModel.CreateAsync();
                DataContext = MWVM;
                string bts = ConfigurationManager.AppSettings.Get("BatchTestSystem");
                if (bts == "BTS2")
                {
                    SetupDFKCam();
                }
            }, scheduler);

        }
        MainWindowViewModel MWVM;
        private void openManualButton_Click(object sender, RoutedEventArgs e)
        {
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(() =>
            {
                System.Diagnostics.Debug.WriteLine("opening DeviceBatchScanWindow");
            }).ContinueWith(async r =>
            {
                SingleDeviceScanVM sdsvm = await SingleDeviceScanVM.ManualSweepCreateAsync();
                ManualSweepWindow window = new ManualSweepWindow(sdsvm);
                window.Show();
            }, scheduler);
        }
        private void openBatchBuilderButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new BatchBuilderWindow();
            window.Show();
            window.BBVM.DeviceBatchCreated += new EventHandler<DeviceBatchCreatedEventArgs>(MWVM.HandleNewDeviceBatch);
        }
        private void openBatchScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (MWVM.SelectedDeviceBatchVM.TheDeviceBatch != null)
            {
                var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
                Task.Factory.StartNew(() =>
                {
                    System.Diagnostics.Debug.WriteLine("opening DeviceBatchScanWindow for " + MWVM.SelectedDeviceBatchVM.TheDeviceBatch.Name);
                    //DeviceBatchScanVM dbsvm = new DeviceBatchScanVM(MWVM.SelectedDeviceBatchVM.TheDeviceBatch);
                }).ContinueWith(async r =>
                {
                    DeviceBatchScanVM dbsvm = await DeviceBatchScanVM.CreateAsync(MWVM.SelectedDeviceBatchVM.TheDeviceBatch);
                    DeviceBatchScanWindow window = new DeviceBatchScanWindow(dbsvm);
                    window.Show();
                }, scheduler);

            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Please select a Device Batch");
            }
        }
        //ICommands not working for reasons not worth knowing
        private void piInitializeButton_Click(object sender, RoutedEventArgs e)
        {
            InstrumentService.LJVScanCoordinator.TheRasPiController.PiInitializeExecute();
        }
        private void piIdleButton_Click(object sender, RoutedEventArgs e)
        {
            InstrumentService.LJVScanCoordinator.TheRasPiController.PiIdleExecute();
        }
        private void piSwapButton_Click(object sender, RoutedEventArgs e)
        {
            InstrumentService.LJVScanCoordinator.TheRasPiController.PiSwapExecute();
        }
        private void piPixelAButton_Click(object sender, RoutedEventArgs e)
        {
            InstrumentService.LJVScanCoordinator.TheRasPiController.PiPixelAExecute();
        }
        private void piPixelBButton_Click(object sender, RoutedEventArgs e)
        {
            InstrumentService.LJVScanCoordinator.TheRasPiController.PiPixelBExecute();
        }
        private void piPixelCButton_Click(object sender, RoutedEventArgs e)
        {
            InstrumentService.LJVScanCoordinator.TheRasPiController.PiPixelCExecute();
        }
        private void piPixelDButton_Click(object sender, RoutedEventArgs e)
        {
            InstrumentService.LJVScanCoordinator.TheRasPiController.PiPixelDExecute();
        }
        private void piCommandButton_Click(object sender, RoutedEventArgs e)
        {
            InstrumentService.LJVScanCoordinator.TheRasPiController.SendPiString(commandTxtBox.Text);
        }
      
        private void SetupDFKCam()
        {
            var TheImagingControl = new TIS.Imaging.ICImagingControl();
            // Let IC Imaging Control fill the complete form.
            TheImagingControl.Dock = DockStyle.Fill;
            // Allow scaling.
            TheImagingControl.LiveDisplayDefault = false;
            if (TheImagingControl.DeviceValid)
                TheImagingControl.LiveStop();
            TheImagingControl.ShowDeviceSettingsDialog();
            if (TheImagingControl.DeviceValid)
                TheImagingControl.LiveStart();
           
            InstrumentService.LJVScanCoordinator.TheImagingControl = TheImagingControl;
        }       
    }
}
