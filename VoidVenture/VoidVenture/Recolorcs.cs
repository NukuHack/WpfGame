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

    public class LastColored
    {
        public string imgSource;
        public Palette _palette;
        public Palette _randomizedPlette;
        public WriteableBitmap _originalBitmap;
        public WriteableBitmap _indexedBitmap;
        public WriteableBitmap _recoloredBitmap;
    }

    public partial class MainWindow : Window
    {
        public LastColored tocolor = new LastColored();

        public WriteableBitmap RecolorImage(string imgSourceRaw)
        {
            try
            {
                tocolor.imgSource = imgSourceRaw;
                tocolor._originalBitmap = LoadBitmap(tocolor.imgSource);
                tocolor._palette = CreatePalette(tocolor._originalBitmap);
                if (tocolor._palette.Colors.Count > 256)
                {
                    throw new Exception("Image has too many colors (>256). Reduce colors to 256 or less.");
                }
                tocolor._indexedBitmap = ConvertToIndexed(tocolor._originalBitmap, tocolor._palette);
                tocolor._randomizedPlette = Palette.CreateRandomized(tocolor._palette);
                tocolor._recoloredBitmap = RecolorIndexedImage(tocolor._indexedBitmap, tocolor._palette, tocolor._randomizedPlette);
                // Ensure the final recolored bitmap is valid
                if (tocolor._recoloredBitmap?.PixelWidth == 0 || tocolor._recoloredBitmap?.PixelHeight == 0)
                    throw new Exception("Recolored image has invalid dimensions.");
                return tocolor._recoloredBitmap;
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




        public WriteableBitmap LoadBitmap(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file '{filePath}' does not exist.");

            BitmapSource bitmapSource;
            var uri_stuff = new Uri(filePath, UriKind.RelativeOrAbsolute);

            var extension = Path.GetExtension(filePath);

            if (extension is ".ico"||extension is ".cur")
            {
                var decoder = new IconBitmapDecoder(
                    uri_stuff,
                    BitmapCreateOptions.None,
                    BitmapCacheOption.None);

                // Get the first frame (icons can have multiple sizes)
                bitmapSource = decoder.Frames[0];
            }
            /*
            else if (extension == ".ani")
            {
                return LoadAniFrame(filePath);
            }
            */
            else if (extension is ".png")
            {
                // Fallback to BitmapImage for other formats
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = uri_stuff;
                bitmapImage.EndInit();
                bitmapSource = bitmapImage;
            }
            else
            {
                MessageBox.Show($"Image extension not supported: '{Path.GetExtension(filePath)}'", "Image Recolor Error");
                throw new ArgumentException($"Image to load '{tocolor.imgSource}' is not formatted to my liking.");
                // if you are here it means the img you want to load is not and old 8-bit image and not an usual 32-bit image
                // now it should support .png and .ico and .cur
                // in that case write your own palette extracting function, cu's I'm lazy and I don't have that kind of files
            }

            if (bitmapSource.Format == PixelFormats.Indexed8)
                return Convert8BitToBGRA(bitmapSource);
            else if (bitmapSource.Format == PixelFormats.Bgra32)
                return new WriteableBitmap(bitmapSource);
            else
            {
                MessageBox.Show($"Image format not supported: '{bitmapSource.Format}'", "Image Recolor Error");
                throw new ArgumentException($"Image to load '{tocolor.imgSource}' is not formatted to my liking.");
                // if you are here it means the img you want to load is not and old 8-bit image and not an usual 32-bit image
                // now it should support .png and .ico and .cur
                // in that case write your own palette extracting function, cu's I'm lazy and I don't have that kind of files
            }
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



        public static Cursor CreateCursorFromBitmap(BitmapSource bitmap, int xHotSpot, int yHotSpot)
        {
            var convertedBitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream, Encoding.Default, true))
            {
                // 1. Write CUR Header (6 bytes)
                writer.Write((short)0);     // Reserved (must be 0)
                writer.Write((short)2);     // Type: 2 = cursor
                writer.Write((short)1);     // Number of images

                // 2. Write Directory Entry (16 bytes)
                writer.Write((byte)(convertedBitmap.PixelWidth > 255 ? 0 : convertedBitmap.PixelWidth));
                writer.Write((byte)(convertedBitmap.PixelHeight > 255 ? 0 : convertedBitmap.PixelHeight));
                writer.Write((byte)0);      // Color count (0 = 32bpp)
                writer.Write((byte)0);      // Reserved
                writer.Write((ushort)xHotSpot);  // Hotspot X
                writer.Write((ushort)yHotSpot);  // Hotspot Y
                writer.Write(GetImageDataSize(convertedBitmap)); // Image data size
                writer.Write(22);                // Data offset (header + entry = 22)

                // 3. Write BITMAPINFOHEADER (40 bytes)
                int width = convertedBitmap.PixelWidth;
                int height = convertedBitmap.PixelHeight;
                writer.Write(40);               // biSize (header size)
                writer.Write(width);            // biWidth
                writer.Write(height);           // biHeight (XOR data only)
                writer.Write((short)1);         // biPlanes (must be 1)
                writer.Write((short)32);        // biBitCount (32bpp)
                writer.Write(0);                // biCompression (BI_RGB)
                writer.Write(0);                // biSizeImage (uncompressed)
                writer.Write(0);                // biXPelsPerMeter
                writer.Write(0);                // biYPelsPerMeter
                writer.Write(0);                // biClrUsed
                writer.Write(0);                // biClrImportant

                // 4. Write XOR (Color) Data (BGRA32 format, bottom-up row order)
                int stride = width * 4;
                var pixelData = new byte[stride * height];

                // Copy rows in reverse order (bottom-up)
                for (int row = 0; row < height; row++)
                {
                    convertedBitmap.CopyPixels(
                        new Int32Rect(0, height - 1 - row, width, 1),
                        pixelData,
                        stride,
                        row * stride);
                }
                writer.Write(pixelData);

                // 5. Write AND (Mask) Data (1bpp monochrome, bottom-up row order)
                int maskStride = (width + 31) / 32 * 4; // DWORD alignment
                var maskData = new byte[maskStride * height];
                writer.Write(maskData);
            }

            stream.Position = 0;
            // quick file save - used for testing
            //File.WriteAllBytes("test.cur", stream.ToArray());
            return new Cursor(stream);
        }

        // help for .cur re-converting
        public static int GetImageDataSize(BitmapSource bitmap)
        {
            int headerSize = 40; // BITMAPINFOHEADER size - took me atleast two hours to figure it out it was missing ...
            int xorSize = bitmap.PixelWidth * bitmap.PixelHeight * 4;
            int maskSize = ((bitmap.PixelWidth + 31) / 32 * 4) * bitmap.PixelHeight;
            return headerSize + xorSize + maskSize;
        }
        /*
        private static WriteableBitmap LoadAniFrame(string filePath, int frameIndex = 0)
        {
            try
            {
                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var reader = new BinaryReader(stream);

                // Read RIFF header
                var riffHeader = reader.ReadBytes(12);
                if (!Encoding.ASCII.GetString(riffHeader, 0, 4).Equals("RIFF") ||
                    !Encoding.ASCII.GetString(riffHeader, 8, 4).Equals("ACON"))
                    throw new NotSupportedException("Not a valid ANI file.");

                // Find ANI header ('anih')
                ANIHeader? anih = null;
                while (stream.Position < stream.Length)
                {
                    var chunkHeader = reader.ReadBytes(8);
                    var chunkSize = BitConverter.ToInt32(chunkHeader, 4);
                    var chunkType = Encoding.ASCII.GetString(chunkHeader, 0, 4);

                    if (chunkType == "anih" && chunkSize >= Marshal.SizeOf<ANIHeader>())
                    {
                        var anihBytes = reader.ReadBytes(Marshal.SizeOf<ANIHeader>());
                        anih = ByteArrayToStructure<ANIHeader>(anihBytes);
                        break;
                    }
                    else
                    {
                        stream.Position += chunkSize;
                    }
                }

                if (anih == null)
                    throw new InvalidDataException("ANI header not found.");

                // Collect CURS chunks from 'fram' LISTs
                var cursChunks = new List<byte[]>();
                while (stream.Position < stream.Length)
                {
                    var chunkHeader = reader.ReadBytes(8);
                    var chunkSize = BitConverter.ToInt32(chunkHeader, 4);
                    var chunkType = Encoding.ASCII.GetString(chunkHeader, 0, 4);

                    if (chunkType == "LIST")
                    {
                        var listType = Encoding.ASCII.GetString(reader.ReadBytes(4));
                        if (listType == "fram")
                        {
                            var listEnd = stream.Position + chunkSize - 4; // 4 bytes for list type
                            while (stream.Position < listEnd)
                            {
                                var subChunkHeader = reader.ReadBytes(8);
                                var subChunkSize = BitConverter.ToInt32(subChunkHeader, 4);
                                var subChunkType = Encoding.ASCII.GetString(subChunkHeader, 0, 4);

                                if (subChunkType == "CURS")
                                {
                                    var cursData = reader.ReadBytes(subChunkSize);
                                    cursChunks.Add(cursData);
                                }
                                else
                                {
                                    stream.Position += subChunkSize;
                                }
                            }
                        }
                        else
                        {
                            stream.Position += chunkSize - 4; // Skip non-fram LISTs
                        }
                    }
                    else
                    {
                        stream.Position += chunkSize;
                    }
                }

                if (cursChunks.Count == 0)
                    throw new InvalidDataException("No CURS chunks found.");

                if (frameIndex >= cursChunks.Count)
                    throw new ArgumentOutOfRangeException(nameof(frameIndex));

                // Load the specified frame
                var cursStream = new MemoryStream(cursChunks[frameIndex]);
                var decoder = new IconBitmapDecoder(cursStream, BitmapCreateOptions.None, BitmapCacheOption.None);
                cursStream.Close();
                stream.Close();
                reader.Close();
                return new BitmapSource(decoder.Frames[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load ANI file: {ex.Message}", "ANI Load Error");
                throw;
            }
        }

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct ANIHeader
        {
            public uint cbSize;
            public uint cFrames;
            public uint cSteps;
            public uint cx;
            public uint cy;
            public uint cBitCount;
            public uint cPlanes;
            public uint cFramesPerSecond;
            public uint cTransparentColor;
            public uint cFlags;
        }
        */


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
