using Avalonia.Controls;
using TSSE.Desktop.ViewModels;

namespace TSSE.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
