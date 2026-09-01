using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SimpleSRT.App.Core;
using SimpleSRT.App.Models;
using SimpleSRT.App.Services.Interfaces;
using SimpleSRT.App.Views;

namespace SimpleSRT.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IMediaPlayerService _mediaPlayerService;
    private VideoWindow? _videoWindow;

    public IMediaPlayerService MediaPlayerService => _mediaPlayerService;

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

    public bool IsHostEnabled => SelectedMode.Equals("caller", StringComparison.OrdinalIgnoreCase);

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

    // --- SEÇÃO BLACKMAGIC DECKLINK ---
    public ObservableCollection<DeckLinkDeviceItem> DeckLinkDevices { get; } = new();

    private DeckLinkDeviceItem? _selectedDeckLinkDevice;
    public DeckLinkDeviceItem? SelectedDeckLinkDevice
    {
        get => _selectedDeckLinkDevice;
        set
        {
            if (SetProperty(ref _selectedDeckLinkDevice, value) && value != null)
            {
                ApplyDeckLinkDeviceSelection(value);
            }
        }
    }
    // ----------------------------------

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand ToggleVideoWindowCommand { get; }
    public ICommand ToggleMuteCommand { get; }

    public MainViewModel(IMediaPlayerService mediaPlayerService)
    {
        _mediaPlayerService = mediaPlayerService;

        ConnectCommand = new RelayCommand(ExecuteConnect, () => !IsConnected);
        DisconnectCommand = new RelayCommand(ExecuteDisconnect, () => IsConnected);
        ToggleVideoWindowCommand = new RelayCommand(ExecuteToggleVideoWindow);
        ToggleMuteCommand = new RelayCommand(() => IsMuted = !IsMuted);

        LoadAudioDevices();
        LoadDeckLinkDevices();
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

    private void LoadDeckLinkDevices()
    {
        DeckLinkDevices.Clear();
        // Adiciona opção para desativar a saída física SDI
        DeckLinkDevices.Add(new DeckLinkDeviceItem(-1, "Desativado (Apenas Janela)"));

        var devices = _mediaPlayerService.GetDeckLinkDevices().ToList();
        for (int i = 0; i < devices.Count; i++)
        {
            DeckLinkDevices.Add(new DeckLinkDeviceItem(i, devices[i]));
        }

        SelectedDeckLinkDevice = DeckLinkDevices.FirstOrDefault();
    }

    private void ApplyDeckLinkDeviceSelection(DeckLinkDeviceItem device)
    {
        if (device.Index < 0)
        {
            _mediaPlayerService.DisableDeckLinkOutput();
        }
        else
        {
            // Ativa o envio de vídeo direto para o canal SDI correspondente (padrão 1080p59.94)
            _mediaPlayerService.EnableDeckLinkOutput(device.Index);
        }
    }

    private void ExecuteConnect()
    {
        var config = new StreamConfig
        {
            Host = SelectedMode.Equals("listener", StringComparison.OrdinalIgnoreCase) ? "0.0.0.0" : Host,
            Port = Port,
            Mode = SelectedMode,
            LatencyMs = LatencyMs,
            StreamId = StreamId
        };

        EnsureVideoWindowOpen();

        _mediaPlayerService.Play(config.ToSrtUrl(), config.NetworkCachingMs);
        _mediaPlayerService.Volume = Volume;
        _mediaPlayerService.IsMuted = IsMuted;

        // Caso uma porta SDI esteja selecionada, garanta a ativação na hora do Play
        if (SelectedDeckLinkDevice != null && SelectedDeckLinkDevice.Index >= 0)
        {
            _mediaPlayerService.EnableDeckLinkOutput(SelectedDeckLinkDevice.Index);
        }

        IsConnected = true;

        ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DisconnectCommand).RaiseCanExecuteChanged();
    }

    private void ExecuteDisconnect()
    {
        IsConnected = false;

        Task.Run(() =>
        {
            _mediaPlayerService.Stop();
        });

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
}

public record AudioDeviceItem(string Id, string Name);
public record DeckLinkDeviceItem(int Index, string Name);