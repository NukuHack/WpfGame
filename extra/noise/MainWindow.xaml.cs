
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
            var random = new Random(seed);

            // Initialize and shuffle the first 256 elements
            for (int i = 0; i < 256; i++)
                permutation[i] = i;

            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
            }

            // Duplicate the shuffled array to the second half
            Array.Copy(permutation, 0, permutation, 256, 256);
        }

        public double Noise1D(double x)
        {
            int X = (int)Math.Floor(x) & 255;
            x -= Math.Floor(x);
            double u = Fade(x);

            int hash = permutation[X];
            int hash2 = permutation[X + 1];

            double a = ((hash & 1) * 2 - 1) * x;
            double b = ((hash2 & 1) * 2 - 1) * (1 - x);

            return a + u * (b - a);
        }

        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    }

    public partial class MainWindow : Window
    {
        public double MoveStep = 100.0; // Movement speed per key press
        public PerlinNoise noiseGenerator;
        public WriteableBitmap terrainBitmap;
        public int currentWidth, currentHeight;
        public double[] columnHeights;
        public double offsetX; // Horizontal offset for terrain panning
        public double offsetY; // Vertical offset for terrain panning
        public int seed;
        public double WorldMulti;
        public Random rnd = new Random();
        public double[,] noiseStuff;
        public bool doDebug = false;
        private uint[] pixels;
        public int ff;
        private (byte r, byte g, byte b)[] skyColors;
        private (byte r, byte g, byte b)[] groundColors;
        private double[] octaveFrequencies;
        private double[] octaveAmplitudes;
        private double[] octaveOffsets;
        private double normalizationFactor;
        private object lockObj = new object();

        public double WaterLevel { get; set; } // Default water level in pixels
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
            this.Loaded += (_, __) => Initialize();
        }
        private void Initialize()
        {
            
            this.terrainImage.MouseLeftButtonDown += terrainImage_MouseLeftButtonDown;
            this.terrainImage.MouseLeftButtonUp += terrainImage_MouseLeftButtonUp;
            this.terrainImage.MouseMove += terrainImage_MouseMove;
            
            this.MouseWheel += (s,e) => { MainWindow_MouseWheel(e); };

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

            offsetX = 0.0;
            offsetY = 0.0;
            seed = Environment.TickCount;
            noiseGenerator = new PerlinNoise(seed);
            // Re-render terrain with updated parameters
            RenderTerrain();
        }

        public double Scale = 1.0;
        public double scrollStep = 1.1;

        private void MainWindow_MouseWheel(MouseWheelEventArgs e)
        {
            double oldScale = Scale;

            // Adjust scale factor
            Scale *= (e.Delta > 0) ? scrollStep : 1/ scrollStep;
            Scale = Math2.Clamp(Scale, 0.1, 10); // Prevent invalid scales

            // Calculate new transform origin
            Point mousePos = e.GetPosition(terrainImage);
            double relativeX = mousePos.X / terrainImage.ActualWidth;
            double relativeY = mousePos.Y / terrainImage.ActualHeight;

            // Update transform
            terrainScaleTransform.ScaleX = Scale;
            terrainScaleTransform.ScaleY = Scale;

            // Adjust translation to keep focus point under mouse
            terrainScaleTransform.CenterX = relativeX;
            terrainScaleTransform.CenterY = relativeY;

            // Regenerate terrain with new scale
            RenderTerrain();
        }

        private Point? _moveStartPoint;
        private void terrainImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _moveStartPoint = e.GetPosition(terrainImage);
            terrainImage.CaptureMouse();
        }

        private void terrainImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_moveStartPoint.HasValue)
            {
                var delta = _moveStartPoint.Value - e.GetPosition(terrainImage);
                offsetX += delta.X;
                offsetY -= delta.Y*0.7;
                WaterLevel += delta.Y * 0.7;
                //y value only if scrolled in
                _moveStartPoint = e.GetPosition(terrainImage);
                RenderTerrain();
            }
        }

        private void terrainImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _moveStartPoint = null;
            terrainImage.ReleaseMouseCapture();
        }

        


        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A) MoveX(-MoveStep);
            else if (e.Key == Key.D) MoveX(MoveStep);
            else if (e.Key == Key.W) MoveY(MoveStep);
            else if (e.Key == Key.S) MoveY(-MoveStep);
            else if (e.Key == Key.R) RegenMap();
            else if (e.Key == Key.T) ShowDebugInfo();
            else if (e.Key == Key.F) DoDebug();
        }
        private void DoDebug()
        {
            doDebug = !doDebug;
            MessageBox.Show($"Now the debug changed to :{doDebug}");
        }
        private void ShowDebugInfo()
        {
            if (doDebug)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Seed: {seed}");
                sb.AppendLine($"Offset: {offsetX:F2}_X {offsetY:F2}_Y");
                sb.AppendLine($"WorldMulti: {WorldMulti}");
                sb.AppendLine("X | Noise | Height");

                for (int x = 0; x < Math.Min(currentWidth, 8000); x += 40)
                    sb.AppendLine($"{x,3} | {noiseStuff[x, 0],5:F1} | {noiseStuff[x, 1],5:F1}");

                MessageBox.Show(sb.ToString(), "Terrain Debug Info");
            }
            else
            {
                MessageBox.Show("First you have to enable debug mode for that (F)");
            }

        }

        private void MoveX(double move)
        {
            offsetX += move;
            RenderTerrain();
        }
        private void MoveY(double move)
        {
            offsetY += move;
            WaterLevel -= move;
            RenderTerrain();
        }

        private void RenderTerrain()
        {
            // Use actual screen dimensions instead of scaled values
            int width = (int)currentWidth;
            int height = (int)ActualHeight;
            double waterY = ActualHeight - WaterLevel;
            int size = 100;

            // Reuse or create bitmap with screen dimensions
            if (terrainBitmap == null || terrainBitmap.PixelWidth != width || terrainBitmap.PixelHeight != height)
            {
                terrainBitmap = new WriteableBitmap(width, height, size, size, PixelFormats.Bgra32, null);
                pixels = new uint[width * height];
            }

            // Precompute octave parameters if scale changed
            if (octaveFrequencies == null || octaveFrequencies.Length != 10 || Math.Abs(octaveFrequencies[0] - 0.003 * Scale) > 1e-6)
            {
                ComputeOctaveParameters(Scale);
            }

            // Compute noise values
            columnHeights = new double[width];
            ComputeNoiseValues(width);

            // Precompute gradients
            PrecomputeGradientColors(height);

            // Render pixels
            RenderPixels(columnHeights, width, height, waterY);

            // Update bitmap
            terrainBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            terrainImage.Source = terrainBitmap;
        }

        private void ComputeOctaveParameters(double scale)
        {
            octaveFrequencies = new double[10];
            octaveAmplitudes = new double[10];
            octaveOffsets = Enumerable.Range(0, 10).Select(o => o * 1.3).ToArray();
            double amplitude = 1;
            double frequency = 0.003 * (1 / scale);
            normalizationFactor = 0.6;

            for (int o = 0; o < 10; o++)
            {
                octaveFrequencies[o] = frequency;
                octaveAmplitudes[o] = amplitude;
                normalizationFactor += amplitude;
                amplitude *= 0.5;
                frequency *= 1.5;
            }
        }

        private void ComputeNoiseValues(int width)
        {
            Parallel.For(0, width, x =>
            {
                double noiseValue = 0;
                double worldX = x + (offsetX + width) * Scale;

                for (int o = 0; o < 10; o++)
                {
                    double sampleX = worldX * octaveFrequencies[o] + octaveOffsets[o];
                    noiseValue += noiseGenerator.Noise1D(sampleX) * octaveAmplitudes[o];
                }

                noiseValue /= normalizationFactor;
                noiseValue = Math.Pow(Math.Abs(noiseValue), 1.2) * Math.Sign(noiseValue) * WorldMulti;
                double normalizedValue = (noiseValue + 1) * 0.5;

                columnHeights[x] = normalizedValue * currentHeight * Scale + offsetY;

                if (doDebug)
                {
                    noiseStuff[x, 0] = noiseValue;
                    noiseStuff[x, 1] = normalizedValue;
                }
            });
        }


        private (byte, byte, byte) MultiplyColor(Color color, byte gradient)
        {
            float factor = gradient / 255f;
            return ((byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));
        }
        // Add a new field to store gradient factors instead of precomputed colors
        private byte[] groundGradients;

        private void PrecomputeGradientColors(int height)
        {
            skyColors = new (byte, byte, byte)[height];
            groundGradients = new byte[height]; // Store gradient factors instead of colors

            for (int y = 0; y < height; y++)
            {
                // Sky gradient remains unchanged
                byte skyGradient = (byte)(Math.Max(y / (double)height, 0.5) * 255);
                skyColors[y] = MultiplyColor(SkyColor, skyGradient);

                // Compute and store ground gradient factor
                groundGradients[y] = (byte)((1 - (y / (double)height) * 0.7) * 255);
            }
        }

        private void RenderPixels(double[] columnHeights, int width, int height, double waterY)
        {
            Parallel.For(0, width, x =>
            {
                double baseHeight = columnHeights[x];
                int startY = Math.Max(0, (int)(baseHeight - GrassDepth));
                int endY = Math.Min(height, (int)(baseHeight + DirtDepth) + 1);

                for (int y = 0; y < height; y++)
                {
                    if (y < baseHeight)
                    {
                        // Sky/water rendering remains unchanged
                        if (y > waterY && baseHeight > waterY)
                        {
                            pixels[y * width + x] = 0xFF000000 | (uint)(WaterColor.R << 16 | WaterColor.G << 8 | WaterColor.B);
                        }
                        else
                        {
                            var color = skyColors[y];
                            pixels[y * width + x] = 0xFF000000 | (uint)(color.r << 16 | color.g << 8 | color.b);
                        }
                    }
                    else
                    {
                        // Apply gradient to correct terrain type
                        if (y < baseHeight + GrassDepth && y > baseHeight - GrassDepth)
                        {
                            var color = y < waterY ? GrassColor : SandColor;
                            var groundColor = MultiplyColor(color, groundGradients[y]);
                            pixels[y * width + x] = 0xFF000000 | (uint)(groundColor.Item1 << 16 | groundColor.Item2 << 8 | groundColor.Item3);
                        }
                        else if (y < baseHeight + DirtDepth)
                        {
                            var groundColor = MultiplyColor(DirtColor, groundGradients[y]);
                            pixels[y * width + x] = 0xFF000000 | (uint)(groundColor.Item1 << 16 | groundColor.Item2 << 8 | groundColor.Item3);
                        }
                        else
                        {
                            var groundColor = MultiplyColor(StoneColor, groundGradients[y]);
                            pixels[y * width + x] = 0xFF000000 | (uint)(groundColor.Item1 << 16 | groundColor.Item2 << 8 | groundColor.Item3);
                        }
                    }
                }
            });
        }


    }
}
