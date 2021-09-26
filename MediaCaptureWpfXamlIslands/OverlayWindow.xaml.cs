using HelixToolkit.Wpf;
using MathNet.Numerics.Statistics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace mPUObserver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private System.Windows.Media.Media3D.Quaternion quaternion;
      
        private int _Xoffset;
        private int _Yoffset;
        private int _Zoffset;
        private int speed;
        private int rpm;
        private int oilTemp;
        private int engineTemp;
        private int throttlePosition;
        private int brakePosition;
        public OverlayWindow()
        {
            InitializeComponent();
            Load3dModels();
        }
        private void Load3dModels()
        {
            string file = @".\Resources\brz2.stl";
            Model3DGroup MdlGrp = null;
            StLReader ObjRed = new StLReader();
            MdlGrp = ObjRed.Read(file);
            GeometryModel3D gm3d = MdlGrp.Children[0] as GeometryModel3D;
            brzModel.Geometry = gm3d.Geometry as MeshGeometry3D;

            string file2 = @".\Resources\circle.stl";
            Model3DGroup MdlGrp2 = null;
            StLReader ObjRed2 = new StLReader();
            MdlGrp2 = ObjRed2.Read(file2);
            GeometryModel3D gm3d2 = MdlGrp2.Children[0] as GeometryModel3D;
            tractionCircleModel.Geometry = gm3d2.Geometry as MeshGeometry3D;
        }
        public void setVisual(GForceChangedEventArgs e)
        {
            CarQuaternion = new System.Windows.Media.Media3D.Quaternion(e.quaternion.X, e.quaternion.Y, e.quaternion.Z, e.quaternion.W);

            Xoffset = (int)(e.accel.X * 50);
            Yoffset = (int)(e.accel.Y * 50);
            Zoffset = (int)(e.accel.Z * 50);

            if (e.HasOBDData)
            {
                Speed = (int)e.Speed;
                Rpm = (int)e.RPM;
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

        public System.Windows.Media.Media3D.Quaternion CarQuaternion
        {
            get => quaternion;
            set
            {
                quaternion = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged()
        {

        }
       
  

    }

}
