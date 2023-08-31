using ExtensionManagerLibrary;
using ExtensionManagerLibrary.Schema;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml.Serialization;

namespace ExtensionManager
{
    /// <summary>
    /// Datagrid elements for extension manager
    /// </summary>
    public class ExtensionItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public Uri WebSite { get; set; }
        public Uri API { get; set; }
        public Uri ChangeLog { get; set; }
        public string SBVersion { get; set; }
        public string ExtVersion { get; set; }
        public string ZipSize { get; set; }
        public string Installed { get; set; }
        public string Downloaded { get; set; }
        public string Location { get; set; }
        public Extension Extension { get; set; }
    }

    /// <summary>
    /// Datagrid elements for database manager
    /// </summary>
    public class DatabaseItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string WebSite { get; set; }
        public string API { get; set; }
        public string ChangeLog { get; set; }
        public string SBVersion { get; set; }
        public string ExtVersion { get; set; }
        public string ZipLocation { get; set; }
        public string dllFiles { get; set; }
        public string xmlFiles { get; set; }
        public string docFiles { get; set; }
        public string sampleFiles { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Initialise();
        }

        private static int DatabaseVersion = 1;

        private WebExtension webExtension = new WebExtension();
        private LocalExtension localExtension = new LocalExtension();
        private Extension smallBasicLibrary = new Extension();
        private ObservableCollection<DatabaseItem> databaseItems = new ObservableCollection<DatabaseItem>();
        private SmallBasicExtensionList extensionList = null;
        private Thread thread = null;
        private bool bWorking = false;
        private Timer timer;

        private void RunDownload(Object obj)
        {
            bWorking = true;
            ExtensionItem dataItem = (ExtensionItem)((Object[])obj)[0];
            if (dataItem.Downloaded == "NO")
            {
                Extension extension = dataItem.Extension;
                if (null != extension && extension.Source == eSource.WEB)
                {
                    extension.Errors.Clear();
                    extension.DownloadZip();
                    extension.UnZip();
                    if (extension.smallBasicExtension.dllFiles.numFile == 0) extension.Errors.Add("No dll files in download zip");
                    else extension.Verify(extension.LocalUnZipPath + "\\" + extension.smallBasicExtension.dllFiles.Files[0].File);
                    extension.Validate();
                    if (extension.Valid)
                    {
                        //MessageBox.Show("Extension Download Succeeded", "Small Basic Extension Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        string message = "";
                        foreach (string Error in extension.Errors)
                        {
                            message += Error + "\n";
                        }
                        MessageBox.Show(message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        try
                        {
                            if (CheckAccess())
                            {
                                extension.Unload();
                                LoadExtensions();
                            }
                            else
                            {
                                Dispatcher.Invoke(() => { extension.Unload(); LoadExtensions(); });
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

        private void OnTimer(object state)
        {
            try
            {
                double progressValue = 0;
                if (ProgressStats.fullSize > 0) progressValue = 100 * ProgressStats.currentSize / ProgressStats.fullSize;
                progressValue = Math.Min(100, Math.Max(0, progressValue));
                if (CheckAccess())
                {
                    progressBar.Value = progressValue;
                    if (progressValue == 100)
                    {
                        ProgressStats.currentSize = 0;
                        LoadExtensions();
                    }
                }
                else
                {
                    Dispatcher.Invoke(() => { progressBar.Value = progressValue; });
                    if (progressValue == 100)
                    {
                        ProgressStats.currentSize = 0;
                        Dispatcher.Invoke(() => { LoadExtensions(); });
                    }
                }
            }
            catch  { }
        }

        private void Initialise()
        {
            tabControl.SelectedIndex = 1;

            textBoxInstallationPath.Text = Environment.Is64BitOperatingSystem ? "C:\\Program Files (x86)\\Microsoft\\Small Basic" : "C:\\Program Files\\Microsoft\\Small Basic";
            smallBasicLibrary.Verify(textBoxInstallationPath.Text + "\\SmallBasicLibrary.dll");
            textBoxSBVersion.Text = smallBasicLibrary.ExtVersion.ToString();
            textBoxSBVersion.IsReadOnly = true;

            string databasePath = "C:\\Users\\Steve\\Documents\\LitDev\\extensions\\ExtensionDatabase.xml";
            if (!File.Exists(databasePath)) databasePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\ExtensionDatabase.xml";
            textBoxDatabasePath.Text = databasePath;
            webExtension.Load(textBoxDatabasePath.Text);

            dataGridDatabases.ItemsSource = databaseItems;
            //dataGridDatabases.CanUserSortColumns = false;

            ContextMenu menu = new ContextMenu();
            dataGridDatabases.ContextMenu = menu;

            MenuItem item = new MenuItem();
            item.Header = "Copy Row(s)";
            item.Click += new RoutedEventHandler(_CopyRows);
            menu.Items.Add(item);

            item = new MenuItem();
            item.Header = "Delete Row(s)";
            item.Click += new RoutedEventHandler(_DeleteRows);
            menu.Items.Add(item);

            LoadDatabases();

            timer = new Timer(OnTimer);
            timer.Change(100, 100);
        }

        private void _CopyRows(object sender, RoutedEventArgs e)
        {
            foreach (DataGridCellInfo info in dataGridDatabases.SelectedCells)
            {
                DatabaseItem item = (DatabaseItem)info.Item;
                DatabaseItem rowItem = new DatabaseItem
                {
                    Name = item.Name,
                    Description = item.Description,
                    Author = item.Author,
                    WebSite = item.WebSite,
                    API = item.API,
                    ChangeLog = item.ChangeLog,
                    SBVersion = item.SBVersion,
                    ExtVersion = item.ExtVersion,
                    ZipLocation = item.ZipLocation,
                    dllFiles = item.dllFiles,
                    xmlFiles = item.xmlFiles,
                    docFiles = item.docFiles,
                    sampleFiles = item.sampleFiles
                };
                databaseItems.Add(rowItem);
            }
        }

        private void _DeleteRows(object sender, RoutedEventArgs e)
        {
            List<DatabaseItem> items = new List<DatabaseItem>();
            foreach (DataGridCellInfo info in dataGridDatabases.SelectedCells)
            {
                items.Add((DatabaseItem)info.Item);
            }
            foreach (DatabaseItem item in items)
            {
                databaseItems.Remove(item);
            }
        }

        private string GetFiles(FileList fileList)
        {
            string files = "";
            for (int i = 0; i < fileList.numFile; i++)
            {
                files += fileList.Files[i].File + ";";
            }
            return files;
        }

        private FileList SetFiles(string files)
        {
            FileList fileList = new FileList();
            if (null == files) return fileList;
            string[] fileSplit = files.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            fileList.numFile = fileSplit.Length;
            if (fileList.numFile == 0) return fileList;
            fileList.Files = new FileListFiles[fileList.numFile];
            for (int i = 0; i < fileList.numFile; i++)
            {
                fileList.Files[i] = new FileListFiles();
                fileList.Files[i].File = fileSplit[i];
            }
            return fileList;
        }

        private void LoadDatabases()
        {
            if (!File.Exists(textBoxDatabasePath.Text)) return;
            XmlSerializer xs = new XmlSerializer(typeof(SmallBasicExtensionList));
            StreamReader sr = new StreamReader(textBoxDatabasePath.Text);
            extensionList = (SmallBasicExtensionList)xs.Deserialize(sr);
            sr.Close();
            databaseItems.Clear();
            for (int i = 0; i < extensionList.numExtension; i++)
            {
                SmallBasicExtension extension = extensionList.Extensions[i].Extension;
                DatabaseItem rowItem = new DatabaseItem
                {
                    Name = extension.Name,
                    Description = extension.Description,
                    Author = extension.Author,
                    WebSite = extension.WebSite,
                    API = extension.API,
                    ChangeLog = extension.ChangeLog,
                    SBVersion = extension.SBVersion,
                    ExtVersion = extension.ExtVersion,
                    ZipLocation = extension.ZipLocation,
                    dllFiles = GetFiles(extension.dllFiles),
                    xmlFiles = GetFiles(extension.xmlFiles),
                    docFiles = GetFiles(extension.docFiles),
                    sampleFiles = GetFiles(extension.sampleFiles)
                };
                databaseItems.Add(rowItem);
            }
        }

        private void SaveDatabases()
        {
            extensionList = new SmallBasicExtensionList();
            extensionList.Version = DatabaseVersion;
            extensionList.numExtension = 0;
            for (int i = 0; i < databaseItems.Count; i++)
            {
                DatabaseItem item = databaseItems[i];
                if (null == item.Name || item.Name == "") continue;
                extensionList.numExtension++;
            }
            extensionList.Extensions = new SmallBasicExtensionListExtensions[extensionList.numExtension];
            int index = 0;
            for (int i = 0; i < databaseItems.Count; i++)
            {
                DatabaseItem item = databaseItems[i];
                if (null == item.Name || item.Name == "") continue;
                extensionList.Extensions[index] = new SmallBasicExtensionListExtensions();
                extensionList.Extensions[index].Extension = new SmallBasicExtension();
                SmallBasicExtension extension = extensionList.Extensions[index].Extension;
                index++;
                extension.Name = item.Name;
                extension.Description = item.Description;
                extension.Author = item.Author;
                extension.WebSite = item.WebSite;
                extension.API = item.API;
                extension.ChangeLog = item.ChangeLog;
                extension.SBVersion = item.SBVersion;
                extension.ExtVersion = item.ExtVersion;
                extension.ZipLocation = item.ZipLocation;
                extension.dllFiles = SetFiles(item.dllFiles);
                extension.xmlFiles = SetFiles(item.xmlFiles);
                extension.docFiles = SetFiles(item.docFiles);
                extension.sampleFiles = SetFiles(item.sampleFiles);
            }
            XmlSerializer xs = new XmlSerializer(typeof(SmallBasicExtensionList));
            StreamWriter sw = new StreamWriter(textBoxDatabasePath.Text, false);
            xs.Serialize(sw, extensionList);
            sw.Close();

            LoadDatabases();
        }

        private void LoadExtensions()
        {
            dataGridExtensions.Items.Clear();
            foreach (Extension extension in webExtension.extensions)
            {
                //if (!extension.Valid) continue;
                string installed = "NO";
                string[] files = Directory.GetFiles(textBoxInstallationPath.Text + "\\lib");
                foreach (string file in files)
                {
                    if (Path.GetFileName(file) == extension.Name + ".dll")
                    {
                        Version ExtVersion = new Extension().GetVersion(file);
                        if (ExtVersion == extension.ExtVersion) installed = "YES";
                    }
                }

                Uri uriWebsite;
                Uri uriAPI;
                Uri uriChangeLog;
                Uri.TryCreate(extension.smallBasicExtension.WebSite, UriKind.RelativeOrAbsolute, out uriWebsite);
                Uri.TryCreate(extension.smallBasicExtension.API, UriKind.RelativeOrAbsolute, out uriAPI);
                Uri.TryCreate(extension.smallBasicExtension.ChangeLog, UriKind.RelativeOrAbsolute, out uriChangeLog);

                double zipSize = 0;
                if (EMWindow.bWebAccess)
                {
                    try
                    {
                        WebRequest.DefaultWebProxy.Credentials = CredentialCache.DefaultCredentials;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        WebRequest webRequest = HttpWebRequest.Create(extension.smallBasicExtension.ZipLocation);
                        webRequest.Method = "HEAD";
                        WebResponse webResponse = webRequest.GetResponse();
                        zipSize = webResponse.ContentLength;
                        webResponse.Close();
                    }
                    catch
                    {
                        EMWindow.bWebAccess = false;
                    }
                }

                ExtensionItem rowItem = new ExtensionItem
                {
                    Name = extension.Name,
                    Description = null == extension.smallBasicExtension.Description ? "" : extension.smallBasicExtension.Description,
                    Author = null == extension.smallBasicExtension.Author ? "" : extension.smallBasicExtension.Author,
                    WebSite = uriWebsite,
                    API = uriAPI,
                    ChangeLog = uriChangeLog,
                    SBVersion = null == extension.SBVersion ? extension.smallBasicExtension.SBVersion : extension.SBVersion.ToString(),
                    ExtVersion = null == extension.ExtVersion ? extension.smallBasicExtension.ExtVersion : extension.ExtVersion.ToString(),
                    ZipSize = zipSize <= 0 ? "" : string.Format("{0:0.###}", zipSize / 1024.0 / 1024.0),
                    Installed = installed,
                    Downloaded = extension.Downloaded ? "YES" : "NO",
                    Location = extension.Source.ToString(),
                    Extension = extension
                };
                dataGridExtensions.Items.Add(rowItem);
            }

            localExtension.Load(textBoxInstallationPath.Text);
            foreach (Extension extension in localExtension.extensions)
            {
                foreach (Extension extension2 in webExtension.extensions)
                {
                    if (extension.Name == extension2.Name) extension.Valid = false;
                }
                if (!extension.Valid) continue;
                ExtensionItem rowItem = new ExtensionItem
                {
                    Name = extension.Name,
                    //Description = "N/A",
                    //Author = "Unknown",
                    //WebSite = new Uri(""),
                    //API = new Uri(""),
                    //ChangeLog = new Uri(""),
                    SBVersion = extension.SBVersion.ToString(),
                    ExtVersion = extension.ExtVersion.ToString(),
                    //ZipSize = "N/A",
                    Installed = "YES",
                    //Downloaded = "N/A",
                    Location = extension.Source.ToString(),
                    Extension = extension
                };
                dataGridExtensions.Items.Add(rowItem);
            }

            dataGridExtensions.UpdateLayout();

            ShowControls();
        }

        private void ShowControls()
        {
            for (int i = 0; i < dataGridExtensions.Items.Count; i++)
            {
                DataGridRow row = (DataGridRow)dataGridExtensions.ItemContainerGenerator.ContainerFromIndex(i);
                if (null != row)
                {
                    for (int j = 0; j < dataGridExtensions.Columns.Count; j++)
                    {
                        var cellContent = dataGridExtensions.Columns[j].GetCellContent(row);
                        if (null != cellContent)
                        {
                            if (cellContent.GetType() == typeof(TextBlock))
                            {
                                TextBlock control = (TextBlock)cellContent;
                                //object item = dataGridExtensions.Items[i];
                                //dataGridExtensions.SelectedItem = item;
                                //dataGridExtensions.ScrollIntoView(item);
                            }
                            else if (cellContent.GetType() == typeof(ContentPresenter))
                            {
                                ContentPresenter control = (ContentPresenter)cellContent;
                                ExtensionItem item = (ExtensionItem)control.Content;
                                if (dataGridExtensions.Columns[j].Header.ToString() == "Install")
                                {
                                    if (item.Name == "" || item.Location == "LOCAL" || item.Downloaded == "NO" || !item.Extension.Valid)
                                    {
                                        control.Visibility = Visibility.Hidden;
                                    }
                                }
                                else if (dataGridExtensions.Columns[j].Header.ToString() == "UnInstall")
                                {
                                    if (item.Installed == "NO" || item.Location == "LOCAL" || !item.Extension.Valid)
                                    {
                                        control.Visibility = Visibility.Hidden;
                                    }
                                }
                                else if (dataGridExtensions.Columns[j].Header.ToString() == "Download")
                                {
                                    if (item.Location == "LOCAL")
                                    {
                                        control.Visibility = Visibility.Hidden;
                                    }
                                }
                                control.IsEnabled = !bWorking;
                            }
                        }
                    }
                }
            }
        }

        private void OnInstall(object sender, RoutedEventArgs e)
        {
            if (bWorking) return;

            try
            {
                Button button = (Button)sender;
                ExtensionItem dataItem = (ExtensionItem)button.DataContext;
                if (dataItem.Downloaded == "YES")
                {
                    Extension extension = dataItem.Extension;
                    string command = "cd \"" + textBoxInstallationPath.Text + "\\lib\"";
                    if (null != extension && extension.Source == eSource.WEB && extension.Valid)
                    {
                        for (int i = 0; i < extension.smallBasicExtension.dllFiles.numFile; i++)
                        {
                            string file = extension.LocalUnZipPath + "\\" + extension.smallBasicExtension.dllFiles.Files[i].File;
                            string newFile = textBoxInstallationPath.Text + "\\lib\\" + Path.GetFileName(file);
                            command += " & del \"" + newFile + "\"";
                            command += " & copy /B/Y \"" + file + "\" \"" + newFile + "\"";
                            //File.Delete(newFile);
                            //File.Copy(file, newFile);
                        }
                        for (int i = 0; i < extension.smallBasicExtension.xmlFiles.numFile; i++)
                        {
                            string file = extension.LocalUnZipPath + "\\" + extension.smallBasicExtension.xmlFiles.Files[i].File;
                            string newFile = textBoxInstallationPath.Text + "\\lib\\" + Path.GetFileName(file);
                            command += " & del \"" + newFile + "\"";
                            command += " & copy /B/Y \"" + file + "\" \"" + newFile + "\"";
                            //File.Delete(newFile);
                            //File.Copy(file, newFile);
                        }
                    }
                    extension.UACcommand(command);
                    //MessageBox.Show("Extension Install Succeeded", "Small Basic Extension Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadExtensions();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnUnInstall(object sender, RoutedEventArgs e)
        {
            if (bWorking) return;

            try
            {
                Button button = (Button)sender;
                ExtensionItem dataItem = (ExtensionItem)button.DataContext;
                if (dataItem.Installed == "YES")
                {
                    Extension extension = dataItem.Extension;
                    string command = "cd \"" + textBoxInstallationPath.Text + "\\lib\"";
                    if (null != extension && extension.Source == eSource.WEB && extension.Valid)
                    {
                        for (int i = 0; i < extension.smallBasicExtension.dllFiles.numFile; i++)
                        {
                            string file = textBoxInstallationPath.Text + "\\lib\\" + extension.smallBasicExtension.dllFiles.Files[i].File;
                            command += " & del \"" + file + "\"";
                            //File.Delete(file);
                        }
                        for (int i = 0; i < extension.smallBasicExtension.xmlFiles.numFile; i++)
                        {
                            string file = textBoxInstallationPath.Text + "\\lib\\" + extension.smallBasicExtension.xmlFiles.Files[i].File;
                            command += " & del \"" + file + "\"";
                            //File.Delete(file);
                        }
                    }
                    extension.UACcommand(command);
                    //MessageBox.Show("Extension UnInstall Succeeded", "Small Basic Extension Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadExtensions();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDownload(object sender, RoutedEventArgs e)
        {
            if (bWorking) return;

            try
            {
                Button button = (Button)sender;
                ExtensionItem dataItem = (ExtensionItem)button.DataContext;
                thread = new Thread(new ParameterizedThreadStart(RunDownload));
                thread.Start(new Object[] { dataItem });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != thread)
            {
                thread.Abort();
                while (thread.IsAlive) Thread.Sleep(10);
            }

            webExtension.Unload();
            localExtension.Unload();
        }

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            if (bWorking) return;

            try
            {
                webExtension.Load(textBoxDatabasePath.Text);
                LoadExtensions();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnTestEM(object sender, RoutedEventArgs e)
        {
            if (bWorking) return;

            try
            {
                //ExtensionLists.disabled.Clear();
                //ExtensionLists.disabled.Add("LitDev");

                EMWindow windowEM = new EMWindow();
                windowEM.ShowDialog();

                //MessageBox.Show(ExtensionLists.disabled.Count.ToString(), "Small Basic Extension Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                //MessageBox.Show(ExtensionLists.enabled.Count.ToString(), "Small Basic Extension Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnLoadDatabase(object sender, RoutedEventArgs e)
        {
            if (bWorking) return;

            try
            {
                LoadDatabases();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSaveDatabase(object sender, RoutedEventArgs e)
        {
            if (bWorking) return;

            try
            {
                SaveDatabases();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnHyperlink(object sender, RoutedEventArgs e)
        {
            try
            {
                var destination = ((Hyperlink)e.OriginalSource).NavigateUri;
                Process.Start(destination.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadExtensions();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
