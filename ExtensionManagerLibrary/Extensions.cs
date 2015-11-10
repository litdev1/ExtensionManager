using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
//using System.IO.Compression;
using Ionic.Zip;
using System.Xml.Serialization;
using ExtensionManagerLibrary.Schema;
using System.Diagnostics;

namespace ExtensionManagerLibrary
{
    /// <summary>
    /// For use with progress bar
    /// </summary>
    public static class ProgressStats
    {
        /// <summary>
        /// max value
        /// </summary>
        public static long fullSize;
        /// <summary>
        /// Current value
        /// </summary>
        public static long currentSize;
    }

    /// <summary>
    /// A class to load and interrogate .Net assemblies for specific SB related properties
    /// </summary>
    public class Proxy : MarshalByRefObject
    {
        private Assembly assembly;

        /// <summary>
        /// Load the assembly
        /// </summary>
        /// <param name="path">The full path to the assembly dll</param>
        public void LoadAssembly(string path)
        {
            assembly = Assembly.Load(AssemblyName.GetAssemblyName(path));
        }

        /// <summary>
        /// Check if the assembly referenced SmallBasicLibrary.dll
        /// </summary>
        /// <returns>true or false</returns>
        public bool IsSmallBasic()
        {
            foreach (AssemblyName assemblyName in assembly.GetReferencedAssemblies())
            {
                if (assemblyName.Name == "SmallBasicLibrary") return true;
            }
            return false;
        }

        /// <summary>
        /// Getthe assembly version
        /// </summary>
        /// <returns>The assembly version (major.minor.build.revision)</returns>
        public Version SmallBasicVersion()
        {
            foreach (AssemblyName assemblyName in assembly.GetReferencedAssemblies())
            {
                if (assemblyName.Name == "SmallBasicLibrary") return assemblyName.Version;
            }
            return null;
        }
    }

    /// <summary>
    /// enum for extension as LOCAL (present in SB/lib) or WEB (available for download and install)
    /// </summary>
    public enum eSource { LOCAL, WEB }

    /// <summary>
    /// Class for an extension
    /// </summary>
    public class Extension : IComparable
    {
        private static int SBEnum = 1;

        /// <summary>
        /// Database xml extension
        /// </summary>
        public SmallBasicExtension smallBasicExtension = null;
        /// <summary>
        /// LOCAL or WEB source for extension
        /// </summary>
        public eSource Source = eSource.LOCAL;
        /// <summary>
        /// Extension name (name.dll and extension namespace)
        /// </summary>
        public string Name = "";
        /// <summary>
        /// If the WEB extension is downloaded
        /// </summary>
        public bool Downloaded = false;
        /// <summary>
        /// The Small Basic version (SmallBaicLibrary) that the extension uses
        /// </summary>
        public Version SBVersion = new Version("0.0.0.0");
        /// <summary>
        /// The version of the extension dll
        /// </summary>
        public Version ExtVersion = new Version("0.0.0.0");
        /// <summary>
        /// The version of the currently installed dll
        /// </summary>
        public Version InstalledVersion = null;
        /// <summary>
        /// A list of any errors encountered
        /// </summary>
        public List<string> Errors = new List<string>();
        /// <summary>
        /// Is the extension valid, including dll, zip , versions etc before an install
        /// </summary>
        public bool Valid = true;
        /// <summary>
        /// Temp file where WEB extension zip is downloaded
        /// </summary>
        public string LocalZip = "";
        /// <summary>
        /// Temp folder where downloaded WEB extension os unzipped
        /// </summary>
        public string LocalUnZipPath = "";

        private AppDomain appDomain = null;
        private Proxy proxy = null;
        private FileStream fs = null;
        private WebResponse webResponse = null;
        private Stream stream = null;
        private ZipFile zip = null;

        /// <summary>
        /// Constructor for a LOCAL extension
        /// </summary>
        public Extension()
        {
            Source = eSource.LOCAL;
        }

        /// <summary>
        /// Constructor for a WEB extension
        /// </summary>
        /// <param name="smallBasicExtension">The database entry for this web downloadable extension</param>
        public Extension(SmallBasicExtension smallBasicExtension)
        {
            this.smallBasicExtension = smallBasicExtension;
            Source = eSource.WEB;
            try
            {
                Name = smallBasicExtension.Name;
                SBVersion = new Version(smallBasicExtension.SBVersion);
                ExtVersion = new Version(smallBasicExtension.ExtVersion);
            }
            catch (Exception ex)
            {
                Errors.Add(ex.Message);
            }
            Valid &= Errors.Count == 0;
        }

