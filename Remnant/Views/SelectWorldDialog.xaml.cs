using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace RemnantSaveManager.Remnant.Views
{
    /// <summary>
    /// Interaction logic for SelectWorldDialog.xaml
    /// </summary>
    public partial class SelectWorldDialog : Window
    {
        private SaveBackup _saveBackup;
        private RemnantSave _activeSave;
        public SelectedWorldResult Result { get; set; }
        public SelectWorldDialog(Manager @mw, SaveBackup @sb, RemnantSave @as)
        {
            this.InitializeComponent();
            this.txtSave.Content = $"Save Name:\t{sb.Name}\nSave Date:\t{sb.SaveDate.ToString(CultureInfo.CurrentCulture)}";
            this._saveBackup = sb;
            this._activeSave = @as;

            this.listCurrent.ItemsSource = this._activeSave.Characters;
            this.listSave.ItemsSource = this._saveBackup.Save.Characters;
        }

        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            this.Result = new SelectedWorldResult();

            this.Result.BackupWorld = this.listSave.SelectedIndex;
            this.Result.SaveWorld = this.listCurrent.SelectedIndex;

            this.DialogResult = true;
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
