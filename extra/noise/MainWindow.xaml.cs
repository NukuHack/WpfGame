
using System;

using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Numerics;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

using System.Windows.Navigation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

using Microsoft.Win32.SafeHandles;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;


namespace noise
{
    public class PerlinNoise
    {
        private readonly int[] permutation = new int[512];

        public PerlinNoise(int seed)
        {
            var p = Enumerable.Range(0, 256).ToArray();
            var random = new Random(seed);

            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }

            Array.Copy(p, 0, permutation, 0, 256);
            Array.Copy(p, 0, permutation, 256, 256);
        }

        public double Noise1D(double x)
        {
            int X = (int)Math.Floor(x) & 255;
            x -= Math.Floor(x);
            double u = Fade(x);
            return Lerp(u, Grad1D(permutation[X], x), Grad1D(permutation[X + 1], x - 1));
        }

        private static double Grad1D(int hash, double x) => (hash & 1) == 0 ? x : -x;
        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static double Lerp(double t, double a, double b) => a + t * (b - a);
    }


    public partial class MainWindow : Window
    {
        public double Scale = 80.0;
        public double MoveStep = 100.0; // Movement speed per key press
        public PerlinNoise noiseGenerator;
        public WriteableBitmap terrainBitmap;
        public Image terrainImage = new Image();
        public int currentWidth, currentHeight;
        public double offset; // Horizontal offset for terrain panning
        public int seed;
        public double WorldMulti;
        public Random rnd = new Random();
        public double[,] noiseStuff;

        public int WaterLevel { get; set; } // Default water level in pixels
        public int DirtDepth { get; set; } // Dirt layer thickness
        public int GrassDepth { get; set; }  // Grass layer thickness
        public Color WaterColor { get; set; } = Colors.Blue;
        public Color GrassColor { get; set; } = Colors.Green;
        public Color SandColor { get; set; } = Colors.LightYellow;
        public Color DirtColor { get; set; } = Colors.SaddleBrown;
        public Color SkyColor { get; set; } = Colors.LightBlue;
        public Color StoneColor { get; set; } = Colors.Gray;

        public MainWindow()
        {
            InitializeComponent();
            TerrainCanvas.Children.Add(terrainImage);
            this.Loaded += (_, __) => Initialize();
        }
        private void Initialize()
        {
            currentWidth = (int)ActualWidth;
            currentHeight = (int)ActualHeight;
            noiseStuff = new double[currentWidth, 2];
            RegenMap();

            SizeChanged += (s, e) =>
            {
                currentWidth = (int)e.NewSize.Width;
                currentHeight = (int)e.NewSize.Height;
                noiseStuff = new double[currentWidth, 2];
                RenderTerrain();
            };

            this.KeyDown += MainWindow_KeyDown;
            this.Focusable = true;
            Focus();
        }
        public void RegenMap()
        {
            // Reset seed and position
            WorldMulti = rnd.Next(15, 35) * 0.1;
            WaterLevel = rnd.Next(75, 250); // Random water level
            GrassDepth = rnd.Next(5, 20);
            DirtDepth = rnd.Next(5, 100) + GrassDepth;

            offset = 0.0;
            seed = Environment.TickCount;
            noiseGenerator = new PerlinNoise(seed);
            // Re-render terrain with updated parameters
            RenderTerrain();
        }


        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A) Move(-MoveStep);
            else if (e.Key == Key.D) Move(MoveStep);
            else if (e.Key == Key.R) RegenMap();
            else if (e.Key == Key.T) ShowDebugInfo();
        }
        private void ShowDebugInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Seed: {seed}");
            sb.AppendLine($"Offset: {offset:F2}");
            sb.AppendLine($"WorldMulti: {WorldMulti}");
            sb.AppendLine("X | Noise | Height");

            for (int x = 0; x < Math.Min(currentWidth, 8000); x += 40)
                sb.AppendLine($"{x,3} | {noiseStuff[x, 0],5:F1} | {noiseStuff[x, 1],5:F1}");

            MessageBox.Show(sb.ToString(), "Terrain Debug Info");
        }

        private void Move(double move)
        {
            offset += move;
            RenderTerrain();
        }


        private void RenderTerrain()
        {
            int width = currentWidth;
            int height = currentHeight;
            double waterY = ActualHeight - WaterLevel;

            // Initialize bitmap
            terrainBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new uint[width * height];

            // Compute noise values
            double[] columnHeights = ComputeNoiseValues(width);

            // Precompute gradients
            var (skyGradients, groundGradients) = PrecomputeGradients(height);

            // Render pixels
            RenderPixels(pixels, columnHeights, width, height, waterY, skyGradients, groundGradients);

            // Update bitmap
            terrainBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            terrainImage.Source = terrainBitmap;
        }

        private double[] ComputeNoiseValues(int width)
        {
            double[] columnHeights = new double[width];

            Parallel.For(0, width, x =>
            {
                double noiseValue = 0;
                double amplitude = 1;
                double frequency = 0.5 / Scale;
                double normalizationFactor = 0.6;

                for (int o = 0; o < 10; o++)
                {
                    double sampleX = ((x + offset) * frequency) + (o * 1.3);
                    double octaveNoise = noiseGenerator.Noise1D(sampleX);
                    noiseValue += octaveNoise * amplitude;
                    normalizationFactor += amplitude;
                    amplitude *= 0.5;
                    frequency *= 1.5;
                }

                noiseValue /= normalizationFactor;
                noiseValue = Math.Pow(Math.Abs(noiseValue), 1.2) * Math.Sign(noiseValue) * WorldMulti;
                double normalizedValue = (noiseValue + 1) * 0.5;

                columnHeights[x] = normalizedValue * currentHeight;
                noiseStuff[x, 0] = noiseValue;
                noiseStuff[x, 1] = normalizedValue;
            });

            return columnHeights;
        }

        private (byte[] sky, byte[] ground) PrecomputeGradients(int height)
        {
            byte[] skyGradients = new byte[height];
            byte[] groundGradients = new byte[height];

            for (int y = 0; y < height; y++)
            {
                skyGradients[y] = (byte)(Math.Max(y / (double)height, 0.5) * 255);
                groundGradients[y] = (byte)((1 - (y / (double)height) * 0.7) * 255);
            }

            return (skyGradients, groundGradients);
        }

        private void RenderPixels(uint[] pixels, double[] columnHeights,
            int width, int height, double waterY,
            byte[] skyGradients, byte[] groundGradients)
        {
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    double baseHeight = columnHeights[x];
                    (byte r, byte g, byte b) = GetColorForPixel(x, y, baseHeight, waterY);

                    // Apply gradients
                    if (y < baseHeight)
                    {
                        r = (byte)(r * skyGradients[y] / 255);
                        g = (byte)(g * skyGradients[y] / 255);
                        b = (byte)(b * skyGradients[y] / 255);
                    }
                    else
                    {
                        r = (byte)(r * groundGradients[y] / 255);
                        g = (byte)(g * groundGradients[y] / 255);
                        b = (byte)(b * groundGradients[y] / 255);
                    }

                    pixels[y * width + x] = (uint)((255 << 24) | (r << 16) | (g << 8) | b);
                }
            });
        }

        private (byte r, byte g, byte b) GetColorForPixel(int x, int y, double baseHeight, double waterY)
        {
            if (y < baseHeight)
            {
                if (y > waterY && baseHeight > waterY)
                    return (WaterColor.R, WaterColor.G, WaterColor.B);

                return (SkyColor.R, SkyColor.G, SkyColor.B);
            }

            if (y < baseHeight + GrassDepth && y > baseHeight - GrassDepth)
            {
                if (y < waterY)
                    return (GrassColor.R, GrassColor.G, GrassColor.B);
                else
                    return (SandColor.R, SandColor.G, SandColor.B);
            }

            if (y < baseHeight + DirtDepth)
                return (DirtColor.R, DirtColor.G, DirtColor.B);

            return (StoneColor.R, StoneColor.G, StoneColor.B);
        }

    }
}
