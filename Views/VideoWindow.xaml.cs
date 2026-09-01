using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using SimpleSRT.App.ViewModels;

namespace SimpleSRT.App.Views;

public partial class VideoWindow : Window
{
    private MainViewModel? _viewModel;

    public VideoWindow()
    {
        InitializeComponent();
        DataContextChanged += VideoWindow_DataContextChanged;
        Loaded += VideoWindow_Loaded;
    }

    private void VideoWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.IsConnected)
        {
            FadeInVideo();
        }
    }

    private void VideoWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            VideoPlayerView.MediaPlayer = vm.MediaPlayerService.MediaPlayer;
            
            if (vm.IsConnected)
            {
                FadeInVideo();
            }
            else
            {
                VideoPlayerView.Opacity = 0;
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsConnected))
        {
            if (_viewModel != null)
            {
                if (_viewModel.IsConnected)
                {
                    FadeInVideo();
                }
                else
                {
                    FadeToBlack();
                }
            }
        }
    }

    private void FadeToBlack()
    {
        Dispatcher.Invoke(() =>
        {
            var fadeOut = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(400)
            };
            VideoPlayerView.BeginAnimation(OpacityProperty, fadeOut);
        });
    }

    private void FadeInVideo()
    {
        Dispatcher.Invoke(() =>
        {
            var fadeIn = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            VideoPlayerView.BeginAnimation(OpacityProperty, fadeIn);
        });
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 || (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt))
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsFullscreen = !vm.IsFullscreen;
                e.Handled = true;
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        base.OnClosed(e);
    }
}