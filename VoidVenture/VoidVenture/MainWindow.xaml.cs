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



namespace VoidVenture
{

    public partial class MainWindow : Window
    {
        public Random rnd = new Random();
        public Window mainwindow;

        private bool isGamePaused = false;
        public bool isMenuOpened = false;
        public bool doDebug = false;

        public MainWindow()
        {
            InitializeComponent();

            mainwindow = this;

            this.Loaded += (s,e) =>
            {
                Console.WriteLine("Initializing game...");
                Initialize();
            };

        }


        public void Initialize()
        {
            InitializeUI();

            if (!DOUseNoise)
            {
                LoadTileMap("maps/default.tmx");
            }
            else
            {
                InitializeNoiseMap();
            }

            // use underscores if not passed use s-e if they are actually passed
            this.SizeChanged += (_, __) => MainResizeFunction();
            this.KeyDown += (s,e) => MainKeyPressHandler(e.Key);
            this.KeyUp += (s,e) => MainKeyReleaseHandler(e.Key);

            CloseButton.Click += (_, __) => CloseButton_Click();
            MenuButton.Click += (_, __) => MenuButton_Click();
            SaveButton.Click += (_, __) => SaveButton_Click();

            closeOverlay.Click += (_, __) => CloseOverlay_Click();
            loadButton.Click += (_, __) => LoadButton_Click();
            deleteButton.Click += (_, __) => DeleteButton_Click();
            resaveButton.Click += (_, __) => ResaveButton_Click();
            loadExternalSave.Click += (_, __) => LoadExternalSave_Click();
            saveFileSelector.SelectionChanged += (_, __) => SaveFileSelector_SelectionChanged();

            StartGameLoop();
            TestingOnStart();

            this.Focusable = true;
            Focus();

        }

        public void MainResizeFunction()
        {
            if (!DOUseNoise)
            {
                RedrawTileMap();
            }
            else
            {
                RenderTerrain();
            }

            Player_RePos();

            InitializeUI();
        }

        public void InitializeUI()
        {

            // Initialize UI
            SaveOverlay.Visibility = Visibility.Collapsed;

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


            CenterOnCanvas(SaveMenu);
        }

        public void CenterOnCanvas(Grid grid)
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


        public void StartGameLoop()
        {
            AddPlayerDynamically("images/player_space.png");

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            timer.Tick += (s,e) =>
            {
                Timer_Tick();
            } ;
            timer.Start();
        }

        public void Timer_Tick()
        {
            if (isGamePaused) return;

            if (player != null)
            {
                player.UpdatePosition(collidableTiles); // Update player's position smoothly
                player.ApplyGravity(gravity);
            }

            UpdatePlayerPosition();
        }



        public void MainKeyPressHandler(Key e)
        {
            Direction direction = Direction.None;

            switch (e)
            {
                case Key.W: case Key.Up: 
                    direction = Direction.Up; break;
                case Key.S: case Key.Down: 
                    direction = Direction.Down; break;
                case Key.A: case Key.Left: 
                    direction = Direction.Left; break;
                case Key.D: case Key.Right: 
                    direction = Direction.Right; break;

                case Key.R: RegenMap(); break;
                case Key.T: ShowDebugInfo(); break;
                case Key.P: ReLoadImage(); break;
                case Key.F: DoDebug(); break;

                case Key.Space: Hover(true); break;

                case Key.Escape: TryMenuSwitch(); break;
            }


            if (direction != Direction.None)
            {
                player.UpdateRotation(direction);

                if (!Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    if (player != null)
                        player.SetTargetPosition(direction, GameCanvas.ActualWidth, GameCanvas.ActualHeight, collidableTiles);
                }
                else if (DOUseNoise)
                {
                    MoveTerrain(direction);
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
            if (place != "")
                MessageBox.Show($"{place}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }


        public void CloseAsk()
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
