using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DuckHunter
{
    public class ImgLoader
    {
        public void LoadImage(Window window, string imagePath, bool background, int width, int height, int x, int y)
        {
            if (background)
            {
                // Létrehozunk egy ImageBrush-t
                ImageBrush imageBrush = new()
                {
                    // A megadott elérési úton betöltjük a képet
                    ImageSource = new BitmapImage(new Uri(imagePath, UriKind.Relative))
                };

                // Alkalmazzuk a háttérre
                window.Background = imageBrush;
            }
            else
            {
                // Ha nem háttér, akkor egy Rectangle-t hozunk létre
                var rectangle = new Rectangle
                {
                    Width = width,  // Méret beállítása
                    Height = height  // Méret beállítása
                };

                // Kép betöltése ImageBrush segítségével
                var imageBrush = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(imagePath, UriKind.Relative))
                };

                // A képet a Rectangle háttereként alkalmazzuk
                rectangle.Fill = imageBrush;

                // Az ablakhoz tartozó Canvas megtalálása
                if (window.FindName("MainCanvas") is Canvas canvas)
                {
                    // A Rectangle pozicionálása a kívánt x és y koordinátákra
                    Canvas.SetLeft(rectangle, x);
                    Canvas.SetTop(rectangle, y);

                    // A képet a Canvas-hoz adjuk, anélkül hogy törölnénk az előzőket
                    canvas.Children.Add(rectangle); // A rectangle hozzáadása
                }
                else
                {
                    MessageBox.Show("Canvas nem található az ablakban!");
                }
            }
        }
        public void LoadImageBitmap(Window window, BitmapImage bitmap, int width, int height, int x, int y)
        {
                // Create a Rectangle to display the image
                var rectangle = new Rectangle
                {
                    Width = width,  // Set the width
                    Height = height  // Set the height
                };

                // Create an ImageBrush and set its ImageSource to the provided BitmapImage
                var imageBrush = new ImageBrush
                {
                    ImageSource = bitmap
                };

                // Apply the brush to the rectangle's Fill property
                rectangle.Fill = imageBrush;

            // Find the Canvas in the window by its name
            if (window.FindName("MainCanvas") is Canvas canvas)
            {
                // Position the rectangle on the canvas
                Canvas.SetLeft(rectangle, x);
                Canvas.SetTop(rectangle, y);

                // Add the rectangle to the canvas without removing previous elements
                canvas.Children.Add(rectangle);
            }
            else
            {
                MessageBox.Show("Canvas not found in the window!");
            }
        }
    }
}
