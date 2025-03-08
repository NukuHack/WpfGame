using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace noise
{
    public class PerlinNoise
    {
        private readonly int[] permutation;

        public PerlinNoise(int seed)
        {
            permutation = new int[512];
            var random = new Random(seed);
            for (int i = 0; i < 256; i++)
                permutation[i] = i;
            for (int i = 0; i < 256; i++)
            {
                int swapIndex = random.Next(256);
                (permutation[i], permutation[swapIndex]) = (permutation[swapIndex], permutation[i]);
            }
            for (int i = 256; i < 512; i++)
                permutation[i] = permutation[i - 256];
        }

        public double Noise1D(double x)
        {
            int X = (int)Math.Floor(x) & 255;
            x -= Math.Floor(x);
            double u = Fade(x);
            int A = permutation[X];
            int B = permutation[X + 1];
            return Lerp(u, Grad1D(A, x), Grad1D(B, x - 1)) * 0.5;
        }

        private double Grad1D(int hash, double x)
        {
            return (hash & 1) == 0 ? x : -x; // Simplified for 1D
        }

        private double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
        private double Lerp(double t, double a, double b) => a + t * (b - a);

    }

    public partial class MainWindow : Window
    {
        private const double Scale = 80.0;
        private const double MoveStep = 10.0; // Movement speed per key press
        private PerlinNoise noiseGenerator;
        private WriteableBitmap terrainBitmap;
        private readonly Image terrainImage = new Image();
        private int currentWidth, currentHeight;
        private double offset; // Horizontal offset for terrain panning
        private int seed;
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

            this.Loaded += (s, e) =>
            {
                currentWidth = (int)ActualWidth;
                currentHeight = (int)ActualHeight;
                noiseStuff = new double[currentWidth, 2];
                RegenMap();

                this.SizeChanged += Window_SizeChanged;
                this.KeyDown += MainWindow_KeyDown; // Add keyboard event handler
                this.Focusable = true; // Ensure window can receive keyboard input
                this.Focus();
            };

        }
        public void RegenMap()
        {

            // Reset seed and position
            WorldMulti = rnd.Next(15, 50) * 0.1;
            WaterLevel = rnd.Next(75, 250); // Random water level
            GrassDepth = rnd.Next(5, 20);
            DirtDepth = rnd.Next(5, 100)+ GrassDepth;
            offset = 0.0;
            seed = (int)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
            noiseGenerator = new PerlinNoise(seed);

            // Re-render terrain with updated parameters
            RenderTerrain(currentWidth, currentHeight);
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A)
                Move(-MoveStep); // Move left
            else if (e.Key == Key.D)
                Move(MoveStep); // Move right
            else if (e.Key == Key.R)
            {
                RegenMap();
            }
            else if (e.Key == Key.T)
            {
                ShowTerrainDebug();
            }

        }
        private void ShowTerrainDebug()
        {
            // ... (debug code)
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"World multi: {WorldMulti}");
            sb.AppendLine("X | Noise Value | Normalized Value");
            sb.AppendLine("----------------------------------");

            for (int x = 0; x < Math.Min(currentWidth, 500); x++)
            {
                sb.AppendFormat("{0,3} | {1,11:F4} | {2,15:F4}\n",
                    x,
                    noiseStuff[x, 0],
                    noiseStuff[x, 1]);
            }

            // Show first 1000 characters to avoid message box overflow
            string displayText = sb.Length > 1000 ? sb.ToString(0, 1000) + "..." : sb.ToString();
            MessageBox.Show(displayText, "Noise Values");
        }
        public void Move(double Move)
        {
            offset += Move;

            // Re-render terrain with updated parameters
            RenderTerrain(currentWidth, currentHeight);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            currentWidth = (int)e.NewSize.Width;
            currentHeight = (int)e.NewSize.Height;
            noiseStuff = new double[currentWidth, 2];
            RenderTerrain(currentWidth, currentHeight);
        }

        private void RenderTerrain(int width, int height)
        {
            terrainBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new uint[width * height];

            // Precompute column data with offset
            double[] columnHeights = new double[width];
            byte[] reds = new byte[width];
            byte[] greens = new byte[width];
            byte[] blues = new byte[width];

            // Fractal parameters
            int octaves = 10;
            double persistence = 0.5;
            double lacunarity = 1.5;


            Parallel.For(0, width, x =>
            {
                double noiseValue = 0;
                double amplitude = 1;
                double frequency = 0.5 / Scale;
                double normalizationFactor = 0.3;

                for (int o = 0; o < octaves; o++)
                {
                    double sampleX = ((x + offset) * frequency) + (o * 1.3); // Add offset for variation
                    double octaveNoise = noiseGenerator.Noise1D(sampleX);
                    noiseValue += octaveNoise * amplitude;

                    normalizationFactor += amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                noiseValue /= normalizationFactor; // Normalize to [-1, 1]
                //noiseValue = Math.Pow(Math.Abs(noiseValue), 0.8) * Math.Sign(noiseValue); // Flatten low areas
                // Enhanced flattening for mid-range values
                noiseValue = Math.Pow(Math.Abs(noiseValue), 1.2) * Math.Sign(noiseValue)*WorldMulti;

                double normalizedValue = (noiseValue + 1) * 0.5;
                columnHeights[x] = normalizedValue * height;

                noiseStuff[x, 0] = noiseValue;
                noiseStuff[x, 1] = normalizedValue;

            });
            

            // Pixel processing
            byte[] skyRed = new byte[height];
            byte[] skyGreen = new byte[height];
            byte[] skyBlue = new byte[height];
            // Render pixels with layers
            // ... (pixel assignment)
            Parallel.For(0, height, y =>
            {
                double gradientGround = 1 - (y / (double)height)*0.7;
                double gradientSky = Math.Max(y / (double)height,0.5);
                for (int x = 0; x < width; x++)
                {
                    byte red, green, blue, alpha = 255;
                    double baseHeight = columnHeights[x];
                    double waterY = ActualHeight - WaterLevel;

                    // Sky gradient
                    if (y < baseHeight)
                    {
                        if (y > waterY && baseHeight > waterY)
                        {
                            // Water layer
                            red = WaterColor.R;
                            green = WaterColor.G;
                            blue = WaterColor.B;
                        }
                        else
                        {
                            // Sky Gradient
                            red = (byte)(SkyColor.R * gradientSky);
                            green = (byte)(SkyColor.G * gradientSky);
                            blue = (byte)(SkyColor.B * gradientSky);
                        }
                    }
                    // Terrain layers only if above water
                    else
                    {
                        //TODO : snow to be like grass just on top of tall hills

                        if (y < baseHeight + GrassDepth && y > baseHeight - GrassDepth && y < waterY)
                        {
                            // Grass layer
                            red = GrassColor.R;
                            green = GrassColor.G;
                            blue = GrassColor.B;
                        }
                        else if (y < baseHeight + GrassDepth && y > baseHeight - GrassDepth)
                        {
                            // If dirt is submerged make it sand
                            red = SandColor.R;
                            green = SandColor.G;
                            blue = SandColor.B;
                        }
                        else if (y < baseHeight + DirtDepth)
                        {
                            // Dirt layer
                            red = DirtColor.R;
                            green = DirtColor.G;
                            blue = DirtColor.B;
                        }
                        else
                        {
                            // Terrain base - Stone layer
                            red = (byte)(StoneColor.R * gradientGround);
                            green = (byte)(StoneColor.G * gradientGround);
                            blue = (byte)(StoneColor.B * gradientGround);
                        }
                    }

                    pixels[y * width + x] = (uint)((alpha << 24) | (red << 16) | (green << 8) | blue);
                }
            });

            // ... (bitmap update)
            terrainBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            terrainImage.Source = terrainBitmap;

        }

    }
}