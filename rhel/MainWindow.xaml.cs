using System;
using System.Timers;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;

namespace rhel {
    public partial class MainWindow : Window {
        System.Windows.Forms.NotifyIcon tray; // yes, we're using Windows.Forms in a WPF project
        bool saveAccounts = false;
        int eveVersion;
        EventHandler contextMenuClick;
        DateTime updateCheckExpiration = new DateTime();
        Timer checkUpdate;
        RijndaelManaged rjm = new RijndaelManaged();

        public MainWindow() {
            InitializeComponent();
            string key = this.getKey();
            string iv = this.getIV();
            this.rjm.Key = Convert.FromBase64String(key);
            this.rjm.IV = Convert.FromBase64String(iv);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if (Properties.Settings.Default.evePath.Length == 0) {
                string path = null;
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                foreach (string dir in Directory.EnumerateDirectories(Path.Combine(appdata, "CCP", "EVE"), "*_tranquility")) {
                    string[] split = dir.Split(new char[] { '_' }, 2);
                    string drive = split[0].Substring(split[0].Length - 1);
                    path = split[1].Substring(0, split[1].Length - "_tranquility".Length).Replace('_', Path.DirectorySeparatorChar);
                    path = drive.ToUpper() + Path.VolumeSeparatorChar + Path.DirectorySeparatorChar + path;
                    break;
                }
                if (path != null && File.Exists(Path.Combine(path, "bin", "ExeFile.exe"))) {
                    Properties.Settings.Default.evePath = path;
                    Properties.Settings.Default.Save();
                }
            }
            this.txtEvePath.Text = EvePath;
            this.tray = new System.Windows.Forms.NotifyIcon();
            this.tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ResourceAssembly.Location);
            this.tray.Text = this.Title;
            this.tray.ContextMenu = new System.Windows.Forms.ContextMenu();
            this.tray.MouseClick += new System.Windows.Forms.MouseEventHandler(this.tray_Click);
            this.contextMenuClick = new EventHandler(this.contextMenu_Click);
            this.tray.ContextMenu.MenuItems.Add("launch all", this.contextMenuClick);
            this.tray.ContextMenu.MenuItems.Add("-");
            if (Properties.Settings.Default.accounts != null) {
                foreach (string credentials in Properties.Settings.Default.accounts) {
                    Account account = new Account(this);
                    string[] split = credentials.Split(new char[] { ':' }, 4);
                    account.username.Text = split[0];
                    account.password.Password = this.decryptPass(rjm, split[1]);
                    if (split.Length > 2) {
                        account.accountID.Text = split[2];
                    }
                    if (split.Length > 3) {
                        string[] chars = split[3].Split(new char[] { ':' });
                        foreach (string character in chars) {
                            if (character != "") {
                                account.charIDs.Add(Convert.ToInt32(character));
                            }
                        }
                    }
                    this.accountsPanel.Children.Add(account);
                }
            }
            this.tray.ContextMenu.MenuItems.Add("-");
            if (Properties.Settings.Default.groups != null) {
                foreach (string gp in Properties.Settings.Default.groups) {
                    Group G = new Group(this);
                    string[] split = gp.Split(new char[] { ':' });
                    foreach (string s in split) {
                        if (s == split[0]) {
                            G.groupname.Text = s;
                        }
                        else {
                            foreach (Account account in this.accountsPanel.Children) {
                                if (s == account.username.Text) {
                                    G.addAccount(account);
                                }
                            }
                        }
                    }
                    this.groupsPanel.Children.Add(G);
                }
            }
            this.popContextMenu();
            this.tray.Visible = true;
            this.saveAccounts = true;
            this.startUpdateCheck();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            this.tray.Visible = false;
        }

        private void Window_StateChanged(object sender, EventArgs e) {
            this.ShowInTaskbar = (this.WindowState != System.Windows.WindowState.Minimized);
        }

        private void tray_Click(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                this.WindowState = System.Windows.WindowState.Normal;
        }

        private void contextMenu_Click(object sender, EventArgs e) {
            string username = ((System.Windows.Forms.MenuItem)sender).Text;
            if (username == "launch all")
                foreach (Account account in this.accountsPanel.Children)
                    account.launchAccount();
            else {
                foreach (Account account in this.accountsPanel.Children) {
                    if (account.username.Text == username) {
                        account.launchAccount();
                        break;
                    }
                }
                foreach (Group group in this.groupsPanel.Children) {
                    if (group.groupname.Text == username) {
                        group.launchGroup();
                        break;
                    }
                }
            }
        }

