using System.Windows;

namespace MRPrintHub.Desktop;

public partial class SetupWizard : Window
{
    private MainViewModel _viewModel;
    private int _step = 1;

    public SetupWizard()
    {
        InitializeComponent();
        _viewModel = (Application.Current.MainWindow as MainWindow)?.DataContext as MainViewModel 
                     ?? new MainViewModel();
        DataContext = _viewModel;
    }

    private void GenerateQrClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.QrPort > 0)
        {
            _viewModel.QrUrl = MRPrintHub.QR.QrCodeGenerator.BuildUploadUrl(
                _viewModel.QrIp, _viewModel.QrPort, _viewModel.QrToken);
        }
    }

    private void FinishClick(object sender, RoutedEventArgs e)
    {
        // Save settings and close
        MessageBox.Show("Setup complete! The QR code has been generated and the service is ready.", 
                       "Setup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
        Close();
    }
}