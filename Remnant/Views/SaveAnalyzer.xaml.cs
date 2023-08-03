using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace RemnantSaveManager.Remnant.Views
{
    /// <summary>
    /// Interaction logic for SaveAnalyzer.xaml
    /// </summary>
    public partial class SaveAnalyzer : Window
    {
        private Manager manager;
        public bool ActiveSave { get; set; }
        private List<RemnantCharacter> listCharacters;
        private AnalyzerColor analyzerColor;
        private Dictionary<string,Dictionary<string,double>> columnWidths;
        private bool initialized;
        public SaveAnalyzer(Manager mw)
        {
            this.initialized = false;
            this.InitializeComponent();

            this.manager = mw;

            this.listCharacters = new List<RemnantCharacter>();

            this.cmbCharacter.ItemsSource = this.listCharacters;

            this.analyzerColor = new AnalyzerColor();
            this.analyzerColor.backgroundColor = (Color)ColorConverter.ConvertFromString("#343a40");
            this.analyzerColor.textColor = (Color)ColorConverter.ConvertFromString("#f8f9fa");
            this.analyzerColor.headerBackgroundColor = (Color)ColorConverter.ConvertFromString("#70a1ff");
            this.analyzerColor.borderColor = (Color)ColorConverter.ConvertFromString("#dddddd");

            this.dgCampaign.VerticalGridLinesBrush = new SolidColorBrush(this.analyzerColor.borderColor);
            this.dgCampaign.HorizontalGridLinesBrush = new SolidColorBrush(this.analyzerColor.borderColor);
            this.dgCampaign.RowBackground = new SolidColorBrush(this.analyzerColor.backgroundColor);
            this.dgCampaign.RowHeaderStyle = new Style(typeof(DataGridRowHeader));
            this.dgCampaign.RowHeaderStyle.Setters.Add(new Setter(DataGridRowHeader.BackgroundProperty, new SolidColorBrush(this.analyzerColor.backgroundColor)));
            this.dgCampaign.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader));
            this.dgCampaign.ColumnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(this.analyzerColor.headerBackgroundColor)));

            this.dgAdventure.VerticalGridLinesBrush = new SolidColorBrush(this.analyzerColor.borderColor);
            this.dgAdventure.HorizontalGridLinesBrush = new SolidColorBrush(this.analyzerColor.borderColor);
            this.dgAdventure.RowBackground = new SolidColorBrush(this.analyzerColor.backgroundColor);
            this.dgAdventure.RowHeaderStyle = new Style(typeof(DataGridRowHeader));
            this.dgAdventure.RowHeaderStyle.Setters.Add(new Setter(DataGridRowHeader.BackgroundProperty, new SolidColorBrush(this.analyzerColor.backgroundColor)));
            this.dgAdventure.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader));
            this.dgAdventure.ColumnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(this.analyzerColor.headerBackgroundColor)));

            this.lblCredits.Content = "Thanks to /u/hzla00 for the original online implementation.\n\nLots of code used here was adapted from his original javascript (as was the styling!).";

            this.txtMissingItems.BorderThickness = new Thickness(0);

            this.columnWidths = new Dictionary<string, Dictionary<string, double>>();
            this.columnWidths.Add(this.dgCampaign.Name, new Dictionary<string, double>());
            this.columnWidths.Add(this.dgAdventure.Name, new Dictionary<string, double>());

            this.sliderSize.Value = Properties.Settings.Default.AnalyzerFontSize;
            this.treeMissingItems.FontSize = this.sliderSize.Value - 4;
            this.lblCredits.FontSize = this.sliderSize.Value;
            this.initialized = true;
            TreeViewItem nodeNormal = new TreeViewItem();
            nodeNormal.Header = "Normal";
            nodeNormal.Foreground = this.treeMissingItems.Foreground;
            nodeNormal.IsExpanded = Properties.Settings.Default.NormalExpanded;
            nodeNormal.Expanded += this.GameType_CollapsedExpanded;
            nodeNormal.Collapsed += this.GameType_CollapsedExpanded;
            nodeNormal.Tag = "mode";
            TreeViewItem nodeHardcore = new TreeViewItem();
            nodeHardcore.Header = "Hardcore";
            nodeHardcore.Foreground = this.treeMissingItems.Foreground;
            nodeHardcore.IsExpanded = Properties.Settings.Default.HardcoreExpanded;
            nodeHardcore.Expanded += this.GameType_CollapsedExpanded;
            nodeHardcore.Collapsed += this.GameType_CollapsedExpanded;
            nodeHardcore.Tag = "mode";
            TreeViewItem nodeSurvival = new TreeViewItem();
            nodeSurvival.Header = "Survival";
            nodeSurvival.Foreground = this.treeMissingItems.Foreground;
            nodeSurvival.IsExpanded = Properties.Settings.Default.SurvivalExpanded;
            nodeSurvival.Expanded += this.GameType_CollapsedExpanded;
            nodeSurvival.Collapsed += this.GameType_CollapsedExpanded;
            nodeSurvival.Tag = "mode";
            this.treeMissingItems.Items.Add(nodeNormal);
            this.treeMissingItems.Items.Add(nodeHardcore);
            this.treeMissingItems.Items.Add(nodeSurvival);
        }

        private void GameType_CollapsedExpanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem modeItem = (TreeViewItem)sender;
            if (modeItem.Header.ToString().Contains("Normal")) {
                Properties.Settings.Default.NormalExpanded = modeItem.IsExpanded;
            }
            else if (modeItem.Header.ToString().Contains("Hardcore"))
            {
                Properties.Settings.Default.HardcoreExpanded = modeItem.IsExpanded;
            }
            else if (modeItem.Header.ToString().Contains("Survival"))
            {
                Properties.Settings.Default.SurvivalExpanded = modeItem.IsExpanded;
            }
            Properties.Settings.Default.Save();
        }

        public void LoadData(List<RemnantCharacter> chars)
        {
            int selectedChar = this.cmbCharacter.SelectedIndex;
            this.listCharacters = chars;
            /*Console.WriteLine("Loading characters in analyzer: " + listCharacters.Count);
            foreach (CharacterData cd in listCharacters)
            {
                Console.WriteLine("\t" + cd);
            }*/
            this.cmbCharacter.ItemsSource = this.listCharacters;
            if (selectedChar == -1 && this.listCharacters.Count > 0) selectedChar = 0;
            if (selectedChar > -1 && this.listCharacters.Count > selectedChar) this.cmbCharacter.SelectedIndex = selectedChar;
            this.cmbCharacter.IsEnabled = (this.listCharacters.Count > 1);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.ActiveSave)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void CmbCharacter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.cmbCharacter.SelectedIndex == -1 && this.listCharacters.Count > 0) return;
            if (this.cmbCharacter.Items.Count > 0 && this.cmbCharacter.SelectedIndex > -1)
            {
                this.dgCampaign.ItemsSource = this.listCharacters[this.cmbCharacter.SelectedIndex].CampaignEvents;
                if (this.listCharacters[this.cmbCharacter.SelectedIndex].AdventureEvents.Count > 0)
                {
                    ((TabItem)this.tabAnalyzer.Items[1]).IsEnabled = true;
                    this.dgAdventure.ItemsSource = this.listCharacters[this.cmbCharacter.SelectedIndex].AdventureEvents;

                } else
                {
                    ((TabItem)this.tabAnalyzer.Items[1]).IsEnabled = false;
                    if (this.tabAnalyzer.SelectedIndex == 1) this.tabAnalyzer.SelectedIndex = 0;
                }
                this.txtMissingItems.Text = string.Join("\n", this.listCharacters[this.cmbCharacter.SelectedIndex].GetMissingItems());

                foreach (TreeViewItem item in this.treeMissingItems.Items)
                {
                    item.Items.Clear();
                }
                foreach (RemnantItem rItem in this.listCharacters[this.cmbCharacter.SelectedIndex].GetMissingItems())
                {
                    TreeViewItem item = new TreeViewItem();
                    item.Header = rItem.ItemName;
                    if (!rItem.ItemNotes.Equals("")) item.ToolTip = rItem.ItemNotes;
                    item.Foreground = this.treeMissingItems.Foreground;
                    item.ContextMenu = this.treeMissingItems.Resources["ItemContext"] as System.Windows.Controls.ContextMenu;
                    item.Tag = "item";
                    TreeViewItem modeNode = ((TreeViewItem)this.treeMissingItems.Items[(int)rItem.ItemMode]);
                    TreeViewItem itemTypeNode = null;
                    foreach (TreeViewItem typeNode in modeNode.Items)
                    {
                        if (typeNode.Header.ToString().Equals(rItem.ItemType))
                        {
                            itemTypeNode = typeNode;
                            break;
                        }
                    }
                    if (itemTypeNode == null)
                    {
                        itemTypeNode = new TreeViewItem();
                        itemTypeNode.Header = rItem.ItemType;
                        itemTypeNode.Foreground = this.treeMissingItems.Foreground;
                        itemTypeNode.IsExpanded = true;
                        itemTypeNode.ContextMenu = this.treeMissingItems.Resources["ItemGroupContext"] as System.Windows.Controls.ContextMenu;
                        itemTypeNode.Tag = "type";
                        ((TreeViewItem)this.treeMissingItems.Items[(int)rItem.ItemMode]).Items.Add(itemTypeNode);
                    }
                    itemTypeNode.Items.Add(item);
                }
            }
        }

        private void dgBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            e.Cancel = true;
        }

        private void logMessage(string message) {
            this.manager.logMessage(this.Title+": "+message);
        }

        struct AnalyzerColor
        {
            internal Color backgroundColor;
            internal Color textColor;
            internal Color headerBackgroundColor;
            internal Color borderColor;
        }
        private void autoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            double fontSize = this.sliderSize.Value;
            e.Column.HeaderStyle = new Style(typeof(DataGridColumnHeader));
            e.Column.HeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(this.analyzerColor.headerBackgroundColor)));
            e.Column.HeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, new SolidColorBrush(Colors.White)));
            e.Column.HeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8,4,8,4)));
            e.Column.HeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, fontSize));
            e.Column.HeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
            e.Column.HeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, new SolidColorBrush(this.analyzerColor.borderColor)));
            e.Column.HeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(1)));

            e.Column.CellStyle = new Style(typeof(DataGridCell));
            e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(this.analyzerColor.backgroundColor)));
            e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(4)));
            //e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(borderColor)));
            //e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(1)));
            if (e.Column.Header.Equals("MissingItems"))
            {
                e.Column.Header = "Missing Items";
                e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.FontSizeProperty, ((fontSize / 3) * 2)));
                if (Properties.Settings.Default.MissingItemColor.Equals("Red"))
                {
                    e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(Colors.Red)));
                } else
                {
                    e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(this.analyzerColor.textColor)));
                }
            } else if (e.Column.Header.Equals("PossibleItems"))
            {
                if (!Properties.Settings.Default.ShowPossibleItems)
                {
                    e.Cancel = true;
                    return;
                }
                e.Column.Header = "All Items";
                e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.FontSizeProperty, ((fontSize / 3) * 2)));
                if (Properties.Settings.Default.MissingItemColor.Equals("Red"))
                {
                    e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(Colors.Red)));
                }
                else
                {
                    e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(this.analyzerColor.textColor)));
                }
            }
            else
            {
                e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.FontSizeProperty, fontSize));
                e.Column.CellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(this.analyzerColor.textColor)));
            }

            /*DataGrid dg = (DataGrid)sender;
            if (columnWidths[dg.Name].ContainsKey(e.Column.Header.ToString()))
            {
                if (columnWidths[dg.Name][e.Column.Header.ToString()] > -1)
                {
                    e.Column.HeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.WidthProperty, columnWidths[dg.Name][e.Column.Header.ToString()]));
                }
            }*/
        }

        private void LblCredits_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://hzla.github.io/Remnant-World-Analyzer/");
        }

        private void dgCampaign_LayoutUpdated(object sender, EventArgs e)
        {
            this.saveColumnWidth(this.dgCampaign);
        }
        private void dgAdventure_LayoutUpdated(object sender, EventArgs e)
        {
            this.saveColumnWidth(this.dgAdventure);
        }

        private void saveColumnWidth(DataGrid dg)
        {
            /*if (dg.Columns.Count > 0)
            {
                for (int i=0; i < dg.Columns.Count; i++)
                {
                    if (!dg.Columns[i].Width.ToString().Equals("Auto"))
                    {
                        columnWidths[dg.Name][dg.Columns[i].Header.ToString()] = dg.Columns[i].Width.Value;
                    }
                }
                dg.UpdateLayout();
            }*/
        }

        private void autoGeneratedColumns(object sender, EventArgs e)
        {
            /*DataGrid dg = (DataGrid)sender;
            if (columnWidths[dg.Name].Count == 0)
            {
                for (int i=0; i < dg.Columns.Count; i++)
                {
                    if (dg.Columns[i].Width.ToString().Equals("Auto"))
                    {
                        columnWidths[dg.Name].Add(dg.Columns[i].Header.ToString(), -1);
                    } else
                    {
                        columnWidths[dg.Name].Add(dg.Columns[i].Header.ToString(), dg.Columns[i].Width.Value);
                    }
                }
            } */
        }

        private void sliderSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.initialized)
            {
                Properties.Settings.Default.AnalyzerFontSize = this.sliderSize.Value;
                Properties.Settings.Default.Save();

                this.dgCampaign.ItemsSource = null;
                this.dgAdventure.ItemsSource = null;
                this.CmbCharacter_SelectionChanged(null, null);
                this.treeMissingItems.FontSize = this.sliderSize.Value - 4;
                this.lblCredits.FontSize = this.sliderSize.Value;
            }

        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            string name = (string)((TabItem)this.tabAnalyzer.SelectedItem).Header;
            saveFileDialog.FileName = name + ".md";
            System.Windows.Forms.DialogResult result = saveFileDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                using (System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog.OpenFile())
                using (System.IO.StreamWriter sw = new StreamWriter(fs))
                {
                    switch (this.tabAnalyzer.SelectedIndex)
                    {
                        case 0:
                            sw.Write(this.ExportCampaign(isAdventure: false));
                            break;
                        case 1:
                            sw.Write(this.ExportCampaign(isAdventure: true));
                            break;
                        case 3:
                            sw.Write(this.ExportMissingItems());
                            break;
                        case 4:
                            sw.Write(this.ExportCredits());
                            break;
                        default:
                            throw new Exception("Tab does not exist");
                    }
                }
            }
        }

        private string FormatItems(string header, string items)
        {
            StringBuilder sb = new StringBuilder();
            if (string.IsNullOrEmpty(items))
            {
                sb.AppendLine($"- **{header}** - None");
            }
            else
            {
                sb.AppendLine($"- **{header}:**");
                foreach (string item in items.Split('\n'))
                {
                    sb.AppendLine($"  - {item}");
                }
            }
            return sb.ToString();
        }

        private string DumpEvents(IEnumerable<RemnantWorldEvent> events)
        {
            StringBuilder sb = new StringBuilder();
            foreach(RemnantWorldEvent eItem in events)
            {
                sb.AppendLine($"##### {eItem.Name}");
                sb.AppendLine($"- **Type** - {eItem.Type}");
                sb.Append(this.FormatItems("Missing Items", eItem.MissingItems));
                if (Properties.Settings.Default.ShowPossibleItems)
                {
                    sb.Append(this.FormatItems("Possible Items", eItem.PossibleItems));
                }
            }
            return sb.ToString();
        }
        private string ExportCampaign(bool isAdventure = false)
        {
            StringBuilder sb = new StringBuilder();

            List<RemnantWorldEvent> events = isAdventure ?
                this.listCharacters[this.cmbCharacter.SelectedIndex].AdventureEvents :
                this.listCharacters[this.cmbCharacter.SelectedIndex].CampaignEvents;
            foreach(var region in events.GroupBy(x => x.Location.Split(':')[0].Trim()))
            {
                sb.AppendLine();
                sb.AppendLine($"## {region.Key}");
                sb.Append(this.DumpEvents(region.Where(x => x.Location.Split(':').Length == 1)));
                foreach (var zone in region.Where(x=>x.Location.Split(':').Length > 1).GroupBy(x => x.Location.Split(':')[1].Trim()))
                {
                    sb.AppendLine($"### {zone.Key}");
                    sb.Append(this.DumpEvents(zone.Where(x => x.Location.Split(':').Length == 2)));
                    foreach (var locality in zone.Where(x => x.Location.Split(':').Length > 2).GroupBy(x => x.Location.Split(':')[2].Trim()))
                    {
                        sb.AppendLine($"#### {locality.Key}");
                        sb.Append(this.DumpEvents(locality));
                    }
                }
            }
            return sb.ToString().TrimStart('\r','\n');
        }
        private string ExportMissingItems()
        {
            StringBuilder sb = new StringBuilder();
            foreach(var mode in this.listCharacters[this.cmbCharacter.SelectedIndex].GetMissingItems().GroupBy(x => x.ItemMode))
            {
                sb.AppendLine($"## {mode.Key}");
                foreach(var type in mode.GroupBy(x=> x.ItemType))
                {
                    sb.AppendLine($"- {type.Key}");
                    foreach (var item in type)
                    {
                        if (string.IsNullOrEmpty(item.ItemNotes))
                        {
                            sb.AppendLine($"  - {item.ItemName}");
                        }
                        else
                        {
                            sb.AppendLine($"  - {item.ItemName} {{{item.ItemNotes}}}");
                        }
                    }
                }
            }
            return sb.ToString();
        }
        private string ExportCredits()
        {
            return (string)this.lblCredits.Content;
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            switch (this.tabAnalyzer.SelectedIndex)
            {
                case 0:
                    Clipboard.SetText(this.ExportCampaign(isAdventure: false));
                    break;
                case 1:
                    Clipboard.SetText(this.ExportCampaign(isAdventure: true));
                    break;
                case 3:
                    Clipboard.SetText(this.ExportMissingItems());
                    break;
                case 4:
                    Clipboard.SetText(this.ExportCredits());
                    break;
                default:
                    throw new Exception("Tab does not exist");
            }

            MessageBox.Show("Content copied.");
        }

        private string GetTreeItem(TreeViewItem item)
        {
            if ((string)item.Tag == "item") return item.Header.ToString();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(item.Header.ToString() + ":");
            foreach (TreeViewItem i in item.Items)
            {
                sb.AppendLine("\t- " + this.GetTreeItem(i));
            }
            return sb.ToString();
        }

        private void CopyItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mnu = sender as MenuItem;
            TreeViewItem treeItem = ((ContextMenu)mnu?.Parent)?.PlacementTarget as TreeViewItem;

            Clipboard.SetText(this.GetTreeItem(treeItem));
        }

        private void SearchItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mnu = sender as MenuItem;
            TreeViewItem treeItem = ((ContextMenu)mnu?.Parent)?.PlacementTarget as TreeViewItem;
            var type = ((TreeViewItem)treeItem?.Parent)?.Header.ToString();
            var itemname = treeItem?.Header.ToString();

            if (type == "Armor")
            {
                itemname = itemname.Substring(0, itemname.IndexOf("(")) + "Set";
            }

            System.Diagnostics.Process.Start($"https://remnantfromtheashes.wiki.fextralife.com/{itemname}");
        }
    }
}
