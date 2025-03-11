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



    public struct GradientConfig
    {
        public int GrassDepth;
        public Color GrassColor;
        public Color SandColor;

        public int DirtDepth;
        public Color DirtColor;

        public Color StoneColor;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public PerlinNoise noiseGenerator;
        public WriteableBitmap terrainBitmap;
        public int currentWidth, currentHeight;
        public double[] columnHeights;
        public double offsetX; // Horizontal offset for terrain panning
        public double offsetY; // Vertical offset for terrain panning
        public int seed;
        public double Scale = 2;
        public double[,] noiseDebug;
        public DispatcherTimer timer;
        public uint[,] pixels;
        public double[] waterLUT;
        public Point? _moveStartPoint;
        public int octaveEase = 10;
        public bool inMouseDown = false;
        public double[] octaveFrequencies;
        public double[] octaveAmplitudes;
        public double[] octaveOffsets;
        public double normalizationFactor;

        public double WorldMulti = 3.5;
        public double WaterLevel = 200; // Default water level in pixels
        public int GrassDepth = 10;  // Grass layer thickness
        public int DirtDepth = 60; // Dirt layer thickness
        public Color WaterColor = Colors.Blue;
        public Color WaterDeepColor = Colors.DarkBlue;
        public Color GrassColor = Colors.Green;
        public Color SandColor = Colors.LightYellow;
        public Color DirtColor = Colors.SaddleBrown;
        public Color SkyColor = Colors.LightBlue;
        public Color StoneColor = Colors.DarkGray;


        public void InitializeNoiseMap()
        {
            SetupTerrainMoveWithRightClick();
            SetupGetInfoFromTerrainOnLeftClick();
            SetupTerrainScalingWithScrolling();

            noiseDebug = new double[currentWidth, 2];
            RegenMap();

            this.SizeChanged += (s, e) =>
            {
                noiseDebug = new double[currentWidth, 2];
                RenderTerrain();
            };
        }

        public void SetupTerrainMoveWithRightClick()
        {

            this.GameCanvas.MouseRightButtonDown += (s, e) => {
                _moveStartPoint = e.GetPosition(terrainImage);
            };
            this.GameCanvas.MouseRightButtonUp += (s, e) => {
                _moveStartPoint = null;
            };
            this.GameCanvas.MouseMove += (s, e) => {
                if (_moveStartPoint.HasValue)
                {
                    // Calculate the delta between the current mouse position and the starting point
                    Point currentPos = e.GetPosition(terrainImage);
                    Vector delta = _moveStartPoint.Value - currentPos;

                    // Update the terrain's offsets
                    offsetX += delta.X / Scale * 0.7;
                    offsetY -= delta.Y / Scale * 0.35;
                    // don't ask me why these are the random values the stuff get's multiplied by ... idk ...
                    // Update the player's position
                    player.X -= delta.X / Scale * 0.21;
                    player.Y -= delta.Y / Scale * 0.37;

                    // Update the starting point for the next movement
                    _moveStartPoint = currentPos;

                    // Update the player's state (gravity, collision, etc.)
                    player.Update(gravity, null, columnHeights);

                    // Regenerate the terrain with the new offsets
                    RenderTerrain();
                }
            };
        }

        public void SetupTerrainScalingWithScrolling()
        {
            this.GameCanvas.MouseWheel += (s, e) => {
                double oldScale = Scale;
                double ZoomFactor = 1.1;

                // Adjust scale factor
                Scale *= (e.Delta > 0) ? ZoomFactor : 1 / ZoomFactor;
                if (Scale >= 10 || Scale <= 0.3)
                {
                    Scale = Math2.Clamp(Scale, 0.3, 10); // Prevent invalid scales
                    return;
                }

                // Get mouse position relative to the terrainImage
                Point mousePos = e.GetPosition(terrainImage);

                // Calculate the offset adjustments for the terrain
                double scaledMouseXOld = mousePos.X / oldScale;
                double scaledMouseXNew = mousePos.X / Scale;
                double offsetXAdjustment = scaledMouseXOld - scaledMouseXNew;

                double scaledMouseYOld = (mousePos.Y - offsetY) / oldScale;
                double scaledMouseYNew = (mousePos.Y - offsetY) / Scale;
                double offsetYAdjustment = mousePos.Y- (scaledMouseYOld - scaledMouseYNew) * Scale;

                // Update offsets for the terrain
                offsetX += offsetXAdjustment;
                offsetY = offsetYAdjustment;

                // Adjust the player's position relative to the mouse position
                double playerXOld = player.X;
                double playerYOld = player.Y;

                // Scale the player's position relative to the mouse
                player.X = mousePos.X + (playerXOld - mousePos.X) * (Scale / oldScale);
                player.Y = mousePos.Y + (playerYOld - mousePos.Y) * (Scale / oldScale);

                // Update the player's state (gravity, collision, etc.)
                player.Update(gravity, null, columnHeights);

                // Regenerate terrain with the new scale and offsets
                RenderTerrain();
            };
        }

        public void SetupGetInfoFromTerrainOnLeftClick()
        {
            this.infoText.MouseDown += (s, e) =>
            {
                this.infoText.Visibility = Visibility.Collapsed;
            };

            this.GameCanvas.MouseMove += (s, e) => {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    inMouseDown = true;
                    UpdateInfoDisplay(e);
                }
            };

            this.GameCanvas.MouseLeftButtonUp += (s, e) => {
                inMouseDown = false;
                Task.Delay(1000).ContinueWith(_ => this.Dispatcher.Invoke(() => {
                    if (!inMouseDown)
                        infoText.Visibility = Visibility.Collapsed;
                }));
            };

            this.GameCanvas.MouseLeftButtonDown += (s, e) => UpdateInfoDisplay(e);
        }
        public void UpdateInfoDisplay(MouseEventArgs e)
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
                if (delta <= GrassDepth * (Scale * 0.8))
                    // Check if near water edge (replicate "sunk" condition)
                    material = (y > waterLevel - 5) ? "Sand" : "Grass";
                else if (delta <= DirtDepth * (Scale * 0.8))
                    material = "Dirt";
                else
                    material = "Stone";
            }

            // Update UI
            infoText.Content = $"Clicked:\n{material}";
            clickPoint = e.GetPosition(MapCanvas);
            Canvas.SetLeft(infoText, (clickPoint.X + 5));
            Canvas.SetTop(infoText, (clickPoint.Y - 5 - infoText.Height));
            infoText.Visibility = Visibility.Visible;

            // Debug output
            if (DODebug)
            {
                label1.Content = $"click: {x:F1}, {y:F1}";
                label2.Content = $"terrain: {terrainHeight:F1}, water: {waterLevel:F1}";
                label3.Content = $"delta: {y - terrainHeight:F1}";
                label4.Content = $"grass: {GrassDepth:F1}, dirt: {DirtDepth:F1}";
                label5.Content = $"bounds: {x}/{currentWidth}, {y}/{currentHeight}";
            }
        }


        public void MoveTerrain(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: offsetY+=100; break;
                case Direction.Down: offsetY -= 100; break;
                case Direction.Right: offsetX += 100; break;
                case Direction.Left: offsetX -= 100; break;
            }
            RenderTerrain();
        }

        public void MoveOffset(Direction direction, double value)
        {
            switch (direction)
            {
                case Direction.Up: offsetY += value; break;
                case Direction.Down: offsetY -= value; break;
                case Direction.Right: offsetX += value; break;
                case Direction.Left: offsetX -= value; break;
            }
            RenderTerrain();
        }

        public void DoDebug()
        {
            DODebug = !DODebug;
            ShowMessage($"Now the debug changed to :{DODebug}");
            if (DODebug)
            {
                ShowMessage(
                    $"Some important stuff:\n" +
                    $"With enabling Debug mode you may run into bugs ... IDK\n" +
                    $"With DOUseNoiseTerrain you can move the terrain with Ctr+W/A/S/D\n" +
                    $"With DOUseNoiseTerrain you can view the heightmap with (T)"
                    );
                debugPanel.Visibility = Visibility.Visible;
            }
            else
            {
                debugPanel.Visibility = Visibility.Collapsed;
                label1.Content = "Click to update";
                Array.Clear(noiseDebug, 0, noiseDebug.Length);
            }
        }
        public void ShowDebugInfo()
        {
            if (DODebug)
            {
                if (noiseDebug[0, 0] == 0)
                {

                    ShowMessage("you have to redraw/reload the map to log the values for it.");
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Seed: {seed}");
                    sb.AppendLine($"Offset: {offsetX:F2}_X {offsetY:F2}_Y");
                    sb.AppendLine($"WorldMulti: {WorldMulti}");
                    sb.AppendLine("X | Noise | Height | End ");

                    for (int x = 0; x < Math.Min(currentWidth, 8000); x += 40)
                        sb.AppendLine($"{x,3} | {noiseDebug[x, 0],5:F1} | {noiseDebug[x, 1],5:F1} | {columnHeights[x],5:F1}");

                    ShowMessage(sb.ToString(), "Terrain Debug Info");
                }
            }
            else
            {
                ShowMessage("First you have to enable debug mode for that (F)");
            }

        }




        public void RegenMap()
        {
            // Reset seed and position

            if (DORandomizeTerrainMulti)
            {
                WorldMulti = rnd.Next(15, 50) * 0.1;
            }

            if (DORandomizeTerrainHeights)
            {
                WaterLevel = rnd.Next(200, 300); // Random water level
                GrassDepth = rnd.Next(5, 20);
                DirtDepth = rnd.Next(5, 50) + GrassDepth;
            }

            if (DORandomizeTerrainColors)
            {
                var colors = TerrainColorGenerator.GenerateColors(7);
                WaterColor = colors[0];
                WaterDeepColor = colors[1];
                GrassColor = colors[2];
                SandColor = colors[3];
                DirtColor = colors[4];
                SkyColor = colors[5];
                StoneColor = colors[6];
            }

            offsetX = 0.0;
            offsetY = 0.0;
            seed = Environment.TickCount;
            noiseGenerator = new PerlinNoise(seed);
            // Re-render terrain with updated parameters
            RenderTerrain();
        }

        public void RenderTerrain()
        {
            // Check if the bitmap needs to be recreated
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

                // Create a new WriteableBitmap with the updated dimensions and DPI
                terrainBitmap = new WriteableBitmap(currentWidth, currentHeight, dpiX, dpiY, PixelFormats.Bgra32, null);

                // Recreate the pixel array
                pixels = new uint[currentHeight, currentWidth];
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
                double ratio = d / 50.0;
                //ratio = Math2.Clamp(ratio, 0, 1);
                waterLut[d] = 0xFF000000 | (uint)(
                        (byte)(WaterColor.R + (WaterDeepColor.R - WaterColor.R) * ratio) << 16 |
                        (byte)(WaterColor.G + (WaterDeepColor.G - WaterColor.G) * ratio) << 8 |
                        (byte)(WaterColor.B + (WaterDeepColor.B - WaterColor.B) * ratio)
                    );
            }

            // Precompute inverse depths for terrain blending
            double invGrass = 1.0 / GrassDepth * (1 / Scale);
            double invDirt = 1.0 / (DirtDepth - GrassDepth) * (1 / Scale);
            double invStone = 1.0 / (currentWidth - DirtDepth) * (1 / Scale);

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
                        if (y < terrainHeight - DirtDepth - 5)
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
                    pixels[y, x] = color;
                }
                // white dots at the top of the terrain
                //pixels[(int)terrainHeight,x] = 0xFF000000 | ((uint)250 << 16) | ((uint)250 << 8) | 250;
            });

            terrainBitmap.WritePixels(new Int32Rect(0, 0, currentWidth, currentHeight), pixels, currentWidth * 4, 0);
            terrainImage.Source = terrainBitmap;

            // Update transform
            terrainScaleTransform.ScaleX = currentWidth / (double)terrainImage.Source.Width;
            terrainScaleTransform.ScaleY = currentHeight / (double)terrainImage.Source.Height;
        }

        public void ComputeNoiseValues()
        {
            double waterY = offsetY + (1000 - WaterLevel) * Scale;
            // this used to contain the current height but decided to just add scrolling - scaling and remove the current height from the calculation
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
                waterLUT[x] = waterY + waterNoise * 70 * Scale;

                // Scale terrain height with Scale and apply vertical offset
                columnHeights[x] = normalizedValue * 1000 * Scale + offsetY;
                // this used to contain the current height but decided to just add scrolling - scaling and remove the current height from the calculation

                if (DODebug)
                {
                    noiseDebug[x, 0] = noiseValue;
                    noiseDebug[x, 1] = normalizedValue;
                }
            });
        }

        public void ComputeOctaveParameters()
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


    }



}
