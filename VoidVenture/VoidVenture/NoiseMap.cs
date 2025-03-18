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
            int X = (int)x & 255;
            x -= Math.Floor(x);
            double u = Fade(x);

            int hash = permutation[X];
            int hash2 = permutation[X + 1];

            double a = ((hash & 1) * 2 - 1) * x;
            double b = ((hash2 & 1) * 2 - 1) * (1 - x);

            return a + u * (b - a);
        }

        private static double Fade(double t)
        {
            // Optimized fade function using polynomial approximation
            return t * t * t * (t * (t * 6 - 15) + 10);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public PerlinNoise noiseGenerator;
        private WriteableBitmap _terrainBitmap;

        public double[] columnHeights;
        public double offsetX,offsetY;
        public int seed;
        public double Scale = 2;
        public double[,] noiseDebug;
        public uint[] _pixelBuffer;
        public double normalizationFactor;
        public int defaultTerrainOffset = 1000;

        public int octaveEase = 10;
        public double[] octaveFrequencies;
        public double[] octaveAmplitudes;
        public double[] octaveOffsets;

        public uint[] SkyLut = new uint[1200];
        public uint[] WaterLut = new uint[51];
        public double[] waterDepthLUT;





        public void InitializeNoiseMap()
        {
            SetupTerrainMoveWithRightClick();
            SetupGetInfoFromTerrainOnLeftClick();
            SetupTerrainScalingWithScrolling();

            skyColorArgb = 0xFF000000 | ((uint)SkyColor.R << 16) | ((uint)SkyColor.G << 8) | SkyColor.B;
            undergroundColorArgb = 0xFF000000 | ((uint)StoneColor.R << 16) | ((uint)StoneColor.G << 8) | StoneColor.B;

            noiseDebug = new double[currentWidth, 2];
            RegenMap();

            SkyLut = new uint[currentHeight];
            WaterLut = new uint[51];

            this.SizeChanged += (s, e) =>
            {
                // Update the player's state (gravity, collision, etc.)
                player.Update(gravity, null, columnHeights, false);

                SkyLut = new uint[currentHeight];
                WaterLut = new uint[51];

                noiseDebug = new double[currentWidth, 2];
                RenderTerrain();
            };
        }




        public void RegenMap()
        {
            // Reset seed and position

            if (DO.RandomizeTerrainMulti)
            {
                WorldMulti = rnd.Next(15, 50) * 0.1;
            }

            if (DO.RandomizeTerrainHeights)
            {
                WaterLevel = rnd.Next(200, 300); // Random water level
                GrassDepth = rnd.Next(5, 20);
                DirtDepth = rnd.Next(5, 50) + GrassDepth;
            }

            if (DO.RandomizeTerrainColors)
            {
                var colors = TerrainColorGenerator.GenerateColors(7);
                WaterColor = colors[0];
                WaterDeepColor = colors[1];
                GrassColor = colors[2];
                SandColor = colors[3];
                DirtColor = colors[4];
                SkyColor = colors[5];
                StoneColor = colors[6];
                skyColorArgb = 0xFF000000 | ((uint)SkyColor.R << 16) | ((uint)SkyColor.G << 8) | SkyColor.B;
                undergroundColorArgb = 0xFF000000 | ((uint)StoneColor.R << 16) | ((uint)StoneColor.G << 8) | StoneColor.B;
            }

            if (DO.UseChunkGen)
            {
                UnloadAllChunks();
            }

            offsetX = 0.0;
            offsetY = 0.0;
            seed = Environment.TickCount;
            noiseGenerator = new PerlinNoise(seed);
            // Re-render terrain with updated parameters
            //RenderTerrainInChunks();
            RenderTerrain();
        }

        public void BeginBitmapRender()
        {
            // Check if the bitmap needs to be recreated
            if (_terrainBitmap == null || _terrainBitmap.PixelWidth != currentWidth || _terrainBitmap.PixelHeight != currentHeight)
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                _terrainBitmap = new WriteableBitmap(
                    currentWidth,
                    currentHeight,
                    dpi.PixelsPerInchX,
                    dpi.PixelsPerInchY,
                    PixelFormats.Bgra32,
                    null
                );
                _pixelBuffer = new uint[currentWidth * currentHeight];
            }
        }

        public void RenderTerrain()
        {

            // Precompute gradient parameters
            gradientConfig = new GradientConfig
            {
                GrassDepth = GrassDepth,
                DirtDepth = DirtDepth,

                GrassColor = GrassColor,
                DirtColor = DirtColor,
                SandColor = SandColor,

                StoneColor = StoneColor,
                SkyColor = SkyColor,
                WaterColor = WaterColor,
                WaterDeepColor = WaterDeepColor,
            };

            if (DO.UseChunkGen)
            {
                RenderTerrainChunks();
            }

            else
                RenderTerrainWhole();

        }







        private uint[] GenerateColorLUT(double height, int x, double localWaterY)
        {
            uint[] lut = new uint[currentHeight];

            double invGrass = 1.0 / (gradientConfig.GrassDepth * Scale);
            double invDirt = 1.0 / ((gradientConfig.DirtDepth - gradientConfig.GrassDepth) * Scale);
            double invStone = 1.0 / ((currentHeight - gradientConfig.DirtDepth) * Scale);

            // Precompute water level check
            int waterThreshold = (int)(localWaterY - 5);

            for (int y = 0; y < currentHeight; y++)
            {
                double delta = y - height;

                // Clamp values directly without using Math.Clamp (faster inline implementation)
                double grassFactor = Math.Clamp(delta * invGrass, 0, 1); // Grass layer
                double dirtFactor = Math.Clamp((delta - gradientConfig.GrassDepth) * invDirt, 0, 1); // Dirt layer
                double stoneFactor = Math.Clamp((delta - gradientConfig.DirtDepth) * invStone, 0, 1); // Stone layer

                // Determine if the pixel is underwater
                bool isUnderwater = y > waterThreshold;
                // Base colors for blending
                Color baseColor = isUnderwater ? gradientConfig.SandColor : gradientConfig.GrassColor;
                Color dirtColor = gradientConfig.DirtColor;
                Color stoneColor = gradientConfig.StoneColor;

                // Blend colors based on factors
                byte r = (byte)(
                    baseColor.R * (1 - grassFactor) +
                    dirtColor.R * grassFactor * (1 - dirtFactor) +
                    stoneColor.R * dirtFactor * (1 - stoneFactor) +
                    stoneColor.R * stoneFactor);

                byte g = (byte)(
                    baseColor.G * (1 - grassFactor) +
                    dirtColor.G * grassFactor * (1 - dirtFactor) +
                    stoneColor.G * dirtFactor * (1 - stoneFactor) +
                    stoneColor.G * stoneFactor);

                byte b = (byte)(
                    baseColor.B * (1 - grassFactor) +
                    dirtColor.B * grassFactor * (1 - dirtFactor) +
                    stoneColor.B * dirtFactor * (1 - stoneFactor) +
                    stoneColor.B * stoneFactor);

                // Combine into ARGB format
                lut[y] = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
            }

            return lut;
        }


        public void PrecomputeGradientColors()
        {
            // Precompute sky gradient LUT
            var skyLut = new uint[currentHeight];
            double invWidth = 1.0 / currentWidth;

            for (int y = 0; y < currentHeight; y++)
            {
                double factor = Math.Pow(1 - y * invWidth, 0.8);

                // Inline byte calculation to avoid redundant casts
                byte r = (byte)(SkyColor.R * factor);
                byte g = (byte)(SkyColor.G * factor);
                byte b = (byte)(SkyColor.B * factor);

                skyLut[y] = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
            }

            // Precompute water gradient LUT
            var waterLut = new uint[51]; // Depth 0-50

            for (int d = 0; d <= 50; d++)
            {
                double ratio = d / 50.0;

                // Inline byte calculation to avoid redundant casts
                byte r = (byte)(WaterColor.R + (WaterDeepColor.R - WaterColor.R) * ratio);
                byte g = (byte)(WaterColor.G + (WaterDeepColor.G - WaterColor.G) * ratio);
                byte b = (byte)(WaterColor.B + (WaterDeepColor.B - WaterColor.B) * ratio);

                waterLut[d] = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
            }

            SkyLut = skyLut;
            WaterLut = waterLut;
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
        public void ComputeNoiseValues()
        {
            double waterY = offsetY + (defaultTerrainOffset - WaterLevel) * Scale;

            double offsetXScale = (offsetX + currentWidth) * Scale;
            Parallel.For(0, currentWidth, x =>
            {
                double noiseValue = 0;
                double worldX = (x + offsetXScale);

                for (int o = 0; o < octaveEase; o++)
                {
                    double sampleX = worldX * octaveFrequencies[o] + octaveOffsets[o];
                    noiseValue += noiseGenerator.Noise1D(sampleX) * octaveAmplitudes[o];
                }

                noiseValue /= normalizationFactor;
                noiseValue = Math.Pow(Math.Abs(noiseValue), 1.2) * Math.Sign(noiseValue) * WorldMulti;
                double normalizedValue = (noiseValue + 1) * 0.5;

                double waterNoise = noiseGenerator.Noise1D(x * 0.0005);
                waterDepthLUT[x] = waterY + waterNoise * 70 * Scale;
                columnHeights[x] = normalizedValue * defaultTerrainOffset * Scale + offsetY;
                if (DO.Debug)
                {
                    noiseDebug[x, 0] = noiseValue;
                    noiseDebug[x, 1] = normalizedValue;
                }
            });
        }


    }



}