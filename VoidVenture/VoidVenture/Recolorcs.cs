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


namespace VoidVenture
{
    public class Palette
    {
        public Dictionary<int, int> colorIndexMap = new Dictionary<int, int>();
        public List<Color> Colors { get; set; } = new List<Color>();

        public void AddColors(IEnumerable<Color> colors)
        {
            foreach (var color in colors)
            {
                int colorKey = GetColorKey(color);
                if (!colorIndexMap.ContainsKey(colorKey))
                {
                    Colors.Add(color);
                    colorIndexMap[colorKey] = Colors.Count - 1;
                }
            }
        }

        public Color GetColor(int index) => index >= 0 && index < Colors.Count ? Colors[index] : Colors[0];
        public int GetColorIndex(Color color) => colorIndexMap.ContainsKey(GetColorKey(color)) ? colorIndexMap[GetColorKey(color)] : 0;

        private int GetColorKey(Color color) => (color.R << 16) | (color.G << 8) | color.B;

        public List<Color> RandomizeColors()
        {
            if (Colors.Count <= 1) return new List<Color>(Colors);
            return ShuffleList(Colors);
        }

        private List<Color> ShuffleList(List<Color> list)
        {
            Random random = new Random();
            var randomizedList = new List<Color>(list);
            for (int i = randomizedList.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (randomizedList[i], randomizedList[j]) = (randomizedList[j], randomizedList[i]);
            }
            return randomizedList;
        }

        // New static method to create a randomized Palette
        public static Palette CreateRandomized(Palette original)
        {
            var randomizedPalette = new Palette();
            randomizedPalette.AddColors(original.RandomizeColors());
            randomizedPalette.colorIndexMap = original.colorIndexMap;
            return randomizedPalette;
        }
    }

    public partial class MainWindow : Window
    {
        public Palette _palette;
        public Palette _randomizedPlette;
        public WriteableBitmap _originalBitmap;
        public WriteableBitmap _indexedBitmap;
        public WriteableBitmap _recoloredBitmap;

        public string imgSource;

        public WriteableBitmap RecolorImage(string imgSourceRaw)
        {
            try
            {
                imgSource = imgSourceRaw;
                _originalBitmap = LoadBitmap(imgSource);
                _palette = CreatePalette(_originalBitmap);
                if (_palette.Colors.Count > 256)
                {
                    throw new Exception("Image has too many colors (>256). Reduce colors to 256 or less.");
                }
                _indexedBitmap = ConvertToIndexed(_originalBitmap, _palette);
                _randomizedPlette = Palette.CreateRandomized(_palette);
                _recoloredBitmap = RecolorIndexedImage(_indexedBitmap, _palette, _randomizedPlette);
                // Ensure the final recolored bitmap is valid
                if (_recoloredBitmap?.PixelWidth == 0 || _recoloredBitmap?.PixelHeight == 0)
                    throw new Exception("Recolored image has invalid dimensions.");
                return _recoloredBitmap;
            }
            catch (OutOfMemoryException)
            {
                // Use Dispatcher.Invoke to show error on UI thread
                Dispatcher.Invoke(() => MessageBox.Show("The image is too large to process. Please try with a smaller image.", "Memory Error", MessageBoxButton.OK, MessageBoxImage.Error));
                return null;
            }
            catch (Exception ex)
            {

                // Use Dispatcher.Invoke to show error on UI thread
                Dispatcher.Invoke(() => ErrorMessage(ex, "Failed to recolor image"));
                return null;
            }
        }

        public WriteableBitmap LoadBitmap(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file '{filePath}' does not exist.");

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath, UriKind.RelativeOrAbsolute);
            bitmap.EndInit();

            if (bitmap.Format == PixelFormats.Indexed8)
                return Convert8BitToBGRA(bitmap);
            else if (bitmap.Format == PixelFormats.Bgra32)
                return new WriteableBitmap(bitmap);
            else
            {
                MessageBox.Show($"Image format not supported: {bitmap.Format}", "Image Recolor Error");
                throw new ArgumentException($"Image to load {imgSource} is not formatted to my liking.");
                // if you are here it means the img you want to load is not and old 8-bit image nd not an usual 32-bit image
                // in that case write your own palette extracting function, cu's I'm lazy and I don't have that kind of files
            }
        }

        public Palette CreatePalette(WriteableBitmap bitmap)
        {
            Palette palette = new Palette();

            // Extract colors from RGB pixels (for non-indexed images)
            var uniqueColors = ExtractColors(bitmap).ToList();
            palette.AddColors(uniqueColors);

            return palette;
        }



        public static IEnumerable<Color> ExtractColors(WriteableBitmap bitmap)
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
                    var a = pixels[offset + 3]; // Alpha
                    var r = pixels[offset + 2]; // Red
                    var g = pixels[offset + 1]; // Green
                    var b = pixels[offset];     // Blue

