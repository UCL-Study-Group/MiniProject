using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CheckinServiceo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class Main : Window
{
    public Main()
    {
        InitializeComponent();
    }

    private void UpdateBackground()
    {
        ValidationBackground.Background = Brushes.Red;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        UpdateBackground();
    }
}