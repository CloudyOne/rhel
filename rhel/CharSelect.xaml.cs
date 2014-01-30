using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace rhel {
    /// <summary>
    /// Interaction logic for CharSelect.xaml
    /// </summary>
    public partial class CharSelect : Window {
        private string settingsPath;
        private Account acct;
        private List<string> SpecificIDs = new List<string>();

        public CharSelect(Account acct, object specificIDs) {
            InitializeComponent();
            this.acct = acct;
            this.SpecificIDs = string.Format("{0}", specificIDs).Split(',').ToList();
            this.settingsPath = String.Format("{0}\\{1}", this.acct.main.localAppPath(), "settings");
            this.popListFromFile();
            this.popListFromSettings();
            this.checkBoxes();
        }

        private void checkBoxes() {
            foreach (CSelect c in this.CharacterPanel.Children) {
                if (this.acct.charIDs.Contains(c.charID)) {
                    c.selected.IsChecked = true;
                }
            }
        }

        private void popListFromFile() {
            if (Properties.Settings.Default.Characters == null) {
                return;
            }
            foreach (string chara in Properties.Settings.Default.Characters) {
                string[] split = chara.Split(new char[] { ',' });
                CSelect character = new CSelect();
                character.charID = Convert.ToInt32(split[0]);
                character.charName.Text = split[1];

                // Handles old files that do not have the userID
                if (split.Length > 2) 
                    character.userID = Convert.ToInt32(split[2]);
                // Only show if no id was specified, or character matches id
                if (SpecificIDs.Count == 0 || SpecificIDs.Contains(character.userID.ToString()))
                    this.CharacterPanel.Children.Add(character);
                
            }
        }

        private void popListFromSettings() {
            string dlstr = String.Format("https://api.eveonline.com/eve/CharacterName.xml.aspx?ids={0}", getIDs());
            System.Xml.XmlReader reader = System.Xml.XmlReader.Create(dlstr);
            List<CSelect> charlist = new List<CSelect>();
            while (reader.Read()) {
                if (reader.HasAttributes) {
                    if (reader.IsEmptyElement) {
                        CSelect cha = new CSelect();
                        cha.charName.Text = reader.GetAttribute(0);
                        cha.charID = Convert.ToInt32(reader.GetAttribute(1));                        
                        charlist.Add(cha);
                    }
                }

            }
            foreach (CSelect c in charlist) {
                List<int> idList = new List<int>();
                if (this.CharacterPanel.Children.Count == 0) {
                    foreach (CSelect chara in charlist) {
                        this.CharacterPanel.Children.Add(chara);
                    }
                }
                foreach (CSelect chara in this.CharacterPanel.Children) {
                    idList.Add(chara.charID);
                }
                if (!idList.Contains(c.charID)) {
                    this.CharacterPanel.Children.Add(c);
                }
            }
        }

        private string getIDs() {
            List<int> temp = new List<int>();

            foreach (string file in System.IO.Directory.EnumerateFiles(this.settingsPath)) {
                string[] split = file.Split(new string[] { "core_char_", ".dat" }, StringSplitOptions.None);
                if (split.Length == 3 && split[1].Length > 1) {
                    temp.Add(Convert.ToInt32(split[1]));
                }
            }
            string ids = "";
            if (SpecificIDs.Count == 0)
            {
                foreach (int i in temp)
                {
                    ids += String.Format("{0}{1}", i, ",");
                }
                ids = ids.Substring(0, ids.Length - 1);
            }
            else ids = string.Join(",",SpecificIDs.ToArray());
            return ids;
        }

        private void cancel_Click(object sender, RoutedEventArgs e) {
            this.acct.charIDs.Clear();
            this.acct.main.updateCredentials();
            foreach (CSelect c in this.CharacterPanel.Children) {
                if (c.selected.IsChecked == true) {
                    this.acct.charIDs.Add(c.charID);
                }
            }
            this.acct.main.updateCredentials();
            this.Close();
        }
    }
}
