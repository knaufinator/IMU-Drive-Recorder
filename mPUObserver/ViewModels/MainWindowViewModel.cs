using EFCore.BulkExtensions;
using OBD.NET.Common.Devices;
using OBD.NET.Common.Logging;
using OBD.NET.Common.OBDData;
using ODB.NET.Desktop.Logging;
using HelixToolkit.Wpf.SharpDX;
using MathNet.Numerics.Statistics;
using mPUObserver.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using GeometryModel3D = System.Windows.Media.Media3D.GeometryModel3D;
using MeshGeometry3D = System.Windows.Media.Media3D.MeshGeometry3D;
using StLReader = HelixToolkit.Wpf.StLReader;
using OBS.WebSocket.NET;
using OBD.NET.Communication;
using System.Threading;
using System.Diagnostics;
using ODB.NET.Desktop.Communication;
using System.Reactive.Linq;
using System.Reactive;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace mPUObserver
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private UdpClient udpClient;
        private SerialConnection connection;
        private ELM327 dev;

        protected ObsWebSocket _obs;
        private bool isRecording = false;
        private static int window = 20;
        private GForceDevice device;
        private AccurateTimer gameTimer;
        private MPUDataContext dbContext;

        private MovingStatistics msX = new MovingStatistics(window);
        private MovingStatistics msY = new MovingStatistics(window);
        private MovingStatistics msZ = new MovingStatistics(window);
        private List<Force> tosaveForces = new List<Force>();
        private int batchSize = 100;
        private int _Xoffset;
        private int _Yoffset;
        private int _Zoffset;
        private int speed;
        private int rpm;
        private int oilTemp;
        private int engineTemp;
        private int throttlePosition;
        private int brakePosition;
        private string CurrentSessionID;
        private Quaternion quaternion;
        private string debugConsole;
        private BlockingCollection<Force> queue = new BlockingCollection<Force>();
        private CancellationTokenSource tokenSource;
        public SceneNodeGroupModel3D GroupModel { get; } = new SceneNodeGroupModel3D();
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
     
        private Task switchTask;

        public MainWindowViewModel()
        {
            //used to com to simtools,.. or anyone that wants to listen .. i suppose..
            udpClient = new UdpClient();
            udpClient.Connect("127.0.0.1", 20777);
            LoadCameraScenes();

            _obs = new ObsWebSocket();
            Load3dModels();
           
            var consumer = Task.Factory.StartNew(() => Consumer());
        }

        private void LoadCameraScenes()
        {
            //set up  camera scenes, for auto switching
            CamItem cam = new CamItem();
            cam.Name = "1";
            cam.MaxSeconds = 10;
            cam.IRname = "Main IR";
            cam.IRname2 = "Main IR 2";
            cam.MinSeconds = 7;
            cam.TransitionName = "Cut";
            cam.Duration = 500;
            camItems.Add(cam);

            cam = new CamItem();
            cam.Name = "2";
            cam.MaxSeconds = 10;
            cam.IRname = "Main IR";
            cam.IRname2 = "Main IR 2";
            cam.MinSeconds = 7;
            cam.TransitionName = "Cut";
            cam.Duration = 500;
            camItems.Add(cam);
        }

        private void Load3dModels()
        {
            string file = @".\Resources\brz2.stl";
            Model3DGroup MdlGrp = null;
            StLReader ObjRed = new StLReader();
            MdlGrp = ObjRed.Read(file);
            GeometryModel3D gm3d = MdlGrp.Children[0] as GeometryModel3D;
            MeshGeo = gm3d.Geometry as MeshGeometry3D;

            string file2 = @".\Resources\circle.stl";
            Model3DGroup MdlGrp2 = null;
            StLReader ObjRed2 = new StLReader();
            MdlGrp2 = ObjRed2.Read(file2);
            GeometryModel3D gm3d2 = MdlGrp2.Children[0] as GeometryModel3D;
            MeshGeso = gm3d2.Geometry as MeshGeometry3D;
        }

        /// <summary>
        /// UDP packet for Simtools consumption, whatever is playing on the visual will be output,
        /// so either real time, or playback.
        /// </summary>
        /// <param name="e"></param>
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


        public void StartRecording()
        {
            //we want to create a new db, we need to then tell obs to start recording
            DateTime start = DateTime.Now;

            if (!_obs.IsConnected)
                Connect();

            try
            {
                _obs.Api.StopRecording();
                Thread.Sleep(100);
            }
            catch (Exception err)
            {
            }

            try
            {
                setTransition("Stinger", 500);
                _obs.Api.SetCurrentScene("1");

                Console.Beep(1000, 1000);
                Thread.Sleep(1000);

                string folder = _obs.Api.GetRecordingFolder();

                FileSystemWatcher fw = new FileSystemWatcher(folder);
                fw.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                fw.Created += fileSystemWatcher_Created;
                fw.EnableRaisingEvents = true;
                   
                _obs.Api.StartRecording();
            }
            catch (Exception err)
            {
                Console.Beep(500, 4000);
            }
        }

        /// <summary>
        /// watch the video filesystem, when new mkv file is created, this means that OBS just started recording, and we want to sync up to it. 
        /// grab the filename, and start datalogging in a db with the same name, so its easy to link
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                if (Path.GetExtension(e.Name).ToLower().CompareTo(".mkv") == 0)
                {

                    if (CurrentSessionID != null && CurrentSessionID.CompareTo(Path.GetFileNameWithoutExtension(e.Name)) == 0)
                        return;//we already started this session...

                    CurrentSessionID = Path.GetFileNameWithoutExtension(e.Name);
                    //make the new sqlite file this name... then the mp4 and db files will match.
                    string fileName = CurrentSessionID + ".db";

                    string fulldbFilePath = Path.Combine(_obs.Api.GetRecordingFolder(), fileName);

                    dbContext = new MPUDataContext(fulldbFilePath);
                    dbContext.Database.EnsureCreated();

                    isRecording = true;
                    gameTimer = new AccurateTimer(new Action(SampleNow), 1);
                  
                    tokenSource = new CancellationTokenSource();
                    CancellationToken token = tokenSource.Token;

                    try
                    {

                        //run this if you want cams to switch
                       // switchTask = Task.Run(() => CamSwitcher(token), token);
                    }
                    catch (Exception err)
                    {
                    }
                }
            }
        }

        internal void StopOBD()
        {

            if (connection != null)
            {
                connection.Dispose();
                connection = null;
            }

            if (dev != null)
            {
                dev.Dispose();
                dev = null;
            }

        }

        internal void CalibrateIMU()
        {
            device.sendKeyMessage("1");
        }

        private readonly object syncLock = new object();
        private CamItem currentCamItem;
        private void CamSwitcher(CancellationToken token)
        {

            try
            {
                if (token.IsCancellationRequested)
                    return;

                //check last time we switched
                if (canSwitch())
                {
                    //select cam to be presented.
                    lastCamSwitch = DateTime.Now;
                    lock (syncLock)//expand out, with more logic? time based desisions...
                    {
                        currentCamItem = chooseCam();

                        setTransition(currentCamItem.TransitionName, currentCamItem.Duration);
                        _obs.Api.SetCurrentScene(currentCamItem.Name);
                    }


                    //delay init
                    Random r = new Random();
                    int rInt = r.Next(currentCamItem.MinSeconds, currentCamItem.MaxSeconds); //for ints

                    Task.Delay(rInt).ContinueWith(t => CamSwitcher(token));
                }
                else
                {
                    //try again in seconds there must have been another event driven switch
                    Task.Delay(9000).ContinueWith(t => CamSwitcher(token));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

        }
        private List<CamItem> camItems = new List<CamItem>();
        private CamItem chooseCam()
        {

            int index = camItems.IndexOf(currentCamItem);

            if (index >= camItems.Count() - 1)
                index = 0;
            else
                index++;

            return camItems[index];
        }

        private int MinSecondsSinceLastEventForSceneSwitch = 12;
        private DateTime lastCamSwitch = DateTime.MinValue;

        private bool canSwitch()
        {
            double secondsSinceLastSwitch = (double)(DateTime.Now - lastCamSwitch).TotalSeconds;


            //dont switch if we are in high heart state, or its been so soon since last switch
            if (secondsSinceLastSwitch > MinSecondsSinceLastEventForSceneSwitch)
                return true;
            else
                return false;
        }

        private void setTransition(string name, int duration)
        {
            string transitionName = name;
            var transitions = _obs.Api.GetTransitionList();
            var targetTransition = transitions.Transitions.FirstOrDefault(i => i.Name == transitionName);

            if (targetTransition != null)
            {
                _obs.Api.SetTransitionDuration(duration);
                _obs.Api.SetCurrentTransition(transitionName);
            }
        }

        internal async void clearDB()
        {
            await dbContext.Forces.BatchDeleteAsync();
            await dbContext.SaveChangesAsync();
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

        public void stopRecording()
        {
            isRecording = false;
          
            if(gameTimer != null)
                gameTimer.Stop();
            
            gameTimer = null;

            try
            {
                if(tokenSource != null)
                    tokenSource.Cancel();
                tokenSource = null;
            }
            catch (Exception)
            {
            }


            try
            {
                if(_obs != null && _obs.IsConnected)
                    _obs.Api.StopRecording();
            }
            catch (Exception)
            {
            }
        }

        private void SampleNow()
        {
            //take sample place on queue, 
            if (isRecording)
            {
                Force force = new Force();
                force.X = msX.Mean;
                force.Y = msY.Mean;
                force.Z = msZ.Mean;
                force.rX = CarQuaternion.X;
                force.rY = CarQuaternion.Y;
                force.rZ = CarQuaternion.Z;
                force.rW = CarQuaternion.W;

                force.Speed = Speed;
                force.RPM = Rpm;

                force.sessionID = CurrentSessionID;
                force.time = DateTime.Now.Ticks;

                queue.Add(force);
            }
        }

        internal void stopIMU()
        {
            if (device != null)
            {
                device.Close();
                device = null;
            }
        }

        public async void startOBD2()
        {
            try
            {
                StopOBD();

                connection = new SerialConnection("Com6");
                dev = new ELM327(connection, new OBDConsoleLogger(OBDLogLevel.Debug));

                dev.SubscribeDataReceived<EngineRPM>((sender, data) =>
                {
                    Rpm = data.Data.Rpm;
                });

                dev.SubscribeDataReceived<EngineRPM>((sender, data) =>
                {
                    Rpm = data.Data.Rpm;
                });

                dev.SubscribeDataReceived<VehicleSpeed>((sender, data) =>
                {   
                    Speed =(int)kmphTOmph(data.Data.Speed.Value);
                });

                dev.Initialize();

                //set up timer
                Observable
                     .Interval(TimeSpan.FromMilliseconds(300))
                     .Sample(TimeSpan.FromMilliseconds(300))
                     .Timestamp()
                     .Subscribe(RepeatAction);
            }
            catch (Exception e)
            {
                string t = "";
            }
        }

        private void RepeatAction(Timestamped<long> obj)
        {
            RequestOBD2();
        }

        private void RequestOBD2()
        {
            dev.RequestData<EngineRPM>();
            dev.RequestData<VehicleSpeed>();
        }

        double kmphTOmph(double kmph)
        {
            return 0.6214 * kmph;
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
      
        public int Speed
        {
            get => speed;
            set
            {
                if (value < 56)//i dont want to speed...
                {
                    speed = value;
                    OnPropertyChanged();
                }
            }
        }
        public int Rpm
        {
            get => rpm;
            set
            {
                rpm = value;
                OnPropertyChanged();
            }
        }
        public int OilTemp
        {
            get => oilTemp;
            set
            {
                oilTemp = value;
                OnPropertyChanged();
            }
        }
        public int EngineTemp
        {
            get => engineTemp;
            set
            {
                engineTemp = value;
                OnPropertyChanged();
            }
        }

        public int ThrottlePosition
        {
            get => throttlePosition;
            set
            {
                throttlePosition = value;
                OnPropertyChanged();
            }
        }

        public int BrakePosition
        {
            get => brakePosition;
            set
            {
                brakePosition = value;
                OnPropertyChanged();
            }
        }

        public int Xoffset
        {
            get => _Xoffset;
            set
            {
                _Xoffset = value;
                OnPropertyChanged();
            }
        }
        public int Yoffset
        {
            get => _Yoffset;
            set
            {
                _Yoffset = value;
                OnPropertyChanged();
            }
        }
        public int Zoffset
        {
            get => _Zoffset;
            set
            {
                _Zoffset = value;
                OnPropertyChanged();
            }
        }

        public Quaternion CarQuaternion
        {
            get => quaternion;
            set
            {
                quaternion = value;
                OnPropertyChanged();
            }
        }

        private void Device_GForceChanged(object sender, GForceChangedEventArgs e)
        {
            submit(e);
        }

        public void submit(GForceChangedEventArgs e)
        {
            setVisual(e);
            sendPacket(e);
        }

        private void setVisual(GForceChangedEventArgs e)
        {
            msX.Push(e.accel.X);
            msY.Push(e.accel.Y);
            msZ.Push(e.accel.Z);

            CarQuaternion = new Quaternion(e.quaternion.X, e.quaternion.Y, e.quaternion.Z, e.quaternion.W);

            Xoffset = (int)(msX.Mean * 50);
            Yoffset = (int)(msY.Mean * 50);
            Zoffset = (int)(msZ.Mean * 50);

            if (e.HasOBDData)
            {
                Speed = (int)e.Speed;
                Rpm = (int)e.RPM;
            }
        }

        public void Connect()
        {
            try
            {
                _obs.Connect("ws://127.0.0.1:4444", "");
            }
            catch (AuthFailureException)
            {
                string t = "";
            }
            catch (ErrorResponseException ex)
            {
                string t = "";
            }

        }


        private void Consumer()
        {
            try
            {
                foreach (var item in queue.GetConsumingEnumerable())
                {
                    if (tosaveForces.Count < batchSize)
                        tosaveForces.Add(item);

                    if (tosaveForces.Count == batchSize)
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

        private MeshGeometry3D _meshGeso;
        public MeshGeometry3D MeshGeso
        {
            get { return _meshGeso; }
            set { _meshGeso = value; }
        }

        private MeshGeometry3D _meshGeo;
        public MeshGeometry3D MeshGeo
        {
            get { return _meshGeo; }
            set { _meshGeo = value; }
        }
    }

    class CamItem
    {
        private int minHeartRate = 0;

        private int maxSeconds;
        private int minSeconds;
        private string name;
        private string iRname;
        private string iRname2;

    
        public int MaxSeconds { get => maxSeconds; set => maxSeconds = value; }
        public string Name { get => name; set => name = value; }
        public int MinSeconds { get => minSeconds; set => minSeconds = value; }
        public int MinHeartRate { get => minHeartRate; set => minHeartRate = value; }
        public string IRname { get => iRname; set => iRname = value; }
        public string IRname2 { get => iRname2; set => iRname2 = value; }
        public string TransitionName { get; internal set; }
        public int Duration { get; internal set; }
    }
}


