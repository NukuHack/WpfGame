
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
using System.Reflection;

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
        private uint[,] pixels;
        private double[] waterLUT;
        private Point? _moveStartPoint;
        private int octaveEase = 10;
        public bool inMouseDown = false;
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
            this.terrainImage.MouseLeftButtonUp += TerrainImage_MouseLeftButtonUp;
            this.infoText.MouseDown += InfoText_MouseDown;

            this.MouseWheel += (s, e) => { MainWindow_MouseWheel(e); };

            currentWidth = (int)ActualWidth;
            currentHeight = (int)ActualHeight;
            noiseDebug = new double[currentWidth, 2];
            RegenMap();

            SizeChanged += (s, e) =>
            {
                currentWidth = (int)ActualWidth;
                currentHeight = (int)ActualHeight;
                noiseDebug = new double[currentWidth, 2];
                RenderTerrain();
            };

            this.KeyDown += MainWindow_KeyDown;
            this.Focusable = true;
            Focus();
        }

        private void TerrainImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            inMouseDown = false;
            Task.Delay(1000).ContinueWith(_ => this.Dispatcher.Invoke(() => {
                if (!inMouseDown)
                    infoText.Visibility = Visibility.Collapsed;
            }));
        }

        private void UpdateInfoDisplay(MouseEventArgs e)
        {
            // Get click position relative to the terrain image
            Point clickPoint = e.GetPosition(terrainImage);

            // Convert screen coordinates to bitmap pixel coordinates
            double scaleX = currentWidth / terrainImage.ActualWidth;
            double scaleY = currentHeight / terrainImage.ActualHeight;
            int x = (int)(clickPoint.X * scaleX);
            int y = (int)(clickPoint.Y * scaleY);

            // Bounds check
            if (x < 0 || x >= currentWidth || y < 0 || y >= currentHeight)
            {
                infoText.Visibility = Visibility.Collapsed;
                return;
            }

            // Get precomputed values
            double terrainHeight = columnHeights[x];
            double waterLevel = waterLUT[x];

            // Determine material
            string material;
            if (y < terrainHeight)
                material = y > waterLevel ? "Water" : "Air";
            else
            {
                double delta = y - terrainHeight;
                if (delta <= GrassDepth)
                    // Check if near water edge (replicate "sunk" condition)
                    material = (y > waterLevel - 5) ? "Sand" : "Grass";
                else if (delta <= DirtDepth)
                    material = "Dirt";
                else
                    material = "Stone";
            }

            // Update UI
            infoText.Content = $"Clicked:\n{material}";
            clickPoint = e.GetPosition(TerrainCanvas);
            Canvas.SetLeft(infoText, (clickPoint.X + 5));
            Canvas.SetTop(infoText, (clickPoint.Y - 5 - infoText.Height));
            infoText.Visibility = Visibility.Visible;

            // Debug output
            if (doDebug)
            {
                label1.Content = $"click: {x:F1}, {y:F1}";
                label2.Content = $"terrain: {terrainHeight:F1}, water: {waterLevel:F1}";
                label3.Content = $"delta: {y - terrainHeight:F1}";
                label4.Content = $"grass: {GrassDepth:F1}, dirt: {DirtDepth:F1}";
                label5.Content = $"bounds: {x}/{currentWidth}, {y}/{currentHeight}";
            }
        }

        private void TerrainImage_MouseMove(object sender, MouseEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
            {
                inMouseDown = true;
                UpdateInfoDisplay(e);
            }

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
            double relativeX = mousePos.X / currentWidth;
            double relativeY = mousePos.Y / currentHeight;

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
                sb.AppendLine("X | Noise | Height | End ");

                for (int x = 0; x < Math.Min(currentWidth, 8000); x += 40)
                    sb.AppendLine($"{x,3} | {noiseDebug[x, 0],5:F1} | {noiseDebug[x, 1],5:F1} | {columnHeights[x],5:F1}");

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

            // Reuse or recreate bitmap and pixel array
            if (terrainBitmap == null || terrainBitmap.PixelWidth != currentWidth || terrainBitmap.PixelHeight != currentHeight)
            {
                // Get system DPI
                var source = PresentationSource.FromVisual(this);
                double dpiX = 96.0, dpiY = 96.0;
                if (source != null)
                {
                    dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                }

                terrainBitmap = new WriteableBitmap(currentWidth, currentHeight, dpiX, dpiY, PixelFormats.Bgra32, null);
            }
            pixels = new uint[currentHeight, currentWidth];

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
                double localWaterY = waterLUT[x];

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
                        if(y< terrainHeight-DirtDepth-5)
                        {
                            color = 0xFF000000 | ((uint)StoneColor.R << 16) | ((uint)StoneColor.G << 8) | StoneColor.B;
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
                    }

                    //int index = y * currentWidth + x;
                    pixels[y,x] = color;
                }
                // white dots at the top of the terrain
                //pixels[(int)terrainHeight,x] = 0xFF000000 | ((uint)250 << 16) | ((uint)250 << 8) | 250;
            });

            terrainBitmap.WritePixels(new Int32Rect(0, 0, currentWidth, currentHeight), pixels, currentWidth * 4, 0);
            terrainImage.Source = terrainBitmap;
        }

        private void ComputeNoiseValues()
        {
            double waterY = offsetY + (currentHeight - WaterLevel) * Scale;
            waterLUT = new double[currentWidth];
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


                double waterNoise = noiseGenerator.Noise1D(x * 0.0005);
                waterLUT[x] = waterY + waterNoise * 50 * Scale;

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
