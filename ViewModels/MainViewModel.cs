using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SimpleSRT.App.Core;
using SimpleSRT.App.Models;
using SimpleSRT.App.Services.Interfaces;
using SimpleSRT.App.Views;

namespace SimpleSRT.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IMediaPlayerService _mediaPlayerService;
    private VideoWindow? _videoWindow;

    private string _host = "127.0.0.1";
    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    private int _port = 9998;
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private string _selectedMode = "caller";
    public string SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(IsHostEnabled));
            }
        }
    }

    public bool IsHostEnabled => SelectedMode.ToLower() == "caller";

    public ObservableCollection<string> SrtModes { get; } = new() { "caller", "listener" };

    private int _latencyMs = 120;
    public int LatencyMs
    {
        get => _latencyMs;
        set => SetProperty(ref _latencyMs, value);
    }

    private string _streamId = string.Empty;
    public string StreamId
    {
        get => _streamId;
        set => SetProperty(ref _streamId, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private bool _isFullscreen;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set
        {
            if (SetProperty(ref _isFullscreen, value) && _videoWindow != null)
            {
                ApplyFullscreenState(_videoWindow, value);
            }
        }
    }

    private int _volume = 80;
    public int Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                _mediaPlayerService.Volume = value;
            }
        }
    }

    private bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                _mediaPlayerService.IsMuted = value;
            }
        }
    }

    public ObservableCollection<AudioDeviceItem> AudioDevices { get; } = new();

    private AudioDeviceItem? _selectedAudioDevice;
    public AudioDeviceItem? SelectedAudioDevice
    {
        get => _selectedAudioDevice;
        set
        {
            if (SetProperty(ref _selectedAudioDevice, value) && value != null)
            {
                _mediaPlayerService.SetAudioOutput(value.Id);
            }
        }
    }

    private WriteableBitmap? _videoSource;
    public WriteableBitmap? VideoSource
    {
        get => _videoSource;
        set => SetProperty(ref _videoSource, value);
    }

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand ToggleVideoWindowCommand { get; }
    public ICommand ToggleMuteCommand { get; }

    public MainViewModel(IMediaPlayerService mediaPlayerService)
    {
        _mediaPlayerService = mediaPlayerService;
        _mediaPlayerService.OnFrameDecoded += OnFrameDecoded;

        ConnectCommand = new RelayCommand(ExecuteConnect, () => !IsConnected);
        DisconnectCommand = new RelayCommand(ExecuteDisconnect, () => IsConnected);
        ToggleVideoWindowCommand = new RelayCommand(ExecuteToggleVideoWindow);
        ToggleMuteCommand = new RelayCommand(() => IsMuted = !IsMuted);

        LoadAudioDevices();
    }

    private void LoadAudioDevices()
    {
        AudioDevices.Clear();
        var devices = _mediaPlayerService.GetAudioOutputs();
        foreach (var dev in devices)
        {
            AudioDevices.Add(new AudioDeviceItem(dev.Id, dev.Description));
        }
        SelectedAudioDevice = AudioDevices.FirstOrDefault();
    }

    private void ExecuteConnect()
    {
        var config = new StreamConfig
        {
            Host = SelectedMode.ToLower() == "listener" ? "0.0.0.0" : Host,
            Port = Port,
            Mode = SelectedMode,
            LatencyMs = LatencyMs,
            StreamId = StreamId
        };

        EnsureVideoWindowOpen();

        _mediaPlayerService.Play(config.ToSrtUrl(), config.NetworkCachingMs);
        _mediaPlayerService.Volume = Volume;
        _mediaPlayerService.IsMuted = IsMuted;

        IsConnected = true;

        ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DisconnectCommand).RaiseCanExecuteChanged();
    }

    private void ExecuteDisconnect()
    {
        _mediaPlayerService.Stop();
        IsConnected = false;
        VideoSource = null;

        ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DisconnectCommand).RaiseCanExecuteChanged();
    }

    private void ExecuteToggleVideoWindow()
    {
        if (_videoWindow == null || !_videoWindow.IsLoaded)
        {
            EnsureVideoWindowOpen();
        }
        else
        {
            _videoWindow.Close();
            _videoWindow = null;
        }
    }

    public void CloseVideoWindow()
    {
        if (_videoWindow != null)
        {
            _videoWindow.Close();
            _videoWindow = null;
        }
    }

    private void EnsureVideoWindowOpen()
    {
        if (_videoWindow == null || !_videoWindow.IsLoaded)
        {
            _videoWindow = new VideoWindow
            {
                DataContext = this
            };

            _videoWindow.Closed += (s, e) => _videoWindow = null;
            ApplyFullscreenState(_videoWindow, IsFullscreen);
            _videoWindow.Show();
        }
    }

    private void ApplyFullscreenState(Window window, bool isFullscreen)
    {
        if (isFullscreen)
        {
            window.WindowStyle = WindowStyle.None;
            window.WindowState = WindowState.Maximized;
        }
        else
        {
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.WindowState = WindowState.Normal;
        }
    }

    private void OnFrameDecoded(byte[] frameData, int width, int height)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (VideoSource == null || VideoSource.PixelWidth != width || VideoSource.PixelHeight != height)
            {
                VideoSource = new WriteableBitmap(
                    width,
                    height,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null);
            }

            VideoSource.WritePixels(
                new Int32Rect(0, 0, width, height),
                frameData,
                width * 4,
                0);
        });
    }
}

public record AudioDeviceItem(string Id, string Name);