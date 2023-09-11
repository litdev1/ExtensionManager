using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Net;
using System.Security;
using System.Security.Policy;

namespace ExtensionManagerLibrary
{
    /// <summary>
    /// For use with passing extension lists
    /// </summary>
    public static class ExtensionLists
    {
        /// <summary>
        /// list of disabled extension names (xxx) in xxx.dll - they must be installed
        /// </summary>
        public static List<string> disabled = new List<string>();

        /// <summary>
        /// list of enabled extension names (xxx) in xxx.dll - they must be installed
        /// </summary>
        public static List<string> enabled = new List<string>();
    }

    /// <summary>
    /// Interaction logic for WindowEM.xaml
    /// </summary>
    public partial class EMWindow : Window
    {
        public static string SettingsPath = "";

        /// <summary>
        /// Start Extension Manager
        /// </summary>
        public EMWindow(string settingsPath = "", string _installationPath = "")
        {
            SettingsPath = settingsPath;
            installationPath = _installationPath;
            InitializeComponent();
        }

        public static string installationPath = "";
        public static string databasePath = "";
        public static bool bWebAccess = true;

        private Extension smallBasicLibrary = new Extension();
        private Version SBVersion = new Version("0.0.0.0");
        private WebExtension webExtension = new WebExtension();
        private LocalExtension localExtension = new LocalExtension();
        private Thread thread = null;
        private bool bWorking = false;
        private Timer timer;
        private List<EMButton> EMButtons = new List<EMButton>();
        private Ellipse help = new Ellipse();
        private bool bInitialised = false;
        private TextBox tbInitialise;
        private int EMVersion = 2;
        private Cursor defaultCursor;

