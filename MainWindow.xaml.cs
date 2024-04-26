using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfProgressbar
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private void ProgressStateButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModel vm)
            {
                switch (vm.ProgressState)
                {
                    case ProgressState.None:
                        vm.ProgressState = ProgressState.Indeterminate;
                        break;
                    case ProgressState.Indeterminate:
                        vm.ProgressState = ProgressState.Normal;
                        break;
                    case ProgressState.Normal:
                        vm.ProgressState = ProgressState.Paused;
                        break;
                    case ProgressState.Paused:
                        vm.ProgressState = ProgressState.Completed;
                        break;
                    case ProgressState.Completed:
                        vm.ProgressState = ProgressState.None;
                        break;
                }
            }
        }

        private bool _progress = false;
        private async void ProgressButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_progress && DataContext is ViewModel vm)
            {
                _progress = true;
                vm.Progress = 0.0;
                vm.ProgressState = ProgressState.Normal;
                var rand = new Random();

                double progress = 0;
                while(progress < 100)
                {
                    await Task.Delay(500);

                    progress += rand.NextDouble() * 10;
                    if (progress > 100)
                        vm.Progress = 100;
                    else
                        vm.Progress = progress;
                }
                _progress = false;
            }
        }

        private void VisibilityButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModel vm)
            {
                switch (vm.Visibility)
                {
                    case Visibility.Visible:
                        vm.Visibility = Visibility.Collapsed;
                        break;
                    case Visibility.Collapsed:
                        vm.Visibility = Visibility.Visible;
                        break;
                }
            }
        }
    }
}