        /// <summary>
        /// Get the version of an extension, this is private since it updates Errors
        /// </summary>
        /// <param name="dllFile">The path to the dll, an argument so we can also use it for any test assembly</param>
        /// <returns>The assembly version</returns>
        public Version GetVersion(string dllFile)
        {
            Version version = null;
            try
            {
                AssemblyName assembyName = AssemblyName.GetAssemblyName(dllFile);
                version = assembyName.Version;
                if (null == version) Errors.Add("Assembly version could not be found");
            }
            catch (Exception ex)
            {
                Errors.Add("GetVersion : " + ex.Message);
            }
            Valid &= Errors.Count == 0;
            return version;
        }

        /// <summary>
        /// Download a WEB assembly dll to a temp file (SBExtension*.tmp), updates Errors
        /// </summary>
        public void DownloadZip()
        {
            try
            {
                LocalZip = GetTempFile();

                FileInfo fileInf = new FileInfo(LocalZip);
                Uri uri = new Uri(smallBasicExtension.ZipLocation);
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);

                int bufferSize = 2048;
                byte[] buffer = new byte[bufferSize];

                fs = fileInf.OpenWrite();
                webResponse = webRequest.GetResponse();
                stream = webResponse.GetResponseStream();
                ProgressStats.fullSize = webResponse.ContentLength;

                int readCount;
                do
                {
                    readCount = stream.Read(buffer, 0, bufferSize);
                    fs.Write(buffer, 0, readCount);
                    ProgressStats.currentSize += readCount;
                } while (readCount > 0);
                stream.Close();
                fs.Close();
                webResponse.Close();

                ProgressStats.currentSize = ProgressStats.fullSize;
                Downloaded = fileInf.Length > 0;
                if (!Downloaded) Errors.Add("Download extension zip failed");
            }
            catch (Exception ex)
            {
                Errors.Add("DownloadZip : " + ex.Message);
                Downloaded = false;
            }
            fs = null;
            stream = null;
            webResponse = null;
            Valid &= Errors.Count == 0;
        }

        /// <summary>
        /// Unzip a WEB assembly dll to a temp folder (SBExtension*.tmp), updates Errors
        /// </summary>
        public void UnZip()
        {
            try
            {
                LocalUnZipPath = GetTempFolder();

                //.Net Version doesn't work on many zip files
                //ZipFile.ExtractToDirectory(extension.LocalLocation, extension.LocalPath);

                //Ionic Version
                zip = ZipFile.Read(LocalZip);
                zip.ExtractAll(LocalUnZipPath, ExtractExistingFileAction.OverwriteSilently);
                zip.Dispose();
                zip = null;
            }
            catch (Exception ex)
            {
                Errors.Add("UnZip : " + ex.Message);
            }
            Valid &= Errors.Count == 0;
        }

        /// <summary>
        /// Unload a WEB assembly (remove temp files/folders) and close all files 
        /// </summary>
        public void Unload()
        {
            if (null != stream) stream.Close();
            if (null != fs) fs.Close();
            if (null != webResponse) webResponse.Close();
            if (null != zip) zip.Dispose();
            stream = null;
            fs = null;
            webResponse = null;
            zip = null;

            if (null != appDomain)
            {
                AppDomain.Unload(appDomain);
                appDomain = null;
            }

            if (Source == eSource.WEB)
            {
                if (File.Exists(LocalZip)) File.Delete(LocalZip);
                if (Directory.Exists(LocalUnZipPath)) Directory.Delete(LocalUnZipPath, true);
            }

            Downloaded = false;
        }

        /// <summary>
        /// Verify a WEB assembly as a main SB dll (does it contain SmallBasicLibarary.dll), updates Errors
        /// </summary>
        /// <param name="dllFile">The assembly path</param>
        public void Verify(string dllFile)
        {
            Name = Path.GetFileNameWithoutExtension(dllFile);
            ExtVersion = GetVersion(dllFile);
            LoadAssembly(dllFile);
            CheckAssembly();
        }

        /// <summary>
        /// Validate a WEB assembly (are the versions of SB and assembly consistent with database), updates Errors
        /// </summary>
        public void Validate()
        {
            try
            {
                if (Name != smallBasicExtension.Name) Errors.Add("Name incorrect");
                if (SBVersion != new Version(smallBasicExtension.SBVersion)) Errors.Add("Small Basic Version incorrect");
                if (ExtVersion != new Version(smallBasicExtension.ExtVersion)) Errors.Add("Extension Version incorrect");
            }
            catch (Exception ex)
            {
                Errors.Add("Validate : " + ex.Message);
            }
            Valid &= Errors.Count == 0;
        }

