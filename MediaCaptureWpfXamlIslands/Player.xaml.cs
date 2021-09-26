using mPUObserver.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace MediaCaptureWpfXamlIslands
{
    /// <summary>
    /// Interaction logic for Player.xaml
    /// </summary>
    public partial class Player : Window
    {
   
        public Player()
        {
            InitializeComponent();
        }

        void StopButton_Click(object sender, RoutedEventArgs e)
        {
           // stop();
        }

        private void stop()
        {

          
        }

        private void Window_StateChanged_1(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.Visibility = Visibility.Collapsed;
                this.Topmost = true;
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.Visibility = Visibility.Visible;
              //  test.Visibility = Visibility.Collapsed;
            }
            else
            {
              //  test.Visibility = Visibility.Visible;
                this.Topmost = false;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.ResizeMode = ResizeMode.CanResize;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
    //        openFileDialog.InitialDirectory = KnownFolders.VideosLibrary.Path;


            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
             //   _vm.LoadFile(openFileDialog.FileName);

                
            }
        }
    }
}
