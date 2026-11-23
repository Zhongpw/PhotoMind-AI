using System.Windows;
using System.Windows.Controls;
using PhotoMind.App.ViewModels;

namespace PhotoMind.App.Views;

public partial class OrganizeView : UserControl
{
    public OrganizeView()
    {
        InitializeComponent();
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OrganizeViewModel vm)
            return;

        var result = MessageBox.Show(
            "确定要撤销上次整理吗？\n\n· 复制操作：删除本次创建的副本\n· 移动操作：将文件移回原位置\n\n此操作不可再次撤销。",
            "撤销整理确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            vm.UndoLastCommand.Execute(null);
    }
}
