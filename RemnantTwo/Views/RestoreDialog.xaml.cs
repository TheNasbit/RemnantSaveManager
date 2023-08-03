using System.Globalization;
using System.Windows;
using RemnantSaveManager.RemnantTwo;

namespace RemnantSaveManager.RemnantTwo.Views
{
    /// <summary>
    /// Interaction logic for RestoreDialog.xaml
    /// </summary>
    public partial class RestoreDialog : Window
    {
        private SaveBackup _saveBackup;
        private RemnantTwoSave _activeSave;
        public string Result { get; set; }
        public RestoreDialog(Manager @mw, SaveBackup @sb, RemnantTwoSave @as)
        {
            InitializeComponent();
            this.txtSave.Content = $"Save Name:\t{sb.Name}\nSave Date:\t{sb.SaveDate.ToString(CultureInfo.CurrentCulture)}";
            this._saveBackup = sb;
            this._activeSave = @as;
        }

        private void btnCharacter_Click(object sender, RoutedEventArgs e)
        {
            this.Result = "Character";
            this.DialogResult = true;
            this.Close();
        }

        private void btnAllWorlds_Click(object sender, RoutedEventArgs e)
        {
            this.Result = "Worlds";
            this.DialogResult = true;
            this.Close();
        }
        private void btnWorld_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirmResult = MessageBox.Show("Worlds may in different order when characters got deleted. This may result in unexpected behavior. Proceed?",
                                     "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirmResult == MessageBoxResult.No)
            {
                this.DialogResult = false;
                this.Close();
                return;
            }

            this.Result = "World";
            this.DialogResult = true;
            this.Close();
        }

        private void btnAll_Click(object sender, RoutedEventArgs e)
        {
            this.Result = "All";
            this.DialogResult = true;
            this.Close();
        }
    }
}
