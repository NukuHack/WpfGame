using System;

using System.IO;
using System.Text;
using System.Linq;
//using System.Drawing;
using System.Xml.Linq;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics.Eventing.Reader;

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

    public class TerrainColorGenerator
    {
        private static readonly Random random = new Random();

        // Biome parameters struct for better memory efficiency
        private readonly struct BiomeParams
        {
            public readonly double HueMin, HueMax, SatMin, SatMax, LightMin, LightMax;

            public BiomeParams(double hueMin, double hueMax, double satMin, double satMax, double lightMin, double lightMax)
            {
                HueMin = hueMin;
                HueMax = hueMax;
                SatMin = satMin;
                SatMax = satMax;
                LightMin = lightMin;
                LightMax = lightMax;
            }
        }

        public static List<Color> GenerateColors(int count = 6)
        {
            var colors = new List<Color>(count);

            // Predefine biome templates as an array of structs
            var biomeTemplates = new BiomeParams[]
            {
            new BiomeParams(180, 240, 10, 30, 10, 30),   // Deep water
            new BiomeParams(200, 270, 15, 40, 20, 40),   // Shallow water
            new BiomeParams(30, 90, 10, 30, 30, 50),     // Sand/beach
            new BiomeParams(40, 100, 15, 40, 40, 60),    // Desert
            new BiomeParams(60, 160, 10, 30, 30, 50),    // Grassland/forest
            new BiomeParams(80, 180, 10, 30, 40, 60),    // Swamp/marsh
            new BiomeParams(10, 70, 5, 25, 20, 40),      // Mountain rock
            new BiomeParams(0, 40, 5, 25, 30, 50),       // Volcanic rock
            new BiomeParams(0, 360, 0, 10, 60, 80),      // Snow
            new BiomeParams(200, 300, 5, 20, 50, 70)     // Ice
            };

            for (int i = 0; i < count; i++)
            {
                // Get biome parameters based on elevation level (index)
                var segment = (double)i / (count - 1); // 0 (low elevation) to 1 (high)
                var templateIndex = random.Next(biomeTemplates.Length);
                var biome = biomeTemplates[templateIndex];

                // Adjust lightness based on elevation segment
                double lightMin = biome.LightMin + segment * 30;
                double lightMax = biome.LightMax + segment * 30;

                // Generate random HSL within biome constraints
                double h = RandomRange(biome.HueMin, biome.HueMax);
                double s = RandomRange(biome.SatMin, biome.SatMax);
                double l = RandomRange(lightMin, lightMax);

                // Convert to RGB and add to list
                colors.Add(HslToRgb(h, s, l));
            }

            return colors;
        }

        private static double RandomRange(double min, double max)
        {
            return min + (random.NextDouble() * (max - min));
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l / 100; // Achromatic
            }
            else
            {
                double q = l < 50 ? l * (1 + s / 100) : l + s - (l * s) / 100;
                double p = (2 * l - q) / 100;
                h /= 360;

                r = HueToRgb(p, q / 100, h + 1.0 / 3);
                g = HueToRgb(p, q / 100, h);
                b = HueToRgb(p, q / 100, h - 1.0 / 3);
            }

            return Color.FromArgb(
                255,
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }



    public struct GradientConfig
    {
        public int GrassDepth { get; set; }
        public int DirtDepth { get; set; }
        public Color GrassColor { get; set; }
        public Color DirtColor { get; set; }
        public Color StoneColor { get; set; }
        public Color SandColor { get; set; }
        public Color SkyColor { get; set; }
        public Color WaterColor { get; set; }
        public Color WaterDeepColor { get; set; }
    }

    public partial class MainWindow : Window
    {

        public System.Windows.Point? _moveStartPoint;
        private uint SkyColorArgb;
        private uint GroundColorArgb;


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

        public GradientConfig gradientConfig;

        public void SetupTerrainMoveWithRightClick()
        {

            this.GameCanvas.MouseRightButtonDown += (s, e) => {
                if (isGamePaused) return;
                _moveStartPoint = e.GetPosition(terrainImage);
            };
            this.GameCanvas.MouseRightButtonUp += (s, e) => {
                _moveStartPoint = null;
            };
            this.GameCanvas.MouseMove += (s, e) => {
                if (isGamePaused || !_moveStartPoint.HasValue) return;

                System.Windows.Point currentPos = e.GetPosition(terrainImage);
                // Calculate the delta between the current mouse position and the starting point
                Vector delta = _moveStartPoint.Value - currentPos;
                _moveStartPoint = currentPos;

                // Update the terrain's offsets
                offsetX += delta.X;
                offsetY += delta.Y;
                // Update the player's position
                player.X -= delta.X * 0.7;
                player.Y -= delta.Y;

                player.Update(gravity, null, columnHeights);
                RenderTerrain();
            };
        }

        public void SetupTerrainScalingWithScrolling()
        {
            this.GameCanvas.MouseWheel += (s, e) => {
                if (isGamePaused)return;
                HideInfoDisplay();

                // Adjust scale factor
                double oldScale = Scale;
                double ZoomFactor = 1.1;
                if ((e.Delta < 0 && Scale == 0.3) || (e.Delta > 0 && Scale == 10)) return;
                Scale *= (e.Delta > 0) ? ZoomFactor : 1 / ZoomFactor;
                Scale = Math.Clamp(Scale, 0.3, 10); // Clamp scale to valid range

                // Get mouse position relative to the terrainImage
                System.Windows.Point mousePos = e.GetPosition(terrainImage);

                // Calculate the center of the zoom in world coordinates
                double centerX = mousePos.X / Scale + offsetX;
                double centerY = (mousePos.Y - offsetY) / Scale + offsetY;

                // Update offsets based on the new scale
                offsetX = centerX - mousePos.X / Scale;
                offsetY = centerY - (mousePos.Y - offsetY) / Scale;

                Player_RePos();
                player.Update(gravity, null, columnHeights);

                RenderTerrain();
            };
        }

        public void SetupGetInfoFromTerrainOnLeftClick()
        {
            this.infoText.MouseDown += (s, e) =>
            {
                HideInfoDisplay();
            };

            this.GameCanvas.MouseMove += (s, e) => {
                if (isGamePaused) return;
                if (inMouseDown) UpdateInfoDisplay(e);
            };

            this.GameCanvas.MouseLeftButtonUp += (s, e) => {
                inMouseDown = false;
                Task.Delay(1000).ContinueWith(_ => this.Dispatcher.Invoke(() => {
                    if (!inMouseDown)
                        HideInfoDisplay();
                }));
            };

            this.GameCanvas.MouseLeftButtonDown += (s, e) =>
            {
                if (isGamePaused) return;
                inMouseDown = true;
                UpdateInfoDisplay(e);
            };
        }
        public void HideInfoDisplay()
        {
            infoText.Visibility = Visibility.Collapsed;
        }
        public void UpdateInfoDisplay(MouseEventArgs e)
        {
            // Get click position relative to the terrain image
            System.Windows.Point clickPoint = e.GetPosition(GameCanvas);

            int x = (int)clickPoint.X; int y = (int)clickPoint.Y;

            // Bounds check
            if (x < 0 || x >= currentWidth || y < 0 || y >= currentHeight)
            {
                HideInfoDisplay();
                return;
            }

            // Get precomputed values
            double terrainHeight = columnHeights[x];
            double waterLevel = waterDepthLUT[x];

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

            Canvas.SetLeft(infoText, (x + 5));
            Canvas.SetTop(infoText, (y - 5 - infoText.Height));
            infoText.Visibility = Visibility.Visible;

            // Debug output
            if (DO.Debug)
            {
                label1.Content = $"click: {x:F1}, {y:F1}";
                label2.Content = $"terrain: {terrainHeight:F1}, water: {waterLevel:F1}";
                label3.Content = $"delta: {y - terrainHeight:F1}";
                label4.Content = $"grass: {GrassDepth:F1}, dirt: {DirtDepth:F1}";
                label5.Content = $"bounds: {x}/{currentWidth}, {y}/{currentHeight}";
            }
        }


        public void MoveOffset(Direction direction, double value)
        {
            if (isGamePaused) return;
            switch (direction)
            {
                case Direction.Up: offsetY -= value; break;
                case Direction.Down: offsetY += value; break;
                case Direction.Right: offsetX += value; break;
                case Direction.Left: offsetX -= value; break;
            }
            RenderTerrain();
        }



        public void DoDebug()
        {
            DO.Debug = !DO.Debug;
            ShowMessage($"Now the debug changed to :{DO.Debug}");
            if (DO.Debug)
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
            if (DO.Debug)
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





    }
}