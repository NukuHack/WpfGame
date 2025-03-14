using System;

using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Xml.Linq;
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
    public class GameSave
    {
        public bool? Saved { get; set; }
        public string? Name { get; set; }
        public int? Coins { get; set; }
    }
    public class Setting
    {
        public string? Name { get; set; }
        public string? Desc { get; set; }
        public bool? Default { get; set; }
        public bool? Value { get; set; }
    }

    public partial class MainWindow : System.Windows.Window
    {
        public bool DORecolorBackground = true; // what it says, if DOUseNoiseTerrain is true this is useless
        public bool DORecolorPlayer = true; // what it says
        public bool DOSelectPlayerManually = false; // adds the option to chose the player (img) by the user
        public bool DOUseNoiseTerrain = true; // terrain gen intead of pre-defined tiles
        public bool DORandomizeTerrainColors = true; // this will make the terrain have random colors, if DOUseNoiseTerrain is false this is useless
        public bool DORandomizeTerrainMulti = false; // this will make the terrain have random multi, if DOUseNoiseTerrain is false this is useless
        public bool DORandomizeTerrainHeights = true; // this will make the terrain have randomly scaled layers, if DOUseNoiseTerrain is false this is useless
        public bool DODebug = false; // what it says

        // currently does not work correctly
        public bool DOUseChunkGen = false; // using the chunk-grid system instead of the usual all at once every frame method ...


        // will make the popup and selecting stuff and all that but not now

        //TODO : make this do stuff

        public GameSave gameData;
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
                if (place == null||place == "default")
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
                    saveFileSelector.SelectedIndex = saveFiles.Length-1;
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

        public void SaveData()
        {
            try
            {
                CheckSavePath();
                Directory.CreateDirectory(Path.GetDirectoryName(saveFilePath));
                SaveGame(gameData, saveFilePath);
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
        }


        private void CheckSavePath()
        {
            // this was when i wanted to implment the manual saving ... not anymore
            if (string.IsNullOrEmpty(saveFilePath))
                saveFilePath = Path.Combine(saveDirectory, "Save_001.txt");

            if (!Path.HasExtension(saveFilePath))
                saveFilePath += ".txt";

            if (Path.GetExtension(saveFilePath) != ".txt")
                saveFilePath = Path.ChangeExtension(saveFilePath, ".txt");
        }

        private string?[] GetSaveFiles()
        {
            if (!Directory.Exists(saveDirectory))
                return Array.Empty<string>();

            return Directory.GetFiles(saveDirectory, "*.txt")
                            .Select(Path.GetFileName)
                            .ToArray();
        }

        private void SaveFileSelector_SelectionChanged()
        {
            if (saveFileSelector.SelectedItem == null) return;

            try
            {
                var selectedName = saveFileSelector.SelectedItem.ToString();
                var selectedFile = Path.Combine(saveDirectory, selectedName ?? "Save_001.txt");
                var loadedGameData = LoadGame(selectedFile);

                if (loadedGameData != null)
                {
                    saveDetails.Text = $"Player Name: {loadedGameData.Name}\n" +
                                       $"Saved: {loadedGameData.Saved}\n" +
                                       $"Coins: {loadedGameData.Coins}";
                }
                else
                {
                    saveDetails.Text = "Failed to load save details.";
                }

            }
            catch (Exception ex)
            {
                saveDetails.Text = $"Error loading save details: {ex.Message}";
            }
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
                var selectedFile = Path.Combine(saveDirectory, selectedName == null ? "Save_001.txt" : selectedName);
                GameSave loadedGameData = LoadGame(selectedFile);

                if (loadedGameData != null)
                {
                    // Check if there's existing game data and confirm overwrite if necessary
                    if (gameData != null && MessageBox.Show(
                        "Looks like you already have player data.\nThis will overwrite your current data.",
                        "Loading",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Question) != MessageBoxResult.OK)
                    {
                        // If the user cancels, do not update gameData
                        ShowMessage("Load operation canceled.");
                        return;
                    }

                    // Update gameData only if the user confirms
                    gameData = loadedGameData;
                    ShowMessage($"Loaded successfully! Loaded: {loadedGameData.Saved}");
                    MenuClose();
                }
                else
                {
                    ShowMessage("Failed to load the game.");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error loading the game: {ex.Message}");
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
            var selectedFile = Path.Combine(saveDirectory, selectedName == null ? "Save_001.txt" : selectedName);

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
            var selectedFile = Path.Combine(saveDirectory, selectedName == null ? "Save_001.txt" : selectedName);

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
                    var loadedGameData = LoadGame(externalFilePath);

                    if (loadedGameData != null)
                    {
                        gameData = loadedGameData;
                        ShowMessage($"External save file loaded successfully!\nLoaded: {loadedGameData.Saved}");
                    }
                    else
                    {
                        ShowMessage("Failed to load the external save file.");
                    }
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

        private void SaveGame(GameSave gameData, string saveFilePath)
        {
            if (gameData == null)
            {
                ShowMessage("No game data to save.");
                ResumeGame();
                return;
            }

            if (File.Exists(saveFilePath))
            {
                if (MessageBox.Show("This will overwrite the existing save file.", "Saving", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                {
                    ResumeGame();
                    return;
                }
            }
            var sb = new StringBuilder();


            // Serialize data
            sb.AppendLine("[Player]");
            sb.AppendLine($"Name={gameData.Name}");
            sb.AppendLine($"Coins={gameData.Coins}");

            sb.AppendLine("[Data]");
            sb.AppendLine($"Saved={gameData.Saved}");



            try
            {
                File.WriteAllText(saveFilePath, sb.ToString());
                ShowMessage("Game saved successfully!");
                ResumeGame();
            }
            catch (Exception ex)
            {
                ErrorMessage(ex, "Error saving game");
            }
        }

        private GameSave LoadGame(string saveFilePath)
        {
            if (!File.Exists(saveFilePath))
                throw new FileNotFoundException("Save file not found.", saveFilePath);

            var lines = File.ReadAllLines(saveFilePath);
            var loadedGameData = new GameSave();


            bool inPlayerSection = false;
            bool inDataSection = false;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line == "[Player]")
                {
                    inPlayerSection = true;
                    inDataSection = false;
                    continue;
                }
                else if (line == "[Data]")
                {
                    inPlayerSection = false;
                    inDataSection = true;
                    continue;
                }

                var parts = line.Split('=');
                if (parts.Length != 2) continue;

                if (inPlayerSection)
                {
                    switch (parts[0])
                    {
                        case "Name": loadedGameData.Name = parts[1]; break;
                        case "Coins": loadedGameData.Coins = int.Parse(parts[1]); break;
                    }
                }
                else if (inDataSection)
                {
                    switch (parts[0])
                    {
                        case "Saved": loadedGameData.Saved = bool.Parse(parts[1]); break;
                    }
                }
                /*
                    else if (inLevelProgressSection)
                    {
                        switch (parts[0])
                        {
                            case "CurrentLevel": loadedGameData.LevelProgress.CurrentLevel = int.Parse(parts[1]); break;
                            case "UnlockedLevels":
                                loadedGameData.LevelProgress.UnlockedLevels = parts[1].Split(',')
                                    .Where(a => !string.IsNullOrWhiteSpace(a))
                                    .Select(int.Parse)
                                    .ToList();
                                break;
                        }
                    }
                */
            }

            return loadedGameData;
        }

    }
}