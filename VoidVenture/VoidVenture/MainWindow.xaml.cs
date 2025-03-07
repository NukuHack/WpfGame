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
using Microsoft.Win32.SafeHandles;
using System.Windows.Interop;



namespace VoidVenture
{

    public partial class MainWindow : Window
    {
        public Random rnd = new Random();
        public Window mainwindow;

        private bool isGamePaused = false;

        public MainWindow()
        {
            InitializeComponent();

            Console.WriteLine("Initializing game...");

            mainwindow = this;

            // Initialize UI
            saveFileOverlay.Visibility = Visibility.Collapsed;

            this.Loaded += MainWindow_Loded;

        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveData();
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            MenuOpen();
        }

        public void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {


            UpdateViewportSize();


            Player_RePos();

            // Update the canvas sizes to match the actual window size
            GameCanvas.Width = ActualWidth;
            GameCanvas.Height = ActualHeight;
            BackgroundCanvas.Width = ActualWidth;
            BackgroundCanvas.Height = ActualHeight;
            MapCanvas.Width = ActualWidth;
            MapCanvas.Height = ActualHeight;
            Canvas.SetTop(SaveMenu, (ActualHeight - SaveMenu.Height) / 2);
            Canvas.SetLeft(SaveMenu, (ActualWidth - SaveMenu.Width) / 2);
        }

        public void MainWindow_Loded(object sender, RoutedEventArgs e)
        {
            LoadMap("maps/default.tmx");

            // does work but it makes the image kinda crappy
            //Cursor = CreateCursorFromBitmap(RecolorImage("Phlame_Arrow.cur"), 0, 0);
            //Cursor = new Cursor(@"C:\Users\nukuh\Desktop\c\app\VoidVenture\VoidVenture\bin\Debug\Phlame_Arrow.cur");

            // not works - too old file format I don't wanna research it more
            //var y = RecolorImage("Busy.ani");

            // should work but did not test it 
            //var z = RecolorImage("smile.cur");



            this.SizeChanged += MainWindow_SizeChanged;
            this.KeyDown += MainWindow_KeyDown;
            this.KeyUp += MainWindow_KeyUp;

            CloseButton.Click += CloseButton_Click;
            MenuButton.Click += MenuButton_Click;
            SaveButton.Click += SaveButton_Click;

            closeOverlay.Click += CloseOverlay_Click;
            loadButton.Click += LoadButton_Click;
            deleteButton.Click += DeleteButton_Click;
            resaveButton.Click += ResaveButton_Click;
            loadExternalSave.Click += LoadExternalSave_Click;
            saveFileSelector.SelectionChanged += SaveFileSelector_SelectionChanged;

            StartGameLoop();

        }

        public void StartGameLoop()
        {
            AddPlayerDynamically("images/player_space.png");

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public void Timer_Tick(object sender, EventArgs e)
        {
            if (isGamePaused) return;

            if (player != null)
            {
                player.UpdatePosition(collidableTiles); // Update player's position smoothly
                player.ApplyGravity(gravity);
            }

            UpdatePlayerPosition();
        }



        public void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            Direction direction = Direction.None;

            switch (e.Key)
            {
                case Key.W: direction = Direction.Up; break;
                case Key.S: direction = Direction.Down; break;
                case Key.A: direction = Direction.Left; break;
                case Key.D: direction = Direction.Right; break;

                case Key.Up: direction = Direction.Up; break;
                case Key.Down: direction = Direction.Down; break;
                case Key.Left: direction = Direction.Left; break;
                case Key.Right: direction = Direction.Right; break;

                case Key.Space: Hover(true); break;

                case Key.Escape: TryMenuSwitch(); break;
            }


            if (direction != Direction.None && player != null)
            {
                player.SetTargetPosition(direction, GameCanvas.ActualWidth, GameCanvas.ActualHeight, collidableTiles);
            }
        }

        public void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space: Hover(false); break;
            }
        }


        public void CloseButton_Click(object sender, RoutedEventArgs e)
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