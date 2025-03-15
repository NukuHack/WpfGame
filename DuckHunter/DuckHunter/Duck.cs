using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Threading;

namespace DuckHunter
{
    public class Duck
    {
        public readonly MainWindow window;
        public Rectangle duckRectangle;
        public string Status { get; set; }
        public int currentFrame = 1;
        public int countFrameHelp = 0;
        public double x { get; set; }
        public double y { get; set; }
        public readonly int Width;
        public readonly int Height;
        public double speedX;  // X irányú sebesség
        public double speedY;  // Y irányú sebesség
        public int Value;

        public Duck(MainWindow mainWindow)
        {
            this.Status = "fly";
            this.window = mainWindow;
            var rnd = window.rnd;

            // Kezdeti pozíció beállítása
            this.x = rnd.Next(10, 10);
            this.y = rnd.Next(10, (int)(this.window.MainCanvas.ActualHeight) - 150);

            // set the size
            this.Width = 150;
            this.Height = 120;

            // Véletlenszerű mozgásirány
            // the problem is : if the difficulty is changed mid game they will still move with the stuff they spawned
            this.speedX = rnd.Next(8, 15)*0.7;
            this.speedY = rnd.Next(2, 5) * (rnd.Next(0, 2)-1)*0.7;

            this.Value = 1;

            // Betöltjük a kacsát
            UpdateDuckImage();

            if (this.window.MainCanvas != null && this.duckRectangle != null)
            {
                Canvas.SetLeft(this.duckRectangle, this.x);
                Canvas.SetTop(this.duckRectangle, this.y);
                this.window.MainCanvas.Children.Add(this.duckRectangle);
            }
            else
            {
                MessageBox.Show("GameCanvas not found in MainWindow or duckRectangle is null.");
            }

        }

        public void MoveDuck()
        {
            this.x += speedX*this.window.difficulty;
            this.y += speedY*this.window.difficulty;

            // Ha eléri a képernyő szélét, eltűnik
            if (this.x <= 0 || this.x + this.duckRectangle.Width/2 >= this.window.MainCanvas.ActualWidth)
            {
                window.GameEnded();
                return;  // Megállítjuk a metódust, mivel a kacsa eltűnt
            }

            if(this.y <= 0 || this.y + this.duckRectangle.Height >= this.window.MainCanvas.ActualHeight)
            {
                if(this.Status == "fly")
                    this.speedY *= -1;
                else if (this.Status == "fainted")
                {
                    // this might need  some extra but for now it's good
                    this.window.MainCanvas.Children.Remove(this.duckRectangle);
                    this.window.ducks.Remove(this);
                    // yeah this should be a bit later ... now theyy dissapear a bit too quickly

                    return;  // Megállítjuk a metódust, mivel a kacsa eltűnt
                }
            }


            // Ha nem érte el a szélét, frissítjük a pozíciót
            Canvas.SetLeft(this.duckRectangle, this.x);
            Canvas.SetTop(this.duckRectangle, this.y);

            this.countFrameHelp ++;

            // this is only for the ducks to dissapear quickly
            if (this.Status == "fainted")
            {
                if (this.countFrameHelp %5==0)
                {
                    UpdateDuck();
                    if (this.countFrameHelp == 10)
                        this.countFrameHelp = 0;
                }
            }
            else if (this.Status == "fly")
            {
                if (this.countFrameHelp %2==0)
                {
                    UpdateDuck();
                    if (this.countFrameHelp == 10)
                        this.countFrameHelp = 0;
                }
            }
        }


        public void UpdateDuck()
        {
            int frameCount = this.window.imageCache[Status].Length;
            this.currentFrame = (this.currentFrame % frameCount) + 1;
            UpdateDuckImage();
        }

        private void UpdateDuckImage()
        {
            MainWindow mainWindow = this.window;
            if (mainWindow.imageCache.ContainsKey(this.Status))
            {
                int frameIndex = this.currentFrame - 1;
                BitmapImage bitmap = mainWindow.imageCache[this.Status][frameIndex];

                if (this.duckRectangle == null)
                {
                    this.duckRectangle = CreateRectangleFromBitmap(bitmap, this.Width, this.Height);
                }
                else
                {
                    if (this.duckRectangle.Fill is ImageBrush imageBrush)
                    {
                        imageBrush.ImageSource = bitmap;
                    }
                    else
                    {
                        this.duckRectangle.Fill = new ImageBrush { ImageSource = bitmap };
                    }
                }
            }
            else
            {
                MessageBox.Show($"No frames found for status: {this.Status}");
            }
        }

        private Rectangle CreateRectangleFromBitmap(BitmapImage bitmap, int width, int height)
        {
            Rectangle rectangle = new()
            {
                Width = width,
                Height = height,
                Fill = new ImageBrush { ImageSource = bitmap }
            };
            return rectangle;
        }
    }
}