                    var colorKey = (a << 24) | (r << 16) | (g << 8) | b;
                    lock (uniqueColors) uniqueColors.Add(colorKey);
                }
            });

            return uniqueColors.Select(key => Color.FromArgb(
                (byte)(key >> 24),
                (byte)((key >> 16) & 0xFF),
                (byte)((key >> 8) & 0xFF),
                (byte)(key & 0xFF)
            ));
        }


        public static WriteableBitmap Convert8BitToBGRA(BitmapSource indexedImage)
        {
            var palette = indexedImage.Palette.Colors.ToList();
            if (palette == null || palette.Count == 0)
                throw new InvalidOperationException("Missing or invalid palette.");

            int width = (int)indexedImage.PixelWidth;
            int height = (int)indexedImage.PixelHeight;

            // Read the pixel indices from the input image
            byte[] pixels = new byte[width * height];
            indexedImage.CopyPixels(pixels, width, 0);

            // Create the output WriteableBitmap (BGRA32 format)
            var output = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            output.Lock();

            IntPtr backBuffer = output.BackBuffer;
            int stride = output.BackBufferStride;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Get the palette index for the current pixel
                    byte paletteIndex = pixels[y * width + x];
                    if (paletteIndex >= palette.Count)
                        throw new IndexOutOfRangeException($"Invalid palette index {paletteIndex} at position ({x}, {y}).");

                    // Get the color from the palette
                    Color color = palette[paletteIndex];

                    // Calculate the offset in the output buffer
                    int offset = y * stride + x * 4;

                    // Write the BGRA components to the buffer
                    Marshal.WriteByte(backBuffer, offset + 0, color.B); // Blue
                    Marshal.WriteByte(backBuffer, offset + 1, color.G); // Green
                    Marshal.WriteByte(backBuffer, offset + 2, color.R); // Red
                    Marshal.WriteByte(backBuffer, offset + 3, color.A); // Alpha
                }
            }

            output.AddDirtyRect(new Int32Rect(0, 0, width, height));
            output.Unlock();

            return output;
        }




        public WriteableBitmap ConvertToIndexed(WriteableBitmap originalBitmap, Palette palette)
        {
            var width = originalBitmap.PixelWidth;
            var height = originalBitmap.PixelHeight;
            var stride = width * 4;

            var originalPixels = new byte[height * stride];
            originalBitmap.CopyPixels(originalPixels, stride, 0);

            var indexedPixels = new byte[height * stride];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    var offset = y * stride + x * 4;
                    var a = originalPixels[offset + 3];
                    var r = originalPixels[offset + 2];
                    var g = originalPixels[offset + 1];
                    var b = originalPixels[offset];

                    var colorKey = (a << 24) | (r << 16) | (g << 8) | b;
                    var color = Color.FromArgb(a, r, g, b);
                    var index = palette.GetColorIndex(color);

                    indexedPixels[offset + 0] = 0; // Unused
                    indexedPixels[offset + 1] = 0; // Unused
                    indexedPixels[offset + 2] = (byte)index; // Store index in red channel
                    indexedPixels[offset + 3] = a; // Preserve alpha
                }
            });

            var indexedBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            indexedBitmap.WritePixels(new Int32Rect(0, 0, width, height), indexedPixels, stride, 0);
            return indexedBitmap;
        }

        public static WriteableBitmap RecolorIndexedImage(WriteableBitmap indexedBitmap, Palette oldPalette, Palette newPalette)
        {
            var width = indexedBitmap.PixelWidth;
            var height = indexedBitmap.PixelHeight;
            var stride = width * 4; // Bgra32 has 4 bytes per pixel

            var indexedPixels = new byte[height * stride];
            indexedBitmap.CopyPixels(indexedPixels, stride, 0);

            var recoloredBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var recoloredPixels = new byte[height * stride];

            // Precompute color mapping (old index → new color)
            var colorMapping = oldPalette.Colors.Select((color, index) => newPalette.GetColor(index)).ToArray();

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    var offset = y * stride + x * 4;
                    var alpha = indexedPixels[offset + 3]; // Alpha channel

                    if (alpha == 0)
                    {
                        Array.Clear(recoloredPixels, offset, 4);
                        continue;
                    }

                    var index = indexedPixels[offset + 2]; // Red channel holds index

                    // Validate index against old palette size
                    if (index >= oldPalette.Colors.Count)
                    {
                        // Fallback to first color if index is invalid
                        var newColorFallback = newPalette.GetColor(0);
                        recoloredPixels[offset + 0] = newColorFallback.B;
                        recoloredPixels[offset + 1] = newColorFallback.G;
                        recoloredPixels[offset + 2] = newColorFallback.R;
                        recoloredPixels[offset + 3] = alpha;
                        continue;
                    }

                    var newColor = colorMapping[index]; // Safe now

                    recoloredPixels[offset + 0] = newColor.B;
                    recoloredPixels[offset + 1] = newColor.G;
                    recoloredPixels[offset + 2] = newColor.R;
                    recoloredPixels[offset + 3] = alpha;
                }
            });

            recoloredBitmap.WritePixels(new Int32Rect(0, 0, width, height), recoloredPixels, stride, 0);
            return recoloredBitmap;
        }
    }
}
