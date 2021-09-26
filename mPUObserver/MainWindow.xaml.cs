using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace mPUObserver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel vm;
        public MainWindow()
        {
            InitializeComponent();
            vm = (MainWindowViewModel)this.DataContext;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => vm.StartRecording());
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.D1 )
            {
                if (menu.Visibility == Visibility.Collapsed)
                    menu.Visibility = Visibility.Visible;
                else
                    menu.Visibility = Visibility.Collapsed;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Task.Run(() => vm.startOBD2());
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Task.Run(() => vm.startIMU());
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Task.Run(() => vm.stopIMU());
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            Task.Run(() => vm.stopRecording());
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            Task.Run(() => vm.Connect());
        }


        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            Task.Run(() => vm.CalibrateIMU());
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            Task.Run(() => vm.StopOBD());
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            Player player = new Player(vm);
            player.Show();
        }
    }

    
}
