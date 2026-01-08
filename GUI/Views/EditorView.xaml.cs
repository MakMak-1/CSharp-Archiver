using BLL.Archives;
using GUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GUI.Views
{
    /// <summary>
    /// Interaction logic for EditorView.xaml
    /// </summary>
    public partial class EditorView : UserControl
    {
        public EditorView()
        {
            InitializeComponent();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ArchiveExplorerViewModel vm)
            {
                if (sender is DataGrid grid && grid.SelectedItem is IArchiveEntry entry)
                {
                    vm.SelectedEntry = entry;
                }
                else
                {
                    vm.SelectedEntry = null;
                }
            }
        }
    }
}
