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
    public class GameDataManager
    {
        public string Name { get; set; } = "Peti";
        public int Coins { get; set; } = 0;
        public bool Saved { get; set; } = false;

        public GameDataManager() { }
        public GameDataManager(string name, int coins, bool saved)
        {
            Name = name;
            Coins = coins;
            Saved = saved;
        }


        // Save settings to a JSON file
        public void Save(string filePath)
        {
            try
            {
                // Serialize the settings dictionary to JSON
                string json = JsonSerializer.Serialize(new GameDataManager(Name, Coins, Saved), new JsonSerializerOptions
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
                var loadedSettings = JsonSerializer.Deserialize<GameDataManager>(json);

                if (loadedSettings != null)
                {
                    Name = loadedSettings.Name;
                    Coins = loadedSettings.Coins;
                    Saved = loadedSettings.Saved;
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

    }


    public class GameManager
    {
        // Public SettingsManager instance
        public SettingsManager Settings { get; set; } = new();
        public Dictionary<string, bool> SettingsBool { get; set; } = new();
        public GameDataManager GameData { get; set; } = new();

        public GameManager(SettingsManager settings, GameDataManager gameData)
        {
            Settings = settings;
            GameData = gameData;
        }
        public GameManager()
        {

        }


        // Old SaveGame method adapted for JSON serialization
        public void SaveGame(string saveFilePath)
        {
            if (File.Exists(saveFilePath))
            {
                if (MessageBox.Show("This will overwrite the existing save file.", "Saving", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                {
                    return;
                }
            }

            try
            {
                // Update SettingsBool with the latest settings data
                SettingsBool = Settings.GetAllData();

                // Create an anonymous object containing only the data to serialize
                var dataToSerialize = new
                {
                    this.GameData,
                    this.SettingsBool
                };

                string json = JsonSerializer.Serialize(dataToSerialize, new JsonSerializerOptions
                {
                    WriteIndented = true // For pretty-printing
                });

                File.WriteAllText(saveFilePath, json);

                ShowMessage("Game saved successfully!");
            }
            catch (Exception ex)
            {
                ErrorMessage(ex, "Error saving game");
            }
        }

        public void LoadGame(string saveFilePath)
        {
            if (!File.Exists(saveFilePath))
            {
                ShowMessage("Save file not found.");
                ClearData(); // Clear data if the file doesn't exist
                return;
            }

            try
            {
                // Read the JSON string from the specified file
                string json = File.ReadAllText(saveFilePath);

                // Deserialize the JSON string back into a SaveContainer
                var container = JsonSerializer.Deserialize<GameManager>(json);

                if (container == null || container.SettingsBool == null || container.GameData == null)
                {
                    ShowMessage("Failed to load settings or game data.");
                    ClearData(); // Clear data if deserialization fails
                    return;
                }

                SettingsBool = container.SettingsBool;
                GameData = container.GameData;
            }
            catch (JsonException jex)
            {
                ErrorMessage(jex, "JSON deserialization error");
                ClearData(); // Clear data if deserialization fails
            }
            catch (Exception ex)
            {
                ErrorMessage(ex, "Error loading game");
                ClearData();
            }
        }

        private void ClearData()
        {
            // Reset to default
            Settings = new();
            GameData = new();
            SettingsBool = new();
        }

        // Helper methods for UI interaction
        private void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        private void ErrorMessage(Exception ex, string context)
        {
            MessageBox.Show($"{context}: {ex.Message}");
        }
    }


    public partial class MainWindow : System.Windows.Window
    {
        public GameManager gameManager = new();
        public GameDataManager gameData = new();

        // will make the popup and selecting stuff and all that but not now (for colors)

        // TODO : make this do stuff (file path)
        // TODO : make this do stuff (save - load game)

        public string saveFilePath;
        public readonly string saveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games\\VoidVenture\\Saves"
        );


        private void generalMenuButton_Click()
        {
            MenuOpen("default");
        }
        private void settingsMenuButton_Click()
        {
            MenuOpen("settings");
        }
        private void saveMenuButton_Click()
        {
            MenuOpen("save");
        }

        private void MenuButton_Click()
        {
            MenuOpen(null);
        }

        public void TryMenuSwitch(string? place)
        {
            if (!isMenuOpened)
            {
                MenuOpen(place);
            }
            else
            {
                MenuClose();
            }
        }
        public void MenuOpen(string? place)
        {
            PauseGame();
            try
            {
                MenuOverlay.Visibility = Visibility.Visible;
                isMenuOpened = true; ChangeHudVisibility(isMenuOpened);
                if (place == null || place == "default")
                {
                    MenuSave.Visibility = Visibility.Collapsed;
                    MenuSettings.Visibility = Visibility.Collapsed;
                    MenuGeneral.Visibility = Visibility.Visible;


                }
                else if (place == "settings")
                {
                    MenuSave.Visibility = Visibility.Collapsed;
                    MenuSettings.Visibility = Visibility.Visible;
                    MenuGeneral.Visibility = Visibility.Collapsed;


                }
                else if (place == "save")
                {
                    MenuSave.Visibility = Visibility.Visible;
                    MenuSettings.Visibility = Visibility.Collapsed;
                    MenuGeneral.Visibility = Visibility.Collapsed;

                    var saveFiles = GetSaveFiles();
                    saveFileSelector.ItemsSource = saveFiles;
                    saveFileSelector.SelectedIndex = saveFiles.Length - 1;
                }

            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
        }
        public void MenuClose()
        {
            isMenuOpened = false; ChangeHudVisibility(isMenuOpened);
            MenuOverlay.Visibility = Visibility.Collapsed; // Close the overlay
            ResumeGame();
        }

        private void ChangeHudVisibility(bool value)
        {
            if (!value)
            {
                CloseButton.Visibility = Visibility.Visible;
                MenuButton.Visibility = Visibility.Visible;
            }
            else
            {
                CloseButton.Visibility = Visibility.Collapsed;
                MenuButton.Visibility = Visibility.Collapsed;
            }
        }


        private void saveButton_Click()
        {
            SaveData();
        }


        private void CheckSavePath()
        {
            // this was when i wanted to implment the manual saving ... not anymore
            if (string.IsNullOrEmpty(saveFilePath))
                saveFilePath = Path.Combine(saveDirectory, "Save_001.json");

            if (!Path.HasExtension(saveFilePath))
                saveFilePath += ".json";

            if (Path.GetExtension(saveFilePath) != ".json" && Path.GetExtension(saveFilePath) != ".txt")
                saveFilePath = Path.ChangeExtension(saveFilePath, ".json");
        }

        private string?[] GetSaveFiles()
        {
            if (!Directory.Exists(saveDirectory))
                return [];

            var txtFiles = Directory.GetFiles(saveDirectory, "*.txt")
                                    .Select(Path.GetFileName);

            var jsonFiles = Directory.GetFiles(saveDirectory, "*.json")
                                     .Select(Path.GetFileName);

            // Combine and return the results as an array
            return txtFiles.Concat(jsonFiles).ToArray();
        }

        private void SaveData()
        {
            try
            {
                CheckSavePath();
                Directory.CreateDirectory(Path.GetDirectoryName(saveFilePath));
                SetGameManagerToCurrentData();
                gameManager.SaveGame(saveFilePath); // Save both objects
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
        }

        public void SetGameManagerToCurrentData()
        {
            gameManager.GameData = gameData;
            gameManager.Settings = DO;
        }

        private void LoadButton_Click()
        {
            if (saveFileSelector.SelectedItem == null)
            {
                ShowMessage("Please select a save file to load.");
                return;
            }

            try
            {
                var selectedName = saveFileSelector.SelectedItem.ToString();
                var selectedFile = Path.Combine(saveDirectory, selectedName ?? "Save_001.txt");

                LoadFile(selectedFile);
            }
            catch (Exception ex)
            {
                ErrorMessage(ex,"Error loading game");
            }
        }

        public void LoadFile(string path)
        {
            // Confirm overwrite if there's existing game data
            if (gameData != null && MessageBox.Show(
                "Looks like you already have player data.\nThis will overwrite your current data.",
                "Loading",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                ShowMessage("Load operation canceled.");
                return;
            }

            // Attempt to load the game data
            gameManager.LoadGame(path);

            // Check if the loaded data is valid
            if (gameManager.GameData != null && gameManager.SettingsBool != null)
            {
                // Update with the newly loaded data
                gameData = gameManager.GameData;
                DO.SetAllData(gameManager.SettingsBool);
                ShowMessage($"Loaded successfully! Loaded: {gameData.Saved}");
            }
            else
            {
                ShowMessage("Failed to load the game. Data is empty.");
            }
        }


        private void SaveFileSelector_SelectionChanged()
        {
            if (saveFileSelector.SelectedItem == null) return;

            try
            {
                var selectedFile = Path.Combine(saveDirectory, saveFileSelector.SelectedItem.ToString());
                GameDataManager gM = new();
                gM.Load(selectedFile); // Load to preview

                saveDetails.Text = $"Name: {gM.Name}\nSaved: {gM.Saved}\nCoins: {gM.Coins}";
            }
            catch (Exception ex)
            {
                saveDetails.Text = $"Error: {ex.Message}";
            }
        }

        private void DeleteButton_Click()
        {
            if (saveFileSelector.SelectedItem == null)
            {
                ShowMessage("Please select a save file to delete.");
                return;
            }

            var selectedName = saveFileSelector.SelectedItem.ToString();
            var selectedFile = Path.Combine(saveDirectory, selectedName ?? "Save_001.txt");

            if (MessageBox.Show("Are you sure you want to delete this save file?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                File.Delete(selectedFile);
                ShowMessage("Save file deleted successfully.");
                UpdateSaveFileList();
            }
        }

        private void ResaveButton_Click()
        {
            if (saveFileSelector.SelectedItem == null)
            {
                ShowMessage("Please select a save file to resave.");
                return;
            }

            var selectedName = saveFileSelector.SelectedItem.ToString();
            var selectedFile = Path.Combine(saveDirectory, selectedName ?? "Save_001.txt");

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(selectedFile),
                DefaultExt = ".txt",
                Filter = "Text Files (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                var newFilePath = dialog.FileName;
                if (newFilePath != selectedFile)
                {
                    File.Copy(selectedFile, newFilePath);
                    ShowMessage("Save file resaved successfully.");
                    UpdateSaveFileList();
                }
            }
        }

        private void LoadExternalSave_Click()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a Save File to Load",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = ".txt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var externalFilePath = openFileDialog.FileName;

                    LoadFile(externalFilePath);
                }
                catch (Exception ex)
                {
                    ErrorMessage(ex, "Error loading external save file");
                }
            }
        }

        private void CloseOverlay_Click()
        {
            MenuClose();
        }

        private void UpdateSaveFileList()
        {
            var saveFiles = GetSaveFiles();
            saveFileSelector.ItemsSource = saveFiles;

            if (saveFiles.Length > 0)
                saveFileSelector.SelectedIndex = 0;
            else
                saveDetails.Text = "Select a save file to view details.";
        }


    }
}