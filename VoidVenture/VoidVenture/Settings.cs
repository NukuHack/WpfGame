using System;

using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Xml.Linq;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
//using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Navigation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

using Microsoft.Win32.SafeHandles;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using System.Numerics;


namespace VoidVenture
{
    public class Setting
    {
        // Properties of the Setting class
        public string Name { get; set; } // The name of the setting
        public string Desc { get; set; } // Description or comment for the setting
        public bool Default { get; set; } // Default value of the setting
        public bool Value { get; set; } // Current value of the setting

        // Constructor to initialize the setting
        public Setting(string name, string desc, bool value, bool? defaultValue=null)
        {
            Name = name;
            Desc = desc;
            Value = value;
            Default = defaultValue==null?value:(bool)defaultValue;
        }

        // Method to reset the setting to its default value
        public void ResetToDefault()
        {
            Value = Default;
        }
    }


    public class SettingsManager
    {
        // Dictionary to store all settings with their names as keys
        public Dictionary<string, Setting> Settings = new();

        // Method to add a new setting
        public void AddSetting(Setting setting)
        {
            if (!Settings.ContainsKey(setting.Name))
            {
                Settings.Add(setting.Name, setting);
            }
            else
            {
                throw new ArgumentException($"Setting with name '{setting.Name}' already exists.");
            }
        }

        public void SetAllData(Dictionary<string, bool> settings)
        {
            foreach (var item in settings)
            {
                Settings[item.Key].Value = item.Value;
            }
        }

        public Dictionary<string, bool> GetAllData()
        {
            var parsed = new Dictionary<string, bool>();

            foreach (var item in Settings)
            {
                parsed[item.Key] = item.Value.Value;
            }

            return parsed;
        }

        // Method to get a setting by its name
        public Setting GetSetting(string name)
        {
            if (Settings.ContainsKey(name))
            {
                return Settings[name];
            }
            else
            {
                throw new KeyNotFoundException($"Setting with name '{name}' not found.");
            }
        }

        // Method to update the value of a setting
        public void UpdateSetting(string name, bool newValue)
        {
            if (Settings.ContainsKey(name))
            {
                Settings[name].Value = newValue;
            }
            else
            {
                throw new KeyNotFoundException($"Setting with name '{name}' not found.");
            }
        }

        // Method to reset all settings to their default values
        public void ResetAllToDefaults()
        {
            foreach (var setting in Settings.Values)
            {
                setting.ResetToDefault();
            }
        }



        // Save settings to a JSON file
        public void Save(string filePath)
        {
            try
            {
                // Serialize the settings dictionary to JSON
                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
                {
                    WriteIndented = true // For pretty-printing
                });

                // Write the JSON string to the specified file
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new IOException("Failed to save settings.", ex);
            }
        }

