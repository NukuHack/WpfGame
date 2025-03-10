using System;

using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
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

namespace VoidVenture
{
    public class GameSave
    {
        public bool Saved { get; set; }
        public string Name { get; set; }
        public int Coins { get; set; }
    }

    public partial class MainWindow : Window
    {
        public bool DORecolorBackground = true;
        public bool DORecolorPlayer = true;
        public bool DOSelectrPlayerManually = false;
        public bool isMenuOpened = false;

        // will make the popup and selecting stuff and all that but not now

        //TODO : make this do stuff

        public GameSave gameData;
        public string saveFilePath;
        public readonly string saveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games\\VoidVenture\\Saves"
        );



        private void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show($"Error: {message}");
        }

        public void TryMenuSwitch()
        {
            if (!isMenuOpened)
                MenuOpen();
            else
                MenuClose();
        }
        public void MenuOpen()
        {
            PauseGame();
            isMenuOpened = true;
            try
            {
                var saveFiles = GetSaveFiles();
                if (saveFiles.Length == 0)
                {
                    ShowMessage("No save files found in the save directory.");
                    ResumeGame();
                    return;
                }

                saveFileSelector.ItemsSource = saveFiles;
                saveFileSelector.SelectedIndex = 0;
                saveFileOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex.Message);
            }
        }
        public void MenuClose()
        {
            isMenuOpened = false;
            saveFileOverlay.Visibility = Visibility.Collapsed; // Close the overlay
            ResumeGame();
        }
        
        public void SaveData()
        {
            PauseGame();
            try
            {
                CheckSavePath();
                Directory.CreateDirectory(Path.GetDirectoryName(saveFilePath));
                SaveGame(gameData, saveFilePath);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex.Message);
            }
        }

        private void GenPlayerData()
        {
            gameData = new GameSave
            {
                Saved = true,
                Coins = 10,
                Name = "Peter",

            };
        }

        private void CheckSavePath()
        {
            if (string.IsNullOrEmpty(saveFilePath))
                saveFilePath = Path.Combine(saveDirectory, "save1.txt");

            if (!Path.HasExtension(saveFilePath))
                saveFilePath += ".txt";

            if (Path.GetExtension(saveFilePath) != ".txt")
                saveFilePath = Path.ChangeExtension(saveFilePath, ".txt");
        }

        private string[] GetSaveFiles()
        {
            if (!Directory.Exists(saveDirectory))
                return Array.Empty<string>();

            return Directory.GetFiles(saveDirectory, "*.txt")
                            .Select(Path.GetFileName)
                            .ToArray();
        }

        private void SaveFileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (saveFileSelector.SelectedItem == null) return;

            try
            {
                var selectedFile = Path.Combine(saveDirectory, saveFileSelector.SelectedItem.ToString());
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

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (saveFileSelector.SelectedItem == null)
            {
                MessageBox.Show("Please select a save file to load.");
                return;
            }

            try
            {
                string selectedFile = Path.Combine(saveDirectory, saveFileSelector.SelectedItem.ToString());
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
                        MessageBox.Show("Load operation canceled.");
                        return;
                    }

                    // Update gameData only if the user confirms
                    gameData = loadedGameData;
                    MessageBox.Show($"Loaded successfully! Loaded: {loadedGameData.Saved}");
                    MenuClose();
                }
                else
                {
                    MessageBox.Show("Failed to load the game.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading the game: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (saveFileSelector.SelectedItem == null)
            {
                ShowMessage("Please select a save file to delete.");
                return;
            }

            var selectedFile = Path.Combine(saveDirectory, saveFileSelector.SelectedItem.ToString());

            if (MessageBox.Show("Are you sure you want to delete this save file?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                File.Delete(selectedFile);
                ShowMessage("Save file deleted successfully.");
                UpdateSaveFileList();
            }
        }

        private void ResaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (saveFileSelector.SelectedItem == null)
            {
                ShowMessage("Please select a save file to resave.");
                return;
            }

            var selectedFile = Path.Combine(saveDirectory, saveFileSelector.SelectedItem.ToString());

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

        private void LoadExternalSave_Click(object sender, RoutedEventArgs e)
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
                    ShowErrorMessage($"Error loading external save file: {ex.Message}");
                }
            }
        }

        private void CloseOverlay_Click(object sender, RoutedEventArgs e)
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
                GenPlayerData();
                MessageBox.Show("No game data to save.");
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
                ShowErrorMessage($"Error saving game: {ex.Message}");
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
