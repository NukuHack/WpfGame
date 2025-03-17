using System;

using System.IO;
using System.Text;
using System.Linq;
//using System.Drawing;
using System.Xml.Linq;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics.Eventing.Reader;


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



namespace VoidVenture
{

    public partial class MainWindow : System.Windows.Window
    {
        public Random rnd = new();
        public Window mainwindow;
        public DispatcherTimer timer;

        private bool isGamePaused = false;
        public bool isMenuOpened = false;
        public bool inMouseDown = false;

        public int currentWidth, currentHeight;

        public MainWindow()
        {
            BeforeEverything();

            InitializeComponent();

            RightAfterBegining();

            DataContext = this; // IDK what this is ...

            mainwindow = this;

            this.Loaded += (s, e) =>
            {
                Console.WriteLine("Initializing game...");
                Initialize();
            };

        }



        public void Initialize()
        {
            InitializeUI();

            if (!DO.UseNoiseTerrain)
            {
                LoadTileMap("maps/default.tmx");
            }
            else
            {
                InitializeNoiseMap();
            }

            // use underscores if not passed use s-e if they are actually passed
            this.SizeChanged += (s, e) => MainResizeFunction(e);
            this.KeyDown += (s, e) => MainKeyPressHandler(e.Key);
            this.KeyUp += (s, e) => MainKeyReleaseHandler(e.Key);

            CloseButton.Click += (_, __) => CloseButton_Click();
            MenuButton.Click += (_, __) => MenuButton_Click();

            generalMenuButton.Click += (_, __) => generalMenuButton_Click();
            settingsMenuButton.Click += (_, __) => settingsMenuButton_Click();
            saveMenuButton.Click += (_, __) => saveMenuButton_Click();

            closeOverlay.Click += (_, __) => CloseOverlay_Click();

            loadButton.Click += (_, __) => LoadButton_Click();
            saveButton.Click += (_, __) => saveButton_Click();
            deleteButton.Click += (_, __) => DeleteButton_Click();
            resaveButton.Click += (_, __) => ResaveButton_Click();
            loadExternalSave.Click += (_, __) => LoadExternalSave_Click();
            saveFileSelector.SelectionChanged += (_, __) => SaveFileSelector_SelectionChanged();

            StartGameLoop();

            TestingOnStart();

            this.Focusable = true;
            Focus();

        }

        public void MainResizeFunction(SizeChangedEventArgs e)
        {
            currentWidth = (int)e.NewSize.Width;
            currentHeight = (int)e.NewSize.Height;

            if (!DO.UseNoiseTerrain)
            {
                RedrawTileMap();
            }
            else
            {
                RenderTerrain();
            }

            Player_RePos();

            ResetUI();
        }

        public void InitializeUI()
        {

            currentWidth = (int)ActualWidth;
            currentHeight = (int)ActualHeight;

            ResetUI();
        }

        public void ResetUI()
        {
            // Update the canvas sizes to match the actual window size
            GameCanvas.Width = ActualWidth;
            GameCanvas.Height = ActualHeight;

            BackgroundCanvas.Width = ActualWidth;
            BackgroundCanvas.Height = ActualHeight;

            MapCanvas.Width = ActualWidth;
            MapCanvas.Height = ActualHeight;

            //DebugOverlay.Width = ActualWidth;
            //DebugOverlay.Height = ActualHeight;

            //SaveOverlay.Width = ActualWidth;
            //SaveOverlay.Height = ActualHeight;

            CenterOnCanvas(MenuGeneral);
            CenterOnCanvas(MenuSettings);
            CenterOnCanvas(MenuSave);
        }

        public void CenterOnCanvas(Border grid)
        {
            Canvas.SetTop(grid, (ActualHeight - grid.Height) / 2);
            Canvas.SetLeft(grid, (ActualWidth - grid.Width) / 2);
        }

        public void TestingOnStart()
        {

            // does work but it makes the image kinda crappy
            //Cursor = CreateCursorFromBitmap(RecolorImage("Phlame_Arrow.cur"), 0, 0);
            //Cursor = new Cursor(@"C:\Users\nukuh\Desktop\c\app\VoidVenture\VoidVenture\bin\Debug\Phlame_Arrow.cur");

            // should work but did not test it 
            //var z = RecolorImage("smile.ico");

            // not works - too old file format I don't wanna research it more
            //var y = RecolorImage("Busy.ani");
        }


        public async void StartGameLoop()
        {
            if (DO.UseNoiseTerrain)
                // Wait for map initialization
                while (columnHeights == null)
                {
                    await Task.Delay(16); // Check every 16ms (60 / sec)
                }

            AddPlayerDynamically(VoidVenture.Properties.PlayerResource.Player_space);

            var timer = new DispatcherTimer // ~60 FPS
            { Interval = TimeSpan.FromMilliseconds(16) };

            timer.Tick += (s, e) => Timer_Tick();
            timer.Start();
        }

