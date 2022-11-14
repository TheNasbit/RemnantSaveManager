using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace RemnantSaveManager
{
    /// <summary>
    /// Interaction logic for SelectWorldDialog.xaml
    /// </summary>
    public partial class SelectWorldDialog : Window
    {
        private SaveBackup _saveBackup;
        private RemnantSave _activeSave;
        public SelectedWorldResult Result { get; set; }
        public SelectWorldDialog(MainWindow @mw, SaveBackup @sb, RemnantSave @as)
        {
            InitializeComponent();
            this.txtSave.Content = $"Save Name:\t{sb.Name}\nSave Date:\t{sb.SaveDate.ToString(CultureInfo.CurrentCulture)}";
            this._saveBackup = sb;
            this._activeSave = @as;

            this.listCurrent.ItemsSource = this._activeSave.Characters;
            this.listSave.ItemsSource = this._saveBackup.Save.Characters;
        }

        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            Result = new SelectedWorldResult();

            Result.BackupWorld = this.listSave.SelectedIndex;
            Result.SaveWorld = this.listCurrent.SelectedIndex;

            DialogResult = true;
            this.Close();
        }

        private void list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.btnRestore.IsEnabled = (this.listCurrent.SelectedItem != null && this.listSave.SelectedItem != null);
        }
    }

    public class SelectedWorldResult
    {
        public int BackupWorld { get; set; }
        public int SaveWorld { get; set; }
    }
}
