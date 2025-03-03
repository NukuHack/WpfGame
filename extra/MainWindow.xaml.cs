using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.IO;

namespace color
{
    public class Palette
    {
        public Dictionary<int, int> _colorIndexMap = new Dictionary<int, int>();
        public List<Color> Colors { get; set; } = new List<Color>();

        public void AddColors(IEnumerable<Color> colors)
        {
            foreach (var color in colors)
            {
                int colorKey = GetColorKey(color);
                if (!_colorIndexMap.ContainsKey(colorKey))
                {
                    Colors.Add(color);
                    _colorIndexMap[colorKey] = Colors.Count - 1;
                }
            }
        }

        public Color GetColor(int index) => index >= 0 && index < Colors.Count ? Colors[index] : Colors[0];
        public int GetColorIndex(Color color) => _colorIndexMap.ContainsKey(GetColorKey(color)) ? _colorIndexMap[GetColorKey(color)] : 0;

        private int GetColorKey(Color color) => (color.R << 16) | (color.G << 8) | color.B;

        /// <summary>
        /// Returns a new list with the colors randomized.
        /// </summary>
        public List<Color> RandomizeColors()
        {
            if (Colors.Count <= 1) return new List<Color>(Colors); // Return a copy if there's one or no colors.

            var randomizedColors = ShuffleList(Colors); // Shuffle the colors.
            return randomizedColors; // Return the shuffled list without modifying the original.
        }

        /// <summary>
        /// Shuffles a list using the Fisher-Yates algorithm.
        /// </summary>
        private List<Color> ShuffleList(List<Color> list)
        {
            var randomizedList = new List<Color>(list); // Create a copy of the list.
            Random random = new Random();

            for (int i = randomizedList.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1); // Pick a random index from 0 to i.
                (randomizedList[i], randomizedList[j]) = (randomizedList[j], randomizedList[i]); // Swap elements.
            }

            return randomizedList;
        }
    }

    public partial class MainWindow : Window
    {
        private Palette _palette;
        private WriteableBitmap _originalBitmap;
        private WriteableBitmap _indexedBitmap;
        private WriteableBitmap _recoloredBitmap;

        public MainWindow()
        {
            InitializeComponent();

            Image1.Loaded += Image1_Loaded;
            Button.Click += Button_Click;

            _originalBitmap = LoadBitmap("big.png");
        }

        private void Image1_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Image1.Source = _originalBitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _palette = CreatePalette(_originalBitmap);
                _indexedBitmap = ConvertToIndexed(_originalBitmap, _palette);
                Palette reordered = new Palette();
                reordered._colorIndexMap = _palette._colorIndexMap; reordered.Colors = _palette.RandomizeColors();
                _recoloredBitmap = RecolorIndexedImage(_indexedBitmap, _palette, reordered);

                Image2.Source = _recoloredBitmap;
            }
            catch (OutOfMemoryException)
            {
                MessageBox.Show("The image is too large to process. Please try with a smaller image.", "Memory Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static WriteableBitmap LoadBitmap(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file '{filePath}' does not exist.");

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath, UriKind.Relative);
            bitmap.EndInit();

            return new WriteableBitmap(bitmap);
        }

        private static Palette CreatePalette(WriteableBitmap bitmap)
        {
            var uniqueColors = ExtractColors(bitmap).ToList();
            Palette palette = new Palette();
            palette.AddColors(uniqueColors);
            return palette;
        }

        private static IEnumerable<Color> ExtractColors(WriteableBitmap bitmap)
        {
            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var stride = width * 4;

            var pixels = new byte[height * stride];
            bitmap.CopyPixels(pixels, stride, 0);

            var uniqueColors = new HashSet<int>();

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    var offset = y * stride + x * 4;
                    var alpha = pixels[offset + 3]; // Alpha channel
                    if (alpha == 0) continue; // Skip fully transparent pixels

                    var colorKey = (pixels[offset + 2] << 16) | (pixels[offset + 1] << 8) | pixels[offset];
                    lock (uniqueColors)
                    {
                        uniqueColors.Add(colorKey);
                    }
                }
            });

            return uniqueColors.Select(key => Color.FromRgb((byte)(key >> 16), (byte)((key >> 8) & 0xFF), (byte)(key & 0xFF)));
        }
        private static WriteableBitmap ConvertToIndexed(WriteableBitmap originalBitmap, Palette palette)
        {
            var width = originalBitmap.PixelWidth;
            var height = originalBitmap.PixelHeight;
            var stride = width * 4;

            var originalPixels = new byte[height * stride];
            originalBitmap.CopyPixels(originalPixels, stride, 0);

            var indexedBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var indexedPixels = new byte[height * stride];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    var offset = y * stride + x * 4;
                    var alpha = originalPixels[offset + 3]; // Alpha channel

                    if (alpha == 0) // Fully transparent pixel
                    {
                        indexedPixels[offset + 0] = 0; // Blue
                        indexedPixels[offset + 1] = 0; // Green
                        indexedPixels[offset + 2] = 0; // Red
                        indexedPixels[offset + 3] = 0; // Alpha
                        continue;
                    }

                    var colorKey = (originalPixels[offset + 2] << 16) | (originalPixels[offset + 1] << 8) | originalPixels[offset];
                    var index = palette.GetColorIndex(Color.FromRgb((byte)(colorKey >> 16), (byte)((colorKey >> 8) & 0xFF), (byte)(colorKey & 0xFF)));

                    // Store the index in the red channel (or any unused channel)
                    indexedPixels[offset + 0] = 0; // Blue (unused)
                    indexedPixels[offset + 1] = 0; // Green (unused)
                    indexedPixels[offset + 2] = (byte)index; // Red (used as index)
                    indexedPixels[offset + 3] = alpha; // Preserve alpha
                }
            });

            indexedBitmap.WritePixels(new Int32Rect(0, 0, width, height), indexedPixels, stride, 0);
            return indexedBitmap;
        }

        private static WriteableBitmap RecolorIndexedImage(WriteableBitmap indexedBitmap, Palette oldPalette, Palette newPalette)
        {
            var width = indexedBitmap.PixelWidth;
            var height = indexedBitmap.PixelHeight;
            var stride = width * 4;

            var indexedPixels = new byte[height * stride];
            indexedBitmap.CopyPixels(indexedPixels, stride, 0);

            var recoloredBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var recoloredPixels = new byte[height * stride];

            // Precompute the color mapping
            var colorMapping = oldPalette.Colors.Select((color, index) => newPalette.GetColor(index)).ToArray();

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    var offset = y * stride + x * 4;
                    var alpha = indexedPixels[offset + 3]; // Alpha channel

                    if (alpha == 0) // Fully transparent pixel
                    {
                        Array.Clear(recoloredPixels, offset, 4);
                        continue;
                    }

                    var index = indexedPixels[offset + 2]; // Use red channel as the index
                    var newColor = colorMapping[index];

                    recoloredPixels[offset + 0] = newColor.B; // Blue
                    recoloredPixels[offset + 1] = newColor.G; // Green
                    recoloredPixels[offset + 2] = newColor.R; // Red
                    recoloredPixels[offset + 3] = alpha;      // Preserve alpha
                }
            });

            recoloredBitmap.WritePixels(new Int32Rect(0, 0, width, height), recoloredPixels, stride, 0);
            return recoloredBitmap;
        }
    }
}