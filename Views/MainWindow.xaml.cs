using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimpleSRT.App.ViewModels;

namespace SimpleSRT.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Auto-seleciona o texto ao focar nos TextBox
        EventManager.RegisterClassHandler(
            typeof(TextBox), 
            UIElement.GotKeyboardFocusEvent, 
            new KeyboardFocusChangedEventHandler(OnTextBoxGotKeyboardFocus));
    }

    private void OnTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (DataContext is MainViewModel vm)
        {
            vm.CloseVideoWindow();
        }

        Application.Current.Shutdown();
    }
}