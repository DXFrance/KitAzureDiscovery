using System.Windows;

namespace WPFClient
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            var model = new MainWindowViewModel();
            model.InitializeWebCamList();

            DataContext = model;

            Application.Current.Exit += Current_Exit;
        }

        void Current_Exit(object sender, ExitEventArgs e)
        {
            ((MainWindowViewModel)DataContext).Dispose();
        }
    }
}
