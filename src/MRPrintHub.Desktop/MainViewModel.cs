using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;
using MRPrintHub.QR;

namespace MRPrintHub.Desktop;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IPCClient _ipc;
    private string _statusText = "Disconnected";
    private UploadStatus _uploadStatus = UploadStatus.Pending;
    private ObservableCollection<UploadInfoDto> _uploads = new();
    private string _qrToken = "";
    private string _qrIp = "127.0.0.1";
    private int _qrPort = 5000;

    public MainViewModel()
    {
        _ipc = new IPCClient();
        Uploads = new ObservableCollection<UploadInfoDto>();
        ConnectCommand = new RelayCommand(OnConnect);
        UploadCommand = new RelayCommand(OnUpload, CanUpload);
        SetupWizardCommand = new RelayCommand(OnSetupWizard);
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public UploadStatus UploadStatus
    {
        get => _uploadStatus;
        set
        {
            _uploadStatus = value;
            OnPropertyChanged();
            UploadCommand?.ChangeCanExecute();
        }
    }

    public ObservableCollection<UploadInfoDto> Uploads
    {
        get => _uploads;
        set
        {
            _uploads = value;
            OnPropertyChanged();
        }
    }

    public string QrToken
    {
        get => _qrToken;
        set
        {
            _qrToken = value;
            OnPropertyChanged();
            QrUrl = QrCodeGenerator.BuildUploadUrl(_qrIp, _qrPort, _qrToken);
        }
    }

    public string QrIp
    {
        get => _qrIp;
        set
        {
            _qrIp = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(QrGenerated));
        }
    }

    public int QrPort
    {
        get => _qrPort;
        set
        {
            _qrPort = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(QrGenerated));
        }
    }

    public string QrUrl
    {
        get;
        set;
    }

    public bool QrGenerated => !string.IsNullOrWhiteSpace(QrUrl);

    public RelayCommand ConnectCommand { get; }
    public RelayCommand UploadCommand { get; }
    public RelayCommand SetupWizardCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private async void OnConnect()
    {
        try
        {
            await _ipc.ConnectAsync();
            StatusText = "Connected to Service";
            
            // Request status
            var response = await _ipc.SendRequestAsync("STATUS|");
            if (!string.IsNullOrEmpty(response) && response.StartsWith("STATUS|"))
            {
                var statusStr = response.Substring(7);
                if (Enum.TryParse(statusStr, out UploadStatus status))
                {
                    UploadStatus = status;
                }
            }

            // Request upload list
            var uploadsResponse = await _ipc.SendRequestAsync("UPLOADS|");
            if (!string.IsNullOrEmpty(uploadsResponse) && uploadsResponse.StartsWith("UPLOADS|"))
            {
                var tokens = uploadsResponse.Substring(8).Split(',');
                Uploads.Clear();
                foreach (var token in tokens)
                {
                    if (!string.IsNullOrEmpty(token))
                        Uploads.Add(new UploadInfoDto(Guid.NewGuid().ToString(), "", "", 0, UploadStatus.Pending, DateTime.UtcNow, null, null, token));
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Connection failed: {ex.Message}";
        }
    }

    private bool CanUpload() => UploadStatus == UploadStatus.Pending || UploadStatus == UploadStatus.Active;

    public async void OnUpload()
    {
        // Select file and upload
        var dlg = new Microsoft.Win32.OpenFileDialog();
        if (dlg.ShowDialog() == true)
        {
            var fileName = dlg.FileName;
            var token = QrToken;
            
            if (string.IsNullOrEmpty(token))
            {
                MessageBox.Show("No active session token. Please complete setup first.");
                return;
            }

            // Send upload request via IPC
            var request = $"UPLOAD|{token}|{fileName}";
            try
            {
                var response = await _ipc.SendRequestAsync(request);
                // Handle response - could show progress, etc.
                MessageBox.Show($"Upload initiated: {response}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Upload failed: {ex.Message}");
            }
        }
    }

    private void OnSetupWizard()
    {
        // Show setup wizard window
        var wizard = new SetupWizard();
        wizard.ShowDialog();
    }
}