        /// <summary>
        /// Load an assembly to temp AppDomain, updates Errors
        /// </summary>
        /// <param name="dllFile">The assembly path</param>
        public void LoadAssembly(string dllFile)
        {
            try
            {
                if (null != appDomain)
                {
                    AppDomain.Unload(appDomain);
                    appDomain = null;
                }
                AssemblyName assembyName = AssemblyName.GetAssemblyName(dllFile);

                AppDomainSetup ads = new AppDomainSetup();
                ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
                ads.DisallowBindingRedirects = false;
                ads.DisallowCodeDownload = true;
                ads.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                appDomain = AppDomain.CreateDomain(Path.GetFileNameWithoutExtension(dllFile), null, ads);

                proxy = (Proxy)appDomain.CreateInstanceAndUnwrap(Assembly.GetAssembly(typeof(Proxy)).FullName, typeof(Proxy).FullName);
                proxy.LoadAssembly(dllFile);
            }
            catch (Exception ex)
            {
                Errors.Add("LoadAssembly : " + ex.Message);
            }
            Valid &= Errors.Count == 0;
        }

        /// <summary>
        /// Check if assembly contains SmallBasicLibrary.dll, updates Errors
        /// </summary>
        public void CheckAssembly()
        {
            bool bIsSmallBasic = false;
            try
            {
                if (null != proxy)
                {
                    bIsSmallBasic = proxy.IsSmallBasic();
                    SBVersion = proxy.SmallBasicVersion();
                    if (!bIsSmallBasic) Errors.Add("Assembly does not reference Small Basic");
                    if (null == SBVersion) Errors.Add("Assembly Small Basic version not found");
                }
            }
            catch (Exception ex)
            {
                Errors.Add("CheckAssembly : " + ex.Message);
            }
            Valid &= Errors.Count == 0;
        }

        /// <summary>
        /// Get permission to read and write to elevated permission folder, perform action with admin privilidges
        /// </summary>
        /// <param name="command">A cmd.exe command to run</param>
        public void UACcommand(string command)
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = @"C:\Windows\System32\cmd.exe";
            psi.Arguments = "/c " + command;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process proc = new Process();
            proc.StartInfo = psi;

            try
            {
                proc.Start();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                Errors.Add("UACcommand : " + ex.Message);
            }
            Valid &= Errors.Count == 0;
        }

        private string GetTempFile()
        {
            string file = Path.GetTempFileName();
            string SBfile = Path.Combine(Path.GetTempPath(), "SBExtension" + (SBEnum++).ToString() + ".tmp");
            while (File.Exists(SBfile))
            {
                SBfile = Path.Combine(Path.GetTempPath(), "SBExtension" + (SBEnum++).ToString() + ".tmp");
            }
            File.Move(file, SBfile);
            return SBfile;
        }

        private string GetTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "SBExtension" + (SBEnum++).ToString());
            while (Directory.Exists(folder) || File.Exists(folder))
            {
                folder = Path.Combine(Path.GetTempPath(), "SBExtension" + (SBEnum++).ToString());
            }
            return folder;
        }

        public int CompareTo(object obj)
        {
            return string.Compare(Name, ((Extension)obj).Name, true);
        }
    }

    /// <summary>
    /// WEB assemblies that can be downloaded and installed
    /// </summary>
    public class WebExtension
    {
        public List<Extension> extensions = new List<Extension>();

        /// <summary>
        /// Load WEB assemblies from database
        /// </summary>
        /// <param name="databasePath">The path of the xml database of WEB extensions</param>
        public void Load(string databasePath)
        {
            if (!File.Exists(databasePath)) return;
            XmlSerializer xs = new XmlSerializer(typeof(SmallBasicExtensionList));
            StreamReader sr = new StreamReader(databasePath);
            SmallBasicExtensionList extensionList = (SmallBasicExtensionList)xs.Deserialize(sr);
            sr.Close();

            Unload();
            extensions.Clear();

            for (int i = 0; i < extensionList.numExtension; i++)
            {
                extensions.Add(new Extension(extensionList.Extensions[i].Extension));
            }
            extensions.Sort();
        }

        /// <summary>
        /// Unload all WEB assemblies, delete all temp files and free resources
        /// </summary>
        public void Unload()
        {
            foreach (Extension extension in extensions)
            {
                extension.Unload();
            }
        }
    }

    /// <summary>
    /// LOCAL assemblies present in SB/lib
    /// </summary>
    public class LocalExtension
    {
        public List<Extension> extensions = new List<Extension>();

        /// <summary>
        /// Load LOCAL assemblies from SB install path
        /// </summary>
        /// <param name="installPath">The extension SB path</param>
        public void Load(string installPath)
        {
            extensions.Clear();

            string[] files = Directory.GetFiles(installPath + "\\lib");
            foreach (string file in files)
            {
                if (file.EndsWith(".dll"))
                {
                    Extension extension = new Extension();
                    extensions.Add(extension);
                    extension.Verify(file);
                    extension.Unload();
                }
            }
            extensions.Sort();
        }

        /// <summary>
        /// Unload all LOCAL assemblies, delete all temp files and free resources
        /// </summary>
        public void Unload()
        {
            foreach (Extension extension in extensions)
            {
                extension.Unload();
            }
        }
    }
}
