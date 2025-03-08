// this is a failed try with the random terrain gen
// this is the best looking out of all i have ever made / seen but it takes up waay too much resources
// also this has vertical lines in it so it's basically useless and i could not fix it 
// decided to upload in case ... idk why it would be helpful


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
    public class FastPerlinNoise
    {
        private readonly int[] permutation;

        public FastPerlinNoise(int seed)
        {
            permutation = Enumerable.Range(0, 512).ToArray();
            var random = new Random(seed);

            // Fisher-Yates shuffle for better performance
            for (int i = 255; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (permutation[i], permutation[swapIndex]) = (permutation[swapIndex], permutation[i]);
            }
            Array.Copy(permutation, 0, permutation, 256, 256);
        }

        public double Noise1D(double x)
        {
            int X = (int)x & 255;
            x -= Math.Floor(x);

            double u = x * x * x * (x * (x * 6 - 15) + 10); // Inlined Fade
            int A = permutation[X];
            int B = permutation[X + 1];

            return (Lerp(u,
                ((A & 1) == 0 ? x : -x),
                ((B & 1) == 0 ? x - 1 : -(x - 1)))
                + 1) * 0.5; // Pre-normalized
        }

        private static double Lerp(double t, double a, double b) => a + t * (b - a);
    }

    public partial class MainWindow : Window
    {
        private const double Scale = 80.0;
        private const double MoveStep = 150.0; // Movement speed per key press

        // Fractal parameters
        private const double DetailWeight = 0.1;
        private const double persistence = 0.5;
        private const double lacunarity = 0.1;
        private const int BaseOctaves = 100;
        private const int DetailOctaves = 20;

        private FastPerlinNoise noiseGenerator;
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
            noiseGenerator = new FastPerlinNoise(seed);

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

            double[] columnHeights = new double[width];
            double[] smoothHeights = new double[width];

            // Multi-resolution noise generation with improved parameters
            int lowResWidth = Math.Max(width / 4, 1); // Reduced division for smoother base
            double[] baseNoise = GenerateBaseNoise(lowResWidth);
            double[] upscaledBase = UpscaleNoise(baseNoise, width);
            double[] detailNoise = GenerateDetailNoise(width);

            // Apply Gaussian smoothing to base noise
            double[] smoothedBase = GaussianSmooth(upscaledBase, 3);

            // Combine noise with improved weighting
            Parallel.For(0, width, x =>
            {
                double combinedNoise = smoothedBase[x] * 0.8 + detailNoise[x] * 0.2;
                combinedNoise = Math.Pow(Math.Abs(combinedNoise), 1.05) * Math.Sign(combinedNoise) * WorldMulti;
                double normalizedValue = (combinedNoise + 1) * 0.25;
                columnHeights[x] = normalizedValue * height;

                // Apply 1D Gaussian blur to column heights
                if (x > 0 && x < width - 1)
                {
                    smoothHeights[x] = (columnHeights[x - 1] + columnHeights[x] * 2 + columnHeights[x + 1]) / 4;
                }
            });

            // Pixel processing with smooth transitions
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    double baseHeight = smoothHeights[x];
                    double waterY = height - WaterLevel;
                    double terrainHeight = baseHeight + (noiseGenerator.Noise1D(x * 0.1) * 10); // Add micro-variations

                    // Smooth gradient transitions
                    double grassBlend = Math.Max(0, Math.Min(1, (terrainHeight - y + GrassDepth) / (GrassDepth * 2)));
                    double dirtBlend = Math.Max(0, Math.Min(1, (terrainHeight - y + DirtDepth) / (DirtDepth * 2)));
                    double stoneBlend = Math.Max(0, 1 - (y / (double)height));

                    // Sky gradient with noise-based clouds
                    if (y < terrainHeight)
                    {
                        double cloudNoise = (noiseGenerator.Noise1D(x * 0.05) + 1) * 0.1;
                        byte skyAlpha = (byte)(255 * Math.Min(1, 0.3 + cloudNoise));

                        pixels[y * width + x] = GetSkyColor(y, height, cloudNoise, skyAlpha);
                    }
                    else
                    {
                        pixels[y * width + x] = GetTerrainColor(y, terrainHeight, waterY, grassBlend, dirtBlend, stoneBlend);
                    }
                }
            });

            terrainBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            terrainImage.Source = terrainBitmap;
        }

        // New helper methods for smooth color transitions
        private uint GetSkyColor(int y, int height, double cloudNoise, byte skyAlpha)
        {
            byte red = (byte)(SkyColor.R * (1 - y / (double)height) + cloudNoise * 50);
            byte green = (byte)(SkyColor.G * (1 - y / (double)height) + cloudNoise * 30);
            byte blue = (byte)(SkyColor.B * (1 - y / (double)height) + cloudNoise * 20);
            return (uint)((skyAlpha << 24) | (red << 16) | (green << 8) | blue);
        }

        private uint GetTerrainColor(double y, double terrainHeight, double waterY,
                                   double grassBlend, double dirtBlend, double stoneBlend)
        {
            byte red = 0, green = 0, blue = 0;

            // Water layer with depth-based color variation
            if (y > waterY)
            {
                double depth = Math.Min(1, (y - waterY) / 50);
                red = (byte)(WaterColor.R * (1 - depth) + SandColor.R * depth);
                green = (byte)(WaterColor.G * (1 - depth) + SandColor.G * depth);
                blue = (byte)(WaterColor.B * (1 - depth) + SandColor.B * depth);
            }
            else
            {
                // Smooth layer transitions using blend factors
                red = (byte)(GrassColor.R * grassBlend +
                             DirtColor.R * dirtBlend +
                             StoneColor.R * stoneBlend);

                green = (byte)(GrassColor.G * grassBlend +
                              DirtColor.G * dirtBlend +
                              StoneColor.G * stoneBlend);

                blue = (byte)(GrassColor.B * grassBlend +
                             DirtColor.B * dirtBlend +
                             StoneColor.B * stoneBlend);
            }

            return (uint)((255 << 24) | (red << 16) | (green << 8) | blue);
        }

        // Gaussian smoothing for noise arrays
        private double[] GaussianSmooth(double[] input, int radius)
        {
            double[] output = new double[input.Length];
            double[] kernel = GenerateGaussianKernel(radius);

            for (int i = 0; i < input.Length; i++)
            {
                double sum = 0;
                double weightSum = 0;

                for (int j = -radius; j <= radius; j++)
                {
                    int index = Math.Max(0, Math.Min(input.Length - 1, i + j));
                    double weight = kernel[Math.Abs(j)];
                    sum += input[index] * weight;
                    weightSum += weight;
                }

                output[i] = sum / weightSum;
            }

            return output;
        }

        private double[] GenerateGaussianKernel(int radius)
        {
            double[] kernel = new double[radius + 1];
            double sigma = radius / 3.0;

            for (int i = 0; i <= radius; i++)
            {
                kernel[i] = Math.Exp(-(i * i) / (2 * sigma * sigma)) / (Math.Sqrt(2 * Math.PI) * sigma);
            }

            return kernel;
        }



        private double[] GenerateBaseNoise(int lowResWidth)
        {
            double[] baseNoise = new double[lowResWidth];
            double baseNormalization = CalculateNormalization(BaseOctaves);

            Parallel.For(0, lowResWidth, xLow =>
            {
                double noiseValue = 0;
                double amplitude = 1;
                double frequency = 1 / Scale;

                for (int o = 0; o < BaseOctaves; o++)
                {
                    double sampleX = (xLow * 7 + offset) * frequency + o * 1.3;
                    noiseValue += noiseGenerator.Noise1D(sampleX) * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                baseNoise[xLow] = noiseValue / baseNormalization;
            });

            return baseNoise;
        }

        private double[] UpscaleNoise(double[] lowResNoise, int targetWidth)
        {
            double[] upscaled = new double[targetWidth];
            int lowRes = lowResNoise.Length;

            for (int x = 0; x < targetWidth; x++)
            {
                double position = (x / (double)targetWidth) * lowRes;
                int index = (int)position;
                double t = position - index;

                double a = lowResNoise[index % lowRes];
                double b = lowResNoise[(index + 1) % lowRes];

                upscaled[x] = CosineInterpolate(a, b, t);
            }

            return upscaled;
        }

        private double[] GenerateDetailNoise(int width)
        {
            double[] detailNoise = new double[width];
            double detailNormalization = CalculateNormalization(DetailOctaves);
            double startFrequency = (1 / Scale) * Math.Pow(lacunarity, BaseOctaves);

            Parallel.For(0, width, x =>
            {
                double noiseValue = 0;
                double amplitude = 1;
                double frequency = startFrequency;

                for (int o = 0; o < DetailOctaves; o++)
                {
                    double sampleX = (x + offset) * frequency + o * 1.3;
                    noiseValue += noiseGenerator.Noise1D(sampleX) * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                detailNoise[x] = noiseValue / detailNormalization;
            });

            return detailNoise;
        }

        private double CalculateNormalization(int octaveCount)
        {
            double normalization = 0;
            double amplitude = 1;
            for (int i = 0; i < octaveCount; i++)
            {
                normalization += amplitude;
                amplitude *= persistence;
            }
            return normalization;
        }

        private double CosineInterpolate(double a, double b, double t)
        {
            double cosT = (1 - Math.Cos(t * Math.PI)) / 2;
            return a * (1 - cosT) + b * cosT;
        }


    }

    
    public static class Math2
    {
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
