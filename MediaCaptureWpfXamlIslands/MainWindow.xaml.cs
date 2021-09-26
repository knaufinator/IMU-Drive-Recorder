using mPUObserver;
using mPUObserver.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using MathNet.Numerics.Statistics;
using System.Collections.Concurrent;
using EFCore.BulkExtensions;
using Windows.Graphics.Capture;
using Windows.UI.Composition;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics;
using System.Diagnostics;

namespace MediaCaptureWpfXamlIslands
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource tokenSource;
        private MediaCapture mediaCapture;
        private LowLagMediaRecording lowLagMediaRecording;
        private DeviceInformation _cameraDevice;
        private StorageFile videoFile;
        private GForceDevice device;
        private AccurateTimer gameTimer;
        private MPUDataContext dbContext;
        private UdpClient udpClient;
        private bool isRecording = false;
        private static int window = 20;
        private MovingStatistics msX = new MovingStatistics(window);
        private MovingStatistics msY = new MovingStatistics(window);
        private MovingStatistics msZ = new MovingStatistics(window);

        private List<Force> tosaveForces = new List<Force>();
        private int batchSize = 100;

        private string debugConsole;
        private BlockingCollection<Force> queue = new BlockingCollection<Force>();

        DateTime currentVideoRecordingStartTime = DateTime.MinValue;
        private double currentSpeed;
        private double currentRpm;
        private Force currentForce = new Force();


        public MainWindow()
        {
            var t = GraphicsCaptureSession.IsSupported();
            udpClient = new UdpClient();
            udpClient.Connect("127.0.0.1", 20777);
            InitializeComponent();
            resetRecordButtons();
            base.Closing += this.MainWindow_Closing;
            Task.Factory.StartNew(() => init(false));
            var consumer = Task.Factory.StartNew(() => Consumer());
        }
  
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            //clean up non used file if non..
        }

        private async void init(bool startlowlag)
        {
            try
            {
                await InitializeMediaCapture(startlowlag);
                await mediaCapture.StartPreviewAsync();
            }
            catch (Exception err)
            {

            }
        }

        private async Task InitializeMediaCapture(bool startLowLag)
        {
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var list = allVideoDevices.ToList();

            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.Name.Contains("IMX"));
            _cameraDevice = desiredDevice ?? allVideoDevices.FirstOrDefault();

            if (_cameraDevice == null)
            {
                System.Diagnostics.Debug.WriteLine("No camera device found!");
                return;
            }

            mediaCapture = new MediaCapture();
            
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var mics = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
            var tt = mics.ToList();
            var settings = new MediaCaptureInitializationSettings { VideoDeviceId = _cameraDevice.Id, AudioDeviceId = tt[0].Id };
            
            mediaCapture.RecordLimitationExceeded += MediaCapture_RecordLimitationExceeded;
            mediaCapture.Failed += MediaCapture_Failed;
   
            try
            {
                await mediaCapture.InitializeAsync(settings);
                var resolutions = mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord).Select(x => x as VideoEncodingProperties).ToList();
                var rres = resolutions[8];
                await mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, rres);
               // var video = mediaCapture.FrameSources.Last().Value;
                string filenameDate = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
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

                 ContainerVisual root = null;
       

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
                    videoFile = await KnownFolders.VideosLibrary.CreateFileAsync(filenameDate + ".mp4", CreationCollisionOption.GenerateUniqueName);
                    lowLagMediaRecording = await mediaCapture.PrepareLowLagRecordToStorageFileAsync(encodingProfile, videoFile);
                }

                Application.Current.Dispatcher.Invoke(new Action(() => { ((CaptureElement)MyCaptureElement.Child).Source = mediaCapture; }));
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
            string t = "";
        }
        private void Reader_FrameArrived(Windows.Media.Capture.Frames.MediaFrameReader sender, Windows.Media.Capture.Frames.MediaFrameArrivedEventArgs args)
        {
            
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
          
        }

        private  void Button_Click_2(object sender, RoutedEventArgs e)
        {
             StartRecording();
        }

        private async void StartRecording()
        {
            //do this in seperate step, to 
            startRecBtn.IsEnabled = false;
            stopRecBtn.IsEnabled = false;

            try
            {
                await Task.Factory.StartNew(() => init(true));
                await Task.Delay(1000);
                var dir = Path.GetDirectoryName(videoFile.Path);
                var name = Path.GetFileNameWithoutExtension(videoFile.Name);
                var newdbPath = Path.Combine(dir, name + ".db");
                dbContext = new MPUDataContext(newdbPath);
                dbContext.Database.EnsureCreated();

                await Task.Delay(1000);
                //spool up timer, is recording will let it record. 
                gameTimer = new AccurateTimer(new Action(SampleNow), 1);
                await lowLagMediaRecording.StartAsync();
                currentVideoRecordingStartTime = DateTime.Now;
                
                isRecording = true;
                tokenSource = new CancellationTokenSource();
                CancellationToken token = tokenSource.Token;
            }
            catch (Exception rr)
            {
                var t = "";
            }

            startRecBtn.IsEnabled = !isRecording;
            stopRecBtn.IsEnabled = isRecording;
        }

        private void SampleNow()
        {
            //take sample place on queue, 
            if (isRecording)
            {
                TimeSpan elaspsed = currentVideoRecordingStartTime - DateTime.Now;

                long elapseTicks = elaspsed.Ticks;
                currentForce.SampleElapseTime = elapseTicks;
                queue.Add(currentForce);
            }
        }

        public string DebugConsole
        {
            get => debugConsole;
            set
            {
                debugConsole = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged()
        { 
        }
     

        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            try
            {
                StopRecording();
            }
            catch (Exception err)
            {

            }
        }

        private async void StopRecording()
        {
            startRecBtn.IsEnabled = false;
            stopRecBtn.IsEnabled = false;

            try
            {
                if (lowLagMediaRecording != null)
                {
                    await lowLagMediaRecording.StopAsync();
                    await lowLagMediaRecording.FinishAsync();
                    isRecording = false;
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

                    await Task.Factory.StartNew(() => init(false));
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

            resetRecordButtons();
        }

        private void resetRecordButtons()
        {
            startRecBtn.IsEnabled = !isRecording;
            stopRecBtn.IsEnabled = isRecording;
        }
        private void MyCaptureElement_ChildChanged(object sender, EventArgs e)
        {
            string t = "";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() => init(false));
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            //start imu
            startIMU();
        }
        internal void stopIMU()
        {
            if (device != null)
            {
                device.Close();
                device = null;
            }
        }

        public void startIMU()
        {
            stopIMU();

            device = new GForceDevice();
            device.GForceChanged += Device_GForceChanged;
            try
            {
                device.OpenGMeter("COM3");
            }
            catch (Exception e)
            {

            }
        }

        private void Device_GForceChanged(object sender, GForceChangedEventArgs e)
        {
            submit(e);
        }

        public void submit(GForceChangedEventArgs e)
        {
            //filter
            currentForce = filter(e,currentSpeed,currentRpm);

         //   setVisual(currentForce);
         //   sendUDPPacket(currentForce);
        }

        private void setVisual(GForceChangedEventArgs e)
        {
           
        }

        private Force filter(GForceChangedEventArgs e,double currentSpeed, double rpm)
        {
            msX.Push(e.accel.X);
            msY.Push(e.accel.Y);
            msZ.Push(e.accel.Z);

            Force force = new Force();
            force.X = (Double.IsNaN(msX.Mean)) ? 0 : msX.Mean;
            force.Y = (Double.IsNaN(msY.Mean)) ? 0 : msY.Mean;
            force.Z = (Double.IsNaN(msZ.Mean)) ? 0 : msZ.Mean;

            force.rX = e.quaternion.X;
            force.rY = e.quaternion.Y;
            force.rZ = e.quaternion.Z;
            force.rW = e.quaternion.W;

            force.Speed = currentSpeed;
            force.RPM = rpm;
            force.time = DateTime.Now.Ticks;

            return force;
        }

        private void Consumer()
        {
            try
            {
                foreach (var item in queue.GetConsumingEnumerable())
                {
                    if (tosaveForces.Count < batchSize)
                        tosaveForces.Add(item);

                    if (tosaveForces.Count >= batchSize)
                    {
                        dbContext.BulkInsert(tosaveForces);
                        dbContext.SaveChanges();
                        tosaveForces.Clear();
                    }
                }
            }
            catch (Exception e)
            {
                String t = "";//derrr
            }
        }
        /// <summary>
        /// UDP packet for Simtools consumption, whatever is playing on the visual will be output,
        /// so either real time, or playback.
        /// </summary>
        /// <param name="e"></param>
        internal void sendUDPPacket(Force e)
        {
            //gather from current values
            string[] toSend = new string[10];

            toSend[0] = e.X.ToString("n14");
            toSend[1] = e.Y.ToString("n14");
            toSend[2] = e.Z.ToString("n14");
            toSend[3] = e.rX.ToString("n14");
            toSend[4] = e.rY.ToString("n14");
            toSend[5] = e.rZ.ToString("n14");
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

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            //open overlaywindow
            try
            {
                OverlayWindow overlayWindow = new OverlayWindow();
                overlayWindow.Show();
            }
            catch (Exception ed)
            {
                string t = "";
            }


        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            //open player
            try
            {
                Player player = new Player();
                player.Show();
            }
            catch (Exception ed)
            {
                var t = "";
            }
        }
    }

    public static class Direct3D11Helpers
    {
        internal static Guid IInspectable = new Guid("AF86E2E0-B12D-4c6a-9C5A-D7AA65101E90");
        internal static Guid ID3D11Resource = new Guid("dc8e63f3-d12b-4952-b47b-5e45026a862d");
        internal static Guid IDXGIAdapter3 = new Guid("645967A4-1392-4310-A798-8053CE3E93FD");
        internal static Guid ID3D11Device = new Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140");
        internal static Guid ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        [DllImport(
            "d3d11.dll",
            EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall
            )]
        internal static extern UInt32 CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [DllImport(
            "d3d11.dll",
            EntryPoint = "CreateDirect3D11SurfaceFromDXGISurface",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall
            )]
        internal static extern UInt32 CreateDirect3D11SurfaceFromDXGISurface(IntPtr dxgiSurface, out IntPtr graphicsSurface);

        public static IDirect3DDevice CreateD3DDevice()
        {
            return CreateD3DDevice(false);
        }

        public static IDirect3DDevice CreateD3DDevice(bool useWARP)
        {
            var d3dDevice = new SharpDX.Direct3D11.Device(
                useWARP ? SharpDX.Direct3D.DriverType.Software : SharpDX.Direct3D.DriverType.Hardware,
                SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);
            IDirect3DDevice device = null;

            // Acquire the DXGI interface for the Direct3D device.
            using (var dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device3>())
            {
                // Wrap the native device using a WinRT interop object.
                uint hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown);

                if (hr == 0)
                {
                    device = Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DDevice;
                    Marshal.Release(pUnknown);
                }
            }

            return device;
        }


        internal static IDirect3DSurface CreateDirect3DSurfaceFromSharpDXTexture(SharpDX.Direct3D11.Texture2D texture)
        {
            IDirect3DSurface surface = null;

            // Acquire the DXGI interface for the Direct3D surface.
            using (var dxgiSurface = texture.QueryInterface<SharpDX.DXGI.Surface>())
            {
                // Wrap the native device using a WinRT interop object.
                uint hr = CreateDirect3D11SurfaceFromDXGISurface(dxgiSurface.NativePointer, out IntPtr pUnknown);

                if (hr == 0)
                {
                    surface = Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DSurface;
                    Marshal.Release(pUnknown);
                }
            }

            return surface;
        }



        internal static SharpDX.Direct3D11.Device CreateSharpDXDevice(IDirect3DDevice device)
        {
            var access = (IDirect3DDxgiInterfaceAccess)device;
            var d3dPointer = access.GetInterface(ref Direct3D11Helpers.ID3D11Device);
            var d3dDevice = new SharpDX.Direct3D11.Device(d3dPointer);
            return d3dDevice;
        }

        internal static SharpDX.Direct3D11.Texture2D CreateSharpDXTexture2D(IDirect3DSurface surface)
        {
            var access = (IDirect3DDxgiInterfaceAccess)surface;
            var d3dPointer = access.GetInterface(ref ID3D11Texture2D);
            var d3dSurface = new SharpDX.Direct3D11.Texture2D(d3dPointer);
            return d3dSurface;
        }

    
        public static SharpDX.Direct3D11.Texture2D InitializeComposeTexture(
            SharpDX.Direct3D11.Device sharpDxD3dDevice,
            SizeInt32 size)
        {
            var description = new SharpDX.Direct3D11.Texture2DDescription
            {
                Width = size.Width,
                Height = size.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SharpDX.DXGI.SampleDescription()
                {
                    Count = 1,
                    Quality = 0
                },
                Usage = SharpDX.Direct3D11.ResourceUsage.Default,
                BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget,
                CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None,
                OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None
            };
            var composeTexture = new SharpDX.Direct3D11.Texture2D(sharpDxD3dDevice, description);


            using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(sharpDxD3dDevice, composeTexture))
            {
                sharpDxD3dDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));
            }

            return composeTexture;
        }
    }
    interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    };
}
