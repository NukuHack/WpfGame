using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;



namespace pattyer
{
    public partial class MainWindow : Window
    {
        public Player player;
        public MatrixTransform playerTransform; // Use MatrixTransform instead of TransformGroup
        public Image playerImage;

        public double _gravity = 5;

        private bool isGamePaused = false;

        public MainWindow()
        {
            InitializeComponent();

            Console.WriteLine("Initializing game...");

            this.Loaded += MainWindow_Loded;
            this.SizeChanged += MainWindow_SizeChanged;
            this.KeyDown += MainWindow_KeyDown;
            this.KeyUp += MainWindow_KeyUp;
            CloseButton.Click += CloseButton_Click;
        }

        public void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawMap();
            Player_RePos();


            GameCanvas.Width = ActualWidth;
            GameCanvas.Height = ActualHeight;
            BackgroundCanvas.Width = ActualWidth;
            BackgroundCanvas.Height = ActualHeight;
            MapCanvas.Width = ActualWidth;
            MapCanvas.Height = ActualHeight;
        }

        public void MainWindow_Loded(object sender, RoutedEventArgs e)
        {
            LoadMap("maps/default.tmx");
            StartGameLoop();
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



        public void MenuOpen()
        {
            PauseGame();

            MessageBoxResult result = MessageBox.Show("The game is stopped\nClick 'OK' to continue.", "Menu",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            if (result == MessageBoxResult.Cancel)
                CloseAsk();

            ResumeGame();
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