using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static System.Formats.Asn1.AsnWriter;

namespace DuckHunter
{
    public class Crosshair
    {
        public readonly int Width;
        public readonly int Height;
        public Crosshair(int wid,int hei)
        {
            this.Width = wid;
            this.Height = hei;
        }
    }
    public partial class MainWindow : Window
    {
        public List<Duck> ducks = new(); // List to store all ducks
        public Dictionary<string, BitmapImage[]> imageCache = new(); // Image cache for all ducks
        public DispatcherTimer duckSpawnTimer; // Timer to spawn ducks
        public DispatcherTimer moveTimer;  // Timer a mozgáshoz
        public ImgLoader loader; // Image loader
        public Menu menu;
        private readonly Menu _menu;

        private int _fireRate = 300; // milliseconds
        private int _duckSpawn = 600;
        public bool Game = false;
        public DateTime _lastShotTime;
        public double _multi = 1;
        public bool hasQuickFirePower = false;
        public double difficulty;
        public int Kills = 0;
        public double Point = 0;
        public Random rnd = new();
        public Crosshair crosshair;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.SizeChanged += MainWindow_SizeChanged;

            // for testing
            difficulty = 1;

            // making the menu
            _menu = new Menu(this);

            // Background péladányosítása
            Background background = new(this, 0, 0);

            // Initialize ImgLoader
            loader = new ImgLoader();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CenterStuff(MainMenuCanvas);
            CenterStuff(SettingsCanvas);
            CenterStuff(UpgradesCanvas);
        }
        private void CenterStuff(Border x)
        {
            int aWid = (int)this.ActualHeight;int aHei = (int)this.ActualWidth;
            int xWid = (int)x.Width;int xHei = (int)x.Height;

            Canvas.SetLeft(x, (aWid - xWid) / 2);
            Canvas.SetTop(x, (aHei - xHei) / 2);
        }

        public void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ShowMainMenu();
            NewGameButton.Visibility = Visibility.Collapsed;
            PreloadImages();
            this.PreviewMouseDown += MainWindow_PreviewMouseDown;
            //GameStart();
            this.KeyDown += MainWindow_KeyDown;

            //setting the cost display for hover
            BuyAbilityQuickfire.MouseMove += (s, e) => HoverArea_MouseMove(s,e,1000);
            UpdrageMulti.MouseMove += (s, e) => HoverArea_MouseMove(s, e, 25);
            UpdrageFireRate.MouseMove += (s, e) => HoverArea_MouseMove(s, e, 25);
            UpdrageDuckspawn.MouseMove += (s, e) => HoverArea_MouseMove(s, e, 50);

            BuyAbilityQuickfire.MouseLeave += (s, e) => HoverArea_MouseLeave();
            UpdrageMulti.MouseLeave += (s, e) => HoverArea_MouseLeave();
            UpdrageFireRate.MouseLeave += (s, e) => HoverArea_MouseLeave();
            UpdrageDuckspawn.MouseLeave += (s, e) => HoverArea_MouseLeave();


            crosshair = new(100, 100);

            duckSpawnTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(_duckSpawn)
            };
            duckSpawnTimer.Tick += DuckSpawnTimer_Tick;
            duckSpawnTimer.Start();


            // Timer beállítása
            // this would be like ... 20 times faster if we would process it once for all ducks but who cares about optimization
            moveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS (16ms)
            };
            moveTimer.Tick += (s, e) => {
                GlobalUpdateTimer_Tick();
            }
            ;
            moveTimer.Start();

        }

        private void HoverArea_MouseMove(object s, MouseEventArgs e, int cost)
        {
            // Show the infoText
            infoText.Visibility = Visibility.Visible;

            // Get the mouse position relative to the parent container
            Point mousePosition = e.GetPosition(MainCanvas);
            double x = mousePosition.X - ActualWidth / 2;
            double y = mousePosition.Y - ActualHeight / 2;

            // Update the content of the infoText
            infoText.Content = $"Cost:\n{cost}";

            // Force layout update to ensure ActualHeight and ActualWidth are accurate
            infoText.UpdateLayout();

            // Position the infoText using TranslateTransform
            infoTextTransform.X = x + 5; // Offset to the right
            infoTextTransform.Y = y - 5 - infoText.ActualHeight/2; // Offset above the cursor
        }

        private void HoverArea_MouseLeave()
        {
            Dispatcher.Invoke(() =>
            {
                infoText.Visibility = Visibility.Collapsed;
            });
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    ShowMainMenu();
                    break;
                case Key.E:
                    ShowUpgrades();
                    break;
                case Key.Q:
                    ActivatePowerUp();
                    break;
                case Key.LeftShift:
                    // speed stuff or idk
                    break;
                default:
                    break;
            }

        }

        private void CloseAsk()
        {
            if (MessageBoxResult.Yes == MessageBox.Show(
                "Are you sure you want to leave now?",
                "Leave",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
                ))
                Close();
        }


        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!this.Game) return;

            // Fire rate check
            if (DateTime.UtcNow - _lastShotTime < TimeSpan.FromMilliseconds(_fireRate))
                return;

            // Update last shot time
            _lastShotTime = DateTime.UtcNow;

            // Rest of your existing code
            Point mousePosition = e.GetPosition(this);

            // Check each duck for a hit
            foreach (var duck in ducks)
            {
                if (duck.Status == "fly")
                {
                    // Calculate the bounding box of the duck
                    double duckLeft = duck.x - duck.Width / 2;
                    double duckRight = duck.x + duck.Width / 2;
                    double duckTop = duck.y - duck.Height / 2;
                    double duckBottom = duck.y + duck.Height / 2;

                    // Calculate the bounding box of the crosshair
                    double crosshairLeft = mousePosition.X - crosshair.Width / 2;
                    double crosshairRight = mousePosition.X + crosshair.Width / 2;
                    double crosshairTop = mousePosition.Y - crosshair.Height / 2;
                    double crosshairBottom = mousePosition.Y + crosshair.Height / 2;

                    // Check for intersection between the duck and the crosshair
                    if (crosshairRight >= duckLeft &&
                        crosshairLeft <= duckRight &&
                        crosshairBottom >= duckTop &&
                        crosshairTop <= duckBottom)
                    {
                        // Duck is hit!
                        duck.Status = "fainted"; // Update the duck's status or perform other actions
                        duck.speedY = 3; // maybe 3 but maybe a bit more so they are not in the way for long
                        duck.speedX = 0;
                        Kills++;
                        UpdatePoint(duck.Value* (int)difficulty * _multi);
                        break;
                        //ofc you can't shoot more than one duck with a single shot ....
                    }
                }
            }
        }
        private void ActivatePowerUp()
        {
            if (!hasQuickFirePower) return;
            int originalRate = _fireRate;
             _fireRate = (int)(originalRate*0.3);
             Task.Delay(5000).ContinueWith(_ => _fireRate = originalRate);

        }
        private void BuyAbility_Click(object sender, EventArgs e)
        {
            if (Point >= 1000 && hasQuickFirePower == false)
            {
                hasQuickFirePower = true;
                BuyAbilityQuickfire.Background = Brushes.DarkGreen;
            }
            else if (hasQuickFirePower == false)
                ShowMessage("Not enough cash to buy that", "Buy_fail");
            else
                ShowMessage("You already have that", "Buy_fail");
        }
        // Upgrade buttons
        private void UpgradeBulletSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (Point >= 50)
            {
                UpdatePoint(-50);
                _duckSpawn -= 50;
                duckSpawnTimer.Interval = TimeSpan.FromMilliseconds(_duckSpawn);
                DuckSpawnText.Text = $"Duckspawn: {_duckSpawn}";
            }
            else
                ShowMessage("Not enough cash to buy that", "Buy_fail");
        }

        private void UpgradeFireRate_Click(object sender, RoutedEventArgs e)
        {
            if (Point >= 25)
            {
                UpdatePoint(-25);
                _fireRate -= 50;
                FireRateText.Text = $"Fire Rate: {_fireRate}ms";
            }
            else
                ShowMessage("Not enough cash to buy that", "Buy_fail");
        }


        private void UpgradeMulti_Click(object sender, RoutedEventArgs e)
        {
            if (Point >= 25)
            {
                UpdatePoint(-25);
                _multi += 0.5;
                MultiText.Text = $"Multi: {_multi}";
            }
            else
                ShowMessage("Not enough cash to buy that", "Buy_fail");  
        }

        private void UpdatePoint(double x)
        {
            Point += Math.Round(x);
            ScoreDisplay.Content = $"Current DICKS hunted : {Kills}";
            pointDisplay.Content = $"Current Points : {Point}";
            ScoreDisplay.Visibility = Visibility.Visible;
            pointDisplay.Visibility = Visibility.Visible;
        }


        private void PreloadImages()
        {
            // Define all possible statuses and their frame counts
            var statuses = new Dictionary<string, int>
            {
                { "fly", 8 }, // 8 frames for "fly"
                { "fainted", 2 } // 2 frames for "faint"
            };

            foreach (var status in statuses.Keys)
            {
                int frameCount = statuses[status];
                BitmapImage[] frames = new BitmapImage[frameCount];

                for (int i = 1; i <= frameCount; i++)
                {
                    string imagePath = $"img/duck/{status}/frame-{i}.png";
                    try
                    {
                        frames[i - 1] = new BitmapImage(new Uri(imagePath, UriKind.Relative));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading image: {ex.Message}");
                    }
                }

                imageCache[status] = frames;
            }
        }


        private void DuckSpawnTimer_Tick(object? sender, EventArgs e)
        {
            if (!this.Game) return;

            // Stop spawning after 20 ducks
            if (ducks.Count >= 40) return;

            // Create a new duck in the list
            ducks.Add(new Duck(this));

            // Debug: Log the number of ducks spawned
            Console.WriteLine($"Duck {ducks.Count} spawned.");
        }

        private void GlobalUpdateTimer_Tick()
        {
            if (!this.Game) return;
            // Update all ducks
            foreach (var duck in ducks.ToList()) // Use ToList() to avoid modifying the collection during iteration
            {
                duck.MoveDuck(); // Pass the MainWindow instance to the duck
            }
        }



        public void GameEnded()
        {
            StopGame();

            this.Cursor = default;
            DeathCanvas.Visibility = Visibility.Visible;

            _fireRate = 0; // milliseconds
            _duckSpawn = 0;
            _lastShotTime = DateTime.Now;
            _multi = 1;
            hasQuickFirePower = false;

            BuyAbilityQuickfire.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            FireRateText.Text = $"Fire Rate: {_fireRate}ms";
            MultiText.Text = $"Multi: {_multi}";
            DuckSpawnText.Text = $"Duckspawn: {_duckSpawn}";
            duckSpawnTimer.Interval = TimeSpan.FromMilliseconds(_duckSpawn);

            if (ScoreDisplay.Content == string.Empty || ScoreDisplay.Content is null)
            {
                ScoreDisplay2.Content = "You really lost before killing your first duck ?";
                deathDisplay.Content = "Pathetic ....";
            }
            else
            {
                ScoreDisplay2.Content = ScoreDisplay.Content;
                deathDisplay.Content = "atleast you killed some before your death";
            }

            MessageBox.Show("DEATH");
            SaveScore();
        }



        private void SaveScore()
        {
            DateTime now = DateTime.Now;

            var (LastScores,BestScores)
            = LoadScores();


            // Add the current score to both dictionaries
            LastScores[now] = Kills;
            BestScores[now] = Kills;

            // Trim the last scores dictionary to the latest 10 scores
            LastScores = LastScores
                .OrderByDescending(score => score.Key)  // Sort by timestamp in descending order
                .Take(10)                               // Keep the latest 10 scores
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            // Trim the best scores dictionary to the top 10 scores
            BestScores = BestScores
                .OrderByDescending(score => score.Value) // Sort by Kills in descending order
                .Take(10)                                 // Keep the top 10 scores
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            // Write both dictionaries to the file
            using (var writer = new StreamWriter("highscores.txt"))
            {
                // Write the last scores section
                writer.WriteLine("[LAST_SCORES]");
                foreach (var score in LastScores)
                {
                    writer.WriteLine($"{score.Key} --- {score.Value}");
                }

                // Write the best scores section
                writer.WriteLine("[BEST_SCORES]");
                foreach (var score in BestScores)
                {
                    writer.WriteLine($"{score.Key} --- {score.Value}");
                }
            }
        }

        private (Dictionary<DateTime, int>, Dictionary<DateTime, int>) LoadScores()
        {
            if (!File.Exists("highscores.txt"))
                return (new Dictionary<DateTime, int>(), new Dictionary<DateTime, int>());

            List<string> data = File.ReadAllLines("highscores.txt").ToList();
            Dictionary<DateTime, int> lastScores = new Dictionary<DateTime, int>();
            Dictionary<DateTime, int> bestScores = new Dictionary<DateTime, int>();

            bool isLastScoresSection = false;
            bool isBestScoresSection = false;

            foreach (string line in data)
            {
                if (line.StartsWith("[LAST_SCORES]"))
                {
                    isLastScoresSection = true;
                    isBestScoresSection = false;
                    continue;
                }
                else if (line.StartsWith("[BEST_SCORES]"))
                {
                    isLastScoresSection = false;
                    isBestScoresSection = true;
                    continue;
                }

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(new[] { " --- " }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    DateTime date = DateTime.Parse(parts[0]);
                    int kills = int.Parse(parts[1]);

                    if (isLastScoresSection)
                        lastScores[date] = kills;
                    else if (isBestScoresSection)
                        bestScores[date] = kills;
                }
            }

            return (lastScores, bestScores);
        }

        private void ShowMessage(string Text, string? Header)
        {
            if (Header != null)
                MessageBox.Show(Text, Header);
            else
                MessageBox.Show(Text);
        }

    }
}
