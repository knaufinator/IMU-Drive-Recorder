using GalaSoft.MvvmLight.Command;
using mPUObserver;
using mPUObserver.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SDKTemplate
{
    public class RecordingViewModel : INotifyPropertyChanged
    {
        private readonly DisplayRequest _displayRequest = new DisplayRequest();
        private bool _mirroringPreview;
        private bool _externalCamera;
        private bool _isInitialized = false;
        private bool _isRecording;
        private DateTime currentVideoRecordingStartTime = DateTime.MinValue;
        private AccurateTimer gameTimer;
        private MainPage rootPage = MainPage.Current;
        private CancellationTokenSource tokenSource;
        private CaptureElement _captureElement;
        private MediaCapture _mediaCapture;
        private LowLagMediaRecording lowLagMediaRecording;
        private DeviceInformation _cameraDevice1;
        private DeviceInformation _audioDevice1;
        private StorageFile cam1VideoFile;
        private MPUDataContext dbContext;
        private BlockingCollection<Force> queue = new BlockingCollection<Force>();
        private ObservableCollection<DeviceInformation> allVideoDevices = new ObservableCollection<DeviceInformation>();
        private ObservableCollection<DeviceInformation> allAudioDevices = new ObservableCollection<DeviceInformation>();


        private DeviceInformation selectedVideoDevice;
        private DeviceInformation selectedAudioDevice;



        public RecordingViewModel()
        {
            if (_mediaCapture == null)
            {
                _mediaCapture = new MediaCapture();
          
            }
          
        }
        private RelayCommand _loadedCommand;
        public RelayCommand LoadedCommand
        {
            get
            {
                return _loadedCommand
                    ?? (_loadedCommand = new RelayCommand(
                           async () =>
                           {
                               await LoadDevices();
                               await Init();
                           }));
            }
        }

        private async Task Init()
        {
            if (SelectedVideoDevice == null | SelectedAudioDevice == null)
            {
                Debug.WriteLine("No device found!");
                return;
            }

            var settings = new MediaCaptureInitializationSettings { VideoDeviceId = SelectedVideoDevice.Id, AudioDeviceId = SelectedAudioDevice.Id };
           
            try
            {
                await _mediaCapture.InitializeAsync(settings);
                _isInitialized = true;
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("The app was denied access to the camera");
            }

            // If initialization succeeded, start the preview
            if (_isInitialized)
            {
                _displayRequest.RequestActive();
                CaptureElement.Source = _mediaCapture;
                await _mediaCapture.StartPreviewAsync();
            }
        }
        
        private async Task LoadDevices()
        {
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            AllVideoDevices.Clear();
            foreach (var item in allVideoDevices)
            {
                AllVideoDevices.Add(item);
            }

            var allMicDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
            AllAudioDevices.Clear();
            foreach (var item in allMicDevices)
            {
                AllAudioDevices.Add(item);
            }

            DeviceInformation desiredVideoDevice = allVideoDevices.FirstOrDefault(x => x.Name.Contains("IMX"));
            DeviceInformation desiredMicDevice = allMicDevices.FirstOrDefault(x => x.Name.Contains("FDUCE"));
          
            SelectedVideoDevice = desiredVideoDevice ?? allVideoDevices.FirstOrDefault();
            SelectedAudioDevice = desiredMicDevice ?? allMicDevices.FirstOrDefault();
        }




        public DeviceInformation SelectedVideoDevice
        {
            get => selectedVideoDevice;
            set
            {
                selectedVideoDevice = value;
                OnPropertyChanged();
            }
        }

        public DeviceInformation SelectedAudioDevice
        {
            get => selectedAudioDevice;
            set
            {
                selectedAudioDevice = value;
                OnPropertyChanged();
            }
        }
        public MediaCapture MediaCaptureSource
        {
            get => _mediaCapture;
            set
            {
                _mediaCapture = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DeviceInformation> AllVideoDevices
        {
            get => allVideoDevices;
            set
            {
                allVideoDevices = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DeviceInformation> AllAudioDevices
        {
            get => allAudioDevices;
            set
            {
                allAudioDevices = value;
                OnPropertyChanged();
            }
        }
         
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        public event PropertyChangedEventHandler PropertyChanged;
        private async Task InitializeMediaCapture(bool startLowLag)
        {
            try
            {
                //cam1
                var cam1ResList = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord).Select(x => x as VideoEncodingProperties).ToList();
                var cam1Res = cam1ResList[8];//choose better here...
                await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, cam1Res);
                string filenameDate = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
                string cam1filenameDateMp4 = filenameDate + ".mp4";
                string filenameDateDB = filenameDate + ".db";

                var encodeProps1 = VideoEncodingProperties.CreateH264();
                encodeProps1.Subtype = MediaEncodingSubtypes.H264;


                var stream1Desc = new VideoStreamDescriptor(encodeProps1);
                stream1Desc.Label = "Main";
                stream1Desc.Name = "Main";

                List<VideoStreamDescriptor> cam1videoStreams = new List<VideoStreamDescriptor>();
                cam1videoStreams.Add(stream1Desc);

                var cam1profile = new MediaEncodingProfile();
                cam1profile.Container = new ContainerEncodingProperties();
                cam1profile.Container.Subtype = MediaEncodingSubtypes.Mpeg4;
                cam1profile.SetVideoTracks(cam1videoStreams);

                List<AudioStreamDescriptor> cam1audioStreams = new List<AudioStreamDescriptor>();
                AudioEncodingProperties encodingProps1 = AudioEncodingProperties.CreatePcm(44100, 2, 32);

                encodingProps1.Subtype = MediaEncodingSubtypes.Ac3;

                var audioStreamDescriptor = new AudioStreamDescriptor(encodingProps1);
                audioStreamDescriptor.Name = "Engine";
                audioStreamDescriptor.Label = "Engine";
                cam1audioStreams.Add(audioStreamDescriptor);
                cam1profile.SetAudioTracks(cam1audioStreams);

                if (startLowLag)
                {
                    var encodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);

                    var myFolder = await StorageFolder.GetFolderFromPathAsync(ApplicationData.Current.LocalFolder.Path);
                    var dbPath = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, filenameDateDB);

                    cam1VideoFile = await myFolder.CreateFileAsync(cam1filenameDateMp4, Windows.Storage.CreationCollisionOption.ReplaceExisting);
                    dbContext = new MPUDataContext(dbPath);

                    var dbcreateresult = await dbContext.Database.EnsureCreatedAsync();
                    lowLagMediaRecording = await _mediaCapture.PrepareLowLagRecordToStorageFileAsync(encodingProfile, cam1VideoFile);
                }
            }
            catch (Exception err)
            {
                var t = "";
            }



        }

        public CaptureElement CaptureElement
        {
            get
            {
                if (_captureElement == null) _captureElement = new CaptureElement();
                return _captureElement;
            }
            set
            {
                _captureElement = value;
                OnPropertyChanged();
            }
        }

        private RelayCommand startRecordingCommand;

        public ICommand StartRecordingCommand
        {
            get
            {
                if (startRecordingCommand == null)
                {
                    startRecordingCommand = new RelayCommand(StartRecording);
                }

                return startRecordingCommand;
            }
        }

        private async void StartRecording()
        {
            await InitializeMediaCapture(true);
            
            if(lowLagMediaRecording != null)
                await lowLagMediaRecording.StartAsync();
        }

        private RelayCommand stopRecordingCommand;

        public ICommand StopRecordingCommand
        {
            get
            {
                if (stopRecordingCommand == null)
                {
                    stopRecordingCommand = new RelayCommand(StopRecording);
                }

                return stopRecordingCommand;
            }
        }

        private async void StopRecording()
        {
            if(lowLagMediaRecording != null)
                await lowLagMediaRecording.StopAsync();
        }

        private RelayCommand initCommand;

        public ICommand InitCommand
        {
            get
            {
                if (initCommand == null)
                {
                    initCommand = new RelayCommand(Reinit);
                }

                return initCommand;
            }
        }

        private async void Reinit()
        {
            MediaCaptureSource.Dispose();
            MediaCaptureSource = new MediaCapture();

            await Init();
        }
    }
}
