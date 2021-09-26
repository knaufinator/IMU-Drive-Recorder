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

using mPUObserver;
using mPUObserver.Helpers;
using System;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Linq;
using System.Threading.Tasks;

namespace SDKTemplate
{
    /// <summary>
    /// Demonstrates basic video playback using MediaElement.
    /// </summary>
    public sealed partial class Playback : Page
    {
        private MediaPlaybackState lastState;
        private AccurateTimer gameTimer;
        private MPUDataContext dbContext;
        private MainPage rootPage = MainPage.Current;
        DateTime startTime = DateTime.MinValue;
        public Playback()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            MediaPlayerHelper.CleanUpMediaPlayerSource(mediaPlayerElement.MediaPlayer);
        }

        /// <summary>
        /// Handles the pick file button click event to load a video.
        /// </summary>
        private async void pickFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous returned file name, if it exists, between iterations of this scenario
            rootPage.NotifyUser("", NotifyType.StatusMessage);

            // Create and open the file picker
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            openPicker.FileTypeFilter.Add(".mp4");

            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                rootPage.NotifyUser("Picked video: " + file.Name, NotifyType.StatusMessage);
                this.mediaPlayerElement.MediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
                mediaPlayerElement.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
                mediaPlayerElement.MediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
                TryLoadDb(file);
            }
        }

        private void TryLoadDb(StorageFile file)
        {
            try
            {
                //load database here.
                string dbPath = System.IO.Path.GetDirectoryName(file.Path);
                string dbFile = System.IO.Path.GetFileNameWithoutExtension(file.Path);
                string dbFileFullPath = System.IO.Path.Combine(dbPath, dbFile + ".db");

             
                if (true)
                {

                    dbContext = new MPUDataContext(dbFileFullPath);
                    //  dbContext.Database.EnsureCreated();

                    //notify user of db
                    rootPage.NotifyUser("db loaded", NotifyType.StatusMessage);
                }
                else
                {
                    rootPage.NotifyUser("No DB File", NotifyType.ErrorMessage);
                }
               
            }
            catch (Exception err)
            {
                //notify user of db
                rootPage.NotifyUser("Err:" + err.Message, NotifyType.ErrorMessage);
            }
        }

        public async Task<bool> IsFilePresent(string fileName)
        {
            var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(fileName);
            return item != null;
        }

        private void PlaybackSession_PositionChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            TimeSpan pos = sender.Position;

            long ticksPos = pos.Ticks;

            //find first in stack matching this time.

            var t = "";
        }

        private void SubmitForce(Force force)
        {
            //send this to vis side
            GForceChangedEventArgs e = new GForceChangedEventArgs();
            e.accel = new System.Numerics.Vector3((float)force.X, (float)force.Y, (float)force.Z);
            e.quaternion = new System.Numerics.Quaternion((float)force.rX, (float)force.rY, (float)force.rZ, (float)force.rW);
            e.rotation = new System.Numerics.Vector3((float)force.rX, (float)force.rY, (float)force.rZ);


            e.HasOBDData = true;
            e.Speed = force.Speed;
            e.RPM = force.RPM;
            rootPage.submit(e);
        }

        private async void SampleNow()
        {
            try
            {
                var time = await getCurrentTime();

                var item3 = dbContext.Forces.Where(a => a.TicksSinceStart <= time).OrderBy(i => i.TicksSinceStart).FirstOrDefault();

                if (item3 != null)
                    SubmitForce(item3);
            }
            catch (Exception err)
            {
                var t = "";
            }
        }
        public async Task<long> getCurrentTime()
        {
            long currentTime = 0;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
            () =>
            {
                currentTime = mediaPlayerElement.MediaPlayer.PlaybackSession.Position.Ticks;
            }).AsTask();

            return currentTime;
        }


        private void PlaybackSession_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            if (lastState != sender.PlaybackState && sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
            {
                //started playing, lets load stuff...
                StartGameTimer();
            }
            if (lastState != sender.PlaybackState && sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Paused)
            {
                StopGameTimer();
            }
            if (lastState != sender.PlaybackState && sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.None)
            {
               //unsupported event, after opening..
                StopGameTimer();
            }

            lastState = sender.PlaybackState;
        }

        private void StartGameTimer()
        {
            startTime = DateTime.Now;
            gameTimer = new AccurateTimer(new Action(SampleNow), 10);
        }

        private void StopGameTimer()
        {
            if (gameTimer != null)
                gameTimer.Stop();
        }

        private void MediaPlayer_VideoFrameAvailable(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            var r = "";
        }

        private void SystemMedMediaPlaybackItemiaTransportControls_PropertyChanged(Windows.Media.SystemMediaTransportControls sender, Windows.Media.SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            var t = args;
        }
    }
}
