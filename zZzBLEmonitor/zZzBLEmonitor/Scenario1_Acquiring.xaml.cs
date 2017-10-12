using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Linq;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;


namespace zZzBLEmonitor
{
    public sealed partial class Scenario1_Acquiring : Page
    {
        private MainPage rootPage = MainPage.Current;
        private BreathingService BrService = new BreathingService();
        private byte position = 0;
        // BLE Services Collection
        private ObservableCollection<BLEAttributeDisplay> ServiceCollection = new ObservableCollection<BLEAttributeDisplay>();
        // BLE Characteristics Collection
        private ObservableCollection<BLEAttributeDisplay> CharacteristicCollection = new ObservableCollection<BLEAttributeDisplay>();
        // BLE Device
        private BluetoothLEDevice sensorTagBLE = null;
        // Selected GATT Service
        private GattDeviceService selectedService;
        // Selected GATT Characteristic
        private GattCharacteristic writeChar = null;
        private List<GattCharacteristic> selectedCharacteristic = new List<GattCharacteristic>();
        private bool IsValueChangedHandlerRegistered = false;
        // Graph
        private GraphClass graph = new GraphClass();
        // Data files names
        public string timeStamp = null;
        // New Data Enqueued Event
        ConcurrentQueue<thetaClass> dataQueue = new ConcurrentQueue<thetaClass>();
        ConcurrentQueue<thetaClass> plotQueue = new ConcurrentQueue<thetaClass>();
        public delegate void ElementEnqueued(object sender, EventArgs e);
        public event ElementEnqueued OnNewDataEnqueued;
        // New Points Added Event
        public delegate void PointsAdded(object sender, EventArgs e);
        public event PointsAdded OnNewPointsAdded;
        //public event 
        bool busy = false;//flag for when the writing function is busy
        Int32 counter = 0;
        Stopwatch stopwatch = new Stopwatch();
        //Timers
        // For Data Acquisition
        //public string folderName = null;// "Data Acquired";
        //public string fileName = null;// "data-" + selectedDeviceName + DateTime.Now.ToString("_yyyy-dd-MM_HHmmss") + ".temp";
        //public string timeStamp = null;
        public StorageFolder dataFolder = null;
        public StorageFile dataFile = null;
        DispatcherTimer timer = new DispatcherTimer();
        int timesTicked = 0;
        int sampling = 0;
        public string rawData = "";
        public string imuData = "";
        public int flag1 = 0;
        public int flag0 = 1;

        public Scenario1_Acquiring()
        {
            InitializeComponent();
        }

