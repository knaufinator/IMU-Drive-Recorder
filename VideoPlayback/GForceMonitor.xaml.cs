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

using System;
using Windows.Media.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Windows.Media.MediaProperties;
using mPUObserver;
using mPUObserver.Helpers;
using System.Collections.Concurrent;

namespace SDKTemplate
{
    /// <summary>
    /// Demonstrates closed captions delivered in-band, specifically SRT in an MKV.
    /// </summary>
    public sealed partial class GForceMonitor : Page
    {
        //private bool isRecording = false;
        private DateTime currentVideoRecordingStartTime = DateTime.MinValue;
        private AccurateTimer gameTimer;
        private MainPage rootPage = MainPage.Current;
        private CancellationTokenSource tokenSource;
        private MediaCapture mediaCapture;
        private LowLagMediaRecording lowLagMediaRecording;
        private DeviceInformation _cameraDevice;
        private StorageFile videoFile;
        private MPUDataContext dbContext;
        private BlockingCollection<Force> queue = new BlockingCollection<Force>();

        public GForceMonitor()
        {
            this.InitializeComponent();
        }
        private void SampleNow()
        {
            //take sample place on queue, 
            if (rootPage.isRecording)
            {
                TimeSpan elaspsed = currentVideoRecordingStartTime - DateTime.Now;

                long elapseTicks = elaspsed.Ticks;

                //grab copy of current sample from root
                var currentForce = rootPage.CurrentForce;

                currentForce.TicksSinceStart = elapseTicks;
                queue.Add(currentForce);
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }
       
        private async Task InitializeMediaCapture(bool startLowLag)
        {
            try
            {
                var resolutions = mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord).Select(x => x as VideoEncodingProperties).ToList();
                var rres = resolutions[8];
                await mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, rres);
             
                string filenameDate = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");

                string filenameDateMp4 = filenameDate + ".mp4";
                string filenameDateDB = filenameDate + ".db";
                var dbPath = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, filenameDateDB);

                var profile = new MediaEncodingProfile();
                profile.Container = new ContainerEncodingProperties();
                profile.Container.Subtype = MediaEncodingSubtypes.Mpeg4;

                List<VideoStreamDescriptor> videoStreams = new List<VideoStreamDescriptor>();
                List<AudioStreamDescriptor> audioStreams = new List<AudioStreamDescriptor>();

                var encodeProps = VideoEncodingProperties.CreateH264();
                encodeProps.Subtype = MediaEncodingSubtypes.H264;
                var stream1Desc = new VideoStreamDescriptor(encodeProps);
                stream1Desc.Label = "Main";
                stream1Desc.Name = "Main";
                videoStreams.Add(stream1Desc);

                profile.SetVideoTracks(videoStreams);

                AudioEncodingProperties encodingProps1 = AudioEncodingProperties.CreatePcm(44100, 2, 32);
                encodingProps1.Subtype = MediaEncodingSubtypes.Ac3;
                var audioStreamDescriptor = new AudioStreamDescriptor(encodingProps1);
                audioStreamDescriptor.Name = "Engine";
                audioStreamDescriptor.Label = "Engine";
                audioStreams.Add(audioStreamDescriptor);
                profile.SetAudioTracks(audioStreams);
                var encodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);

                if (startLowLag)
                {
                    var videoPath = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, filenameDateMp4);

                    // await StorageFile..CreateFileAsync(videoPath);

                    //   System.IO.File.Create(videoPath);
                    //var videoFile = await StorageFile.GetFileFromPathAsync(videoPath);


                    // videoFile = await KnownFolders.VideosLibrary.CreateFileAsync(filenameDateMp4, CreationCollisionOption.ReplaceExisting);

                   
                    var myFolder = await StorageFolder.GetFolderFromPathAsync(ApplicationData.Current.LocalFolder.Path);
                    videoFile = await myFolder.CreateFileAsync(filenameDateMp4, Windows.Storage.CreationCollisionOption.ReplaceExisting);
                 
                    
                    dbContext = new MPUDataContext(dbPath);
                    var dbcreateresult = await dbContext.Database.EnsureCreatedAsync();
                    lowLagMediaRecording = await mediaCapture.PrepareLowLagRecordToStorageFileAsync(encodingProfile, videoFile);
                }

                mediaCaptureElement.Source = mediaCapture;
            }
            catch (Exception err)
            {
                return;
            }
        }

        private void MediaCapture_RecordLimitationExceeded(MediaCapture sender)
        {
            string t = "";
        }

        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
          
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            mediaCapture.Dispose();
        }

        private async void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await init(false);
        }

        private async Task init(bool initRecording)
        {
            try
            {
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                var mics = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
                var tt = mics.ToList();
                var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                var list = allVideoDevices.ToList();

                DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.Name.Contains("IMX"));
                _cameraDevice = desiredDevice ?? allVideoDevices.FirstOrDefault();

                if (_cameraDevice == null)
                {
                    System.Diagnostics.Debug.WriteLine("No camera device found!");
                    return;
                }
                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = _cameraDevice.Id, AudioDeviceId = tt[0].Id };

                mediaCapture = new MediaCapture();
                mediaCapture.RecordLimitationExceeded += MediaCapture_RecordLimitationExceeded;
                mediaCapture.Failed += MediaCapture_Failed;
                await mediaCapture.InitializeAsync(settings);

                mediaCaptureElement.Source = mediaCapture;
                await InitializeMediaCapture(initRecording);
                await mediaCapture.StartPreviewAsync();
            }
            catch (Exception ee)
            {
                var t = "";
            }
        }

        private void Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            StartRecording();
        }
        private void Button_Click_1(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            StopRecording();
        }

        private async void StartRecording()
        {
            await init(true);

            gameTimer = new AccurateTimer(new Action(SampleNow), 1);
            await lowLagMediaRecording.StartAsync();
            currentVideoRecordingStartTime = DateTime.Now;

            rootPage.isRecording = true;
            tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
        }
        
        private async void StopRecording()
        {
            try
            {
                if (lowLagMediaRecording != null)
                {
                    await lowLagMediaRecording.StopAsync();
                    await lowLagMediaRecording.FinishAsync();
                    rootPage.isRecording = false;
                    mediaCapture.Dispose();

                    if (gameTimer != null)
                        gameTimer.Stop();

                    gameTimer = null;

                    try
                    {
                        if (tokenSource != null)
                            tokenSource.Cancel();
                        tokenSource = null;
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        dbContext.Dispose();
                    }
                    catch (Exception)
                    {

                    }

                    await init(false);
                }
            }
            catch (NullReferenceException err)
            {
                var t = "";
            }
            catch (Exception err)
            {
                var t = "";
            }
        }

  
    }
}
