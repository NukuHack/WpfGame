using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DuckHunter
{
    public partial class MainWindow : Window
    {

        // Button click handlers
        private void NewGameButton_Click(object sender, RoutedEventArgs e) =>
            ResetGameAks();
        private void StartGameButton_Click(object sender, RoutedEventArgs e) =>
            _menu.ShowGameScreen();
        private void BackToGameButton_Click(object sender, RoutedEventArgs e) =>
            _menu.ShowGameScreen();
        private void ExitUpgradesButton_Click(object sender, RoutedEventArgs e) =>
            _menu.ShowMainMenu();
        private void UpgradesButton_Click(object sender, RoutedEventArgs e) =>
            _menu.ShowUpgrades();
        private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
            _menu.ShowSettingsScreen();

        private void ExitButton_Click(object sender, RoutedEventArgs e) =>
            CloseAsk();

        private void ExitSettingsButton_Click(object sender, RoutedEventArgs e) =>
            _menu.ShowMainMenu();


        public void EasyDifficulty(object sender, RoutedEventArgs e) =>
            difficulty = 1;
        public void MediumDifficulty(object sender, RoutedEventArgs e) =>
            difficulty = 2;
        public void HardDifficulty(object sender, RoutedEventArgs e) =>
            difficulty = 3;
            // lehet ezt mind külön metódusba rakni , külön a classban egy picit fölösleges de mindegy
        public void ResetGameAks()
        {

            if (MessageBoxResult.OK == MessageBox.Show(
                "Are you sure you want to Start a new Game?\nEvery data will be cleared.",
                "Restart",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information
                ))
                ResetGame();
        }
        public void ResetGame()
        {
            Point = 0;Kills = 0;
            foreach (var duck in ducks)
                this.MainCanvas.Children.Remove(duck.duckRectangle);
            ducks.Clear();
            UpdatePoint(0);

            ShowGameScreen();
        }


        // Public methods to control screens
        public void ShowMainMenu()
        {
            if (DeathCanvas.Visibility == Visibility.Visible) return;

            StopGame();
            this.Cursor = default;
            UpgradesCanvas.Visibility = Visibility.Collapsed;
            MainMenuCanvas.Visibility = Visibility.Visible;
            SettingsCanvas.Visibility = Visibility.Collapsed;
        }
        public void ShowUpgrades()
        {
            StopGame();
            this.Cursor = default;
            UpgradesCanvas.Visibility = Visibility.Visible;
            MainMenuCanvas.Visibility = Visibility.Collapsed;
            SettingsCanvas.Visibility = Visibility.Collapsed;
        }

        public void ShowSettingsScreen()
        {
            StopGame();
            this.Cursor = default;
            UpgradesCanvas.Visibility = Visibility.Collapsed;
            MainMenuCanvas.Visibility = Visibility.Collapsed;
            SettingsCanvas.Visibility = Visibility.Visible;
        }

        public void ShowGameScreen()
        {
            RestartGame();
            this.Cursor = new Cursor("img/crosshair.cur");
            DeathCanvas.Visibility = Visibility.Collapsed;
            UpgradesCanvas.Visibility = Visibility.Collapsed;
            MainMenuCanvas.Visibility = Visibility.Collapsed;
            SettingsCanvas.Visibility = Visibility.Collapsed;
        }

        public void StopGame()
        {
            Game = false;
        }

        public void RestartGame()
        {
            Game = true;
        }
    }

    public class Menu
    {
        private readonly MainWindow _window;

        public Menu(MainWindow window)
        {
            _window = window;
        }

        public void ShowMainMenu() =>
            _window.Dispatcher.Invoke(() => _window.ShowMainMenu());

        public void ShowSettingsScreen() =>
            _window.Dispatcher.Invoke(() => _window.ShowSettingsScreen());

        public void ShowUpgrades() =>
            _window.Dispatcher.Invoke(() => _window.ShowUpgrades());
        public void ShowGameScreen()
        {
            _window.Dispatcher.Invoke(() =>
            {
                _window.ShowGameScreen();
                // Add game initialization logic here
                _window.NewGameButton.Visibility = Visibility.Visible;
                _window.BackToGameButton.Visibility = Visibility.Visible;
                _window.Game = true;
                _window.StartGameButton.Content = "Resume Game";
            });
        }
    }
}
