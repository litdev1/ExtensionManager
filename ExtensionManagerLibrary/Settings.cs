using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ExtensionManagerLibrary
{
    class Setting
    {
        private string key;
        private string property;

        public string Key
        {
            get { return key.ToUpper(); }
            set { key = value.ToUpper(); }
        }

        public string Property
        {
            get { return property; }
            set { property = value; }
        }

        public Setting(string key, string property)
        {
            this.key = key.ToUpper();
            this.property = property;
        }
    }

    static class Settings
    {
        private static List<Setting> settings = null;

        private static void Initialise()
        {
            settings = new List<Setting>();
            string settingsPath = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, "settings");
            if (File.Exists(settingsPath))
            {
                using (StreamReader stream = new StreamReader(settingsPath))
                {
                    string line = stream.ReadLine();
                    while (null != line)
                    {
                        string[] setting = line.Split(new char[] { '#' });
                        settings.Add(new Setting(setting[1].Trim(), setting[0].Trim()));
                        line = stream.ReadLine();
                    }
                }
            }
        }

        public static List<string> GetValues(string key)
        {
            if (null == settings) Initialise();

            List<string> values = new List<string>();
            foreach (Setting setting in settings)
            {
                if (setting.Key == key.ToUpper()) values.Add(setting.Property);
            }
            return values;
        }

        public static string GetValue(string key)
        {
            if (null == settings) Initialise();

            foreach (Setting setting in settings)
            {
                if (setting.Key == key.ToUpper()) return setting.Property;
            }
            return "";
        }
    }
}