        ~Scenario1_Acquiring() { }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Deletes conected device
            sensorTagBLE = null;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            acquireButton.IsEnabled = false;
            if (rootPage.selectedDeviceId != null)
            {
                nameDeviceConnected.Text = $"Device: {rootPage.selectedDeviceName}";
                connectButton.IsEnabled = true;
            }
            else
            {
                nameDeviceConnected.Text = "No Device Selected";
                connectButton.IsEnabled = false;
            }
        }
        // -->> Search BLE Sensor: Click Event <<--
        //_________________________________________
        private async void searchDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(PairingPanel));
        }

        // >>> Timer
        async void timer_Tick(object sender, object e)
        {
            //TimerLog.Text = "Timer running";
            //if (flag0 == 0) return;
            //flag0 = 0;
            timesTicked++;
            if ((rawData.Length >= 42))//Is there enough data for next sample?
            {
                string writeData = rawData.Substring(6, 36);//Extracts first set of measurements
                rawData = rawData.Substring(42);//Eliminates read data from rawData
                byte[] dataByte = new byte[writeData.Length / 2];//Converts to bytes
                for (int i = 0; i < writeData.Length; i += 2)
                {
                    dataByte[i / 2] = Convert.ToByte(writeData.Substring(i, 2), 16);
                }
                // >>> Write over BLE
                DataWriter writer = new DataWriter();
                writer.WriteBytes(dataByte);
                await writeChar.WriteValueAsync(writer.DetachBuffer());
            }
            else
            {
                timer.Stop();
            }
        }
        /// <summary>
        /// TimerSetUp: Initializes the Timer for both the initial count and the length of the study if there's a study length set.
        /// </summary>
        /// <returns></returns>
        void TimerSetUp()
        {
            timer.Tick += timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 10);//Tick every 50ms
            Debug.WriteLine("timer.IsEnabled = " + timer.IsEnabled + "\n");
            Debug.WriteLine("Calling timer.Start()\n");
            if (!timer.IsEnabled)
            {
                timer.Start();
            }
            Debug.WriteLine("timer.IsEnabled = " + timer.IsEnabled + "\n");
        }

        // -->> Connect to sensor <<--
        // Looks for an specific service and characteristic
        //_________________________________________________
        private async void connectButton_Click(object sender, RoutedEventArgs e)
        {
            connectButton.IsEnabled = false;
            graph.Initialize(graphStackPanel,1);
            graph.Background(Colors.WhiteSmoke);
            graph.AddPlot(Colors.Red);
            graph.AddPlot(Colors.Blue);
            graph.AddPlot(Colors.LimeGreen);
            rootPage.ShowProgressRing(connectingProgressRing, true);
            if (IsValueChangedHandlerRegistered)
            {// Reconnecting
                acquireButton.IsEnabled = false;
                foreach(GattCharacteristic characteristic in selectedCharacteristic)
                {
                    characteristic.ValueChanged -= Characteristic_ValueChanged;
                }
                OnNewDataEnqueued -= OnNewDataEnqueuedFxn;
                IsValueChangedHandlerRegistered = false;
                acquireButton.Label = "Acquire";
                acquireButton.Icon = new FontIcon { Glyph = "\uE9D9" };
                selectedService = null;
                selectedCharacteristic = null;
            }
            if (rootPage.selectedDeviceId != null)
            {// A device has been selected
                try
                {
                    // Creates BLE device from the selected DeviceID
                    sensorTagBLE = await BluetoothLEDevice.FromIdAsync(rootPage.selectedDeviceId);
                }
                catch (Exception ex) when ((uint)ex.HResult == 0x800710df)
                {
                    // ERROR_DEVICE_NOT_AVAILABLE because the bluetooth radio is off
                }
                
                // Device found!
                if (sensorTagBLE != null)
                {
                    GattDeviceServicesResult servicesResult = null;
                    GattCharacteristicsResult characResult = null;
                    try // Try to get the service from the UUID Uncached
                    {
                        // Gets the UUID specific service directly from the device
                        servicesResult = await sensorTagBLE.GetGattServicesForUuidAsync(
                                            BrService.BREATHINGSERVICE_UUID, BluetoothCacheMode.Uncached);
                    }
                    catch (Exception ex)
                    {
                        rootPage.notifyFlyout(ex.Message, connectButton);
                    }

                    if (servicesResult.Status == GattCommunicationStatus.Success)
                    {
                        selectedService = servicesResult.Services.Single();
                        Debug.WriteLine($"Service found: {selectedService.Uuid}");
                        try {

                            // Gets the Breathing characteristic
                            characResult = await selectedService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                            //characResult = await selectedService.GetCharacteristicsForUuidAsync(
                                                //BrService.BREATHING_UUID, BluetoothCacheMode.Uncached);
                        }
                        catch (Exception ex)
                        {
                            rootPage.notifyFlyout(
                                "Can't read characteristics. Make sure service is not restricted. " + ex.Message, connectButton);
                        }
                        if (characResult.Status == GattCommunicationStatus.Success)
                        { // SUCCESS!!!
                            int number = characResult.Characteristics.Count();
                            rootPage.notifyFlyout(number.ToString() + " characteristics found", connectButton);
                            foreach(GattCharacteristic characteristic in characResult.Characteristics)
                            {
                                selectedCharacteristic.Add(characteristic);
                            }
                            nameDeviceConnected.Text = $"Connected to {rootPage.selectedDeviceName}";
                            connectButton.Label = "Reconnect";
                            acquireButton.IsEnabled = true;

                            rootPage.folderName = "Data Acquired";
                            rootPage.fileName = "data-" + rootPage.selectedDeviceName + DateTime.Now.ToString("_yyyy-dd-MM_HHmmss") + ".zZz";
                            rootPage.dataFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(rootPage.folderName, CreationCollisionOption.OpenIfExists);
                            rootPage.dataFile = await rootPage.dataFolder.CreateFileAsync(rootPage.fileName, CreationCollisionOption.ReplaceExisting);

                            // >>> Data to read
                            //var uri = new System.Uri("ms-appx:///imu_Supine_n31.txt");
                            //fileTextBlock.Text = "ms-appx:///imu_Supine_n31.txt";
                            //var uri = new System.Uri("ms-appx:///imu_Supine_n48.txt");
                            //fileTextBlock.Text = "ms-appx:///imu_Supine_n48.txt";
                            //var uri = new System.Uri("ms-appx:///imu_RightSide_n51.txt");
                            //fileTextBlock.Text = "ms-appx:///imu_RightSide_n51.txt";
                            //var uri = new System.Uri("ms-appx:///imu_Supine_n61.txt");
                            //fileTextBlock.Text = "ms-appx:///imu_Supine_n61.txt";
                            var uri = new System.Uri("ms-appx:///imu_RightSide_n63.txt");

                            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                            rawData = await FileIO.ReadTextAsync(file);//Reads file
                            int index = rawData.IndexOf("0D0A00");//Looks for the first end of transmission characters
                            rawData = rawData.Substring(index);//Eliminates everything before first en of transmission
                        }
                        else
                        {
                            rootPage.notifyFlyout(
                                "Connection failed. Characteristic " + characResult.Status.ToString(), connectButton);
                        }
                    }
                    else
                    {
                        // Failed because device...
                        rootPage.notifyFlyout(
                            "Connection failed: " + servicesResult.Status.ToString(), connectButton);
                    }
                }
                else
                { // Device not found
                    rootPage.notifyFlyout(
                        "Connection failed. Check that the device is on.", connectButton);
                }
            }
            rootPage.ShowProgressRing(connectingProgressRing, false);
            connectButton.IsEnabled = true;
        }

        // -->> Aquire data <<--
        // Subscribes to the notifications for the specefied characteristic
        // thus enabling the data acquisition
        //_________________________________________________________________
        private async void acquireButton_Click(object sender, RoutedEventArgs e)
        {
            acquireButton.IsEnabled = false;
            if (!IsValueChangedHandlerRegistered)
            {// Not registered to notifications
                try
                {

                    GattCharacteristic charSubscribe = null;
                    // Looks for the Breathing Characteristic
                    foreach(GattCharacteristic characteristic in selectedCharacteristic)
                    {
                        if (characteristic.Uuid.Equals(BrService.BREATHING_UUID))
                        {
                            charSubscribe = characteristic;
                        }else if (characteristic.Uuid.Equals(BrService.IMUWRITE_UUID))
                        {
                            writeChar = characteristic;
                        }
                    }

                    GattCommunicationStatus result =
                    await charSubscribe.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    if (result == GattCommunicationStatus.Success)
                    {   
                        charSubscribe.ValueChanged += Characteristic_ValueChanged;
                        OnNewDataEnqueued += new ElementEnqueued(OnNewDataEnqueuedFxn);
                        OnNewPointsAdded += new PointsAdded(OnNewPointsAddedFxn);
                        IsValueChangedHandlerRegistered = true;
                        acquireButton.Label = "Stop";
                        acquireButton.Icon = new SymbolIcon(Symbol.Stop);
                        stopwatch.Start();
                        TimerSetUp();
                    }
                    else
                    {
                        rootPage.notifyFlyout("Error acquiring! " + result.ToString(), acquireButton);
                    }
                }
                catch (Exception ex) //(UnauthorizedAccessException ex)
                {
                    rootPage.notifyFlyout("Can't subscribe to notifications: " + ex.Message, acquireButton);
                }

            }
            else
            {// Unregister for notifications
                foreach (GattCharacteristic characteristic in selectedCharacteristic)
                {
                    if (characteristic.Uuid.Equals(BrService.BREATHING_UUID))
                    {
                        characteristic.ValueChanged -= Characteristic_ValueChanged;
                    }
                }
                if (timer.IsEnabled)
                {
                    timer.Stop();
                }
                OnNewDataEnqueued -= OnNewDataEnqueuedFxn;
                OnNewPointsAdded -= OnNewPointsAddedFxn;
                IsValueChangedHandlerRegistered = false;
                acquireButton.Label = "Acquire";
                acquireButton.Icon = new FontIcon { Glyph = "\uE9D9" };
            }
            acquireButton.IsEnabled = true;

            #region //Code for direct reading
            /*GattReadResult result = null;
            try
            {
                result = await selectedCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            }
            catch (Exception ex)
            {
                notifyFlyout("Acquiring failed! " + ex.Message);
            }
            
            if (result.Status == GattCommunicationStatus.Success)
            {
                byte[] data;
                CryptographicBuffer.CopyToByteArray(result.Value, out data);
                dataTextBlock.Text = BitConverter.ToInt32(data, 0).ToString();
            }
            else
            {
                notifyFlyout("Acquiring failed");
            }*/
            #endregion
        }
        //New data is available at the sensor.
        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // BT_Code: An Indicate or Notify reported that the value has changed.
            byte[] newValue;//Stores the byte arraw
            //Reads the BLE buffer and stores its content into the newValue array
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out newValue);
            // Breathing monitoring notification
            if (sender.Uuid.Equals(BrService.BREATHING_UUID))
            {// Notification from the IMU
                double[] newData = new double[3];//Stores the IMU values
                //Converting bytes into Int8, and then to degrees
                // The angles are quantized as a signed 8 bit number that 
                // represents -180 to 180 degrees
                sbyte tmp0;
                // Getting position
                position = unchecked((byte)newValue[18]);
                int counter = 0;
                for (int i = 0; i < 18; i++)
                {//Getting angles
                    tmp0 = unchecked((sbyte)newValue[i]);
                    newData[counter] = (tmp0 * 360) / 256;
                    counter++;
                    if (counter >= 3)
                    {
                        thetaClass newTetha = new thetaClass();
                        newTetha.ThetaData = newData;
                        dataQueue.Enqueue(newTetha);
                        plotQueue.Enqueue(newTetha);
                        counter = 0;
                    }
                }
                flag0 = 1;
                // Triggers queue event
                if (OnNewDataEnqueued != null)
                { OnNewDataEnqueued(this, EventArgs.Empty); }

                //Fires graph refresh event
                if (OnNewPointsAdded != null)
                { OnNewPointsAdded(this, EventArgs.Empty); }
            }
            //Check for other services sending data...
        }

        // -->> Writes the new data to a file <<--
        //________________________________________
        private async void OnNewDataEnqueuedFxn(object sender, EventArgs e)
        {
            int counter = 0;
            while (busy || counter < 200)//Waits for another writing operation to complete
            { counter++; }
            if (!busy)
            {//Writes to file
                busy = true;//Sets busy flag
                thetaClass dequeued = null;
                while(dataQueue.Count > 0)
                {
                    if (dataQueue.TryDequeue(out dequeued))//Dqueues new data
                    { await FileIO.AppendTextAsync(rootPage.dataFile, dequeued.StringTheta); }
                }
                busy = false;//Releases flag
            }
            else
            {//Failed to write to file
                rootPage.notifyFlyout("Reached max number of iterations", connectButton);
            }

        }

        // -->> Refreshes UI and Graph <<--
        //_________________________________
        private async void OnNewPointsAddedFxn(object sender, EventArgs e)
        {// Updates the UI and the graph
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    counter +=6;
                    counterTextBlock.Text = counter.ToString();
                    thetaClass dequeued = null;
                    while (dataQueue.Count > 0)
                    {
                        if (dataQueue.TryDequeue(out dequeued))//Dequeues new data
                        {
                            dataTextBlock.Text = dequeued.StringTheta;
                            graph.AddPoints(dequeued.ThetaData);
                        }
                    }
                    timerTextBlock.Text = Convert.ToString(stopwatch.Elapsed);
                });

        }
        

        // >>> Check this code for guidance in writing to the MCU
        /*Boolean ledState = false;

        private async void ledButton_Click()
        {
            var attributeInfoDisp = (BLEAttributeDisplay)characteristicsListView.SelectedItem;
            selectedCharacteristic = attributeInfoDisp.characteristic;
            var writer = new DataWriter();
            if (ledState)
            {// It's ON. Turn it OFF
                writer.WriteByte((Byte)0x00);
                ledState = false;
            }
            else
            {// It's OFF. Turn it ON
                writer.WriteByte((Byte)0x01);
                ledState = true;
            }
            await selectedCharacteristic.WriteValueAsync(writer.DetachBuffer());
        }/*

        /*private async void WriteToFile(string data)
        {
            lock (fileLock)
            {
                FileIO.AppendText(rootPage.dataFile, data.ToString() + "," + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
            }
            //await FileIO.AppendTextAsync(rootPage.dataFile, data + "\r\n");
            
        }*/

        //private async Task

        //_______________________________________________
        //-----------------------------------------------
        //  BLE Attributes - Services and Characteristics
        //_______________________________________________

        /*private void ClearBLEDevice()
        {
            sensorTagBLE?.Dispose();
            sensorTagBLE = null;
        }

        private async void servicesWatcher()
        {// Here we start looking at the device's attributes


            ClearBLEDevice();
            ServiceCollection.Clear();

            try
            {
                // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                sensorTagBLE = await BluetoothLEDevice.FromIdAsync(rootPage.selectedDeviceId);
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x800710df)
            {
                // ERROR_DEVICE_NOT_AVAILABLE because the Bluetooth radio is not on.
            }

            if (sensorTagBLE != null)
            {
                // BT_Code: GattServices returns a list of all the supported services of the device.
                // If the services supported by the device are expected to change
                // during BT usage, subscribe to the GattServicesChanged event.
                foreach (var service in sensorTagBLE.GattServices)
                {
                    ServiceCollection.Add(new BLEAttributeDisplay(service));
                }


                //connectButton.Visibility = Visibility.Collapsed;
                //servicesListView.Visibility = Visibility.Visible;
            }
            else
            {
                //ClearBluetoothLEDevice();
                Debug.WriteLine("!!! Connection failed !!!");
            }

        }

        private async void servicesComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var attributeInfoDisp = (BLEAttributeDisplay)servicesComboBox.SelectedItem;

            CharacteristicCollection.Clear();
            IReadOnlyCollection<GattCharacteristic> characteristics = null;
            try
            {
                // Get all the child characteristics of a service
                characteristics = attributeInfoDisp.service.GetAllCharacteristics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Restricted service. Can't read characteristics: " + ex.Message);
                characteristics = new List<GattCharacteristic>();
            }

            foreach (GattCharacteristic c in characteristics)
            {
                CharacteristicCollection.Add(new BLEAttributeDisplay(c));
            }
        }

        private DeviceInformationDisplay FindDeviceDisplay(string id)
        {// Searchs for a device in the collection
            foreach (DeviceInformationDisplay deviceDisplay in ResultCollection)
            {
                if (deviceDisplay.Id == id)
                {
                    return deviceDisplay;
                }
            }
            return null;
        }

        /*Boolean ledState = false;

        private async void ledButton_Click()
        {
            var attributeInfoDisp = (BLEAttributeDisplay)characteristicsListView.SelectedItem;
            selectedCharacteristic = attributeInfoDisp.characteristic;
            var writer = new DataWriter();
            if (ledState)
            {// It's ON. Turn it OFF
                writer.WriteByte((Byte)0x00);
                ledState = false;
            }
            else
            {// It's OFF. Turn it ON
                writer.WriteByte((Byte)0x01);
                ledState = true;
            }
            await selectedCharacteristic.WriteValueAsync(writer.DetachBuffer());
        }*/
    }

    /*public class DataStorage : INotifyPropertyChanged
    {
        private AcquiredData dataAcquired;

        public DataStorage(AcquiredData dataAcquiredIn)
        {
            dataAcquired = dataAcquiredIn;
        }

        public DateTime timeStamp
        {
            get
            {
                return dataAcquired.TimeStamp;
            }
        }

        public Int16 data
        {
            get
            {
                return dataAcquired.Data;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }*/
}
