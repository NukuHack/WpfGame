using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

public class Player
{
    public string Name { get; set; }
    public int Health { get; set; }
    public int Coins { get; set; }
    public Position Position { get; set; } = new Position();
}

public class Position
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class LevelProgress
{
    public int CurrentLevel { get; set; }
    public List<int> UnlockedLevels { get; set; } = new List<int>();
}

public class Item
{
    public string ItemName { get; set; }
    public int Quantity { get; set; }
}

public class GameSave
{
    public Player Player { get; set; } = new Player();
    public LevelProgress LevelProgress { get; set; } = new LevelProgress();
    public List<Item> Inventory { get; set; } = new List<Item>();
}

namespace save_load
{
    public partial class MainWindow : Window
    {
        private GameSave gameData;
        private string saveFilePath;
        private readonly string saveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games\\SpaceGame\\Saves"
        );

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize UI
            saveFileOverlay.Visibility = Visibility.Collapsed;

            // Event handlers
            loadData.Click += LoadData_Click;
            saveData.Click += SaveData_Click;
            genData.Click += GenPlayer_Click;

            // Overlay event handlers
            closeOverlay.Click += CloseOverlay_Click;
            loadButton.Click += LoadButton_Click;
            deleteButton.Click += DeleteButton_Click;
            resaveButton.Click += ResaveButton_Click;
            loadExternalSave.Click += LoadExternalSave_Click;
            saveFileSelector.SelectionChanged += SaveFileSelector_SelectionChanged;
        }

        private void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show($"Error: {message}");
        }

        private void LoadData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFiles = GetSaveFiles();
                if (saveFiles.Length == 0)
                {
                    ShowMessage("No save files found in the save directory.");
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

        private void SaveData_Click(object sender, RoutedEventArgs e)
        {
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

        private void GenPlayer_Click(object sender, RoutedEventArgs e)
        {
            gameData = new GameSave
            {
                Player = new Player
                {
                    Name = "Hero",
                    Health = 100,
                    Coins = 50,
                    Position = new Position { X = 100, Y = 200 }
                },
                LevelProgress = new LevelProgress
                {
                    CurrentLevel = 3,
                    UnlockedLevels = new List<int> { 1, 2, 3, 4 }
                },
                Inventory = new List<Item>
                {
                    new Item { ItemName = "Sword", Quantity = 1 },
                    new Item { ItemName = "Potion", Quantity = 3 }
                }
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
                    saveDetails.Text = $"Player Name: {loadedGameData.Player.Name}\n" +
                                       $"Health: {loadedGameData.Player.Health}\n" +
                                       $"Coins: {loadedGameData.Player.Coins}";
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
                    MessageBox.Show($"Loaded successfully! Player Name: {loadedGameData.Player.Name}");
                    saveFileOverlay.Visibility = Visibility.Collapsed; // Close the overlay
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
                        ShowMessage($"External save file loaded successfully!\nPlayer Name: {loadedGameData.Player.Name}");
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
            saveFileOverlay.Visibility = Visibility.Collapsed;
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
                throw new InvalidOperationException("No game data to save.");

            if (File.Exists(saveFilePath))
            {
                if (MessageBox.Show("This will overwrite the existing save file.", "Saving", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                    return;
            }

            var sb = new StringBuilder();

            // Serialize Player data
            sb.AppendLine("[Player]");
            sb.AppendLine($"Name={gameData.Player.Name}");
            sb.AppendLine($"Health={gameData.Player.Health}");
            sb.AppendLine($"Coins={gameData.Player.Coins}");
            sb.AppendLine($"PositionX={gameData.Player.Position.X}");
            sb.AppendLine($"PositionY={gameData.Player.Position.Y}");

            // Serialize LevelProgress data
            sb.AppendLine("[LevelProgress]");
            sb.AppendLine($"CurrentLevel={gameData.LevelProgress.CurrentLevel}");
            sb.AppendLine("UnlockedLevels=" + string.Join(",", gameData.LevelProgress.UnlockedLevels));

            // Serialize Inventory data
            sb.AppendLine("[Inventory]");
            foreach (var item in gameData.Inventory)
            {
                sb.AppendLine($"{item.ItemName}={item.Quantity}");
            }

            try
            {
                File.WriteAllText(saveFilePath, sb.ToString());
                ShowMessage("Game saved successfully!");
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
            bool inLevelProgressSection = false;
            bool inInventorySection = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line == "[Player]")
                {
                    inPlayerSection = true;
                    inLevelProgressSection = false;
                    inInventorySection = false;
                    continue;
                }
                else if (line == "[LevelProgress]")
                {
                    inPlayerSection = false;
                    inLevelProgressSection = true;
                    inInventorySection = false;
                    continue;
                }
                else if (line == "[Inventory]")
                {
                    inPlayerSection = false;
                    inLevelProgressSection = false;
                    inInventorySection = true;
                    continue;
                }

                var parts = line.Split('=');
                if (parts.Length != 2) continue;

                if (inPlayerSection)
                {
                    switch (parts[0])
                    {
                        case "Name": loadedGameData.Player.Name = parts[1]; break;
                        case "Health": loadedGameData.Player.Health = int.Parse(parts[1]); break;
                        case "Coins": loadedGameData.Player.Coins = int.Parse(parts[1]); break;
                        case "PositionX": loadedGameData.Player.Position.X = int.Parse(parts[1]); break;
                        case "PositionY": loadedGameData.Player.Position.Y = int.Parse(parts[1]); break;
                    }
                }
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
                else if (inInventorySection)
                {
                    loadedGameData.Inventory.Add(new Item { ItemName = parts[0], Quantity = int.Parse(parts[1]) });
                }
            }

            return loadedGameData;
        }

    }
}