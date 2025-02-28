using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Xml.Linq;

namespace WpfApp5
{
    public static class MathUtils
    {
        // Clamp function for integers and doubles
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));
        public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(value, max));
    }

    public class Player
    {
        public int x { get; set; }
        public int y { get; set; }
        public int speed { get; set; }

        public int width;
        public int height;

        public Player(int X, int Y, int Speed, int Width, int Height)
        {
            this.x = X;
            this.y = Y;
            this.speed = Speed;
            this.width = Width;
            this.height = Height;
        }

        public void Move(Direction direction, int canvasWidth, int canvasHeight)
        {
            switch (direction)
            {
                case Direction.Up: y -= speed; break;
                case Direction.Down: y += speed; break;
                case Direction.Left: x -= speed; break;
                case Direction.Right: x += speed; break;
            }

            // Ensure the player stays within bounds
            x = MathUtils.Clamp(x, 0, canvasWidth - width);
            y = MathUtils.Clamp(y, 0, canvasHeight - height);
        }
    }

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
        None
    }

    public class GameMap
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TileWidth { get; private set; }
        public int TileHeight { get; private set; }
        public List<int[]> Layers { get; private set; } // Corrected type
        public BitmapImage TileSetImage { get; private set; }

        public GameMap(string tmxFilePath, string tileSetImagePath)
        {
            LoadMap(tmxFilePath, tileSetImagePath);
        }

        private void LoadMap(string tmxFilePath, string tileSetImagePath)
        {
            if (!File.Exists(tmxFilePath))
            {
                MessageBox.Show($"Map file not found: {tmxFilePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var doc = XDocument.Load(tmxFilePath);
                var mapElement = doc.Root;

                Width = int.Parse(mapElement.Attribute("width").Value);
                Height = int.Parse(mapElement.Attribute("height").Value);

                TileWidth = int.Parse(mapElement.Attribute("tilewidth").Value);
                TileHeight = int.Parse(mapElement.Attribute("tileheight").Value);

                Layers = new List<int[]>();
                foreach (var layer in mapElement.Elements("layer"))
                {
                    var data = layer.Element("data").Value.Split(',').Select(int.Parse).ToArray();
                    Layers.Add(data);
                    Console.WriteLine($"Loaded layer with {data.Length} tiles.");
                }

                if (File.Exists(tileSetImagePath))
                {
                    TileSetImage = new BitmapImage(new Uri(tileSetImagePath, UriKind.Relative));
                    Console.WriteLine("Tileset image loaded successfully.");
                }
                else
                {
                    MessageBox.Show($"Tileset image not found: {tileSetImagePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading map: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public partial class MainWindow : Window
    {
        private const string MapFilePath = "maps/default.tmx";
        private const string TileSetImagePath = "tiles/basic.png";
        private const string BackgroundImagePath = "backgrounds/Night_Space.png";

        private readonly GameMap gameMap;
        private Player player;
        private TranslateTransform playerTransform;

        public MainWindow()
        {
            InitializeComponent();

            Console.WriteLine("Initializing game...");
            gameMap = new GameMap(MapFilePath, TileSetImagePath);

            Loaded += Window_Loaded;
            SizeChanged += MainWindow_SizeChanged;
            KeyDown += MainWindow_KeyDown;

            StartGameLoop();
        }

        private void StartGameLoop()
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdatePlayerPosition();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            Direction direction = Direction.None;

            switch (e.Key)
            {
                case Key.Up: direction = Direction.Up; break;
                case Key.Down: direction = Direction.Down; break;
                case Key.Left: direction = Direction.Left; break;
                case Key.Right: direction = Direction.Right; break;
            }

            if (direction != Direction.None && player != null)
                player.Move(direction, (int)GameCanvas.ActualWidth, (int)GameCanvas.ActualHeight);
        }

        private void UpdatePlayerPosition()
        {
            if (playerTransform != null)
            {
                playerTransform.X = player?.x ?? 0;
                playerTransform.Y = player?.y ?? 0;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayBackgroundTiles(BackgroundImagePath);
            DrawMap();

            // Dynamically add the player at startup
            AddPlayerDynamically();
        }


        private void AddPlayerDynamically()
        {
            if (GameCanvas == null || GameCanvas.ActualWidth <= 0 || GameCanvas.ActualHeight <= 0)
            {
                Console.WriteLine("GameCanvas is not ready. Cannot add player.");
                return;
            }

            player = new Player(
                X: (int)(GameCanvas.ActualWidth / 2),
                Y: (int)(GameCanvas.ActualHeight / 2),
                Speed: 10,
                Width: 46,
                Height: 84
            );


            try
            {
                var playerImageSource = new BitmapImage();
                playerImageSource.BeginInit();
                playerImageSource.UriSource = new Uri("images/player_space.png", UriKind.Relative);
                playerImageSource.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                playerImageSource.EndInit();

                Image playerImage = new Image
                {
                    Source = playerImageSource,
                    Width = 46,
                    Height = 84,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                playerTransform = new TranslateTransform(player.x, player.y);
                playerImage.RenderTransform = playerTransform;

                GameCanvas.Children.Add(playerImage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Something fucked up at player stuff : {ex.Message}", "Duck", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Console.WriteLine($"Player added successfully. Position: X={player.x}, Y={player.y}");

        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Resize the canvases to match the window size
            GameCanvas.Width = ActualWidth;
            GameCanvas.Height = ActualHeight;
            MapCanvas.Width = ActualWidth;
            MapCanvas.Height = ActualHeight;

            // Clear the MapCanvas and redraw the map to reflect the new size
            MapCanvas.Children.Clear();
            DrawMap();

            // Scale the tiles to fit the new canvas size
            ScaleMapTiles();
        }

        private void DisplayBackgroundTiles(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;

            try
            {
                var backgroundImage = new Image
                {
                    Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)),
                    Stretch = Stretch.Fill
                };
                GameCanvas.Background = new ImageBrush(backgroundImage.Source);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load background image: {ex.Message}");
            }
        }

        private void DrawMap()
        {
            if (gameMap == null || gameMap.Layers == null || gameMap.Layers.Count == 0) return;

            int tilesetWidth = gameMap.TileSetImage.PixelWidth;
            int tilesetColumns = tilesetWidth / gameMap.TileWidth;

            foreach (var layer in gameMap.Layers)
            {
                for (int i = 0; i < layer.Length; i++)
                {
                    int tileIndex = layer[i];
                    int x = i % gameMap.Width; // Use the actual map width
                    int y = i / gameMap.Width; // Use the actual map height

                    if (tileIndex > 0) // Skip empty tiles
                    {
                        var tileImage = GetTileImage(tileIndex, tilesetColumns, gameMap.TileWidth, gameMap.TileHeight);
                        if (tileImage != null)
                        {
                            Canvas.SetLeft(tileImage, x * gameMap.TileWidth);
                            Canvas.SetTop(tileImage, y * gameMap.TileHeight);
                            MapCanvas.Children.Add(tileImage);
                        }
                        else
                        {
                            Console.WriteLine($"Failed to render tile at ({x}, {y}) with index {tileIndex}.");
                        }
                    }
                }
            }

            ScaleMapTiles(); // Ensure tiles are scaled correctly after drawing
        }

        private Image GetTileImage(int gid, int tilesetColumns, int tileWidth, int tileHeight)
        {
            int srcX = ((gid - 1) % tilesetColumns) * tileWidth;
            int srcY = ((gid - 1) / tilesetColumns) * tileHeight;

            try
            {
                CroppedBitmap croppedBitmap = new CroppedBitmap(gameMap.TileSetImage, new Int32Rect(srcX, srcY, tileWidth, tileHeight));
                return new Image
                {
                    Source = croppedBitmap,
                    Width = tileWidth,
                    Height = tileHeight
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cropping tile with GID {gid}: {ex.Message}");
                return null;
            }
        }

        private void ScaleMapTiles()
        {
            if (MapCanvas.ActualWidth == 0 || MapCanvas.ActualHeight == 0) return;

            double scaleFactor = Math.Min(
                GameCanvas.ActualWidth / MapCanvas.ActualWidth,
                GameCanvas.ActualHeight / MapCanvas.ActualHeight
            );

            foreach (UIElement tile in MapCanvas.Children)
            {
                if (tile is Image image)
                {
                    double originalWidth = image.Width;
                    double originalHeight = image.Height;

                    // Apply scaling
                    image.Width = originalWidth * scaleFactor;
                    image.Height = originalHeight * scaleFactor;

                    // Adjust position
                    double left = Canvas.GetLeft(image) * scaleFactor;
                    double top = Canvas.GetTop(image) * scaleFactor;

                    Canvas.SetLeft(image, left);
                    Canvas.SetTop(image, top);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Do you want to quit?\nYou will lose all your progress.",
                "Close",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Stop
            );

            if (result == MessageBoxResult.OK)
                Close();
        }
    }
}