        /// <summary>
        /// Download database of extensions
        /// </summary>
        private int UpdateDatabase()
        {
            bWorking = true;

            FileInfo fileInf = new FileInfo(databasePath);
            int iValid = (fileInf.Exists && fileInf.Length > 0) ? 1 :- 1;
            try
            {
                Uri uri = new Uri("https://litdev.uk/extensions/ExtensionDatabase.xml");
                WebRequest.DefaultWebProxy.Credentials = CredentialCache.DefaultCredentials;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);

                int bufferSize = 2048;
                byte[] buffer = new byte[bufferSize];

                WebResponse webResponse = webRequest.GetResponse();
                Stream stream = webResponse.GetResponseStream();
                FileStream fs = fileInf.OpenWrite();

                int readCount;
                do
                {
                    readCount = stream.Read(buffer, 0, bufferSize);
                    fs.Write(buffer, 0, readCount);
                } while (readCount > 0);
                stream.Close();
                fs.Close();
                webResponse.Close();

                fileInf = new FileInfo(databasePath);
                if (fileInf.Exists && fileInf.Length > 0) iValid = 0;
                else MessageBox.Show("Database could not be downloaded or is empty", "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
            catch (Exception ex)
            {
                if (iValid < 0)
                {
                    MessageBox.Show("Database could not be downloaded\n\n" + ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    bWebAccess = false;
                    MessageBox.Show("Database could not be downloaded\n\n" + ex.Message + "\n\nUsing a previous existing version\nWeb downloads will not be possible", "Small Basic Extension Manager Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            LogDownload("Database downloaded");

            bWorking = false;
            return iValid;
        }

        public static void LogDownload(string message)
        {
            if (!bWebAccess) return;
            string url = "https://litdev.uk/extensions/server.php?message=" + message;
            try
            {
                WebRequest.DefaultWebProxy.Credentials = CredentialCache.DefaultCredentials;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                WebRequest webRequest = WebRequest.Create(url);
                WebResponse webResponse = webRequest.GetResponse();
            }
            catch (Exception ex)
            {
            }
        }

        private void Initialise()
        {
            bInitialised = true;
            this.Title += " (Version " + EMVersion + ")";

            if (installationPath == "") installationPath = Settings.GetValue("SBINSTALLATIONPATH");
            if (null == installationPath || !Directory.Exists(installationPath))
            {
                installationPath = Environment.Is64BitOperatingSystem ? "C:\\Program Files (x86)\\Microsoft\\Small Basic" : "C:\\Program Files\\Microsoft\\Small Basic";
            }
            installationPath.Trim(new char[] { '\\' });

            databasePath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\ExtensionDatabase.xml";

            if (!Directory.Exists(installationPath + "\\lib"))
            {
                MessageBox.Show(installationPath + "\\lib" + " Not found", "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            if (UpdateDatabase() < 0)
            {
                this.Close();
                return;
            }

            smallBasicLibrary.Verify(installationPath + "\\SmallBasicLibrary.dll");
            SBVersion = smallBasicLibrary.ExtVersion;

            MakeButtons();
            SetButtonExtensionLists();
            if (webExtension.version > EMVersion)
            {
                if (MessageBox.Show("A more recent verion of this Extension Manager is available.\n\nDo you want to visit download site?", "Small Basic Extension Manager Information", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Process.Start("https://gallery.technet.microsoft.com/Small-Basic-Extension-e54560ce");
                }
            }

            help.Width = 32;
            help.Height = 32;
            help.Fill = new ImageBrush(GetBitmapImage(Properties.Resources.Help));
            help.MouseDown += new MouseButtonEventHandler(OnHelp);
            help.MouseEnter += new MouseEventHandler(OnHelpEnter);
            help.MouseLeave += new MouseEventHandler(OnHelpLeave);
            help.Opacity = 0.6;
            gridMain.Children.Add(help);
            help.RenderTransform = new TranslateTransform(gridMain.ActualWidth/2 - 50, gridMain.ActualHeight/2 - 40);

            ToolTip tooltip = new ToolTip();
            help.ToolTip = tooltip;
            tooltip.Content = "";
            tooltip.Foreground = new SolidColorBrush(Colors.Black);
            tooltip.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 255));
            tooltip.BorderThickness = new Thickness(0);
            tooltip.FontSize = 14;
            tooltip.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            ToolTipService.SetInitialShowDelay(help, 0);
            ToolTipService.SetShowDuration(help, 20000);
            ToolTipService.SetHorizontalOffset(help, -275);
            ToolTipService.SetVerticalOffset(help, -140);
            ToolTipService.SetPlacement(help, PlacementMode.Right);
            ToolTipService.SetHasDropShadow(help, false);
            string info = "Small Basic extensions\nallow access to additional\nfunctionality\n\nRight click an extension\nfor more details\n\nExtensions can be installed,\nuninstalled, updated or\nenabled/disabled";
            tooltip.Content = info;

            progressBar.Visibility = Visibility.Visible;
            tbInitialise.Visibility = Visibility.Hidden;
        }

        private void MakeButtons()
        {
            webExtension.Load(databasePath);
            localExtension.Load(installationPath);

            double top = 20;
            EMButtons.Clear();

            foreach (Extension extensionWeb in webExtension.extensions)
            {
                if (extensionWeb.SBVersion == SBVersion)
                {
                    EMButton.eState state = EMButton.eState.INSTALL;
                    foreach (Extension extensionLocal in localExtension.extensions)
                    {
                        extensionLocal.InstalledVersion = extensionLocal.ExtVersion;
                        // Web and local have the same name - assumed the same extension - we will ignore one of them
                        if (extensionLocal.Name == extensionWeb.Name)
                        {
                            state = EMButton.eState.UPDATE;
                            extensionWeb.InstalledVersion = extensionLocal.InstalledVersion;
                            // Ignore local (include web) if identical since web has access to download and descriptionn etc
                            if (extensionLocal.ExtVersion == extensionWeb.ExtVersion)
                            {
                                extensionLocal.SBVersion = null;
                                if (File.Exists(installationPath + "\\lib\\" + extensionLocal.Name + ".dll"))
                                {
                                    state = EMButton.eState.INSTALLED;
                                }
                                else
                                {
                                    state = EMButton.eState.DISABLED;
                                }
                            }
                            // Include local (ignore web) if web version is less than local installed
                            else if (IsVersionLess(extensionWeb.ExtVersion, extensionLocal.ExtVersion))
                            {
                                state = EMButton.eState.NONE;
                            }
                            // Ignore local (include web) if web version is greater than local installed
                            else
                            {
                                extensionLocal.SBVersion = null;
                                if (!File.Exists(installationPath + "\\lib\\" + extensionLocal.Name + ".dll"))
                                {
                                    state = EMButton.eState.DISABLED;
                                }
                                extensionLocal.SBVersion = null;
                            }
                            break;
                        }
                    }
                    if (state != EMButton.eState.NONE)
                    {
                        EMButton button = AddButton(extensionWeb, 50, top);
                        button.SetState(state);
                        EMButtons.Add(button);
                        top += 50;
                    }
                }
            }
            bool test = false; //Test to create the different types
            int i = 0;
            foreach (Extension extensionLocal in localExtension.extensions)
            {
                if (extensionLocal.SBVersion == SBVersion)
                {
                    EMButton button = AddButton(extensionLocal, 50, top);
                    button.SetState(EMButton.eState.INSTALLED);
                    if (!File.Exists(installationPath + "\\lib\\" + extensionLocal.Name + ".dll"))
                    {
                        button.SetState(EMButton.eState.DISABLED);
                    }
                    EMButtons.Add(button);
                    top += 50;
                }
                else if (test && null != extensionLocal.SBVersion)
                {
                    EMButton button = AddButton(extensionLocal, 50, top);
                    if (i == 0) button.SetState(EMButton.eState.DISABLED);
                    else if (i == 1) button.SetState(EMButton.eState.INSTALL);
                    else if (i == 2) button.SetState(EMButton.eState.INSTALLED);
                    else button.SetState(EMButton.eState.UPDATE);
                    i++;
                    EMButtons.Add(button);
                    top += 50;
                }
            }

            grid.Height = top;
        }

        private void OnHelpEnter(object sender, MouseEventArgs e)
        {
            help.Opacity = 0.9;
        }

        private void OnHelpLeave(object sender, MouseEventArgs e)
        {
            help.Opacity = 0.6;
        }

        private void OnHelp(object sender, MouseButtonEventArgs e)
        {
            //((ToolTip)help.ToolTip).IsOpen = true;
        }

        private bool IsVersionLess(Version version1, Version version2)
        {
            return false;
            if (version1.Major < version2.Major) return true;
            if (version1.Minor < version2.Minor) return true;
            if (version1.Build < version2.Build) return true;
            if (version1.Revision < version2.Revision) return true;
            return false;
        }

        private void GetButtonExtensionLists()
        {
            ExtensionLists.disabled.Clear();
            ExtensionLists.enabled.Clear();
            foreach (EMButton button in EMButtons)
            {
                if (button.GetState() == EMButton.eState.DISABLED)
                {
                    ExtensionLists.disabled.Add(((Extension)button.Tag).Name);
                }
                else if (button.GetState() == EMButton.eState.INSTALLED)
                {
                    ExtensionLists.enabled.Add(((Extension)button.Tag).Name);
                }
            }
        }

        private void SetButtonExtensionLists()
        {
            foreach (string name in ExtensionLists.disabled)
            {
                foreach (EMButton button in EMButtons)
                {
                    Extension extension = (Extension)button.Tag;
                    if (button.GetState() == EMButton.eState.INSTALLED && extension.Name == name)
                    {
                        button.SetState(EMButton.eState.DISABLED);
                    }
                }
            }
        }

        public static BitmapImage GetBitmapImage(Bitmap image)
        {
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            MemoryStream ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            return bitmapImage;
        }

        private EMButton AddButton(Extension extension, double left, double top)
        {
            EMButton button = new EMButton(extension);
            button.Click += new RoutedEventHandler(OnButtonClicked);
            canvas.Children.Add(button);
            canvas.Children.Add(button.bar);
            Canvas.SetLeft(button, left < 0 ? (canvas.ActualWidth - button.Width) / 2.0: left);
            Canvas.SetTop(button, top);
            Canvas.SetLeft(button.bar, (left < 0 ? (canvas.ActualWidth - button.Width) / 2.0 : left) - button.bar.Width);
            Canvas.SetTop(button.bar, top);
            foreach (MenuItem item in button.ContextMenu.Items)
            {
                item.Click += new RoutedEventHandler(OnMenuItemClicked);
            }
            return button;
        }

        private void OnTimer(object state)
        {
            try
            {
                if (!bInitialised)
                {
                    if (CheckAccess())
                    {
                        Cursor = Cursors.Wait;
                        Initialise();
                        Cursor = defaultCursor;
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Cursor = Cursors.Wait;
                            Initialise();
                            Cursor = defaultCursor;
                        });
                    }
                }

                double progressValue = 0;
                if (ProgressStats.fullSize > 0) progressValue = 100 * ProgressStats.currentSize / ProgressStats.fullSize;
                progressValue = Math.Min(100, Math.Max(0, progressValue));
                if (CheckAccess())
                {
                    Cursor = bWorking ? Cursors.Wait : defaultCursor;
                    progressBar.Value = progressValue;
                    if (progressValue == 100)
                    {
                        ProgressStats.currentSize = 0;
                    }
                }
                else
                {
                    Dispatcher.Invoke(() => {
                        Cursor = bWorking ? Cursors.Wait : defaultCursor;
                        progressBar.Value = progressValue;
                    });
                    if (progressValue == 100)
                    {
                        ProgressStats.currentSize = 0;
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void RunInstall(Object obj)
        {
            bWorking = true;
            EMButton button = (EMButton)((Object[])obj)[0];
            Extension extension = (Extension)((Object[])obj)[1];
            bool bInstall = (bool)((Object[])obj)[2];
            if (null != extension && extension.Source == eSource.WEB)
            {
                if (button.GetState() == EMButton.eState.DISABLED)
                {
                    MessageBox.Show("Enable before installing or uninstalling", "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    bWorking = false;
                    return;
                }
                extension.Errors.Clear();
                extension.Valid = true;
                if (bInstall)
                {
                    extension.DownloadZip();
                    extension.UnZip();
                    if (extension.smallBasicExtension.dllFiles.numFile == 0) extension.Errors.Add("No dll files in download zip");
                    else extension.Verify(extension.LocalUnZipPath + "\\" + extension.smallBasicExtension.dllFiles.Files[0].File);
                    extension.Validate();
                }
                if (extension.Valid)
                {
                    string command = "cd \"" + installationPath + "\\lib\"";
                    if (null != extension && extension.Valid)
                    {
                        for (int i = 0; i < extension.smallBasicExtension.dllFiles.numFile; i++)
                        {
                            string file = extension.LocalUnZipPath + "\\" + extension.smallBasicExtension.dllFiles.Files[i].File;
                            string newFile = installationPath + "\\lib\\" + System.IO.Path.GetFileName(file);
                            command += " & del \"" + newFile + "\"";
                            if (bInstall) command += " & copy /B/Y \"" + file + "\" \"" + newFile + "\"";
                        }
                        for (int i = 0; i < extension.smallBasicExtension.xmlFiles.numFile; i++)
                        {
                            string file = extension.LocalUnZipPath + "\\" + extension.smallBasicExtension.xmlFiles.Files[i].File;
                            string newFile = installationPath + "\\lib\\" + System.IO.Path.GetFileName(file);
                            command += " & del \"" + newFile + "\"";
                            if (bInstall) command += " & copy /B/Y \"" + file + "\" \"" + newFile + "\"";
                        }
                    }
                    extension.UACcommand(command);
                    if (null != extension && extension.Valid && bInstall)
                    {
                        for (int i = 0; i < extension.smallBasicExtension.dllFiles.numFile; i++)
                        {
                            string file = extension.LocalUnZipPath + "\\" + extension.smallBasicExtension.dllFiles.Files[i].File;
                            Zone zone = Zone.CreateFromUrl(file);
                            if (File.Exists(file) && zone.SecurityZone != SecurityZone.MyComputer)
                            {
                                MessageBox.Show(extension.smallBasicExtension.dllFiles.Files[i].File + " is internet blocked", "Small Basic Extension Manager Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }
                if (bInstall && !extension.Valid)
                {
                    string message = "";
                    foreach (string Error in extension.Errors)
                    {
                        message += Error + "\n";
                    }
                    message += "\nDo you want to continue anyway?";
                    if (MessageBox.Show(message, "Small Basic Extension Manager Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        ZipFile zip = ZipFile.Read(extension.LocalZip);
                        string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/" + extension.Name + "-" + extension.ExtVersion.ToString();
                        zip.ExtractAll(path, ExtractExistingFileAction.OverwriteSilently);
                        zip.Dispose();
                        extension.Valid = true; //We did it anyway
                    }
                    else
                    {
                        try
                        {
                            if (CheckAccess())
                            {
                                extension.Unload();
                            }
                            else
                            {
                                Dispatcher.Invoke(() => { extension.Unload(); });
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                if (extension.Valid)
                {
                    try
                    {
                        if (CheckAccess())
                        {
                            GetButtonExtensionLists();
                            MakeButtons();
                            SetButtonExtensionLists();
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                GetButtonExtensionLists();
                                MakeButtons();
                                SetButtonExtensionLists();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else if (null != extension && extension.Source == eSource.LOCAL && !bInstall)
            {
                if (MessageBox.Show("This will remove a local extension that cannot be reinstalled from this Extension Manager\nOK to proceed?", "Uninstall Local Extension", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                string command = "cd \"" + installationPath + "\\lib\"";
                if (null != extension)
                {
                    string file = installationPath + "\\lib\\" + extension.Name + ".dll";
                    command += " & del \"" + file + "\"";
                    file = installationPath + "\\lib\\" + extension.Name + ".xml";
                    command += " & del \"" + file + "\"";
                }
                extension.UACcommand(command);
                try
                {
                    if (!File.Exists(installationPath + "\\lib\\" + extension.Name + ".dll"))
                    {
                        if (CheckAccess())
                        {
                            GetButtonExtensionLists();
                            MakeButtons();
                            SetButtonExtensionLists();
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                GetButtonExtensionLists();
                                MakeButtons();
                                SetButtonExtensionLists();
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            bWorking = false;
        }

        private void RunDownload(Object obj)
        {
            bWorking = true;
            EMButton button = (EMButton)((Object[])obj)[0];
            Extension extension = (Extension)((Object[])obj)[1];
            if (null != extension && extension.Source == eSource.WEB)
            {
                if (!extension.Downloaded)
                {
                    extension.Errors.Clear();
                    extension.Valid = true;
                    extension.DownloadZip();
                    extension.UnZip();
                    if (extension.smallBasicExtension.dllFiles.numFile == 0) extension.Errors.Add("No dll files in download zip");
                    else extension.Verify(extension.LocalUnZipPath + "\\" + extension.smallBasicExtension.dllFiles.Files[0].File);
                    extension.Validate();
                }
                if (extension.Valid)
                {
                    ZipFile zip = ZipFile.Read(extension.LocalZip);
                    string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/" + extension.Name + "-" + extension.ExtVersion.ToString();
                    zip.ExtractAll(path, ExtractExistingFileAction.OverwriteSilently);
                    zip.Dispose();

                    string message = "Downloaded to "+path;
                    message += "\n\nDo you want to open this folder?";
                    if (MessageBox.Show(message, "Small Basic Extension Manager", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        Process.Start(path);
                    }
                }
                else
                {
                    string message = "";
                    foreach (string Error in extension.Errors)
                    {
                        message += Error + "\n";
                    }
                    message += "\nDo you want to continue anyway?";
                    if (MessageBox.Show(message, "Small Basic Extension Manager Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        ZipFile zip = ZipFile.Read(extension.LocalZip);
                        string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/" + extension.Name + "-" + extension.ExtVersion.ToString();
                        zip.ExtractAll(path, ExtractExistingFileAction.OverwriteSilently);
                        zip.Dispose();
                        extension.Valid = true; //We did it anyway
                    }
                    else
                    {
                        try
                        {
                            if (CheckAccess())
                            {
                                extension.Unload();
                            }
                            else
                            {
                                Dispatcher.Invoke(() => { extension.Unload(); });
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            bWorking = false;
        }

        private void RunDisable(Object obj)
        {
            bWorking = true;
            EMButton button = (EMButton)((Object[])obj)[0];
            Extension extension = (Extension)((Object[])obj)[1];
            if (null != extension)
            {
                extension.Errors.Clear();
                extension.Valid = true;
                string file = installationPath + "\\lib\\" + extension.Name;
                string command = "cd \"" + installationPath + "\\lib\"";
                command += " & move /Y \"" + file + ".dll\" \"" + file + "._dll\"";
                file = installationPath + "\\lib\\" + extension.Name;
                command += " & move /Y \"" + file + ".xml\" \"" + file + "._xml\"";
                extension.UACcommand(command);
                if (CheckAccess())
                {
                    if (extension.Valid) button.SetState(EMButton.eState.DISABLED);
                    GetButtonExtensionLists();
                    MakeButtons();
                    SetButtonExtensionLists();
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (extension.Valid) button.SetState(EMButton.eState.DISABLED);
                        GetButtonExtensionLists();
                        MakeButtons();
                        SetButtonExtensionLists();
                    });
                }
            }
            bWorking = false;
        }

        private void RunEnable(Object obj)
        {
            bWorking = true;
            EMButton button = (EMButton)((Object[])obj)[0];
            Extension extension = (Extension)((Object[])obj)[1];
            if (null != extension)
            {
                extension.Errors.Clear();
                extension.Valid = true;
                string file = installationPath + "\\lib\\" + extension.Name;
                string command = "cd \"" + installationPath + "\\lib\"";
                command += " & move /Y \"" + file + "._dll\" \"" + file + ".dll\"";
                file = installationPath + "\\lib\\" + extension.Name;
                command += " & move /Y \"" + file + "._xml\" \"" + file + ".xml\"";
                extension.UACcommand(command);
                if (CheckAccess())
                {
                    if (extension.Valid)
                    {
                        button.SetState(EMButton.eState.INSTALLED);
                        foreach (Extension extensionWeb in webExtension.extensions)
                        {
                            if (extension.Name == extensionWeb.Name && extension.SBVersion == SBVersion && extension.ExtVersion != extensionWeb.ExtVersion) button.SetState(EMButton.eState.UPDATE);
                        }
                    }
                    GetButtonExtensionLists();
                    MakeButtons();
                    SetButtonExtensionLists();
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (extension.Valid)
                        {
                            button.SetState(EMButton.eState.INSTALLED);
                            foreach (Extension extensionWeb in webExtension.extensions)
                            {
                                if (extension.Name == extensionWeb.Name && extension.SBVersion == SBVersion && extension.ExtVersion != extensionWeb.ExtVersion) button.SetState(EMButton.eState.UPDATE);
                            }
                        }
                        GetButtonExtensionLists();
                        MakeButtons();
                        SetButtonExtensionLists();
                    });
                }
            }
            bWorking = false;
        }

        private void OnMenuItemClicked(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            EMButton button = (EMButton)item.Tag;
            Extension extension = (Extension)button.Tag;

            switch (item.Name)
            {
                case "Install":
                    try
                    {
                        if (!bWorking)
                        {
                            thread = new Thread(new ParameterizedThreadStart(RunInstall));
                            thread.Start(new Object[] { button, extension, true });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "Uninstall":
                    try
                    {
                        if (!bWorking)
                        {
                            thread = new Thread(new ParameterizedThreadStart(RunInstall));
                            thread.Start(new Object[] { button, extension, false });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "Disable":
                    try
                    {
                        if (bWorking) return;
                        if (button.GetState() == EMButton.eState.INSTALLED || button.GetState() == EMButton.eState.UPDATE) RunDisable(new Object[] { button, extension });
                        else if (button.GetState() == EMButton.eState.DISABLED) RunEnable(new Object[] { button, extension });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "SBVersion":
                    break;
                case "ExtVersion":
                    break;
                case "InstalledVersion":
                    break;
                case "Description":
                    break;
                case "Author":
                    break;
                case "WebSite":
                    try
                    {
                        if (null != extension.smallBasicExtension)
                        {
                            Process.Start(extension.smallBasicExtension.WebSite);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "API":
                    try
                    {
                        if (null != extension.smallBasicExtension)
                        {
                            Process.Start(extension.smallBasicExtension.API);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "APIgenerated":
                    try
                    {
                        ShowAPI(extension);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "ChangeLog":
                    try
                    {
                        if (null != extension.smallBasicExtension)
                        {
                            Process.Start(extension.smallBasicExtension.ChangeLog);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "SaveZip":
                    try
                    {
                        if (bWorking) return;
                        if (null != extension.smallBasicExtension)
                        {
                            thread = new Thread(new ParameterizedThreadStart(RunDownload));
                            thread.Start(new Object[] { button, extension });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
            }
        }

        private void OnButtonClicked(object sender, RoutedEventArgs e)
        {
            if (bWorking) return;

            try
            {
                EMButton button = (EMButton)sender;
                if (button.GetState() == EMButton.eState.INSTALL || button.GetState() == EMButton.eState.UPDATE)
                {
                    Extension extension = (Extension)button.Tag;
                    thread = new Thread(new ParameterizedThreadStart(RunInstall));
                    thread.Start(new Object[] { button, extension, true });
                }
                else if (button.GetState() == EMButton.eState.INSTALLED)
                {
                    Extension extension = (Extension)button.Tag;
                    thread = new Thread(new ParameterizedThreadStart(RunDisable));
                    thread.Start(new Object[] { button, extension });
                }
                else if (button.GetState() == EMButton.eState.DISABLED)
                {
                    Extension extension = (Extension)button.Tag;
                    thread = new Thread(new ParameterizedThreadStart(RunEnable));
                    thread.Start(new Object[] { button, extension });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            GetButtonExtensionLists();

            if (null != thread)
            {
                thread.Abort();
                while (thread.IsAlive) Thread.Sleep(10);
            }

            webExtension.Unload();
            localExtension.Unload();

            string path = System.IO.Path.GetTempPath() + "SBExtension_API";
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK);
                }
            }

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //For some reason icon in class library failing from xaml
            Icon icon = Properties.Resources.AppIcon;
            this.Icon = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            gridMain.Background = new ImageBrush(GetBitmapImage(Properties.Resources.appworkspace));
            progressBar.Background = new SolidColorBrush(Colors.Transparent);
            progressBar.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255));
            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Hidden;

            defaultCursor = Cursor;

            tbInitialise = new TextBox();
            tbInitialise.Text = "Downloading Database...";
            tbInitialise.FontSize = 50;
            tbInitialise.BorderThickness = new Thickness(0);
            tbInitialise.Background = new SolidColorBrush(Colors.Transparent);
            canvas.Children.Add(tbInitialise);
            Canvas.SetLeft(tbInitialise, gridMain.ActualWidth / 2 - 270);
            Canvas.SetTop(tbInitialise, gridMain.ActualHeight / 2 - 80);

            timer = new Timer(OnTimer);
            timer.Change(100, 100);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            help.RenderTransform = new TranslateTransform(gridMain.ActualWidth / 2 - 50, gridMain.ActualHeight / 2 - 40);
        }

        private void ShowAPI(Extension extension)
        {
            string xmlFile = installationPath + "\\lib\\" + extension.Name + ".xml";
            if (File.Exists(xmlFile))
            {
                Parser parser = new Parser(xmlFile, extension.Name);
                string htmlFile = parser.writeHTML(false);
                Process.Start(htmlFile);
            }
        }
    }
}
