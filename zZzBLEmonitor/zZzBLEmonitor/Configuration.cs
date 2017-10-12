using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Storage;

namespace zZzBLEmonitor
{
    public class BreathingService
    {
        //-->> Inertial Measurement Unit Service
        const string ZZZ_GUID_PREFIX = "A000";
        const string ZZZ_GUID_SUFFIX = "-07E1-4000-E7C7-BD31071482B7";
        // IMU Service
        public readonly Guid BREATHINGSERVICE_UUID = new Guid(ZZZ_GUID_PREFIX + "1110" + ZZZ_GUID_SUFFIX);
        public readonly Guid BREATHING_UUID = new Guid(ZZZ_GUID_PREFIX + "1111" + ZZZ_GUID_SUFFIX);
        public readonly Guid IMUWRITE_UUID = new Guid(ZZZ_GUID_PREFIX + "1112" + ZZZ_GUID_SUFFIX);

        public string Name
        {
            get { return "Breathing Service"; }
        }
        public string Breathing
        {
            get { return "Breathing monitoring"; }
        }
        public string Position
        {
            get { return "Body position"; }
        }      
    }

    public partial class MainPage : Page
    {
        public string selectedDeviceId = null;
        public string selectedDeviceName = null;

        // For Data Acquisition
        public string folderName = null;// "Data Acquired";
        public string fileName = null;// "data-" + selectedDeviceName + DateTime.Now.ToString("_yyyy-dd-MM_HHmmss") + ".temp";
        public string timeStamp = null;
        public StorageFolder dataFolder = null;
        public StorageFile dataFile = null;

        // !! Default pin is set on PairingPanel.cs
        //public const string defaultPin = "000000";

        //_____________________________________________________________________________________________
        // UUID Declarations for the custom services
        //_____________________________________________________________________________________________

        // Sensor Tag Test Services
        const string SENSORTAG_GUID_PREFIX = "F000";
        const string SENSORTAG_GUID_SUFFIX = "-0451-4000-B000-000000000000";
        // LED service
        readonly Guid LED_SERVICE_UUID = new Guid(SENSORTAG_GUID_PREFIX + "1110" + SENSORTAG_GUID_SUFFIX);
            readonly Guid LED0_UUID = new Guid(SENSORTAG_GUID_PREFIX + "1111" + SENSORTAG_GUID_SUFFIX);
            readonly Guid LED1_UUID = new Guid(SENSORTAG_GUID_PREFIX + "1112" + SENSORTAG_GUID_SUFFIX);
        // Button service
        readonly Guid BUTTON_SERVICE_UUID = new Guid(SENSORTAG_GUID_PREFIX + "1120" + SENSORTAG_GUID_SUFFIX);
            readonly Guid BUTTON0_UUID = new Guid(SENSORTAG_GUID_PREFIX + "1121" + SENSORTAG_GUID_SUFFIX);
            readonly Guid BUTTON1_UUID = new Guid(SENSORTAG_GUID_PREFIX + "1122" + SENSORTAG_GUID_SUFFIX);
        // String service
        readonly Guid STRING_SERVICE_UUID = new Guid(SENSORTAG_GUID_PREFIX + "1130" + SENSORTAG_GUID_SUFFIX);
            readonly Guid STRING0_UUID = new Guid(SENSORTAG_GUID_PREFIX + "1131" + SENSORTAG_GUID_SUFFIX);
        // Counter service
        public readonly Guid COUNTER_SERVICE_UUID = new Guid(SENSORTAG_GUID_PREFIX + "BA55" + SENSORTAG_GUID_SUFFIX);
            public readonly Guid COUNTER_UUID = new Guid(SENSORTAG_GUID_PREFIX + "2BAD" + SENSORTAG_GUID_SUFFIX);

        //_______________________________
        //-------------------------------
        // -->> Flyout notifications <<--
        //_______________________________________________
        // Shows a Flyout with the message at the element
        //_______________________________________________
        public void notifyFlyout(string message, FrameworkElement element)
        {
            Flyout flyout = new Flyout();
            var messageTextBlock = new TextBlock();
            messageTextBlock.Text = message;
            flyout.Content = messageTextBlock;
            flyout.Placement = FlyoutPlacementMode.Bottom;
            FlyoutBase.SetAttachedFlyout(element, flyout);
            Flyout.ShowAttachedFlyout(element);
        }

        //_____________________________
        //-----------------------------
        // -->> Show Progress Ring <<--
        //____________________________________________________________
        // The progress ring shold be already defined at the XAML file
        //____________________________________________________________
        public void ShowProgressRing(ProgressRing progressRing, bool show)
        {
            if (show)
            {
                progressRing.Visibility = Visibility.Visible;
                progressRing.IsActive = true;
            }
            else
            {
                progressRing.IsActive = false;
                progressRing.Visibility = Visibility.Collapsed;
            }

        }
    }
}
