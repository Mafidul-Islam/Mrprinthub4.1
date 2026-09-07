using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MRPrintHub.Core.DTOs.Cloud;
using MRPrintHub.Core.Enums;
using MRPrintHub.Desktop.Cloud;
using MRPrintHub.QR;

namespace MRPrintHub.Desktop;

public class FileItemModel
{
    public string FileName { get; set; } = string.Empty;
    public string FileIcon { get; set; } = "📄";
    public string MetaInfo { get; set; } = string.Empty;
    public string Status { get; set; } = "Received";
}

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IPCClient _ipc;
    private readonly CloudConnectionManager _cloudManager;
    private string _currentIp = "127.0.0.1";
    private readonly int _port = 5000;
    private string _currentToken = "";
    private readonly ObservableCollection<FileItemModel> _fileItems = new();
    private FileSystemWatcher? _fileWatcher;
    private readonly string _storageFolder;
    private bool _isLocalConnected = true;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _ipc = new IPCClient();
        _cloudManager = new CloudConnectionManager();

        _storageFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MRPrintHub",
            "Received");

        UploadsListBox.ItemsSource = _fileItems;

        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ResolveLocalIp();
        TryConnectIpc();
        InitFileWatcher();
        RefreshFileList();
        InitCloudConnection();
        GenerateNewSession();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _fileWatcher?.Dispose();
        _ = _cloudManager.DisposeAsync();
    }

    private void InitCloudConnection()
    {
        _cloudManager.StatusChanged += (s, status) =>
        {
            Dispatcher.Invoke(() =>
            {
                UpdateCloudStatusUI(status);
            });
        };

        _cloudManager.UploadReceived += (s, notif) =>
        {
            Dispatcher.Invoke(() =>
            {
                RefreshFileList();
            });
        };

        // Background Cloud Connection - Completely independent from Local Wi-Fi Mode
        _ = _cloudManager.StartAsync();
    }

    private void UpdateCloudStatusUI(CloudStatusDto status)
    {
        switch (status.State)
        {
            case CloudConnectionState.Connected:
                CloudBadgeText.Text = "Cloud: Connected";
                CloudBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                CloudBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
                CloudBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7F3D0"));
                MetricCloudText.Text = $"Connected ({status.ShopCode ?? "Online"})";
                break;
            case CloudConnectionState.Connecting:
            case CloudConnectionState.Reconnecting:
                CloudBadgeText.Text = $"Cloud: {status.State}";
                CloudBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                CloudBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
                CloudBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BFDBFE"));
                MetricCloudText.Text = $"{status.State}...";
                break;
            case CloudConnectionState.Failed:
            case CloudConnectionState.Disconnected:
            default:
                CloudBadgeText.Text = "Cloud: Offline";
                CloudBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                CloudBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
                CloudBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                MetricCloudText.Text = "Offline (Local Only)";
                break;
        }

        GenerateNewSession();
    }

    private void ResolveLocalIp()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var ipProps = ni.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var ipStr = addr.Address.ToString();
                        if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254."))
                        {
                            _currentIp = ipStr;
                            IpBadgeText.Text = $"IP: {_currentIp}:{_port}";
                            return;
                        }
                    }
                }
            }
        }
        catch
        {
            _currentIp = "127.0.0.1";
        }

        IpBadgeText.Text = $"IP: {_currentIp}:{_port}";
    }

    private void GenerateNewSession()
    {
        _currentToken = Guid.NewGuid().ToString("N");
        var cloudStatus = _cloudManager.CurrentStatus;
        var shopCode = cloudStatus.ShopCode;
        var isCloudConnected = cloudStatus.State == CloudConnectionState.Connected;

        string url;
        byte[]? qrBytes;

        if (isCloudConnected && !string.IsNullOrWhiteSpace(shopCode))
        {
            url = QrCodeGenerator.BuildHybridUploadUrl(_cloudManager.CloudBaseUrl, shopCode, _currentIp, _port, _currentToken);
            qrBytes = QrCodeGenerator.GenerateHybrid(_cloudManager.CloudBaseUrl, shopCode, _currentIp, _currentToken, _port);
            QrSubtitleText.Text = "Scan to upload via Local Wi-Fi or 4G/5G Internet";
        }
        else
        {
            url = QrCodeGenerator.BuildUploadUrl(_currentIp, _port, _currentToken);
            qrBytes = QrCodeGenerator.Generate(_currentIp, _currentToken, _port);
            QrSubtitleText.Text = "Connect phone to the same Wi-Fi network";
        }

        QrUrlText.Text = url;
        BottomStatusText.Text = $"Local: Connected (http://{_currentIp}:{_port}) • Cloud: {(isCloudConnected ? $"Connected ({shopCode})" : "Offline")}";

        try
        {
            if (qrBytes != null && qrBytes.Length > 0)
            {
                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(qrBytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                QrImage.Source = bitmap;
            }
        }
        catch (Exception ex)
        {
            QrUrlText.Text = $"URL: {url} (Error rendering QR: {ex.Message})";
        }
    }

    private async void TryConnectIpc()
    {
        try
        {
            await _ipc.ConnectAsync();
            _isLocalConnected = true;
            StatusBadgeText.Text = "Local: Connected";
            StatusBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            MetricServiceText.Text = $"Active (Port {_port})";
        }
        catch
        {
            _isLocalConnected = true;
            StatusBadgeText.Text = "Local: Standalone";
            StatusBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            MetricServiceText.Text = $"Standalone (Port {_port})";
        }
    }

    private void InitFileWatcher()
    {
        try
        {
            if (!Directory.Exists(_storageFolder))
            {
                Directory.CreateDirectory(_storageFolder);
            }

            _fileWatcher = new FileSystemWatcher(_storageFolder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _fileWatcher.Created += (_, _) => Dispatcher.Invoke(RefreshFileList);
            _fileWatcher.Changed += (_, _) => Dispatcher.Invoke(RefreshFileList);
            _fileWatcher.Deleted += (_, _) => Dispatcher.Invoke(RefreshFileList);
            _fileWatcher.Renamed += (_, _) => Dispatcher.Invoke(RefreshFileList);
        }
        catch
        {
            // Ignore watcher failure
        }
    }

    private void RefreshFileList()
    {
        try
        {
            if (!Directory.Exists(_storageFolder))
            {
                Directory.CreateDirectory(_storageFolder);
            }

            var dirInfo = new DirectoryInfo(_storageFolder);
            var files = dirInfo.GetFiles();

            _fileItems.Clear();

            // Sort by most recently received
            Array.Sort(files, (a, b) => b.CreationTime.CompareTo(a.CreationTime));

            foreach (var f in files)
            {
                string icon = GetFileIcon(f.Extension);
                string sizeStr = FormatBytes(f.Length);
                string timeStr = f.CreationTime.ToString("HH:mm:ss  dd-MMM");
                string extLabel = string.IsNullOrEmpty(f.Extension) ? "FILE" : f.Extension.TrimStart('.').ToUpperInvariant();

                _fileItems.Add(new FileItemModel
                {
                    FileName = f.Name,
                    FileIcon = icon,
                    MetaInfo = $"{extLabel} • {sizeStr} • {timeStr}",
                    Status = "Received"
                });
            }

            int count = _fileItems.Count;
            FileCountBadgeText.Text = count.ToString();
            MetricFilesText.Text = $"{count} {(count == 1 ? "file" : "files")} received";
            TotalUploadsText.Text = $"Storage: {count} {(count == 1 ? "file" : "files")} in folder • Auto-Saved";

            if (count > 0)
            {
                EmptyStateView.Visibility = Visibility.Collapsed;
                UploadsListBox.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyStateView.Visibility = Visibility.Visible;
                UploadsListBox.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            // Ignore file read errors
        }
    }

    private static string GetFileIcon(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "📄",
            ".docx" or ".doc" or ".txt" or ".rtf" => "📝",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "🖼️",
            ".xlsx" or ".xls" or ".csv" => "📊",
            ".pptx" or ".ppt" => "📑",
            ".zip" or ".rar" or ".7z" => "📦",
            _ => "📁"
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
        if (bytes >= 1024)
            return $"{(bytes / 1024.0):F0} KB";
        return $"{bytes} B";
    }

    private void RefreshQrClick(object sender, RoutedEventArgs e)
    {
        ResolveLocalIp();
        GenerateNewSession();
    }

    private void CopyUrlClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(QrUrlText.Text))
        {
            Clipboard.SetText(QrUrlText.Text);
            MessageBox.Show("Upload URL copied to clipboard!", "MR Print Hub", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_storageFolder))
        {
            Directory.CreateDirectory(_storageFolder);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _storageFolder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open folder: {ex.Message}", "MR Print Hub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshUploadsClick(object sender, RoutedEventArgs e)
    {
        RefreshFileList();
    }
}