        private void popContextMenu() {
            while (this.tray.ContextMenu.MenuItems.Count > 2) {
                this.tray.ContextMenu.MenuItems.RemoveAt(this.tray.ContextMenu.MenuItems.Count - 1);
            }
            foreach (Account account in this.accountsPanel.Children) {
                this.tray.ContextMenu.MenuItems.Add(account.username.Text, this.contextMenuClick);
            }
            this.tray.ContextMenu.MenuItems.Add("-");
            foreach (Group group in this.groupsPanel.Children) {
                this.tray.ContextMenu.MenuItems.Add(group.groupname.Text, this.contextMenu_Click);
            }
        }

        private void txtEvePath_LostFocus(object sender, RoutedEventArgs e) {
            this.SetEvePath(this.txtEvePath.Text);
        }

        private void browse_Click(object sender, RoutedEventArgs e) {
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            fbd.ShowNewFolderButton = false;
            fbd.SelectedPath = this.txtEvePath.Text;
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                this.txtEvePath.Text = fbd.SelectedPath;
                this.SetEvePath(fbd.SelectedPath);
            }
        }

        private void addAccount_Click(object sender, RoutedEventArgs e) {
            Account account = new Account(this);
            this.accountsPanel.Children.Add(account);
        }

        private void addGroup_Click(object sender, RoutedEventArgs e) {
            Group group = new Group(this);
            this.groupsPanel.Children.Add(group);
        }

        public string EvePath
        {
            get
            {
                if (string.IsNullOrEmpty(Properties.Settings.Default.evePath))
                { // Attempts to find the install directory if first time loading
                    foreach (var path in InstallationPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            Properties.Settings.Default.evePath = path;
                            Properties.Settings.Default.Save();                            
                            break;
                        }
                    }
                }
                return Properties.Settings.Default.evePath;
            }
        }
        public string EveExeFile
        {
            get
            {
                if (string.IsNullOrEmpty(EvePath))
                    throw new FileNotFoundException("Unable to find the location of your EVE installation");

                return Path.Combine(EvePath, "bin", "ExeFile.exe");
            }
        }


        public void SetEvePath(string path) {
            string exefilePath = Path.Combine(path, "bin", "ExeFile.exe");
            if (File.Exists(exefilePath))
            {
                Properties.Settings.Default.evePath = path;
                Properties.Settings.Default.Save();
            }
            else
                this.showBalloon("eve path", "could not find " + exefilePath, System.Windows.Forms.ToolTipIcon.Error);
        }

        public void updateCredentials() {
            if (!this.saveAccounts) // don't save accounts when we're still loading them into textboxes
                return;
            StringCollection accounts = new StringCollection();
            foreach (Account account in this.accountsPanel.Children) {
                string credentials = String.Format("{0}:{1}:{2}", account.username.Text, this.encryptPass(this.rjm,account.password.Password), account.accountID.Text);
                foreach ( int character in account.charIDs ) {
                    credentials = String.Format("{0}:{1}", credentials, character);
                }
                accounts.Add(credentials);
            }
            Properties.Settings.Default.accounts = accounts;
            Properties.Settings.Default.Save();
            this.popContextMenu();
        }

        public void updateGroups() {
            if (!this.saveAccounts)
                return;
            StringCollection groups = new StringCollection();
            foreach (Group gp in this.groupsPanel.Children) {
                string gName = gp.groupname.Text + ":";
                foreach (Account acct in gp.getAccounts()) {
                    gName += (acct.username.Text + ":");
                }
                groups.Add(gName);
            }
            Properties.Settings.Default.groups = groups;
            Properties.Settings.Default.Save();
            this.popContextMenu();
        }
        public void showBalloon(string title, string text, System.Windows.Forms.ToolTipIcon icon) {
            this.tray.ShowBalloonTip(1000, title, text, icon);
        }

        public DateTime getUpdateTime() {
            return this.updateCheckExpiration;
        }

        public void setUpdateTime(DateTime dt) {
            this.updateCheckExpiration = dt;
        }

        public bool checkClientVersion() {
            this.updateEveVersion();
            StreamReader sr = new StreamReader(this.EvePath + "\\start.ini");
            List<string> lines = new List<string>();
            while (!sr.EndOfStream) {
                lines.Add(sr.ReadLine());
            }
            sr.Close();
            int clientVers = Convert.ToInt32(lines[2].Substring(8));
            if (this.eveVersion == clientVers) {
                return true;
            }
            else {
                System.Diagnostics.ProcessStartInfo repair = new System.Diagnostics.ProcessStartInfo(@".\repair.exe", "-c");
                repair.WorkingDirectory = this.EvePath;
                System.Diagnostics.Process.Start(repair);
                return false;
            }
        }

        private void updateEveVersion() {
            if (DateTime.UtcNow > this.getUpdateTime()) {
                System.Net.WebClient wc = new System.Net.WebClient();
                string ds = wc.DownloadString(new Uri("http://client.eveonline.com/patches/patches.asp?s=&test=&system=win"));
                string[] var = ds.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);
                this.eveVersion = Convert.ToInt32(var[104].Substring(29,6));
                wc.Dispose();
                updateCheckExpiration = (DateTime.UtcNow + TimeSpan.FromHours(1));
            }
        }

        private void timedUpdate() {
            if (!this.checkClientVersion()) {
            }
        }

        private void startUpdateCheck() {
            checkUpdate = new Timer(3600000);
            checkUpdate.Enabled = true;
            checkUpdate.Elapsed += new ElapsedEventHandler(checkUpdate_Elapsed);
        }

        private void checkUpdate_Elapsed(object source, ElapsedEventArgs e) {
            this.timedUpdate();
        }

        private void patchClient_Click(object sender, RoutedEventArgs e) {
            System.Diagnostics.ProcessStartInfo repair = new System.Diagnostics.ProcessStartInfo(@".\repair.exe", "-c");
            repair.WorkingDirectory = this.EvePath;
            System.Diagnostics.Process.Start(repair);
        }

        private string getKey() {
            if (Properties.Settings.Default.Key != null && Properties.Settings.Default.Key != "") {
                return Properties.Settings.Default.Key;
            }
            else {
                this.rjm.GenerateKey();
                Properties.Settings.Default.Key = Convert.ToBase64String(this.rjm.Key);
                Properties.Settings.Default.Save();
                return Properties.Settings.Default.Key;
            }
        }

        private string getIV() {
            if (Properties.Settings.Default.IV != null && Properties.Settings.Default.IV != "") {
                return Properties.Settings.Default.IV;
            }
            else {
                this.rjm.GenerateIV();
                Properties.Settings.Default.IV = Convert.ToBase64String(this.rjm.IV);
                Properties.Settings.Default.Save();
                return Properties.Settings.Default.IV;
            }
        }

        private string encryptPass(RijndaelManaged rin, string pass) {
            ICryptoTransform encryptor = rin.CreateEncryptor();
            byte[] inblock = Encoding.Unicode.GetBytes(pass);
            byte[] encrypted = encryptor.TransformFinalBlock(inblock, 0, inblock.Length);
            string epass = Convert.ToBase64String(encrypted);
            return epass;
        }

        private string decryptPass(RijndaelManaged rin, string epass) {
            ICryptoTransform decryptor = rin.CreateDecryptor();
            byte[] pass = Convert.FromBase64String(epass);
            byte[] outblock = decryptor.TransformFinalBlock(pass, 0, pass.Length);
            string dstring = Encoding.Unicode.GetString(outblock);
            return dstring;
        }

        private void clientSettings_Click(object sender, RoutedEventArgs e) {
            SettingsWindow sw = new SettingsWindow();
            sw.Show();
        }

        private void uiSettings_Click(object sender, RoutedEventArgs e) {
            uiWindow ui = new uiWindow(this);
            ui.Show();
        }

        private string[] InstallationPaths = 
        {             
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\CCP\EVE\",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\CCP\EVE\",                                          
        };
        private string AppPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\CCP\EVE\";

        public string localAppPath() {
            var appdata = string.Format("{0}{1}tranquility", this.AppPath, this.EvePath.Replace("\\", "_").Replace(" ","_").Replace(":",""));
            return appdata;
        }
    }
}
