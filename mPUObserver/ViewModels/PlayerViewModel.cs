
using Gma.System.MouseKeyHook;
using LibVLCSharp.Shared;
using mPUObserver.Helpers;
using mPUObserver.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace mPUObserver
{
    public class PlayerViewModel : INotifyPropertyChanged
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private string file;
        private AccurateTimer gameTimer;
        private MPUDataContext dbContext;
        private MainWindowViewModel _vm;
        private IKeyboardMouseEvents m_GlobalHook;
        private DateTime start = DateTime.Now;
        private List<Force> forces = new List<Force>();
        private long offsetMS = -700;
        private float seeker = 0;
        private bool repeat = false;
        private long lastPlayTime = 0;
        private long lastPlayTimeGlobal = 0;

        private WindowState windowState = WindowState.Normal;


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        public PlayerViewModel()
        {
            Core.Initialize();

            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.Playing += _mediaPlayer_Playing;
            _mediaPlayer.EndReached += _mediaPlayer_EndReached;
            _mediaPlayer.PositionChanged += _mediaPlayer_PositionChanged;  
        }
        internal void loadMainViewModel(MainWindowViewModel vm)
        {
            _vm = vm;
            Subscribe();
        }

        DateTime lastseekerChange = DateTime.Now;
        private void _mediaPlayer_PositionChanged(object sender, MediaPlayerPositionChangedEventArgs e)
        {
            //slider position change caused performance issue, no slider till this is fixed...
            //even when only doing this every 5 seconds... 
  
            //    Seeker = e.Position;
            //    lastseekerChange = DateTime.Now;
       
        }

        private void _mediaPlayer_EndReached(object sender, EventArgs e)
        {
            if (Repeat)
                Play();
        }
        
        public WindowState FormWindowState
        {
            get => windowState;
            set
            {
                windowState = value;
                OnPropertyChanged();
            }
        }
        public bool Repeat
        {
            get => repeat;
            set
            {
                repeat = value;
                OnPropertyChanged();
            }
        }
        public float Seeker
        {
            get => seeker;
            set
            {
                seeker = value;
                _mediaPlayer.Position = seeker;
                OnPropertyChanged();
            }
        }
        public long OffsetMS
        {
            get => offsetMS;
            set
            {
                if (value < 200000)
                {
                    offsetMS = value;
                    OnPropertyChanged();

                }
            }
        }

        public MediaPlayer MediaPlayer
        {
            get => _mediaPlayer;
            set
            {
                _mediaPlayer = value;
                OnPropertyChanged();
            }
        }

        private void _mediaPlayer_Playing(object sender, EventArgs e)
        {
            if (gameTimer != null)
                gameTimer.Stop();

            //started playing, lets load stuff...
            gameTimer = new AccurateTimer(new Action(SampleNow), 10);
        }

        internal void LoadFile(string fileName)
        {
            file = fileName;
            //may want to load first frame???
        }

        private DateTime getStartingTime(long videoTimeMs, int toleranceMS, long offsetMS)
        {
            long startTol = videoTimeMs - toleranceMS;
            if (startTol < 0)
                startTol = 0;

            var spanStart = new TimeSpan(0, 0, 0, 0, (int)startTol);
            var offsetTimeSpan = new TimeSpan(0, 0, 0, 0, (int)offsetMS);

            var startTime = start + spanStart + offsetTimeSpan;

            return startTime;
        }

        private DateTime getEndingTime(long videoTimeMs, int toleranceMS, long offsetMS)
        {
            var endTime = start + new TimeSpan(0, 0, 0, 0, (int)(videoTimeMs + toleranceMS + offsetMS));// + offsetTimeSpan;
            return endTime;
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

            _vm.submit(e);
        }
    
        private void SampleNow()
        {
            var time = getCurrentTime();
            long timeMS = (long)(time * 1000);

            int toleranceMS = 2;//ms

            var start = getStartingTime(timeMS, toleranceMS, OffsetMS);
            var end = getEndingTime(timeMS, toleranceMS, OffsetMS);

            var item3 = dbContext.Forces.Where(a => a.time >= start.Ticks & a.time <= end.Ticks).OrderBy(i => i.time).FirstOrDefault();

                if (item3 != null)
                SubmitForce(item3);
        }
        public float getCurrentTime()
        {
            long currentTime = _mediaPlayer.Time;

            if (lastPlayTime == currentTime && lastPlayTime != 0)
            {
                currentTime += DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastPlayTimeGlobal;
            }
            else
            {
                lastPlayTime = currentTime;
                lastPlayTimeGlobal = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            return currentTime * 0.001f;    //to float
        }

    

        private ICommand _playCommand;
        public ICommand PlayCommand
        {
            get
            {
                return _playCommand ?? (_playCommand = new CommandHandler(() => Play(), () => CanExecute));
            }
        }

        public bool CanExecute
        {
            get
            {
                // check if executing is allowed, i.e., validate, check if a process is running, etc. 
                return true;
            }
        }

        private void stop()
        {
            if (gameTimer != null)
                gameTimer.Stop();

            if (MediaPlayer.IsPlaying)
            {
                MediaPlayer.Stop();
            }
        }

        public void Play()
        {
            stop();//be smarter here/?

            if (!MediaPlayer.IsPlaying)
            {
                string dbPath = Path.GetDirectoryName(file);
                string dbFile = Path.GetFileNameWithoutExtension(file);
                string dbFileFullPath = Path.Combine(dbPath, dbFile + ".db");

                dbContext = new MPUDataContext(dbFileFullPath);
                var forceStart = dbContext.Forces.OrderBy(i => i.time).FirstOrDefault();

                if (dbContext.Forces.Count() > 0)
                {
                    start = new DateTime(forceStart.time);

                    using (var media = new Media(_libVLC, new Uri(file)))
                    {
                        MediaPlayer.Play(media);
                        _mediaPlayer.Volume = 70;//replace with ui slider
                    }
                }
            }
        }

    }
}

