using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RemnantSaveManager.Remnant.Views
{
    /// <summary>
    /// Interaction logic for Manager.xaml
    /// </summary>
    public partial class Manager : Window
    {
        private static string defaultBackupFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Remnant\\Saved\\Backups";
        private static string backupDirPath;
        private static string defaultSaveFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Remnant\\Saved\\SaveGames";
        private static string defaultWgsSaveFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Packages\PerfectWorldEntertainment.RemnantFromtheAshes_jrajkyc4tsa6w\SystemAppData\wgs";
        private static string saveDirPath;
        private static string gameDirPath;
        private List<SaveBackup> listBackups;
        private Boolean suppressLog;
        private FileSystemWatcher saveWatcher;
        private Process gameProcess;

        //private List<RemnantCharacter> activeCharacters;
        private RemnantSave activeSave;

        private SaveAnalyzer activeSaveAnalyzer;
        private List<SaveAnalyzer> backupSaveAnalyzers;

        private RestoreDialog restoreDialog;
        private SelectWorldDialog selectWorldDialog;

        private System.Timers.Timer saveTimer;
        private DateTime lastUpdateCheck;
        private int saveCount;

        public enum LogType
        {
            Normal,
            Success,
            Error
        }

        private bool ActiveSaveIsBackedUp {
            get {
                DateTime saveDate = File.GetLastWriteTime(this.activeSave.SaveProfilePath);
                for (int i = 0; i < this.listBackups.Count; i++)
                {
                    DateTime backupDate = this.listBackups.ToArray()[i].SaveDate;
                    if (saveDate.Equals(backupDate))
                    {
                        return true;
                    }
                }
                return false;
            }
            set
            {
                if (value)
                {
                    this.lblStatus.ToolTip = "Backed Up";
                    this.lblStatus.Content = this.FindResource("StatusOK");
                    this.btnBackup.IsEnabled = false;
                    this.btnBackup.Content = this.FindResource("SaveGrey");
                }
                else
                {
                    this.lblStatus.ToolTip = "Not Backed Up";
                    this.lblStatus.Content = this.FindResource("StatusNo");
                    this.btnBackup.IsEnabled = true;
                    this.btnBackup.Content = this.FindResource("Save");
                }
            }
        }

        public Manager()
        {
            InitializeComponent();
            this.suppressLog = false;
            this.txtLog.Text = "Version " + typeof(Manager).Assembly.GetName().Version;
            if (Properties.Settings.Default.CreateLogFile)
            {
                System.IO.File.WriteAllText("log.txt", DateTime.Now.ToString() + ": Version " + typeof(Manager).Assembly.GetName().Version + "\r\n");
            }
            this.logMessage("Loading...");
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            if (Properties.Settings.Default.SaveFolder.Length == 0)
            {
                this.logMessage("Save folder not set; reverting to default.");
                Properties.Settings.Default.SaveFolder = defaultSaveFolder;
                if (!Directory.Exists(defaultSaveFolder))
                {
                    if (Directory.Exists(defaultWgsSaveFolder))
                    {
                        var dirs = Directory.GetDirectories(defaultWgsSaveFolder);
                        foreach (var dir in dirs)
                        {
                            if (dir != "t" && Directory.GetDirectories(dir).Length > 0)
                            {
                                var saveDir = Directory.GetDirectories(dir)[0];
                                Properties.Settings.Default.SaveFolder = saveDir;
                            }
                        }
                    }
                }
                Properties.Settings.Default.Save();
            }
            else if (!Directory.Exists(Properties.Settings.Default.SaveFolder) && !Properties.Settings.Default.SaveFolder.Equals(defaultSaveFolder))
            {
                this.logMessage("Save folder (" + Properties.Settings.Default.SaveFolder + ") not found; reverting to default.");
                Properties.Settings.Default.SaveFolder = defaultSaveFolder;
                Properties.Settings.Default.Save();
            }
            if (Properties.Settings.Default.BackupFolder.Length == 0)
            {
                this.logMessage("Backup folder not set; reverting to default.");
                Properties.Settings.Default.BackupFolder = defaultBackupFolder;
                Properties.Settings.Default.Save();
            }
            else if (!Directory.Exists(Properties.Settings.Default.BackupFolder) && !Properties.Settings.Default.BackupFolder.Equals(defaultBackupFolder))
            {
                this.logMessage("Backup folder ("+ Properties.Settings.Default.BackupFolder + ") not found; reverting to default.");
                Properties.Settings.Default.BackupFolder = defaultBackupFolder;
                Properties.Settings.Default.Save();
            }
            saveDirPath = Properties.Settings.Default.SaveFolder;
            if (!Directory.Exists(saveDirPath))
            {
                this.logMessage("Save folder not found, creating...");
                Directory.CreateDirectory(saveDirPath);
            }
            this.txtSaveFolder.Text = saveDirPath;

            gameDirPath = Properties.Settings.Default.GameFolder;
            this.txtGameFolder.Text = gameDirPath;
            if (!Directory.Exists(gameDirPath))
            {
                this.logMessage("Game folder not found...");
                this.btnStartGame.IsEnabled = false;
                this.btnStartGame.Content = this.FindResource("PlayGrey");
                this.backupCMStart.IsEnabled = false;
                this.backupCMStart.Icon = this.FindResource("PlayGrey");
                if (gameDirPath == "")
                {
                    this.TryFindGameFolder();
                }
            }

            backupDirPath = Properties.Settings.Default.BackupFolder;
            this.txtBackupFolder.Text = backupDirPath;

            this.chkCreateLogFile.IsChecked = Properties.Settings.Default.CreateLogFile;

            this.chkKeepNamedBackup.IsChecked = Properties.Settings.Default.KeepNamedBackups;

            this.saveTimer = new System.Timers.Timer();
            this.saveTimer.Interval = 2000;
            this.saveTimer.AutoReset = false;
            this.saveTimer.Elapsed += this.OnSaveTimerElapsed;

            this.saveWatcher = new FileSystemWatcher();
            this.saveWatcher.Path = saveDirPath;

            // Watch for changes in LastWrite times.
            this.saveWatcher.NotifyFilter = NotifyFilters.LastWrite;

            // Only watch sav files.
            this.activeSave = new RemnantSave(saveDirPath);
            if (this.activeSave.SaveType == RemnantSaveType.Normal)
            {
                this.saveWatcher.Filter = "profile.sav";
            }
            else
            {
                this.saveWatcher.Filter = "container.*";
            }

            // Add event handlers.
            this.saveWatcher.Changed += this.OnSaveFileChanged;
            this.saveWatcher.Created += this.OnSaveFileChanged;
            this.saveWatcher.Deleted += this.OnSaveFileChanged;
            //watcher.Renamed += OnRenamed;

            this.listBackups = new List<SaveBackup>();

            this.activeSaveAnalyzer = new SaveAnalyzer(this)
            {
                ActiveSave = true,
                Title = "Active Save World Analyzer"
            };
            this.backupSaveAnalyzers = new List<SaveAnalyzer>();

            GameInfo.GameInfoUpdate += this.OnGameInfoUpdate;
            this.dataBackups.CanUserDeleteRows = false;
            this.saveCount = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.txtLog.IsReadOnly = true;
            this.logMessage("Current save date: " + File.GetLastWriteTime(this.activeSave.SaveProfilePath).ToString());
            //logMessage("Backups folder: " + backupDirPath);
            //logMessage("Save folder: " + saveDirPath);
            this.loadBackups();
            bool autoBackup = Properties.Settings.Default.AutoBackup;
            this.chkAutoBackup.IsChecked = autoBackup;
            this.txtBackupMins.Text = Properties.Settings.Default.BackupMinutes.ToString();
            this.txtBackupLimit.Text = Properties.Settings.Default.BackupLimit.ToString();
            this.chkShowPossibleItems.IsChecked = Properties.Settings.Default.ShowPossibleItems;
            this.chkAutoCheckUpdate.IsChecked = Properties.Settings.Default.AutoCheckUpdate;

            this.cmbMissingItemColor.Items.Add("Red");
            this.cmbMissingItemColor.Items.Add("White");
            if (Properties.Settings.Default.MissingItemColor.ToString().Equals("Red"))
            {
                this.cmbMissingItemColor.SelectedIndex = 0;
            } else
            {
                this.cmbMissingItemColor.SelectedIndex = 1;
            }
            this.cmbMissingItemColor.SelectionChanged += this.cmbMissingItemColorSelectionChanged;

            this.saveWatcher.EnableRaisingEvents = true;
            this.updateCurrentWorldAnalyzer();

            if (Properties.Settings.Default.AutoCheckUpdate)
            {
                this.checkForUpdate();
            }
        }

        private void loadBackups()
        {
            if (!Directory.Exists(backupDirPath))
            {
                this.logMessage("Backups folder not found, creating...");
                Directory.CreateDirectory(backupDirPath);
            }
            this.dataBackups.ItemsSource = null;
            this.listBackups.Clear();
            Dictionary<long, string> backupNames = this.getSavedBackupNames();
            Dictionary<long, bool> backupKeeps = this.getSavedBackupKeeps();
            string[] files = Directory.GetDirectories(backupDirPath);
            SaveBackup activeBackup = null;
            for (int i = 0; i < files.Length; i++)
            {
                if (RemnantSave.ValidSaveFolder(files[i]))
                {
                    SaveBackup backup = new SaveBackup(files[i]);
                    if (backupNames.ContainsKey(backup.SaveDate.Ticks))
                    {
                        backup.Name = backupNames[backup.SaveDate.Ticks];
                    }
                    if (backupKeeps.ContainsKey(backup.SaveDate.Ticks))
                    {
                        backup.Keep = backupKeeps[backup.SaveDate.Ticks];
                    }

                    if (this.backupActive(backup))
                    {
                        backup.Active = true;
                        activeBackup = backup;
                    }

                    backup.Updated += this.saveUpdated;

                    this.listBackups.Add(backup);
                }
            }
            this.dataBackups.ItemsSource = this.listBackups;
            this.logMessage("Backups found: " + this.listBackups.Count);
            if (this.listBackups.Count > 0)
            {
                this.logMessage("Last backup save date: " + this.listBackups[this.listBackups.Count - 1].SaveDate.ToString());
            }
            if (activeBackup != null)
            {
                this.dataBackups.SelectedItem = activeBackup;
            }
            this.ActiveSaveIsBackedUp = (activeBackup != null);
        }

        private void saveUpdated(object sender, UpdatedEventArgs args)
        {
            if (args.FieldName.Equals("Name"))
            {
                this.updateSavedNames();
            }
            else if (args.FieldName.Equals("Keep"))
            {
                this.updateSavedKeeps();
            }
        }

        private void loadBackups(Boolean verbose)
        {
            Boolean oldVal = this.suppressLog;
            this.suppressLog = !verbose;
            this.loadBackups();
            this.suppressLog = oldVal;
        }

        private Boolean backupActive(SaveBackup saveBackup)
        {
            if (DateTime.Compare(saveBackup.SaveDate, File.GetLastWriteTime(this.activeSave.SaveProfilePath)) == 0)
            {
                return true;
            }
            return false;
        }

        public void logMessage(string msg)
        {
            this.logMessage(msg, Colors.White);
        }

        public void logMessage(string msg, LogType lt)
        {
            Color color = Colors.White;
            if (lt == LogType.Success)
            {
                color = Color.FromRgb(0, 200, 0);
            }
            else if (lt == LogType.Error)
            {
                color = Color.FromRgb(200, 0, 0);
            }
            this.logMessage(msg, color);
        }

        public void logMessage(string msg, Color color)
        {
            if (!this.suppressLog)
            {
                this.txtLog.Text = this.txtLog.Text + Environment.NewLine + DateTime.Now.ToString() + ": " + msg;
                this.lblLastMessage.Content = msg;
                this.lblLastMessage.Foreground = new SolidColorBrush(color);
                if (color.Equals(Colors.White))
                {
                    this.lblLastMessage.FontWeight = FontWeights.Normal;
                }
                else
                {
                    this.lblLastMessage.FontWeight = FontWeights.Bold;
                }
            }
            if (Properties.Settings.Default.CreateLogFile)
            {
                StreamWriter writer = System.IO.File.AppendText("log.txt");
                writer.WriteLine(DateTime.Now.ToString() + ": " + msg);
                writer.Close();
            }
        }

        private void BtnClearBackups_Click(object sender, RoutedEventArgs e)
        {
            // Keep last 5 backups, don't matter if named or not (just for safety)
            int toDeleteCount = this.listBackups.Take(this.listBackups.Count-5).Count(t => !t.Keep && !t.Active && t.Name == t.SaveDate.Ticks.ToString());
            if (toDeleteCount == 0) return;
            MessageBoxResult confirmResult = MessageBox.Show($"Are you sure to delete {toDeleteCount} backups?\nWill NOT delete named backups, or backups marked as \"Keep\".",
                                     "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (confirmResult == MessageBoxResult.No) return;

            List<SaveBackup> removeBackups = new List<SaveBackup>();
            for (int i = 0; i < this.listBackups.Count-5; i++)
            {
                if (!this.listBackups[i].Keep && !this.listBackups[i].Active && this.listBackups[i].Name == this.listBackups[i].SaveDate.Ticks.ToString())
                {
                    this.logMessage("Deleting backup " + this.listBackups[i].Name + " (" + this.listBackups[i].SaveDate + ")");
                    removeBackups.Add(this.listBackups[i]);
                }
            }

            foreach (SaveBackup backup in removeBackups)
            {
                Directory.Delete(backupDirPath + "\\" + backup.SaveDate.Ticks, true);
                this.listBackups.Remove(backup);
            }
            this.dataBackups.Items.Refresh();
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            this.doBackup();
        }

        private void doBackup()
        {
            try
            {
                if (!this.activeSave.Valid)
                {
                    this.logMessage("Active save is not valid; backup skipped.");
                    return;
                }
                int existingSaveIndex = -1;
                DateTime saveDate = File.GetLastWriteTime(this.activeSave.SaveProfilePath);
                string backupFolder = backupDirPath + "\\" + saveDate.Ticks;
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }
                else if (RemnantSave.ValidSaveFolder(backupFolder))
                {
                    for (int i=this.listBackups.Count-1; i >= 0; i--)
                    {
                        if (this.listBackups[i].SaveDate.Ticks == saveDate.Ticks)
                        {
                            existingSaveIndex = i;
                            break;
                        }
                    }
                }
                foreach (string file in Directory.GetFiles(saveDirPath))
                    File.Copy(file, backupFolder + "\\" + System.IO.Path.GetFileName(file), true);
                if (RemnantSave.ValidSaveFolder(backupFolder))
                {
                    Dictionary<long, string> backupNames = this.getSavedBackupNames();
                    Dictionary<long, bool> backupKeeps = this.getSavedBackupKeeps();
                    SaveBackup backup = new SaveBackup(backupFolder);
                    if (backupNames.ContainsKey(backup.SaveDate.Ticks))
                    {
                        backup.Name = backupNames[backup.SaveDate.Ticks];
                    }
                    if (backupKeeps.ContainsKey(backup.SaveDate.Ticks))
                    {
                        backup.Keep = backupKeeps[backup.SaveDate.Ticks];
                    }
                    foreach (SaveBackup saveBackup in this.listBackups)
                    {
                        saveBackup.Active = false;
                    }
                    backup.Active = true;
                    backup.Updated += this.saveUpdated;
                    if (existingSaveIndex > -1)
                    {
                        this.listBackups[existingSaveIndex] = backup;
                    } else
                    {
                        this.listBackups.Add(backup);
                    }
                }
                this.CheckBackupLimit();
                this.dataBackups.Items.Refresh();
                this.ActiveSaveIsBackedUp = true;
                this.logMessage($"Backup completed ({saveDate.ToString()})!", LogType.Success);
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("being used by another process"))
                {
                    this.logMessage("Save file in use; waiting 0.5 seconds and retrying.");
                    System.Threading.Thread.Sleep(500);
                    this.doBackup();
                }
            }
        }

        private Boolean isRemnantRunning()
        {
            Process[] pname = Process.GetProcessesByName("Remnant");
            if (pname.Length == 0)
            {
                return false;
            }
            return true;
        }
        private void BtnRestoreStart_Click(object sender, RoutedEventArgs e)
        {
            if (this.isRemnantRunning())
            {
                this.logMessage("Exit the game before restoring a save backup.", LogType.Error);
                return;
            }

            if (this.dataBackups.SelectedItem == null)
            {
                this.logMessage("Choose a backup to restore from the list!", LogType.Error);
                return;
            }
            SaveBackup selectedBackup = (SaveBackup)this.dataBackups.SelectedItem;

            this.restoreDialog = new RestoreDialog(this, selectedBackup, this.activeSave);
            this.restoreDialog.Owner = this;
            bool? dialogResult = this.restoreDialog.ShowDialog();
            if (dialogResult.HasValue && dialogResult.Value == false) return;

            string restoreResult = this.restoreDialog.Result;

            this.RestoreBackup(selectedBackup, restoreResult, true);
        }

        private void RestoreBackup(SaveBackup backup, string type = "All", bool startGame = false)
        {

            if (!this.ActiveSaveIsBackedUp)
            {
                this.doBackup();
            }

            this.saveWatcher.EnableRaisingEvents = false;

            DirectoryInfo di = new DirectoryInfo(saveDirPath);
            DirectoryInfo buDi = new DirectoryInfo(backupDirPath + "\\" + backup.SaveDate.Ticks);

            switch (type)
            {
                case "All":
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }

                    foreach (FileInfo file in buDi.GetFiles())
                    {
                        file.CopyTo($"{saveDirPath}\\{file.Name}");
                    }
                    break;
                case "Character":

                    foreach (FileInfo file in buDi.GetFiles("profile.sav"))
                    {
                        FileInfo oldFile = new FileInfo($"{di.FullName}\\{file.Name}");
                        if (oldFile.Exists) oldFile.Delete();

                        file.CopyTo($"{saveDirPath}\\{file.Name}");
                    }
                    break;
                case "Worlds":
                    foreach (FileInfo file in buDi.GetFiles("save_?.sav"))
                    {
                        FileInfo oldFile = new FileInfo($"{di.FullName}\\{file.Name}");
                        if (oldFile.Exists) oldFile.Delete();

                        file.CopyTo($"{saveDirPath}\\{file.Name}");
                    }

                    break;
                case "World":
                    this.selectWorldDialog = new SelectWorldDialog(this, backup, this.activeSave);
                    this.selectWorldDialog.Owner = this;

                    bool? dialogResult = this.selectWorldDialog.ShowDialog();
                    if (dialogResult.HasValue && dialogResult.Value == false) return;

                    SelectedWorldResult selectWorldResult = this.selectWorldDialog.Result;

                    FileInfo currentWorld = new FileInfo($"{di.FullName}\\save_{selectWorldResult.SaveWorld}.sav");
                    FileInfo backupWorld = new FileInfo($"{buDi.FullName}\\save_{selectWorldResult.BackupWorld}.sav");

                    if (currentWorld.Exists && backupWorld.Exists)
                    {
                        currentWorld.Delete();
                        backupWorld.CopyTo($"{saveDirPath}\\save_{selectWorldResult.SaveWorld}.sav");
                    }
                    break;
                default:
                    this.logMessage("Something went wrong!", LogType.Error);
                    return;
            }

            foreach (SaveBackup saveBackup in this.listBackups)
            {
                saveBackup.Active = false;
            }
            if (type != "Character")
            {
                backup.Active = true;
            }

            this.updateCurrentWorldAnalyzer();
            this.dataBackups.Items.Refresh();
            this.logMessage("Backup restored!", LogType.Success);
            this.saveWatcher.EnableRaisingEvents = Properties.Settings.Default.AutoBackup;


            if (startGame) this.LaunchGame();
        }
        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (this.isRemnantRunning())
            {
                this.logMessage("Exit the game before restoring a save backup.", LogType.Error);
                return;
            }

            if (this.dataBackups.SelectedItem == null)
            {
                this.logMessage("Choose a backup to restore from the list!", LogType.Error);
                return;
            }
            SaveBackup selectedBackup = (SaveBackup)this.dataBackups.SelectedItem;

            this.restoreDialog = new RestoreDialog(this, selectedBackup, this.activeSave);
            this.restoreDialog.Owner = this;
            bool? dialogResult = this.restoreDialog.ShowDialog();
            if (dialogResult.HasValue && dialogResult.Value == false) return;

            string restoreResult = this.restoreDialog.Result;

            this.RestoreBackup(selectedBackup, restoreResult);
        }

        private void ChkAutoBackup_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoBackup = this.chkAutoBackup.IsChecked.HasValue ? this.chkAutoBackup.IsChecked.Value : false;
            Properties.Settings.Default.Save();
        }

        private void OnSaveFileChanged(object source, FileSystemEventArgs e)
        {
            if (this.activeSave.SaveType == RemnantSaveType.WindowsStore)
            {
                var newSave = new RemnantSave(saveDirPath);
                if (!newSave.Valid)
                {
                    return;

                }
                this.activeSave = newSave;
            }
            // Specify what is done when a file is changed, created, or deleted.
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    //When the save files are modified, they are modified
                    //four times in relatively rapid succession.
                    //This timer is refreshed each time the save is modified,
                    //and a backup only occurs after the timer expires.
                    this.saveTimer.Interval = 10000;
                    this.saveTimer.Enabled = true;
                    this.saveCount++;
                    if (this.saveCount == 4)
                    {
                        this.updateCurrentWorldAnalyzer();
                        this.saveCount = 0;
                    }
                }
                catch (Exception ex)
                {
                    this.logMessage(ex.GetType()+" setting save file timer: " +ex.Message+"("+ex.StackTrace+")");
                }
            });
        }

        private void OnSaveTimerElapsed(Object source, System.Timers.ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    //logMessage($"{DateTime.Now.ToString()} File: {e.FullPath} {e.ChangeType}");
                    if (Properties.Settings.Default.AutoBackup)
                    {
                        //logMessage($"Save: {File.GetLastWriteTime(e.FullPath)}; Last backup: {File.GetLastWriteTime(listBackups[listBackups.Count - 1].Save.SaveFolderPath + "\\profile.sav")}");
                        DateTime latestBackupTime;
                        DateTime newBackupTime;
                        if (this.listBackups.Count > 0)
                        {
                            latestBackupTime = this.listBackups[this.listBackups.Count - 1].SaveDate;
                            newBackupTime = latestBackupTime.AddMinutes(Properties.Settings.Default.BackupMinutes);
                        }
                        else
                        {
                            latestBackupTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                            newBackupTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        }
                        if (DateTime.Compare(DateTime.Now, newBackupTime) >= 0)
                        {
                            this.doBackup();
                        }
                        else
                        {
                            this.ActiveSaveIsBackedUp = false;
                            foreach (SaveBackup backup in this.listBackups)
                            {
                                if (backup.Active) backup.Active = false;
                            }
                            this.dataBackups.Items.Refresh();
                            TimeSpan span = (newBackupTime - DateTime.Now);
                            this.logMessage($"Save change detected, but {span.Minutes + Math.Round(span.Seconds / 60.0, 2)} minutes, left until next backup");
                        }
                    }
                    if (this.saveCount != 0)
                    {
                        this.updateCurrentWorldAnalyzer();
                        this.saveCount = 0;
                    }

                    if (this.gameProcess == null || this.gameProcess.HasExited)
                    {
                        Process[] processes = Process.GetProcessesByName("Remnant");
                        if (processes.Length > 0)
                        {
                            this.gameProcess = processes[0];
                            this.gameProcess.EnableRaisingEvents = true;
                            this.gameProcess.Exited += (s, eargs) =>
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    this.doBackup();
                                });
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logMessage(ex.GetType() + " processing save file change: " + ex.Message + "(" + ex.StackTrace + ")");
                }
            });
        }

        private void TxtBackupMins_LostFocus(object sender, RoutedEventArgs e)
        {
            this.updateBackupMins();
        }

        private void TxtBackupMins_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.updateBackupMins();
            }
        }

        private void updateBackupMins()
        {
            string txt = this.txtBackupMins.Text;
            int mins;
            bool valid = false;
            if (txt.Length > 0)
            {
                if (int.TryParse(txt, out mins))
                {
                    valid = true;
                }
                else
                {
                    mins = Properties.Settings.Default.BackupMinutes;
                }
            }
            else
            {
                mins = Properties.Settings.Default.BackupMinutes;
            }
            if (mins != Properties.Settings.Default.BackupMinutes)
            {
                Properties.Settings.Default.BackupMinutes = mins;
                Properties.Settings.Default.Save();
            }
            if (!valid)
            {
                this.txtBackupMins.Text = Properties.Settings.Default.BackupMinutes.ToString();
            }
        }

        private void TxtBackupLimit_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.updateBackupLimit();
            }
        }

        private void TxtBackupLimit_LostFocus(object sender, RoutedEventArgs e)
        {
            this.updateBackupLimit();
        }

        private void updateBackupLimit()
        {
            string txt = this.txtBackupLimit.Text;
            int num;
            bool valid = false;
            if (txt.Length > 0)
            {
                if (int.TryParse(txt, out num))
                {
                    valid = true;
                }
                else
                {
                    num = Properties.Settings.Default.BackupLimit;
                }
            }
            else
            {
                num = 0;
            }
            if (num != Properties.Settings.Default.BackupLimit)
            {
                Properties.Settings.Default.BackupLimit = num;
                Properties.Settings.Default.Save();
            }
            if (!valid)
            {
                this.txtBackupLimit.Text = Properties.Settings.Default.BackupLimit.ToString();
            }
        }

        private void CheckBackupLimit()
        {
            bool keepNamed = Properties.Settings.Default.KeepNamedBackups;
            int countBackups = keepNamed ? this.listBackups.Count(t => t.Name == t.SaveDate.Ticks.ToString()) : this.listBackups.Count;
            if (countBackups > Properties.Settings.Default.BackupLimit && Properties.Settings.Default.BackupLimit > 0)
            {
                List<SaveBackup> removeBackups = new List<SaveBackup>();
                int delNum = countBackups - Properties.Settings.Default.BackupLimit;
                for (int i = 0; i < this.listBackups.Count && delNum > 0; i++)
                {
                    if (!this.listBackups[i].Keep && !this.listBackups[i].Active && (!keepNamed || this.listBackups[i].Name == this.listBackups[i].SaveDate.Ticks.ToString()))
                    {
                        this.logMessage("Deleting excess backup " + this.listBackups[i].Name + " (" + this.listBackups[i].SaveDate + ")");
                        removeBackups.Add(this.listBackups[i]);
                        delNum--;
                    }
                }

                foreach (SaveBackup backup in removeBackups)
                {
                    Directory.Delete(backupDirPath + "\\" + backup.SaveDate.Ticks, true);
                    this.listBackups.Remove(backup);
                }
                this.dataBackups.Items.Refresh();
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(backupDirPath))
            {
                this.logMessage("Backups folder not found, creating...");
                Directory.CreateDirectory(backupDirPath);
            }
            Process.Start(backupDirPath+"\\");
        }

        private Dictionary<long, string> getSavedBackupNames()
        {
            Dictionary<long, string> names = new Dictionary<long, string>();
            string savedString = Properties.Settings.Default.BackupName;
            string[] savedNames = savedString.Split(',');
            for (int i = 0; i < savedNames.Length; i++)
            {
                string[] vals = savedNames[i].Split('=');
                if (vals.Length == 2)
                {
                    names.Add(long.Parse(vals[0]), System.Net.WebUtility.UrlDecode(vals[1]));
                }
            }
            return names;
        }

        private Dictionary<long, bool> getSavedBackupKeeps()
        {
            Dictionary<long, bool> keeps = new Dictionary<long, bool>();
            string savedString = Properties.Settings.Default.BackupKeep;
            string[] savedKeeps = savedString.Split(',');
            for (int i = 0; i < savedKeeps.Length; i++)
            {
                string[] vals = savedKeeps[i].Split('=');
                if (vals.Length == 2)
                {
                    keeps.Add(long.Parse(vals[0]), bool.Parse(vals[1]));
                }
            }
            return keeps;
        }

        private void DataBackups_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Column.Header.ToString().Equals("SaveDate") || e.Column.Header.ToString().Equals("Active")) e.Cancel = true;
        }

        private void DataBackups_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString().Equals("Name") && e.EditAction == DataGridEditAction.Commit)
            {
                SaveBackup sb = (SaveBackup)e.Row.Item;
                if (sb.Name.Equals(""))
                {
                    sb.Name = sb.SaveDate.Ticks.ToString();
                }
            }
        }

        private void updateSavedNames()
        {
            List<string> savedNames = new List<string>();
            for (int i = 0; i < this.listBackups.Count; i++)
            {
                SaveBackup s = this.listBackups[i];
                if (!s.Name.Equals(s.SaveDate.Ticks.ToString()))
                {
                    savedNames.Add(s.SaveDate.Ticks + "=" + System.Net.WebUtility.UrlEncode(s.Name));
                }
                else
                {
                }
            }
            if (savedNames.Count > 0)
            {
                Properties.Settings.Default.BackupName = string.Join(",", savedNames.ToArray());
            }
            else
            {
                Properties.Settings.Default.BackupName = "";
            }
            Properties.Settings.Default.Save();
        }

        private void updateSavedKeeps()
        {
            List<string> savedKeeps = new List<string>();
            for (int i = 0; i < this.listBackups.Count; i++)
            {
                SaveBackup s = this.listBackups[i];
                if (s.Keep)
                {
                    savedKeeps.Add(s.SaveDate.Ticks + "=True");
                }
            }
            if (savedKeeps.Count > 0)
            {
                Properties.Settings.Default.BackupKeep = string.Join(",", savedKeeps.ToArray());
            }
            else
            {
                Properties.Settings.Default.BackupKeep = "";
            }
            Properties.Settings.Default.Save();
        }

        private void DataBackups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MenuItem analyzeMenu = ((MenuItem)this.dataBackups.ContextMenu.Items[0]);
            MenuItem deleteMenu = ((MenuItem)this.dataBackups.ContextMenu.Items[1]);
            if (e.AddedItems.Count > 0)
            {
                SaveBackup selectedBackup = (SaveBackup)(this.dataBackups.SelectedItem);

                analyzeMenu.IsEnabled = true;
                deleteMenu.IsEnabled = true;
            }
            else
            {
                analyzeMenu.IsEnabled = false;
                deleteMenu.IsEnabled = false;
            }
        }

        private void analyzeMenuItem_Click(object sender, System.EventArgs e)
        {
            SaveBackup saveBackup = (SaveBackup)this.dataBackups.SelectedItem;
            this.logMessage("Showing backup save (" + saveBackup.Name + ") world analyzer...");
            SaveAnalyzer analyzer = new SaveAnalyzer(this);
            analyzer.Title = "Backup Save ("+saveBackup.Name+") World Analyzer";
            analyzer.Closing += this.Backup_Analyzer_Closing;
            List<RemnantCharacter> chars = saveBackup.Save.Characters;
            for (int i = 0; i < chars.Count; i++)
            {
                chars[i].LoadWorldData(i);
            }
            analyzer.LoadData(chars);
            this.backupSaveAnalyzers.Add(analyzer);
            analyzer.Show();
        }

        private void Backup_Analyzer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.backupSaveAnalyzers.Remove((SaveAnalyzer)sender);
        }

        private void openFolderMenuItem_Click(object sender, System.EventArgs e)
        {
            SaveBackup selectedBackup = (SaveBackup)this.dataBackups.SelectedItem;
            Process.Start(backupDirPath + "\\" + selectedBackup.SaveDate.Ticks);
        }

        private void deleteMenuItem_Click(object sender, System.EventArgs e)
        {
            SaveBackup save = (SaveBackup)this.dataBackups.SelectedItem;
            var confirmResult = MessageBox.Show("Are you sure to delete backup \"" + save.Name + "\" (" + save.SaveDate.ToString() + ")?",
                                     "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (confirmResult == MessageBoxResult.Yes)
            {
                if (save.Keep)
                {
                    confirmResult = MessageBox.Show("This backup is marked for keeping. Are you SURE to delete backup \"" + save.Name + "\" (" + save.SaveDate.ToString() + ")?",
                                     "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                    if (confirmResult != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
                if (save.Active)
                {
                    this.ActiveSaveIsBackedUp = false;
                }
                if (Directory.Exists(backupDirPath + "\\" + save.SaveDate.Ticks))
                {
                    Directory.Delete(backupDirPath + "\\" + save.SaveDate.Ticks, true);
                }
                this.listBackups.Remove(save);
                this.dataBackups.Items.Refresh();
                this.logMessage("Backup \"" + save.Name + "\" (" + save.SaveDate + ") deleted.");
            }
        }

        private void BtnAnalyzeCurrent_Click(object sender, RoutedEventArgs e)
        {
            this.logMessage("Showing current save world analyzer...");
            this.activeSaveAnalyzer.Show();
        }

        private void updateCurrentWorldAnalyzer()
        {
            this.activeSave.UpdateCharacters();
            /*for (int i = 0; i < activeSave.Characters.Count; i++)
            {
                Console.WriteLine(activeSave.Characters[i]);
            }*/
            this.activeSaveAnalyzer.LoadData(this.activeSave.Characters);
        }

        private void OnGameInfoUpdate(object source, GameInfoUpdateEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.logMessage(e.Message);
                if (e.Result == GameInfoUpdateResult.Updated)
                {
                    this.updateCurrentWorldAnalyzer();
                }
            });
        }

        private void checkForUpdate()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                GameInfo.CheckForNewGameInfo();
            }).Start();
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                try
                {
                    WebClient client = new WebClient();
                    string source = client.DownloadString("https://github.com/TheNasbit/RemnantSaveManager/releases/latest");
                    string title = Regex.Match(source, @"\<title\b[^>]*\>\s*(?<Title>[\s\S]*?)\</title\>", RegexOptions.IgnoreCase).Groups["Title"].Value;
                    string remoteVer = Regex.Match(source, @"Remnant Save Manager (?<Version>([\d.]+)?)", RegexOptions.IgnoreCase).Groups["Version"].Value;

                    Version remoteVersion = new Version(remoteVer);
                    Version localVersion = typeof(Manager).Assembly.GetName().Version;

                    this.Dispatcher.Invoke(() =>
                    {
                        //do stuff in here with the interface
                        if (localVersion.CompareTo(remoteVersion) == -1)
                        {
                            var confirmResult = MessageBox.Show("There is a new version available. Would you like to open the download page?",
                                     "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                            if (confirmResult == MessageBoxResult.Yes)
                            {
                                Process.Start("https://github.com/TheNasbit/RemnantSaveManager/releases/latest");
                                System.Environment.Exit(1);
                            }
                        } else
                        {
                            //logMessage("No new version found.");
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        this.logMessage("Error checking for new version: " + ex.Message, LogType.Error);
                    });
                }
            }).Start();
            this.lastUpdateCheck = DateTime.Now;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            /*activeSaveAnalyzer.ActiveSave = false;
            activeSaveAnalyzer.Close();
            for (int i = backupSaveAnalyzers.Count - 1; i > -1; i--)
            {
                backupSaveAnalyzers[i].Close();
            }*/
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            System.Environment.Exit(1);
        }

        private void BtnBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = backupDirPath;
            openFolderDialog.Description = "Select the folder where you want your backup saves kept.";
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string folderName = openFolderDialog.SelectedPath;
                if (folderName.Equals(saveDirPath))
                {
                    MessageBox.Show("Please select a folder other than the game's save folder.",
                                     "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
                    return;
                }
                if (folderName.Equals(backupDirPath))
                {
                    return;
                }
                if (this.listBackups.Count > 0)
                {
                    var confirmResult = MessageBox.Show("Do you want to move your backups to this new folder?",
                                     "Move Backups", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        List<String> backupFiles = Directory.GetDirectories(backupDirPath).ToList();
                        foreach (string file in backupFiles)
                        {
                            string subFolderName = file.Substring(file.LastIndexOf(@"\"));
                            Directory.CreateDirectory(folderName + subFolderName);
                            Directory.SetCreationTime(folderName + subFolderName, Directory.GetCreationTime(file));
                            Directory.SetLastWriteTime(folderName + subFolderName, Directory.GetCreationTime(file));
                            foreach (string filename in Directory.GetFiles(file))
                            {
                                File.Copy(filename, filename.Replace(backupDirPath, folderName));
                            }
                            Directory.Delete(file, true);
                            //Directory.Move(file, folderName + subFolderName);
                        }
                    }
                }
                this.txtBackupFolder.Text = folderName;
                backupDirPath = folderName;
                Properties.Settings.Default.BackupFolder = folderName;
                Properties.Settings.Default.Save();
                this.loadBackups();
            }
        }

        private void DataBackups_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.Column.Header.Equals("Save")) {
                e.Cancel = true;
            }
        }

        private void btnGameInfoUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (this.lastUpdateCheck.AddMinutes(10) < DateTime.Now)
            {
                this.checkForUpdate();
            }
            else
            {
                TimeSpan span = (this.lastUpdateCheck.AddMinutes(10) - DateTime.Now);
                this.logMessage("Please wait " + span.Minutes+" minutes, "+span.Seconds+" seconds before checking for update.");
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            //need to call twice for some reason
            this.dataBackups.CancelEdit();
            this.dataBackups.CancelEdit();
        }

        private void chkCreateLogFile_Click(object sender, RoutedEventArgs e)
        {
            bool newValue = this.chkCreateLogFile.IsChecked ?? false;
            if (newValue & !Properties.Settings.Default.CreateLogFile)
            {
                System.IO.File.WriteAllText("log.txt", DateTime.Now.ToString() + ": Version " + typeof(Manager).Assembly.GetName().Version + "\r\n");
            }
            Properties.Settings.Default.CreateLogFile = newValue;
            Properties.Settings.Default.Save();
        }

        private void ChkKeepNamedBackup_Click(object sender, RoutedEventArgs e)
        {
            bool newValue = this.chkKeepNamedBackup.IsChecked ?? false;
            if (newValue & !Properties.Settings.Default.KeepNamedBackups)
            {

            }
            Properties.Settings.Default.KeepNamedBackups = newValue;
            Properties.Settings.Default.Save();
        }

        private void cmbMissingItemColorSelectionChanged(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MissingItemColor = this.cmbMissingItemColor.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            this.updateCurrentWorldAnalyzer();
        }

        private void chkShowPossibleItems_Click(object sender, RoutedEventArgs e)
        {
            bool newValue = this.chkShowPossibleItems.IsChecked.HasValue ? this.chkShowPossibleItems.IsChecked.Value : false;
            Properties.Settings.Default.ShowPossibleItems = newValue;
            Properties.Settings.Default.Save();
            this.updateCurrentWorldAnalyzer();
        }

        private void chkAutoCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            bool newValue = this.chkAutoCheckUpdate.IsChecked.HasValue ? this.chkAutoCheckUpdate.IsChecked.Value : false;
            Properties.Settings.Default.AutoCheckUpdate = newValue;
            Properties.Settings.Default.Save();
        }

        private void btnSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = saveDirPath;
            openFolderDialog.Description = "Select where your Remnant saves are stored.";
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string folderName = openFolderDialog.SelectedPath;
                if (folderName.Equals(backupDirPath))
                {
                    MessageBox.Show("Please select a folder other than the backup folder.",
                                     "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
                    return;
                }
                if (folderName.Equals(saveDirPath))
                {
                    return;
                }
                if (!RemnantSave.ValidSaveFolder(folderName))
                {
                    MessageBox.Show("Please select the folder containing your Remnant save.",
                                     "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
                    return;
                }
                this.txtSaveFolder.Text = folderName;
                saveDirPath = folderName;
                Properties.Settings.Default.SaveFolder = folderName;
                Properties.Settings.Default.Save();
                var newSave = new RemnantSave(saveDirPath);
                if (!newSave.Valid)
                {
                    return;
                }
                this.activeSave = newSave;
                this.updateCurrentWorldAnalyzer();
            }
        }


        private void TryFindGameFolder()
        {
            if (File.Exists(gameDirPath + "\\Remnant.exe"))
            {
                return;
            }

            // Check if game is installed via Steam
            // In registry, we can see IF the game is installed with steam or not
            // To find the actual game, we need to search within ALL library folders
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\Apps\\617290", false);
            if (key != null) // null if remnant is not in steam library (or steam itself is not (or never was) installed)
            {
                bool? steamRemnantInstalled = null;
                object keyValue = key.GetValue("Installed"); // Value is true when remnant is installed
                if (keyValue != null) steamRemnantInstalled = Convert.ToBoolean(keyValue);
                if (steamRemnantInstalled.HasValue && steamRemnantInstalled.Value)
                {
                    Microsoft.Win32.RegistryKey steamRegKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam", false);
                    string steamInstallPath = steamRegKey?.GetValue("SteamPath") as string; // Get install path for steam
                    DirectoryInfo steamInstallDir = new DirectoryInfo(steamInstallPath);
                    if (steamInstallDir.Exists)
                    {
                        FileInfo libraryFolders = new FileInfo(steamInstallDir.FullName + "\\steamapps\\libraryfolders.vdf");
                        // Find Steam-Library, remnant is installed in
                        //
                        string[] libraryFolderContent = File.ReadAllLines(libraryFolders.FullName);
                        int remnantIndex = Array.IndexOf(libraryFolderContent, libraryFolderContent.FirstOrDefault(t => t.Contains("\"617290\"")));
                        libraryFolderContent = libraryFolderContent.Take(remnantIndex).ToArray();
                        string steamLibraryPathRaw = libraryFolderContent.LastOrDefault(t => t.Contains("\"path\""));
                        string[] steamLibraryPathRawSplit = steamLibraryPathRaw?.Split('\"');
                        string steamLibraryPath = steamLibraryPathRawSplit?[3];

                        string steamRemnantInstallPath = $"{steamLibraryPath?.Replace("\\\\", "\\")}\\steamapps\\common\\Remnant";
                        if (Directory.Exists(steamRemnantInstallPath))
                        {
                            if (File.Exists(steamRemnantInstallPath + "\\Remnant.exe"))
                            {
                                this.SetGameFolder(steamRemnantInstallPath);
                                return;
                            }
                        }
                    }
                }
            }

            // Check if game is installed via Epic
            // Epic stores manifests for every installed game withing "C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests"
            // These "Manifests" are in json format, so if one of them is for Remnant, then Remnant is installed with epic
            var epicManifestFolder = new DirectoryInfo("C:\\ProgramData\\Epic\\EpicGamesLauncher\\Data\\Manifests");
            if (epicManifestFolder.Exists) // If Folder don't exist, epic is not installed
            {
                foreach (FileInfo fi in epicManifestFolder.GetFiles("*.item"))
                {
                    string[] itemContent = File.ReadAllLines(fi.FullName);
                    if (itemContent.All(t => t.Contains("Remnant: From the Ashes") == false)) continue;

                    string epicRemnantInstallPathRaw = itemContent.FirstOrDefault(t => t.Contains("\"InstallLocation\""));
                    string[] epicRemnantInstallPathRawSplit = epicRemnantInstallPathRaw?.Split('\"');
                    string epicRemnantInstallPath = epicRemnantInstallPathRawSplit?[3].Replace("\\\\", "\\");

                    if (Directory.Exists(epicRemnantInstallPath))
                    {
                        if (File.Exists($"{epicRemnantInstallPath}\\Remnant.exe"))
                        {
                            this.SetGameFolder(epicRemnantInstallPath);
                            return;
                        }
                    }

                    break;
                }
            }
            // Check if game is installed via Windows Store
            // TODO - don't have windows store version (yet)

            // Remnant not found or not installed, clear path
            gameDirPath = "";
            this.txtGameFolder.Text = "";
            this.btnStartGame.IsEnabled = false;
            this.btnStartGame.Content = this.FindResource("PlayGrey");
            this.backupCMStart.IsEnabled = false;
            this.backupCMStart.Icon = this.FindResource("PlayGrey");
            Properties.Settings.Default.GameFolder = "";
            Properties.Settings.Default.Save();
        }

        private void SetGameFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            gameDirPath = folderPath;
            this.txtGameFolder.Text = folderPath;
            this.btnStartGame.IsEnabled = true;
            this.btnStartGame.Content = this.FindResource("Play");
            this.backupCMStart.IsEnabled = true;
            this.backupCMStart.Icon = this.FindResource("Play");
            Properties.Settings.Default.GameFolder = folderPath;
            Properties.Settings.Default.Save();
        }
        private void BtnStartGame_Click(object sender, RoutedEventArgs e)
        {
            this.LaunchGame();
        }

        private void LaunchGame()
        {
            if (!Directory.Exists(gameDirPath))
            {
                return;
            }

            FileInfo remnantExe = new FileInfo(gameDirPath + "\\Remnant.exe");
            FileInfo remnantExe64 = new FileInfo(gameDirPath + "\\Remnant\\Binaries\\Win64\\Remnant-Win64-Shipping.exe");
            if (!remnantExe64.Exists && !remnantExe.Exists)
            {
                return;
            }

            Process.Start((remnantExe64.Exists && Environment.Is64BitOperatingSystem) ? remnantExe64.FullName : remnantExe.FullName);
        }

        private void btnGameFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = gameDirPath;
            openFolderDialog.Description = "Select where your Remnant game is installed.";
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string folderName = openFolderDialog.SelectedPath;
                if (!File.Exists(folderName + "\\Remnant.exe"))
                {
                    MessageBox.Show("Please select the folder containing your Remnant game.",
                                     "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
                    return;
                }
                if (folderName.Equals(gameDirPath))
                {
                    return;
                }

                this.SetGameFolder(folderName);
            }
        }

        private void btnFindGameFolder_Click(object sender, RoutedEventArgs e)
        {
            this.TryFindGameFolder();
        }
    }
}
