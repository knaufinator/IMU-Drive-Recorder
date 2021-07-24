using Gma.System.MouseKeyHook;
using LibVLCSharp.Shared;
using mPUObserver.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace mPUObserver
{
    /// <summary>
    /// Interaction logic for Player.xaml
    /// </summary>
    public partial class Player : Window
    {
        PlayerViewModel _vm;
        public Player(MainWindowViewModel vm)
        {
            InitializeComponent();
            _vm = (PlayerViewModel)this.DataContext;
            _vm.loadMainViewModel(vm);
        }

        void StopButton_Click(object sender, RoutedEventArgs e)
        {
           // stop();
        }

        private void stop()
        {

            //if (gameTimer != null)
            //    gameTimer.Stop();

            //if (VideoView.MediaPlayer.IsPlaying)
            //{
            //    VideoView.MediaPlayer.Stop();
            //}
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
                test.Visibility = Visibility.Collapsed;
            }
            else
            {
                test.Visibility = Visibility.Visible;
                this.Topmost = false;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.ResizeMode = ResizeMode.CanResize;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _vm.LoadFile(openFileDialog.FileName);
            }
        }
    }
}
