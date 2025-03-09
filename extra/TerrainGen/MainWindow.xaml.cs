
using System;

using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Numerics;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

using Microsoft.Win32.SafeHandles;
using System.Windows.Interop;

namespace TerrainGen
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public PerlinNoise noiseGenerator;
        public WriteableBitmap terrainBitmap;
        public int currentWidth, currentHeight;
        public Random rnd = new Random();
        public double[] columnHeights;
        public double offsetX; // Horizontal offset for terrain panning
        public double offsetY; // Vertical offset for terrain panning
        public int seed;
        public double Scale = 1.0;
        public double WorldMulti = 3.5; // wanted it to be random between 1 and 5 but decided to use a constant :/
        public double[,] noiseDebug;
        public bool doDebug = false;
        private uint[] pixels;
        private Point? _moveStartPoint;
        private int octaveEase = 10;
        private double[] octaveFrequencies;
        private double[] octaveAmplitudes;
        private double[] octaveOffsets;
        private double normalizationFactor;

        public double WaterLevel; // Default water level in pixels
        public int DirtDepth; // Dirt layer thickness
        public int GrassDepth;  // Grass layer thickness
        public Color WaterColor = Colors.Blue;
        public Color WaterDeepColor = Colors.DarkBlue;
        public Color GrassColor = Colors.Green;
        public Color SandColor = Colors.LightYellow;
        public Color DirtColor = Colors.SaddleBrown;
        public Color SkyColor = Colors.LightBlue;
        public Color StoneColor = Colors.DarkGray;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += (_, __) => Initialize();
        }


        private void Initialize()
        {

            this.terrainImage.MouseRightButtonDown += terrainImage_MouseRightButtonDown;
            this.terrainImage.MouseRightButtonUp += terrainImage_MouseRightButtonUp;
            this.terrainImage.MouseMove += terrainImage_MouseMove;

            this.terrainImage.MouseMove += TerrainImage_MouseMove;
            this.terrainImage.MouseLeftButtonDown += (s,e) => { UpdateInfoDisplay(e); } ;
            this.infoText.MouseDown += InfoText_MouseDown;

            this.MouseWheel += (s, e) => { MainWindow_MouseWheel(e); };

            currentWidth = (int)ActualWidth;
            currentHeight = (int)ActualHeight;
            noiseDebug = new double[currentWidth, 2];
            RegenMap();

            SizeChanged += (s, e) =>
            {
                currentWidth = (int)e.NewSize.Width;
                currentHeight = (int)e.NewSize.Height;
                noiseDebug = new double[currentWidth, 2];
                RenderTerrain();
            };

            this.KeyDown += MainWindow_KeyDown;
            this.Focusable = true;
            Focus();
        }

        private void UpdateInfoDisplay(MouseEventArgs e)
        {
            // Get click position relative to the terrain image
            Point clickPoint = e.GetPosition(terrainImage);

            // Convert to world coordinates (account for scaling and panning)
            double worldX = (clickPoint.X / Scale); // no need to use offset x because only the offset-ed values are saved in "columnHeights"
            double worldY = (clickPoint.Y / Scale);

            // Validate column index
            int xIndex = (int)worldX;
            if (xIndex < 0 || xIndex >= columnHeights.Length)
                return;

            // Get terrain height at this column (already includes scaling)
            double terrainHeight = columnHeights[xIndex];

            // Calculate water level in UI coordinates
            double waterHeight = (currentHeight - WaterLevel);
            double waterY = waterHeight * Scale;

            if (doDebug)
            {
                label1.Content = $"click pos y: {clickPoint.Y}";
                label2.Content = $"scaled y: {worldY}";
                label3.Content = $"terrain y: {terrainHeight}";
                label4.Content = $"water y: {waterHeight}";
                label5.Content = $"water scaled y: {waterY}";
            }

            // Determine terrain type based on world coordinates
            if (worldY>terrainHeight)// Click is above terrain
            {
                if (worldY<terrainHeight + GrassDepth)
                    if (worldY > waterY - 5)
                        infoText.Content = "click:\nSand";
                    else
                        infoText.Content = "click:\nGrass";
                else if (worldY<terrainHeight + DirtDepth)
                    infoText.Content = "click:\nDirt";
                else
                    infoText.Content = "click:\nStone";
            }
            else// Click is below terrain
            {
                if (worldY>waterY)
                    infoText.Content = "click:\nWater";
                else
                    infoText.Content = "click:\nAir";
            }

            // Position info text
            Canvas.SetLeft(infoText, clickPoint.X + 5);
            Canvas.SetTop(infoText, clickPoint.Y - 5 - infoText.Height);
            infoText.Visibility = Visibility.Visible;

        }

        private void TerrainImage_MouseMove(object sender, MouseEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
                UpdateInfoDisplay(e);

        }

        private void InfoText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.infoText.Visibility = Visibility.Collapsed;
        }

        public void RegenMap()
        {
            // Reset seed and position
            //WorldMulti = rnd.Next(15, 50) * 0.1;
            WaterLevel = rnd.Next(175, 250); // Random water level
            GrassDepth = rnd.Next(5, 20);
            DirtDepth = rnd.Next(5, 100) + GrassDepth;

            offsetX = 0.0;
            offsetY = 0.0;
            seed = Environment.TickCount;
            noiseGenerator = new PerlinNoise(seed);
            // Re-render terrain with updated parameters
            RenderTerrain();
        }


        private void MainWindow_MouseWheel(MouseWheelEventArgs e)
        {
            double oldScale = Scale;

            // Adjust scale factor
            Scale *= (e.Delta > 0) ? 1.1 : 1 / 1.1;
            Scale = Math2.Clamp(Scale, 0.4, 10); // Prevent invalid scales

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

        private void terrainImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
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
                offsetY -= delta.Y * 0.7;
                //y value only if scrolled in
                _moveStartPoint = e.GetPosition(terrainImage);
                RenderTerrain();
            }
        }

        private void terrainImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _moveStartPoint = null;
            terrainImage.ReleaseMouseCapture();
        }




        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A) MoveX(-100);
            else if (e.Key == Key.D) MoveX(100);
            else if (e.Key == Key.W) MoveY(100);
            else if (e.Key == Key.S) MoveY(-100);
            else if (e.Key == Key.R) RegenMap();
            else if (e.Key == Key.T) ShowDebugInfo();
            else if (e.Key == Key.F) DoDebug();
        }


        private void MoveX(double move)
        {
            offsetX += move;
            RenderTerrain();
        }
        private void MoveY(double move)
        {
            offsetY += move;
            RenderTerrain();
        }
        private void DoDebug()
        {
            doDebug = !doDebug;
            MessageBox.Show($"Now the debug changed to :{doDebug}");
            if (doDebug)
                debugPanel.Visibility = Visibility.Visible;
            else
                debugPanel.Visibility = Visibility.Collapsed;
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
                    sb.AppendLine($"{x,3} | {noiseDebug[x, 0],5:F1} | {noiseDebug[x, 1],5:F1}");

                MessageBox.Show(sb.ToString(), "Terrain Debug Info");
            }
            else
            {
                MessageBox.Show("First you have to enable debug mode for that (F)");
            }

        }



        private void RenderTerrain()
        {
            // Calculate waterY considering scale and vertical offset
            double waterY = offsetY + (currentHeight - WaterLevel) * Scale;
            int size = 100;

            // Reuse or recreate bitmap and pixel array
            if (terrainBitmap == null || terrainBitmap.PixelWidth != currentWidth || terrainBitmap.PixelHeight != currentHeight)
            {
                terrainBitmap = new WriteableBitmap(currentWidth, currentHeight, size, size, PixelFormats.Bgra32, null);
                pixels = new uint[currentWidth * currentHeight];
            }

            // Recompute octave parameters if needed
            if (octaveFrequencies == null || octaveFrequencies.Length != 10 || Math.Abs(octaveFrequencies[0] - 0.003 * Scale) > 1e-6)
            {
                ComputeOctaveParameters();
            }

            // Reuse columnHeights array if possible
            if (columnHeights == null || columnHeights.Length != currentWidth)
            {
                columnHeights = new double[currentWidth];
            }
            ComputeNoiseValues();

            // Precompute gradient parameters
            var gradientConfig = new GradientConfig
            {
                GrassDepth = GrassDepth,
                GrassColor = GrassColor,
                SandColor = SandColor,
                DirtDepth = DirtDepth,
                DirtColor = DirtColor,
                StoneColor = StoneColor,
            };

            // Precompute sky gradient LUT
            var skyLut = new uint[currentHeight];
            double invWidth = 1.0 / currentWidth;
            for (int y = 0; y < currentHeight; y++)
            {
                double factor = Math.Pow(1 - y * invWidth, 0.8);
                skyLut[y] = 0xFF000000 | (uint)(
                    (byte)(SkyColor.R * factor) << 16 |
                    (byte)(SkyColor.G * factor) << 8 |
                    (byte)(SkyColor.B * factor));
            }

            // Precompute water gradient LUT
            var waterLut = new uint[51]; // Depth 0-50
            for (int d = 0; d <= 50; d++)
            {
                waterLut[d] = BlendColorsUnsafe(WaterColor, WaterDeepColor, d / 50.0);
            }

            // Precompute inverse depths for terrain blending
            double invGrass = 1.0 / GrassDepth;
            double invDirt = 1.0 / (DirtDepth - GrassDepth);
            double invStone = 1.0 / (currentWidth - DirtDepth);

            // Render terrain in parallel
            Parallel.For(0, currentWidth, x =>
            {
                double terrainHeight = columnHeights[x];
                double noise = noiseGenerator.Noise1D(x * 0.0005);
                // Scale noise perturbation with terrain scale
                double localWaterY = waterY + noise * 50 * Scale;

                for (int y = 0; y < currentHeight; y++)
                {
                    uint color;
                    if (y < terrainHeight)
                    {
                        if (y > localWaterY)
                        {
                            int depth = (int)Math.Min(y - localWaterY, 50);
                            color = waterLut[depth];
                        }
                        else
                        {
                            color = skyLut[y];
                        }
                    }
                    else
                    {
                        double delta = y - terrainHeight;
                        double grass = Math2.Clamp(delta * invGrass, 0, 1);
                        double dirt = Math2.Clamp((delta - GrassDepth) * invDirt, 0, 1);
                        double stone = Math2.Clamp((delta - DirtDepth) * invStone, 0, 1);
                        bool sunk = y > localWaterY - 5;

                        byte baseR = sunk ? gradientConfig.SandColor.R : gradientConfig.GrassColor.R;
                        byte baseG = sunk ? gradientConfig.SandColor.G : gradientConfig.GrassColor.G;
                        byte baseB = sunk ? gradientConfig.SandColor.B : gradientConfig.GrassColor.B;

                        byte r = (byte)(
                            baseR * (1 - grass) +
                            gradientConfig.DirtColor.R * grass * (1 - dirt) +
                            gradientConfig.StoneColor.R * dirt * (1 - stone) +
                            gradientConfig.StoneColor.R * stone);

                        byte g = (byte)(
                            baseG * (1 - grass) +
                            gradientConfig.DirtColor.G * grass * (1 - dirt) +
                            gradientConfig.StoneColor.G * dirt * (1 - stone) +
                            gradientConfig.StoneColor.G * stone);

                        byte b = (byte)(
                            baseB * (1 - grass) +
                            gradientConfig.DirtColor.B * grass * (1 - dirt) +
                            gradientConfig.StoneColor.B * dirt * (1 - stone) +
                            gradientConfig.StoneColor.B * stone);

                        color = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
                    }

                    int index = y * currentWidth + x;
                    pixels[index] = color;
                }
            });

            terrainBitmap.WritePixels(new Int32Rect(0, 0, currentWidth, currentHeight), pixels, currentWidth * 4, 0);
            terrainImage.Source = terrainBitmap;
        }

        private void ComputeNoiseValues()
        {
            Parallel.For(0, currentWidth, x =>
            {
                double noiseValue = 0;
                double worldX = x + (offsetX + currentWidth) * Scale;

                for (int o = 0; o < octaveEase; o++)
                {
                    double sampleX = worldX * octaveFrequencies[o] + octaveOffsets[o];
                    noiseValue += noiseGenerator.Noise1D(sampleX) * octaveAmplitudes[o];
                }

                noiseValue /= normalizationFactor;
                noiseValue = Math.Pow(Math.Abs(noiseValue), 1.2) * Math.Sign(noiseValue) * WorldMulti;
                double normalizedValue = (noiseValue + 1) * 0.5;

                // Scale terrain height with Scale and apply vertical offset
                columnHeights[x] = normalizedValue * currentHeight * Scale + offsetY;

                if (doDebug)
                {
                    noiseDebug[x, 0] = noiseValue;
                    noiseDebug[x, 1] = normalizedValue;
                }
            });
        }

        private void ComputeOctaveParameters()
        {
            octaveFrequencies = new double[octaveEase];
            octaveAmplitudes = new double[octaveEase];
            octaveOffsets = Enumerable.Range(0, octaveEase).Select(o => o * 1.3).ToArray();
            double amplitude = 1;
            double frequency = 0.003 * (1 / Scale);
            normalizationFactor = 0.6;

            for (int o = 0; o < octaveEase; o++)
            {
                octaveFrequencies[o] = frequency;
                octaveAmplitudes[o] = amplitude;
                normalizationFactor += amplitude;
                amplitude *= 0.5;
                frequency *= 1.5;
            }
        }

        private uint BlendColorsUnsafe(Color a, Color b, double ratio)
        {
            //ratio = Math2.Clamp(ratio, 0, 1);
            return 0xFF000000 | (uint)(
                (byte)(a.R + (b.R - a.R) * ratio) << 16 |
                (byte)(a.G + (b.G - a.G) * ratio) << 8 |
                (byte)(a.B + (b.B - a.B) * ratio)
            );
        }

        private struct GradientConfig
        {
            public int GrassDepth;
            public Color GrassColor;
            public Color SandColor;

            public int DirtDepth;
            public Color DirtColor;

            public Color StoneColor;
        }


    }
}
