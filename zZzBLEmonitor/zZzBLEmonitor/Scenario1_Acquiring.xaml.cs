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
        private List<BluetoothLEDevice> zZzBRdevice = new List<BluetoothLEDevice>();
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
        // >> Stored data
        public string bleData1 = "";
        public string bleData2 = "";

        public Scenario1_Acquiring()
        {
            InitializeComponent();
        }

        ~Scenario1_Acquiring() { }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Deletes conected device
            zZzBRdevice.Clear();
            rootPage.bleDeviceId.Clear();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            acquireButton.IsEnabled = false;
            if (rootPage.bleDeviceId.Any())
            {
                nameDeviceConnected.Text = $"Device: {rootPage.ble1DeviceName}";
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
            refGraph.AddPlot(Colors.AliceBlue);
            refGraph.AddPlot(Colors.BlanchedAlmond);
            refGraph.AddPlot(Colors.BlueViolet);

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
            if (rootPage.bleDeviceId.Any())//Is there any devices selected?
            {// A device has been selected
                foreach (var deviceId in rootPage.bleDeviceId)
                {
                    BluetoothLEDevice sensorTagBLE = null;
                    try
                    {
                        // Creates BLE device from the selected DeviceID
                        sensorTagBLE = await BluetoothLEDevice.FromIdAsync(deviceId);
                        //sensorTagBLE.
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
                            //GattCharacteristicsResult characResult;
                            selectedService = servicesResult.Services.Single();
                            Debug.WriteLine($"Service found: {selectedService.Uuid}");
                            try
                            {

                                // Gets the Breathing characteristic
                                //characResult = await selectedService.GetCharacteristicsForUuidAsync(
                                //              BrService.DEVICE_1_UUID, BluetoothCacheMode.Uncached);
                                characResult = await selectedService.GetCharacteristicsAsync();
                            }
                            catch (Exception ex)
                            {
                                rootPage.notifyFlyout(
                                    "Can't read characteristics. Make sure service is not restricted. " + ex.Message, connectButton);
                            }
                            if (characResult.Status == GattCommunicationStatus.Success)
                            { // SUCCESS!!!
                                int number = characResult.Characteristics.Count();
                                statusTextBox.Text += number.ToString() + " characteristics found in " + sensorTagBLE.Name + Environment.NewLine;
                                foreach (GattCharacteristic characteristic in characResult.Characteristics)
                                {
                                    if ( characteristic.Uuid.Equals(BrService.DEVICE_1_UUID) || characteristic.Uuid.Equals(BrService.DEVICE_2_UUID))
                                        selectedCharacteristic.Add(characteristic);
                                }
                                nameDeviceConnected.Text = $"Connected to {sensorTagBLE.Name}";
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
                foreach (GattCharacteristic characteristic in selectedCharacteristic)
                {
                    try
                    {
                        //characteristic = selectedCharacteristic.Last();
                        GattCommunicationStatus result =
                        await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.Notify);
                        if (result == GattCommunicationStatus.Success)
                        {
                            statusTextBox.Text += "Receiving Notifications from " + characteristic.Uuid.ToString();
                            // Updates the GUI
                            acquireButton.Label = "Stop";
                            acquireButton.Icon = new SymbolIcon(Symbol.Stop);
                            recordButton.Visibility = Visibility.Visible;
                            // Starts to read the sensors
                            characteristic.ValueChanged += Characteristic_ValueChanged;
                            IsValueChangedHandlerRegistered = true;
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
            }
            else
            {// Unregister for notifications
                foreach (GattCharacteristic characteristic in selectedCharacteristic)
                {
                    characteristic.ValueChanged -= Characteristic_ValueChanged;
                }
                //GattCharacteristic characteristic = selectedCharacteristic.Last();
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
                    {
                        if (sender.Uuid.Equals(BrService.DEVICE_1_UUID))
                            bleData1 += tempStr;//Save to file
                        else
                            bleData2 += tempStr;
                    }
                    tempStr = "";//Resets tempStr
                    double[] temp = new double[4];
                    for (int j = 0; j < 3; j++)
                        temp[j] = newData[j];
                    temp[3] = (double)position;
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        temp[1] *= 2;
                        if (sender.Uuid.Equals(BrService.DEVICE_1_UUID))
                            graph.AddPoints(temp);
                        else
                            refGraph.AddPoints(temp);
                    });
                    counter = 0;
                }
            }
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
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                timerTextBlock.Text = Convert.ToString(stopwatch.Elapsed);
                if (sender.Uuid.Equals(BrService.DEVICE_1_UUID))
                    position1TextBlock.Text = positionStr;
                else
                    position2TextBlock.Text = positionStr;
            });
        }

        
        /// <summary>
        /// WriteDataToFile: Function to write all the data to a file
        /// </summary>
        /// <returns></returns>
        /// 
        private async void WriteDataToFile()
        {

            rootPage.folderName = "Data Acquired";
            rootPage.fileName = "ble1Data-" + rootPage.ble1DeviceName + DateTime.Now.ToString("_yyyy-dd-MM_HHmmss") + ".csv";
            string beltFileName = "ble2Data-" + rootPage.ble1DeviceName + DateTime.Now.ToString("_yyyy-dd-MM_HHmmss") + ".csv";
            StorageFile beltFile = null;
            try
            {
                rootPage.dataFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(rootPage.folderName, CreationCollisionOption.OpenIfExists);
                rootPage.dataFile = await rootPage.dataFolder.CreateFileAsync(rootPage.fileName, CreationCollisionOption.ReplaceExisting);
                beltFile = await rootPage.dataFolder.CreateFileAsync(beltFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(rootPage.dataFile, bleData2);
                await FileIO.WriteTextAsync(beltFile, bleData1);
                rootPage.notifyFlyout("Data stored", acquireButton);
                /* Copies the file path to the clipboard */
                var dataPackage = new DataPackage();
                dataPackage.SetText(rootPage.dataFolder.Path.ToString());
                Clipboard.SetContent(dataPackage);
                bleData2 = "";
                bleData1 = "";
            }
            catch (Exception ex)
            {
                rootPage.notifyFlyout("Error writing data: " + ex.Message, acquireButton);
            }
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
