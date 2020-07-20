using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeviceBatchGenerics.Support.ExtendedTreeView;
using DeviceBatchGenerics.Support;
using DeviceBatchGenerics.Support.Bases;
using DeviceBatchGenerics.Support.OriginSupport;
using DeviceBatchGenerics.ViewModels.EntityVMs;
using DeviceBatchGenerics;
using EFDeviceBatchCodeFirst;
using AutoLJV.Instrument_Control;
using System.Windows.Input;
using DeviceBatchWPF.ViewModels;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace AutoLJV
{
    public class MainWindowViewModel : NotifyUIBase
    {
        public MainWindowViewModel()
        {
        }
        #region Members
        ObservableCollection<DeviceBatchVM> _deviceBatchVMsObservableCollection = new ObservableCollection<DeviceBatchVM>();
        DeviceBatchVM _selectedDeviceBatchVM;
        List<Item> _devBatchPaths;
        Item _selectedItem;
        DeviceBatchContext ctx;
       
        #endregion
        #region Properties        
        public ObservableCollection<DeviceBatchVM> DeviceBatchVMs
        {
            get
            {
                return _deviceBatchVMsObservableCollection;
            }
            set
            {
                _deviceBatchVMsObservableCollection = value;
                OnPropertyChanged();
            }
        }
        public DeviceBatchVM SelectedDeviceBatchVM
        {
            get { return _selectedDeviceBatchVM; }
            set
            {
                _selectedDeviceBatchVM = value;
                OnPropertyChanged();
            }
        }
        public List<Item> DevBatchPaths
        {
            get { return _devBatchPaths; }
            set
            {
                _devBatchPaths = value;
                OnPropertyChanged();
            }
        }
        public Item SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
                Debug.WriteLine("SelectedItem changed to " + SelectedItem.Path);
                if (_selectedItem != null)
                    UpdateDeviceBatches();
            }
        }
        #endregion       
        public static Task<MainWindowViewModel> CreateAsync()
        {
            var ret = new MainWindowViewModel();
            return ret.InitializeAsync();
        }
        private async Task<MainWindowViewModel> InitializeAsync()
        {
            return await Task.Run(async () =>
            {
                Trace.AutoFlush = true;
                
                var sysname = System.Environment.MachineName;
                if (sysname == "JAKE-PC")
                    ConfigurationManager.AppSettings.Set("BatchTestSystem", "BTS1");
                else if (sysname == "DESKTOP-GQQ0M3J")
                    ConfigurationManager.AppSettings.Set("BatchTestSystem", "BTS2");
                Debug.WriteLine("sysname: " + sysname);
                await InstrumentService.CreateCoordinatorAsync();
                await OriginService.CreateControllerAsync();
                await DataProcessingService.CreateLEDCalculatorAsync();
                if (Debugger.IsAttached)
                {
                    Properties.Settings.Default.Reset();
                }
                var dBConnectionManager = new DBConnectionManager();
                ctx = new DeviceBatchContext();
                Task[] initTasks = new Task[2];
                initTasks[0] = LoadDirectoryTreeView();
                initTasks[1] = UpdateIPAddress();
                await Task.WhenAll(initTasks);
                Trace.TraceInformation("Initialized MainWindowViewModel");
                return this;
            }).ConfigureAwait(false);
        }
        #region Methods
        private Task UpdateIPAddress()
        {
            return Task.Run(() =>
            {
                string command = string.Concat("IPv4Addr", GetLocalIPv4(NetworkInterfaceType.Ethernet));
                Debug.WriteLine("IPv4 command: " + command);
                InstrumentService.LJVScanCoordinator.TheRasPiController.SendPiString(command);
            });
        }
        private Task LoadDirectoryTreeView()
        {
            return Task.Run(() =>
            {
                var devBatchPathItemProvider = new ItemProvider();
                DevBatchPaths = devBatchPathItemProvider.GetItems(@"Z:\Data (LJV, Lifetime)\Device Batches");
                //Trace.WriteLine("Loaded Directory Treeview");
                SelectedItem = DevBatchPaths.Where(x => x.Name == DateTime.Now.Year.ToString()).First();
            });
        }      
        private Task UpdateDeviceBatches()
        {
            return Task.Run(() =>
            {
                var q = (from a in ctx.DeviceBatches
                         select a)
                         .OrderByDescending(db => db.FabDate)
                         .Where(db => db.FilePath.Contains(SelectedItem.Path))
                         .ToList();
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    DeviceBatchVMs = new ObservableCollection<DeviceBatchVM>();
                    foreach (DeviceBatch devbatch in q)
                    {
                        DeviceBatchVMs.Add(new DeviceBatchVM(devbatch, false));
                    }
                });               
            });
        }
        public void HandleNewDeviceBatch(object sender, DeviceBatchCreatedEventArgs e)
        {
            Debug.WriteLine("new batchId: " + e.batchId);
            var newDevBatch = ctx.DeviceBatches.Where(x => x.DeviceBatchId == e.batchId).FirstOrDefault();
            if (newDevBatch != null)
            {
                Debug.WriteLine("devbatch not null");
                DeviceBatchVM batchVM = new DeviceBatchVM(newDevBatch);
            }
            UpdateDeviceBatches();
        }
        private string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }
        #endregion
    }
}
