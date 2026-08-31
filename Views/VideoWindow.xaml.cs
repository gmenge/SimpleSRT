using System.Windows;
using System.Windows.Input;
using SimpleSRT.App.ViewModels;

namespace SimpleSRT.App.Views;

public partial class VideoWindow : Window
{
    public VideoWindow()
    {
        InitializeComponent();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Atalhos F11 e Alt+Enter para alternar Fullscreen
        if (e.Key == Key.F11 || (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt))
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsFullscreen = !vm.IsFullscreen;
                e.Handled = true;
            }
        }
    }
}