        public void Timer_Tick()
        {
            if (isGamePaused) return;

            if (player != null)
            {
                player.Update(gravity, collidableTiles, columnHeights); // Update player's position smoothly
                //player.ApplyGravity(gravity);
            }
        }

        public BitmapImage ConvertByteArrayToBitmapImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                throw new ArgumentException("Invalid image data.");

            var bitmapImage = new BitmapImage();
            using (var stream = new MemoryStream(imageData))
            {
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensure the stream is fully loaded
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
            }
            bitmapImage.Freeze(); // Optional: Makes the image cross-thread accessible
            return bitmapImage;
        }


        private string GetImageExtension(byte[] imageData)
        {
            // Ensure the data is long enough to contain a valid header
            if (imageData.Length < 12)
                throw new ArgumentException("Invalid image data.");

            // Check for PNG format
            else if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
                return ".png";

            // Check for ICO format
            else if (imageData[0] == 0x00 && imageData[1] == 0x00 && imageData[2] == 0x01 && imageData[3] == 0x00)
                return ".ico";

            // Check for CUR format
            else if (imageData[0] == 0x00 && imageData[1] == 0x00 && imageData[2] == 0x02 && imageData[3] == 0x00)
                return ".cur";

            // Check for JPEG format
            else if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
                return ".jpg";

            // Check for GIF format
            else if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
                return ".gif";

            // Check for BMP format
            else if (imageData[0] == 0x42 && imageData[1] == 0x4D)
                return ".bmp";

            // Check for TIFF format (little-endian)
            else if (imageData[0] == 0x49 && imageData[1] == 0x49 && imageData[2] == 0x2A && imageData[3] == 0x00)
                return ".tiff";

            // Check for TIFF format (big-endian)
            else if (imageData[0] == 0x4D && imageData[1] == 0x4D && imageData[2] == 0x00 && imageData[3] == 0x2A)
                return ".tiff";

            // Check for WebP format
            else if (imageData[0] == 0x52 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x46 &&
                imageData[8] == 0x57 && imageData[9] == 0x45 && imageData[10] == 0x42 && imageData[11] == 0x50)
                return ".webp";

            // Unsupported format
            else
                throw new ArgumentException("Unsupported image format.");
        }



        public void MainKeyPressHandler(Key e)
        {
            Direction direction = Direction.None;

            switch (e)
            {
                case Key.W:
                case Key.Up:
                    direction = Direction.Up; break;
                case Key.S:
                case Key.Down:
                    direction = Direction.Down; break;
                case Key.A:
                case Key.Left:
                    direction = Direction.Left; break;
                case Key.D:
                case Key.Right:
                    direction = Direction.Right; break;

                case Key.R: RegenMap(); break;
                case Key.T: ShowDebugInfo(); break;
                case Key.P: ReLoadImage(); break;
                case Key.F: DoDebug(); break;

                case Key.J: PauseGame(); break;
                case Key.K: ResumeGame(); break;

                case Key.Space: Hover(true); break;

                case Key.Escape: TryMenuSwitch(null); break;
            }

            if (isGamePaused) return;

            if (direction != Direction.None)
            {
                if (!Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    if (player != null)
                        player.SetMovementDirection(direction);
                }
                else if (DO.UseNoiseTerrain)
                {
                    if (DO.Debug)
                        MoveTerrain(direction);
                    else
                        ShowMessage("First you have to enable debug mode for that (F)");
                }
            }
        }

        public void MainKeyReleaseHandler(Key e)
        {
            switch (e)
            {
                case Key.Space: Hover(false); break;
            }
        }


        public void CloseButton_Click()
        {
            PauseGame();

            CloseAsk();

            ResumeGame();
        }

        public void PauseGame()
        {
            isGamePaused = true;
        }

        public void ResumeGame()
        {
            isGamePaused = false;
        }

        public void ErrorMessage(Exception ex, string place = "")
        {
            PauseGame();
            if (place != "")
                MessageBox.Show($"{place}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private void ShowMessage(string message, string place = "")
        {
            PauseGame();
            if (place != "")
                MessageBox.Show(message);
            else
                MessageBox.Show(message, place);
        }


        public void CloseAsk()
        {
            MessageBoxResult result = MessageBox.Show(
                "Do you want to quit?\nYou will lose all your unsaved progress.",
                "Close",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Stop
            );
            if (result == MessageBoxResult.OK)
                Close();
        }

    }
}