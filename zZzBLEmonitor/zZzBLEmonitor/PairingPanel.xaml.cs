using System;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Diagnostics;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace zZzBLEmonitor
{
    public sealed partial class PairingPanel : Page
    {
        private MainPage rootPage = MainPage.Current;

        // This is the default pin set in the CC2650 code
        const string defaultPin = "000000";

        private DeviceWatcher deviceWatcher = null;

        public ObservableCollection<DeviceInformationDisplay> ResultCollection
        {
            get;
            private set;
        }

        public PairingPanel()
        {
            this.InitializeComponent();
            ResultCollection = new ObservableCollection<DeviceInformationDisplay>();
            StartWatcher();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            StopWatcher();
        }

        // Back Button: Click Event
        //_________________________
        private async void backButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(Scenario1_Acquiring));
        }

        //  Devices List View: Selection Changed Event
        //____________________________________________
        private async void devicesListView_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (devicesListView.SelectedItems.Count() > 2)
            {
                devicesListView.SelectedItems.Remove(devicesListView.SelectedItems.Last());
            }
            else if (devicesListView.SelectedItems.Count() == 2)
            {
                List<DeviceInformationDisplay> devicesInfoDisp = new List<DeviceInformationDisplay>();
                int c = 0;
                foreach (var device in devicesListView.SelectedItems)
                {
                    DeviceInformationDisplay tempDevice = device as DeviceInformationDisplay;
                    if (!tempDevice.IsPaired)
                    {
                        devicesListView.SelectedItems.Clear();
                        devicesListView.SelectedItem = device;
                    }
                    devicesInfoDisp.Add(tempDevice);
                    c++;
                }
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    pairingCommandBar.Background = (SolidColorBrush)Application.Current.Resources["CommandBarBackground"];
                    foreach (DeviceInformationDisplay device in devicesInfoDisp)
                    {
                        if (!device.IsPaired) // Device is not paired
                        {
                            pairingStatusTextBlock.Text = $"{device.Name} is not paired.";
                            pairButton.Label = "Pair";
                            pairButton.Icon = new SymbolIcon(Symbol.Add);
                            return;
                        }
                    }
                    // If the 2 devices are paired, proceed
                    pairingStatusTextBlock.Text = $"Devices are already paired. You can connect.";
                    pairButton.Label = "Unpair";
                    pairButton.Icon = new SymbolIcon(Symbol.Clear);
                    foreach(DeviceInformationDisplay device in devicesInfoDisp)
                    {
                        rootPage.bleDeviceId.Add(device.Id);
                        rootPage.bleDeviceName.Add(device.Name);
                    }
                });
            }
        }

        // Rescan Button: Click Event
        //___________________________
        private async void rescanButton_Click(object sender, RoutedEventArgs e)
        {
            pairingCommandBar.Background = (SolidColorBrush)Application.Current.Resources["CommandBarBackground"];
            if (deviceWatcher.Status == DeviceWatcherStatus.Started)
            {
                StopWatcher();
            }
            ResultCollection.Clear();
            StartWatcher();
        }

        //  ______________________________________________________
        //  ------------------------------------------------------
        //  Device scanning and pairing
        //  ______________________________________________________

        // -->> Start Watcher <<--
        //________________________
        private void StartWatcher()
        {
            ResultCollection.Clear();
            // -->> Watcher Creation <<--
            // ______________________________________________
            // for bluetooth LE Devices use this protocal ID 
            // Check https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/enumerate-devices-over-a-network
            //aqsFilter = Advanced Query Syntax string to filtering devices
            string aqsFilter = "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";

            // Request the IsPaired, IsPresent, and SignalStrength properties to display on the UI
            string[] requestedProperties =
                { "System.Devices.Aep.IsPaired",
                "System.Devices.Aep.IsPresent",
                "System.Devices.Aep.SignalStrength",};

            deviceWatcher = DeviceInformation.CreateWatcher(
                aqsFilter,
                requestedProperties,
                DeviceInformationKind.AssociationEndpoint);

            // Watcher Event Handlers. Hook them before starting the watcher!
            deviceWatcher.Added += deviceWatcher_AddedAsync; // Event: Device Added
            deviceWatcher.Updated += deviceWatcher_UpdatedAsync; // Event: Device Updated
            deviceWatcher.Removed += deviceWatcher_RemovedAsync; // Event: Device Removed
            deviceWatcher.EnumerationCompleted += deviceWatcher_EnumerationCompletedAsync; // Event: Enumeration completed
            deviceWatcher.Stopped += deviceWatcher_StoppedAsync; // Event: Watcher Stopped

            deviceWatcher.Start();
            pairingStatusTextBlock.Text = "Looking for Bluetooth Low Energy Devices";
        }

        // Device Watcher Event: Device Added
        //___________________________________
        private async void deviceWatcher_AddedAsync(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // Since we have the collection databound to a UI element,
            // we need to update the collection on the UI thread.
            await devicesListView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Protect form race condition
                if (sender == deviceWatcher)
                {
                    //Make sure device name isn't blank or already in the list
                    if (deviceInfo.Name != string.Empty)// && ResultCollection.Where(x => x.Id == deviceInfo.Id) == null)
                    {
                        ResultCollection.Add(new DeviceInformationDisplay(deviceInfo));
                        Debug.WriteLine("Watcher Add: " + deviceInfo.Id);
                    }
                }
            });
        }

        // Device Watcher Event: Device Updated
        //_____________________________________
        private async void deviceWatcher_UpdatedAsync(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // Collection must be update in the UI
            await devicesListView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Protect against race condition
                if (sender == deviceWatcher)
                {
                    DeviceInformationDisplay deviceInfoDisp = ResultCollection.Where(x => x.Id == deviceInfoUpdate.Id) as DeviceInformationDisplay;
                    if (deviceInfoDisp != null)
                    {
                        deviceInfoDisp.Update(deviceInfoUpdate);
                    }
                }
            });
        }

        // Device Watcher Event: Device Removed
        //_____________________________________
        private async void deviceWatcher_RemovedAsync(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection in the UI
            await devicesListView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Protect against race condition
                if (sender == deviceWatcher)
                {
                    // Find the device in the collection and remove it
                    DeviceInformationDisplay deviceInfoDisp = ResultCollection.Where(x => x.Id == deviceInfoUpdate.Id) as DeviceInformationDisplay;
                    if (deviceInfoDisp != null)
                    {
                        ResultCollection.Remove(deviceInfoDisp);
                    }
                }
            });
        }

        // Device Watcher Event: Enumeration Completed
        //____________________________________________
        private async void deviceWatcher_EnumerationCompletedAsync(DeviceWatcher sender, object e)
        {
            // Update collection in the UI thread
            await devicesListView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Protect against race condition
                if (sender == deviceWatcher)
                {
                    //pairingStatusTextBlock.Text = "Enumeration Completed.";
                    Debug.WriteLine("Enumeration completed");
                }
            });
        }

        // Device Watcher Event: Watcher Stopped
        //______________________________________
        private async void deviceWatcher_StoppedAsync(DeviceWatcher sender, object e)
        {
            // Update the collection in the UI thread
            await devicesListView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Protect against race condition
                if (sender == deviceWatcher)
                {
                    Debug.WriteLine("Watcher stopped");
                }
            });
        }

        // -->> Stop Watcher <<--
        //________________________________________
        private void StopWatcher()
        {
            if (null != deviceWatcher)
            {
                // Cancel all Device Watcher Event Handlers
                deviceWatcher.Added -= deviceWatcher_AddedAsync;
                deviceWatcher.Updated -= deviceWatcher_UpdatedAsync;
                deviceWatcher.Removed -= deviceWatcher_RemovedAsync;
                deviceWatcher.EnumerationCompleted -= deviceWatcher_EnumerationCompletedAsync;
                deviceWatcher.Stopped -= deviceWatcher_StoppedAsync;

                // Checks if Watcher is running OR completed enumeration
                if (DeviceWatcherStatus.Started == deviceWatcher.Status ||
                    DeviceWatcherStatus.EnumerationCompleted == deviceWatcher.Status)
                {
                    deviceWatcher.Stop();
                    deviceWatcher = null;
                }
            }
        }

        // -->> Device Pairing/Unpairing <<--
        // Pair Button: Click Event
        //___________________________________
        private async void pairButton_Click(object sender, RoutedEventArgs e)
        {
            pairButton.IsEnabled = false;
            devicesListView.IsEnabled = false;
            rootPage.ShowProgressRing(pairingProgressRing, true);
            pairingCommandBar.Background = (SolidColorBrush)Application.Current.Resources["CommandBarBackground"];
            DeviceInformationDisplay deviceInfoSelected = devicesListView.SelectedItem as DeviceInformationDisplay;

            if (deviceInfoSelected != null) // Checks a device has been selected
            {
                bool paired = true;

                if (deviceInfoSelected.IsPaired != true) // Is device unpaired?
                { // Pair Selectede Device
                    paired = false;

                    // Selects all the available ceremonies for pairing
                    DevicePairingKinds ceremoniesSelected =
                        DevicePairingKinds.ConfirmOnly | DevicePairingKinds.DisplayPin |
                        DevicePairingKinds.ProvidePin | DevicePairingKinds.ConfirmPinMatch;
                    DevicePairingProtectionLevel protectionLevel = DevicePairingProtectionLevel.Default;

                    // Specify a custom pairing with all ceremony types and EncryptionAndAuthentication protection
                    DeviceInformationCustomPairing customPairing = deviceInfoSelected.DeviceInformation.Pairing.Custom;
                    customPairing.PairingRequested += PairingRequested_EventHandler;

                    // Requesting pairing ...
                    pairingStatusTextBlock.Text = $"Pairing to {deviceInfoSelected.Name}";

                    // -->> Pairing device
                    DevicePairingResult result = await customPairing.PairAsync(ceremoniesSelected, protectionLevel);
                    customPairing.PairingRequested -= PairingRequested_EventHandler;

                    switch (result.Status)
                    {
                        case DevicePairingResultStatus.Paired:// Pairing succeeded
                            paired = true;
                            break;
                        case DevicePairingResultStatus.AccessDenied:// Permission denied
                            pairingStatusTextBlock.Text = "Operation cancelled by the user";
                            break;
                        default:// Failed
                            pairingStatusTextBlock.Text = $"Pairing to {deviceInfoSelected.Name} failed";
                            pairingCommandBar.Background = new SolidColorBrush(Colors.Tomato);
                            break;
                    }

                    if (paired)
                    {
                        // Device paired correctly
                        StopWatcher();
                        StartWatcher();
                        pairingStatusTextBlock.Text = $"Successfully paired to {deviceInfoSelected.Name}.";
                        pairingCommandBar.Background = new SolidColorBrush(Colors.LightGreen);
                        pairButton.Icon = new SymbolIcon(Symbol.Clear);

                        // Saving the Device ID and Name for future use
                        rootPage.ble1DeviceId = deviceInfoSelected.Id;
                        rootPage.ble1DeviceName = deviceInfoSelected.Name;
                    }
                }
                else if (deviceInfoSelected.IsPaired) // Else, device is already paired
                {// Unpair device
                    pairingStatusTextBlock.Text = $"Unpairing {deviceInfoSelected.Name}";

                    // -->> Unpairing device
                    DeviceInformationPairing deviceUnpairing = deviceInfoSelected.DeviceInformation.Pairing;
                    DeviceUnpairingResult result = await deviceUnpairing.UnpairAsync();

                    switch (result.Status)
                    {
                        case DeviceUnpairingResultStatus.Unpaired:// Succeeded
                            StopWatcher();
                            StartWatcher();
                            pairingStatusTextBlock.Text = $"{deviceInfoSelected.Name} unpaired successfully";
                            pairingCommandBar.Background = new SolidColorBrush(Colors.LightGreen);
                            pairButton.Content = "Pair/Unpair Device";
                            rootPage.ble1DeviceId = null;
                            rootPage.ble1DeviceName = null;
                            break;
                        case DeviceUnpairingResultStatus.AccessDenied:// Permission denied
                            pairingStatusTextBlock.Text = "Operation cancelled by the user";
                            break;
                        default:// Failed
                            pairingStatusTextBlock.Text = "Unpairing failed!";
                            pairingCommandBar.Background = new SolidColorBrush(Colors.Tomato);
                            break;
                    }
                }
            }
            rootPage.ShowProgressRing(pairingProgressRing, false);
            pairButton.IsEnabled = true;
            devicesListView.IsEnabled = true;
        }

        // -->> Pairing Requested Events Handler <<--
        //___________________________________________
        private async void PairingRequested_EventHandler(
            DeviceInformationCustomPairing sender,
            DevicePairingRequestedEventArgs args)
        {
            switch (args.PairingKind)
            {
                case DevicePairingKinds.ConfirmOnly:
                    // Windows itself will pop the confirmation dialog as part of "consent" if this is running on Desktop or Mobile
                    // If this is an App for 'Windows IoT Core' where there is no Windows Consent UX, you may want to provide your own confirmation.
                    args.Accept();
                    break;

                case DevicePairingKinds.DisplayPin:
                    // We just show the PIN on this side. The ceremony is actually completed when the user enters the PIN
                    // on the target device. We automatically except here since we can't really "cancel" the operation
                    // from this side.
                    args.Accept();

                    // No need for a deferral since we don't need any decision from the user
                    /*await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        ShowPairingPanel(
                            "Please enter this PIN on the device you are pairing with: " + args.Pin,
                            args.PairingKind);

                    });*/
                    break;

                case DevicePairingKinds.ProvidePin:
                    // A PIN may be shown on the target device and the user needs to enter the matching PIN on 
                    // this Windows device. Get a deferral so we can perform the async request to the user.
                    var collectPinDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        /*string pin = await GetPinFromUserAsync();
                        if (!string.IsNullOrEmpty(pin))
                        {
                            args.Accept(pin);
                        }*/
                        // This is the default pin at the SensorTag
                        args.Accept(defaultPin);
                        collectPinDeferral.Complete();
                    });
                    break;

                case DevicePairingKinds.ConfirmPinMatch:
                    // We show the PIN here and the user responds with whether the PIN matches what they see
                    // on the target device. Response comes back and we set it on the PinComparePairingRequestedData
                    // then complete the deferral.
                    /*var displayMessageDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        bool accept = await GetUserConfirmationAsync(args.Pin);
                        if (accept)
                        {
                            args.Accept();
                        }

                        displayMessageDeferral.Complete();
                    });*/
                    break;
            }
        }

        private void clearSelButton_Click(object sender, RoutedEventArgs e)
        {
            devicesListView.SelectedItems.Clear();
        }
    }
}
