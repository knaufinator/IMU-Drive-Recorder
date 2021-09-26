//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using MathNet.Numerics.Statistics;
using mPUObserver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ODB.NET.Desktop.Communication;
using System.Threading.Tasks;
using mPUObserver.Helpers;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SDKTemplate
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public bool isRecording = false;
        public static MainPage Current;
        private UdpClient udpClient;
        private SerialConnection connection;
        private static int window = 20;
        private MovingStatistics msX = new MovingStatistics(window);
        private MovingStatistics msY = new MovingStatistics(window);
        private MovingStatistics msZ = new MovingStatistics(window);
 
        private GForceDevice gForce = new GForceDevice();
        private List<Force> tosaveForces = new List<Force>();
        private int batchSize = 100;

        DateTime currentVideoRecordingStartTime = DateTime.MinValue;
        private double currentSpeed;
        private double currentRpm;
        private Force currentForce = new Force();

        public MainPage()
        {
            gForce.GForceChanged += GForce_GForceChanged;
            this.InitializeComponent();
            Current = this;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Populate the scenario list from the SampleConfiguration.cs file
            var itemCollection = new List<Scenario>();
            int i = 1;
            foreach (Scenario s in scenarios)
            {
                itemCollection.Add(new Scenario { Title = $"{i++}) {s.Title}", ClassType = s.ClassType });
            }

            ScenarioControl.ItemsSource = itemCollection;

            if (Window.Current.Bounds.Width < 640)
            {
                ScenarioControl.SelectedIndex = -1;
            }
            else
            {
                ScenarioControl.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Called whenever the user changes selection in the scenarios list.  This method will navigate to the respective
        /// sample scenario page.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScenarioControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear the status block when navigating scenarios.
            NotifyUser(String.Empty, NotifyType.StatusMessage);

            ListBox scenarioListBox = sender as ListBox;
            Scenario s = scenarioListBox.SelectedItem as Scenario;
            if (s != null)
            {
                ScenarioFrame.Navigate(s.ClassType);
                if (Window.Current.Bounds.Width < 640)
                {
                    Splitter.IsPaneOpen = false;
                }
            }
        }

        public List<Scenario> Scenarios
        {
            get { return this.scenarios; }
        }

        public Force CurrentForce {

            get {
                    lock (lockem)
                    {
                        return currentForce;
                    }
                }
            set { currentForce = value; }

        }

        /// <summary>
        /// Display a message to the user.
        /// This method may be called from any thread.
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage, type);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
            }
        }

        private void UpdateStatus(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }

			// Raise an event if necessary to enable a screen reader to announce the status update.
			var peer = FrameworkElementAutomationPeer.FromElement(StatusBlock);
			if (peer != null)
			{
				peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
			}
		}

        public void submit(GForceChangedEventArgs e)
        {
            setVisual(e);
            sendPacket(e);
        }
        internal void sendPacket(GForceChangedEventArgs e)
        {
            //gather from current values
            string[] toSend = new string[10];

            toSend[0] = e.accel.X.ToString("n14");
            toSend[1] = e.accel.Y.ToString("n14");
            toSend[2] = e.accel.Z.ToString("n14");
            toSend[3] = e.rotation.X.ToString("n14");
            toSend[4] = e.rotation.Y.ToString("n14");
            toSend[5] = e.rotation.Z.ToString("n14");
            toSend[6] = e.RPM.ToString("n14");
            toSend[7] = e.Speed.ToString("n14");

            string toSendString = getSendString(toSend);

            byte[] data = Encoding.UTF8.GetBytes(toSendString);
            udpClient.Send(data, data.Length);
        }


        private string getSendString(string[] toSend)
        {
            string toSendString = "S:";
            string toSendEndString = "E";

            foreach (var item in toSend)
            {
                toSendString += item + ":";
            }

            toSendString += toSendEndString;
            return toSendString;
        }


        private void setVisual(GForceChangedEventArgs e)
        {
            msX.Push(e.accel.X);
            msY.Push(e.accel.Y);
            msZ.Push(e.accel.Z);

            //CarQuaternion = new Quaternion(e.quaternion.X, e.quaternion.Y, e.quaternion.Z, e.quaternion.W);

            //Xoffset = (int)(msX.Mean * 50);
            //Yoffset = (int)(msY.Mean * 50);
            //Zoffset = (int)(msZ.Mean * 50);

            //if (e.HasOBDData)
            //{
            //    Speed = (int)e.Speed;
            //    Rpm = (int)e.RPM;
            //}
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
     
                Task.Run(() => gForce.OpenGMeter("COM3"));
            }
            catch (Exception ee)
            {
                string t = "";
            }
        }

        private void GForce_GForceChanged(object sender, GForceChangedEventArgs e)
        {
            //if recording, pop this onto the queue for db save

            //lockme
            lock (lockem)
            {
                currentForce.rW = e.quaternion.W;
                currentForce.rX = e.quaternion.X;
                currentForce.rY = e.quaternion.Y;
                currentForce.rZ = e.quaternion.Z;
                currentForce.X = e.accel.X;
                currentForce.Y = e.accel.Y;
                currentForce.Z = e.accel.Z;
            }
         
            //unlock me
        }
        object lockem = new object();

        private void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };
}
