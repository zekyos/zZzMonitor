using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.ViewManagement;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace zZzBLEmonitor
{

    // Scenario Class
    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
    public partial class MainPage : Page
    {
        List<Scenario> scenarios = new List<Scenario>
        {
            // Add new scenarios as follows
            //new Scenario() { Title="Name of the scenario", ClassType=typeof(Name_of_the_Namespace_of_Scenario) },
            new Scenario() { Title="Acquire data", ClassType=typeof(Scenario1_Acquiring) },
            //new Scenario() { Title="Pairing Panel", ClassType=typeof(PairingPanel) },
        };
    }

    public sealed partial class MainPage : Page
    {
        public static MainPage Current;

        public MainPage()
        {
            this.InitializeComponent();
            Current = this;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Adds the declared scenarios to the ScenarioControl
            ScenarioControl.ItemsSource = scenarios;
            ScenarioControl.SelectedIndex = 0;
            //Add code for the case that screen is smaller
        }

        private void ScenarioControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox scenarioListBox = sender as ListBox;
            Scenario s = scenarioListBox.SelectedItem as Scenario;
            if (s != null)
            {
                ScenarioFrame.Navigate(s.ClassType);
                if (Window.Current.Bounds.Width < 640)
                {
                    // closes the Hamburguer menu if window is small
                    Splitter.IsPaneOpen = false;
                }
            }
        }

        public List<Scenario> Scenarios
        {
            get { return this.scenarios; }
        }
    }

    public class ScenarioBindingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Scenario s = value as Scenario;
            return (MainPage.Current.Scenarios.IndexOf(s) + 1) + ")" + s.Title;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
}
