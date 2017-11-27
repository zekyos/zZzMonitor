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
using Windows.Devices.SerialCommunication;
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
using Windows.ApplicationModel.DataTransfer;

namespace zZzBLEmonitor
{
    public sealed partial class Scenario1_Acquiring : Page
    {
        private MainPage rootPage = MainPage.Current;
        private BreathingService BrService = new BreathingService();
        private sbyte position = 0;
        // BLE Services Collection
        private ObservableCollection<BLEAttributeDisplay> ServiceCollection = new ObservableCollection<BLEAttributeDisplay>();
        // BLE Characteristics Collection
        private ObservableCollection<BLEAttributeDisplay> CharacteristicCollection = new ObservableCollection<BLEAttributeDisplay>();
        // BLE Device
        private BluetoothLEDevice sensorTagBLE = null;
        // Selected GATT Service
        private GattDeviceService selectedService;
        // Selected GATT Characteristic
        private List<GattCharacteristic> selectedCharacteristic = new List<GattCharacteristic>();
        private bool IsValueChangedHandlerRegistered = false;
        // Graph
        private GraphClass graph = new GraphClass();
        private GraphClass refGraph = new GraphClass();
        // Data files names
        public string timeStamp = null;
        Int32 counter = 0;
        Stopwatch stopwatch = new Stopwatch();
        public int record = 0;//If active, starts to record data to the Strings
        // >> Serial communication
        public string beltData = "";
        public string imuData = "";
        static string beltIdString = "USB#VID_0451&PID_BEF3&MI_00#6&3030f56c&0&0000#";
        private SerialDevice beltSerialPort = null;
        DataReader beltDataReaderObject = null;
        DataWriter beltDataWriteObject = null;
        private ObservableCollection<DeviceInformation> listOfUartDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

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
                listOfUartDevices = new ObservableCollection<DeviceInformation>();
                ListAvailableUartPorts();
            }
            else
            {
                nameDeviceConnected.Text = "No Device Selected";
                connectButton.IsEnabled = false;
            }
        }

        // -->> Looks for UART devices connected <<--
        //_________________________________________
        private async void ListAvailableUartPorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                foreach (DeviceInformation port in dis)
                {
                    if (port.Id.Contains(beltIdString))
                    {
                        listOfUartDevices.Add(port);
                        Debug.WriteLine(port.Id + "added");
                    }
                }
                if (listOfUartDevices.Count != 0)
                {
                    beltSerialPort = await SerialDevice.FromIdAsync(listOfUartDevices.Last().Id);
                    var IsNull = true;
                    int counter = 0;
                    do
                    {
                        if (beltSerialPort != null)
                        {
                            IsNull = false;
                        }
                        if (counter > 100)
                            break;
                        counter++;
                    } while (IsNull == true);

                    if (IsNull != true)
                    {
                        beltDataWriteObject = new DataWriter(beltSerialPort.OutputStream);
                        // Configure serial settings
                        beltSerialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                        beltSerialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                        beltSerialPort.BaudRate = 9600;
                        beltSerialPort.Parity = SerialParity.None;
                        beltSerialPort.StopBits = SerialStopBitCount.One;
                        beltSerialPort.DataBits = 8;
                        beltSerialPort.Handshake = SerialHandshake.None;
                        // Create cancellation token object to close I/O operations when closing the device
                        ReadCancellationTokenSource = new CancellationTokenSource();
                        rootPage.notifyFlyout("UART ready", connectButton);
                    }
                    else
                    {
                        rootPage.notifyFlyout("Couldn't connect to UART device", connectButton);
                    }
                }
                else
                {
                    rootPage.notifyFlyout("No UART device found", connectButton);
                }
            }
            catch (Exception ex)
            {
                rootPage.notifyFlyout("Problems loading UART:" + ex.Message, connectButton);
            }
        }

        // -->> Search BLE Sensor: Click Event <<--
        //_________________________________________
        private async void searchDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(PairingPanel));
        }

        // -->> Connect to sensor <<--
        // Looks for an specific service and characteristic
        //_________________________________________________
        private async void connectButton_Click(object sender, RoutedEventArgs e)
        {
            connectButton.IsEnabled = false;
            // IMU graph
            graph.Initialize(graphStackPanel, 1);
            graph.Background(Colors.WhiteSmoke);
            graph.AddPlot(Colors.Red);
            graph.AddPlot(Colors.Blue);
            graph.AddPlot(Colors.LimeGreen);
            graph.AddPlot(Colors.DarkOrange);
            // Belt graph
            refGraph.Initialize(refGraphStackPanel, 1);
            refGraph.Background(Colors.Black);
            refGraph.AddPlot(Colors.White);
            rootPage.ShowProgressRing(connectingProgressRing, true);
            if (IsValueChangedHandlerRegistered)
            {// Reconnecting
                acquireButton.IsEnabled = false;
                foreach (GattCharacteristic characteristic in selectedCharacteristic)
                {
                    characteristic.ValueChanged -= Characteristic_ValueChanged;
                }
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

                    //await dataService.Services.Single(s => s.Uuid == rootPage.COUNTER_SERVICE_UUID).GetCharacteristicsAsync();
                    if (servicesResult.Status == GattCommunicationStatus.Success)
                    {
                        selectedService = servicesResult.Services.Single();
                        Debug.WriteLine($"Service found: {selectedService.Uuid}");
                        try
                        {

                            // Gets the Breathing characteristic
                            characResult = await selectedService.GetCharacteristicsForUuidAsync(
                                            BrService.BREATHING_UUID, BluetoothCacheMode.Uncached);
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
                            foreach (GattCharacteristic characteristic in characResult.Characteristics)
                            {
                                selectedCharacteristic.Add(characteristic);
                            }
                            nameDeviceConnected.Text = $"Connected to {rootPage.selectedDeviceName}";
                            connectButton.Label = "Reconnect";
                            acquireButton.IsEnabled = true;
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
                    GattCharacteristic characteristic = selectedCharacteristic.Last();
                    GattCommunicationStatus result =
                    await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    if (result == GattCommunicationStatus.Success)
                    {
                        // Updates the GUI
                        acquireButton.Label = "Stop";
                        acquireButton.Icon = new SymbolIcon(Symbol.Stop);
                        recordButton.Visibility = Visibility.Visible;
                        // Starts to read the sensors
                        characteristic.ValueChanged += Characteristic_ValueChanged;
                        IsValueChangedHandlerRegistered = true;
                        await WriteAsync(beltDataWriteObject, "s");
                        BeltListen();
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
                GattCharacteristic characteristic = selectedCharacteristic.Last();
                characteristic.ValueChanged -= Characteristic_ValueChanged;
                await WriteAsync(beltDataWriteObject, "t");//Stops belt MCU 
                CancelReadTask();
                CloseDevice();
                listOfUartDevices.Clear();
                if (record == 1)
                    WriteDataToFile();
                record = 0;
                IsValueChangedHandlerRegistered = false;
                recordButton.Visibility = Visibility.Collapsed;
                recordButton.Icon = new FontIcon { Glyph = "\uE7C8" };
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
            string tempStr = "";
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
                position = unchecked((sbyte)newValue[18]);
                int counter = 0;
                for (int i = 0; i < 18; i++)
                {//Getting angles
                    tmp0 = unchecked((sbyte)newValue[i]);
                    tempStr += tmp0.ToString() + ",";
                    newData[counter] = (tmp0 * 360) / 256;
                    counter++;
                    if (counter >= 3)
                    {
                        tempStr += position.ToString() + "\n";
                        if (record == 1)//Recording?
                            imuData += tempStr;//Save to file
                        tempStr = "";//Resets tempStr
                        double[] temp = new double[4];
                        for (int j = 0; j < 3; j++)
                            temp[j] = newData[j];
                        temp[3] = (double)position;
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            temp[1] *= 2;
                            graph.AddPoints(temp);
                        });
                        counter = 0;
                    }
                }
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    timerTextBlock.Text = Convert.ToString(stopwatch.Elapsed);
                    string positionStr = "";
                    switch (position)
                    {
                        case 2:
                            positionStr = "Prone";
                            break;
                        case 1:
                            positionStr = "Vertical";
                            break;
                        case 0:
                            positionStr = "Supine";
                            break;
                        case -1:
                            positionStr = "Left Lat.Rec.";
                            break;
                        case -2:
                            positionStr = "Right Lat. Rec.";
                            break;
                        default:
                            positionStr = "ERROR";
                            break;
                    }
                    positionTextBlock.Text = positionStr;
                });
            }
            //Check for other services sending data...
        }

        /// <summary>
        /// WriteDataToFile: Function to write all the data to a file
        /// </summary>
        /// <returns></returns>
        /// 
        private async void WriteDataToFile()
        {

            rootPage.folderName = "Data Acquired";
            rootPage.fileName = "bleData-" + rootPage.selectedDeviceName + DateTime.Now.ToString("_yyyy-dd-MM_HHmmss") + ".csv";
            string beltFileName = "refData-" + rootPage.selectedDeviceName + DateTime.Now.ToString("_yyyy-dd-MM_HHmmss") + ".csv";
            StorageFile beltFile = null;
            try
            {
                rootPage.dataFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(rootPage.folderName, CreationCollisionOption.OpenIfExists);
                rootPage.dataFile = await rootPage.dataFolder.CreateFileAsync(rootPage.fileName, CreationCollisionOption.ReplaceExisting);
                beltFile = await rootPage.dataFolder.CreateFileAsync(beltFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(rootPage.dataFile, imuData);
                await FileIO.WriteTextAsync(beltFile, beltData);
                rootPage.notifyFlyout("Data stored", acquireButton);
                /* Copies the file path to the clipboard */
                var dataPackage = new DataPackage();
                dataPackage.SetText(rootPage.dataFolder.Path.ToString());
                Clipboard.SetContent(dataPackage);
                imuData = "";
                beltData = "";
            }
            catch (Exception ex)
            {
                rootPage.notifyFlyout("Error writing data: " + ex.Message, acquireButton);
            }
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream 
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync(DataWriter dataWriteObject, string command)
        {
            Task<UInt32> storeAsyncTask;

            if (command.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriteObject.WriteString(command);

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten <= 0)
                {
                    //Writing unsuccesful
                    rootPage.notifyFlyout("UART write failed", acquireButton);
                }
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BeltListen()
        {
            try
            {
                if (beltSerialPort != null)
                {
                    beltDataReaderObject = new DataReader(beltSerialPort.InputStream);

                    // keep reading the serial input
                    var trash = beltSerialPort.InputStream;
                    while (true)
                    {
                        await BeltReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (TaskCanceledException tce)
            {
                //status.Text = "Reading task was cancelled, closing device and cleaning up";
                CloseDevice();
            }
            catch (Exception ex)
            {
                rootPage.notifyFlyout("Error listening to UART:" + ex.Message, acquireButton);
            }
            finally
            {
                // Cleanup once complete
                if (beltDataReaderObject != null)
                {
                    beltDataReaderObject.DetachStream();
                    beltDataReaderObject = null; ;
                }
            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task BeltReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> beltLoadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            beltDataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            using (var childCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // Create a task object to wait for data on the serialPort.InputStream
                beltLoadAsyncTask = beltDataReaderObject.LoadAsync(ReadBufferLength).AsTask(childCancellationTokenSource.Token);

                // Launch the task and wait
                UInt32 bytesRead = await beltLoadAsyncTask;
                if (bytesRead > 0)
                {
                    byte[] newValue = new byte[bytesRead];
                    beltDataReaderObject.ReadBytes(newValue);
                    newValue[2] = newValue[0];
                    UInt16 belt = BitConverter.ToUInt16(newValue, 1);
                    if (record == 1)
                        beltData += belt.ToString() + "\n";
                    double[] temp = new double[1];
                    temp[0] = (double)(belt - 33032) * 0.05;
                    refGraph.AddPoints(temp);
                }
            }
        }

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }


        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            if (beltSerialPort != null)
            {
                beltSerialPort.Dispose();
            }
            beltSerialPort = null;
            listOfUartDevices.Clear();
        }

        /// <summary>
        /// recordButton_Click:
        /// - Changes the value of the record flag, and the data starts to be 
        /// recorded in their respectives strings
        /// - The record button is Disabled, and record will stop when Acquiring data
        /// stops (at this point the acquireButton displays the Stop symbol)
        /// - A stopwatch is also started at this point
        /// </summary>
        private void recordButton_Click(object sender, RoutedEventArgs e)
        {
            recordButton.Icon = new SymbolIcon(Symbol.Target);
            recordButton.IsEnabled = false;
            stopwatch.Start();
            record = 1;
        }
    }
}
