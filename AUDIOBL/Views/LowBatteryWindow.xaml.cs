using System.Windows;
using System.Windows.Input;
using AUDIOBL.Helpers;

namespace AUDIOBL.Views;

public partial class LowBatteryWindow : Window
{
    public LowBatteryWindow(int level)
    {
        InitializeComponent();

        TitleText.Text   = Loc.LowBatteryTitle;
        MessageText.Text = Loc.LowBatteryMessage(level);
        CloseButton.Content = Loc.LowBatteryClose;

        // Let the user move the card out of the way before dismissing it.
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