        // Load settings from a JSON file
        public void Load(string filePath)
        {
            try
            {
                // Read the JSON string from the specified file
                string json = File.ReadAllText(filePath);

                // Deserialize the JSON string back into a dictionary
                var loadedSettings = JsonSerializer.Deserialize<Dictionary<string, Setting>>(json);

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                }
                else
                {
                    throw new InvalidOperationException("Deserialized settings are null.");
                }
            }
            catch (Exception ex)
            {
                throw new IOException("Failed to load settings.", ex);
            }
        }


        public bool RecolorBackground
        {
            get => GetSetting(nameof(RecolorBackground)).Value;
            set => UpdateSetting(nameof(RecolorBackground), value);
        }

        public bool RecolorPlayer
        {
            get => GetSetting(nameof(RecolorPlayer)).Value;
            set => UpdateSetting(nameof(RecolorPlayer), value);
        }

        public bool SelectPlayerManually
        {
            get => GetSetting(nameof(SelectPlayerManually)).Value;
            set => UpdateSetting(nameof(SelectPlayerManually), value);
        }

        public bool UseNoiseTerrain
        {
            get => GetSetting(nameof(UseNoiseTerrain)).Value;
            set => UpdateSetting(nameof(UseNoiseTerrain), value);
        }

        public bool RandomizeTerrainColors
        {
            get => GetSetting(nameof(RandomizeTerrainColors)).Value;
            set => UpdateSetting(nameof(RandomizeTerrainColors), value);
        }

        public bool RandomizeTerrainMulti
        {
            get => GetSetting(nameof(RandomizeTerrainMulti)).Value;
            set => UpdateSetting(nameof(RandomizeTerrainMulti), value);
        }

        public bool RandomizeTerrainHeights
        {
            get => GetSetting(nameof(RandomizeTerrainHeights)).Value;
            set => UpdateSetting(nameof(RandomizeTerrainHeights), value);
        }

        public bool Debug
        {
            get => GetSetting(nameof(Debug)).Value;
            set => UpdateSetting(nameof(Debug), value);
        }

        public bool UseChunkGen
        {
            get => GetSetting(nameof(UseChunkGen)).Value;
            set => UpdateSetting(nameof(UseChunkGen), value);
        }

    }



    public partial class MainWindow : System.Windows.Window
    {
        // Create a settings manager
        public SettingsManager DO = new SettingsManager();

        public void BeforeEverything()
        {
            // Add settings to the manager
            DO.AddSetting(new Setting("RecolorBackground", "If true, recolors the background. If DOUseNoiseTerrain is true, this is useless.", true));
            DO.AddSetting(new Setting("RecolorPlayer", "If true, recolors the player.", true));
            DO.AddSetting(new Setting("SelectPlayerManually", "Adds the option to choose the player image manually.", false));
            DO.AddSetting(new Setting("UseNoiseTerrain", "If true, generates terrain using noise instead of predefined tiles.", true));
            DO.AddSetting(new Setting("RandomizeTerrainColors", "If true, randomizes terrain colors. If DOUseNoiseTerrain is false, this is useless.", true));
            DO.AddSetting(new Setting("RandomizeTerrainMulti", "If true, randomizes terrain multipliers. If DOUseNoiseTerrain is false, this is useless.", false));
            DO.AddSetting(new Setting("RandomizeTerrainHeights", "If true, randomizes terrain heights. If DOUseNoiseTerrain is false, this is useless.", true));
            DO.AddSetting(new Setting("Debug", "Enables debug mode.", false));
            DO.AddSetting(new Setting("UseChunkGen", "Uses the chunk-grid system instead of the usual all-at-once method. Currently does not work correctly.", false));

        }


        public void RightAfterBegining()
        {
            PopulateSettingsListBox();
        }


        private void ResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            // Reset all settings to their default values
            DO.ResetAllToDefaults();

            // Repopulate the ListBox
            PopulateSettingsListBox();
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Save the current settings to a JSON file
            SaveData();

            // Optionally repopulate the ListBox if needed
            PopulateSettingsListBox();
        }

        private void PopulateSettingsListBox()
        {
            // Clear existing items
            SettingsListBox.Items.Clear();

            // Add each setting object to the ListBox
            foreach (var setting in DO.Settings.Values)
            {
                SettingsListBox.Items.Add(setting);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Get the CheckBox that triggered the event
            var checkBox = sender as CheckBox;

            // Get the associated Setting object
            var setting = checkBox?.DataContext as Setting;

            if (setting != null)
            {
                // Call your custom function here
                OnSettingChecked(setting);
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Get the CheckBox that triggered the event
            var checkBox = sender as CheckBox;

            // Get the associated Setting object
            var setting = checkBox?.DataContext as Setting;

            if (setting != null)
            {
                // Call your custom function here
                OnSettingUnchecked(setting);
            }
        }

        private void OnSettingChecked(Setting setting)
        {
            // Handle the "Checked" event
            Console.WriteLine($"Setting '{setting.Name}' was checked.");
            // Add your logic here
        }

        private void OnSettingUnchecked(Setting setting)
        {
            // Handle the "Unchecked" event
            Console.WriteLine($"Setting '{setting.Name}' was unchecked.");
            // Add your logic here
        }

    }
}