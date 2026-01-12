using System.ComponentModel;
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

using MiniBoard;

// 공부하면서 추가
using System.Runtime.CompilerServices;


namespace wpfstudy;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var repo = new JsonPostRepository("posts.json");
        var service = new BoardService(repo);

        DataContext = new MainViewModel(service);
    }
}