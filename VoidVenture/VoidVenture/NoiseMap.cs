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
    public class TerrainChunk
    {
        public int StartX { get; }
        public int Width { get; }
        public uint[,] Pixels { get; }
        public double[] Heights { get; }

        public TerrainChunk(int startX, int width, int height)
        {
            StartX = startX;
            Width = width;
            Pixels = new uint[height, width];
            Heights = new double[width];
        }
    }

    public class StaticChunk
    {
        public int StartX { get; }
        public int Width { get; }
        public uint Color { get; }

        public StaticChunk(int startX, int width, uint color)
        {
            StartX = startX;
            Width = width;
            Color = color;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public PerlinNoise noiseGenerator;
        public WriteableBitmap terrainBitmap;

        public double[] columnHeights;
        public double offsetX; // Horizontal offset for terrain panning
        public double offsetY; // Vertical offset for terrain panning
        public int seed;
        public double Scale = 2;
        public double[,] noiseDebug;
        public uint[] pixels;
        public double[] waterDepthLUT;
        public System.Windows.Point? _moveStartPoint;
        public int octaveEase = 10;
        public double[] octaveFrequencies;
        public double[] octaveAmplitudes;
        public double[] octaveOffsets;
        public double normalizationFactor;
        public int defaultTerrainOffset = 1000;

        private const int StaticChunkHeight = 1200;
        private uint skyColorArgb;
        private uint undergroundColorArgb;
        private const int ChunkSize = 256; // Fixed chunk size
        private readonly Dictionary<int, object> loadedChunks = new();
        private readonly object renderLock = new object();
        private readonly HashSet<int> activeChunks = new();




        public void InitializeNoiseMap()
        {
            SetupTerrainMoveWithRightClick();
            SetupGetInfoFromTerrainOnLeftClick();
            SetupTerrainScalingWithScrolling();

            skyColorArgb = 0xFF000000 | ((uint)SkyColor.R << 16) | ((uint)SkyColor.G << 8) | SkyColor.B;
            undergroundColorArgb = 0xFF000000 | ((uint)StoneColor.R << 16) | ((uint)StoneColor.G << 8) | StoneColor.B;

            noiseDebug = new double[currentWidth, 2];
            RegenMap();

            this.SizeChanged += (s, e) =>
            {
                // Update the player's state (gravity, collision, etc.)
                player.Update(gravity, null, columnHeights, false);

                noiseDebug = new double[currentWidth, 2];
                RenderTerrain();
            };
        }



        private void RenderTerrainWhole()
        {

            BeginBitmapRender();

            // Recompute octave parameters if needed
            if (octaveFrequencies == null || octaveFrequencies.Length != 10 || Math.Abs(octaveFrequencies[0] - 0.003 * Scale) > 1e-6)
                ComputeOctaveParameters();

            // Reuse columnHeights array if possible
            if (columnHeights == null || columnHeights.Length != currentWidth)
            {
                columnHeights = new double[currentWidth];
                waterDepthLUT = new double[currentWidth];
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

            var (skyLut, waterLut) = PrecomputeColors();


            // Precompute stone color in ARGB format
            uint stoneColorArgb = 0xFF000000 | ((uint)StoneColor.R << 16) | ((uint)StoneColor.G << 8) | StoneColor.B;

            // Render terrain in parallel
            Parallel.For(0, currentWidth, x =>
            {
                double terrainHeight = columnHeights[x];
                double localWaterY = waterDepthLUT[x];
                uint[] gradientMap = GenerateColorLUT(terrainHeight, x, localWaterY, gradientConfig);

                int baseIndex = x; // Base index for this column
                for (int y = 0; y < currentHeight; y++)
                {
                    uint color;
                    if (y < terrainHeight)
                    {
                        if (y > localWaterY)
                            color = waterLut[(int)Math.Min(y - localWaterY, 50)];
                        else
                            color = skyLut[y];
                    }
                    else
                    {
                        if (y < terrainHeight - DirtDepth - 5 && y > terrainHeight * 1.5)
                            color = stoneColorArgb;
                        else
                            color = gradientMap[y];
                    }

                    // Store the color in the 1D array
                    pixels[baseIndex] = color;
                    baseIndex += currentWidth; // Move to the next row in the same column
                }
            });
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
                pixels = new uint[currentHeight * currentWidth]; // Use 1D array for faster access
            }
        }

        public void RenderTerrain()
        {
            if (DO.UseChunkGen) RenderTerrainChunks();
            else RenderTerrainWhole();


            // Write pixels to the bitmap
            terrainBitmap.WritePixels(new Int32Rect(0, 0, currentWidth, currentHeight), pixels, currentWidth * 4, 0);
            terrainImage.Source = terrainBitmap;

            // Update transform
            terrainScaleTransform.ScaleX = currentWidth / (double)terrainImage.Source.Width;
            terrainScaleTransform.ScaleY = currentHeight / (double)terrainImage.Source.Height;
        }

        private void RenderTerrainChunks()
        {
            BeginBitmapRender();

            // Recompute octave parameters if needed
            if (octaveFrequencies == null || octaveFrequencies.Length != 10 || Math.Abs(octaveFrequencies[0] - 0.003 * Scale) > 1e-6)
                ComputeOctaveParameters();

            // Reuse columnHeights array if possible
            if (columnHeights == null || columnHeights.Length != currentWidth)
            {
                columnHeights = new double[currentWidth];
                waterDepthLUT = new double[currentWidth];
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

            var (skyLut, waterLut) = PrecomputeColors();


            double viewportStartX = offsetX;
            double viewportEndX = offsetX + currentWidth;

            int firstChunkIndex = (int)Math.Floor(viewportStartX / ChunkSize);
            int lastChunkIndex = (int)Math.Floor((viewportEndX - 1) / ChunkSize);
            int numChunks = lastChunkIndex - firstChunkIndex + 1;

            Parallel.For(0, numChunks, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, chunkIndex =>
            {

                int chunkStartX = firstChunkIndex * ChunkSize + chunkIndex * ChunkSize;
                int startX = chunkStartX;
                int width = Math.Min(ChunkSize, (int)(viewportEndX - startX));

                if (width <= 0)
                    return;

                if (!loadedChunks.ContainsKey(startX))
                {
                    GenerateAppropriateChunk(startX, width);
                }

                RenderAppropriateChunk(loadedChunks[startX]);

            });

            UnloadUnusedChunks();
        }





        private void GenerateAppropriateChunk(int startX, int width)
        {
            if (startX < -ChunkSize * 2 || startX > currentWidth * 2)
            {
                GenerateStaticChunk(startX, width);
            }
            else
            {
                GenerateDynamicChunk(startX, width);
            }
        }

        private void GenerateStaticChunk(int startX, int width)
        {
            uint color = (startX < -ChunkSize) ? skyColorArgb : undergroundColorArgb;
            var chunk = new StaticChunk(startX, width, color);
            lock (loadedChunks)
            {
                loadedChunks[startX] = chunk;
            }
        }

        private void GenerateDynamicChunk(int startX, int width)
        {
            TerrainChunk chunk = new TerrainChunk(startX, width, currentHeight);
            for (int x = 0; x < width; x++)
            {
                int globalX = startX + x;
                int localX = globalX - (int)offsetX;
                if (localX < 0 || localX >= columnHeights.Length) continue;
                double terrainHeight = columnHeights[localX];
                double localWaterY = waterDepthLUT[localX];

                for (int y = 0; y < currentHeight; y++)
                {
                    // Calculate world Y coordinate
                    double worldY = offsetY + y;
                    uint color = worldY < terrainHeight ? 0xFF00FF00 : 0xFF8B4513;
                    chunk.Pixels[y, x] = color;
                }
                chunk.Heights[x] = terrainHeight;
            }
            lock (loadedChunks)
            {
                loadedChunks[startX] = chunk;
            }
        }

        private void RenderAppropriateChunk(object chunk)
        {
            switch (chunk)
            {
                case TerrainChunk tc:
                    RenderDynamicChunk(tc);
                    break;
                case StaticChunk sc:
                    RenderStaticChunk(sc);
                    break;
            }
        }

        private void RenderDynamicChunk(TerrainChunk chunk)
        {
            lock (renderLock)
            {
                int startX = chunk.StartX;
                int endX = startX + chunk.Width;

                // Calculate visible portion
                int visibleStart = Math.Max(startX, (int)offsetX);
                int visibleEnd = Math.Min(endX, (int)(offsetX + currentWidth));

                if (visibleStart >= visibleEnd) return;

                for (int x = 0; x < chunk.Width; x++)
                {
                    int globalX = chunk.StartX + x;
                    int viewportX = globalX - (int)offsetX;

                    if (viewportX < 0 || viewportX >= currentWidth) continue;

                    int baseIndex = viewportX;

                    for (int y = 0; y < currentHeight; y++)
                    {
                        pixels[baseIndex] = chunk.Pixels[y, x];
                        if (DO.Debug && x==chunk.StartX)
                            pixels[baseIndex] = 0xFFFF00FF; // Magenta

                        baseIndex += currentWidth;
                    }
                }
            }
        }

        private void RenderStaticChunk(StaticChunk chunk)
        {
            lock (renderLock)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    int globalX = chunk.StartX + x;
                    int viewportX = globalX - (int)offsetX;

                    if (viewportX < 0 || viewportX >= currentWidth) continue;

                    int baseIndex = viewportX;

                    for (int y = 0; y < currentHeight; y++)
                    {
                        pixels[baseIndex] = chunk.Color;
                        baseIndex += currentWidth;
                    }
                }
            }
        }

        private void UnloadUnusedChunks()
        {
            int viewportStartX = (int)Math.Max(0, offsetX - ChunkSize);
            int viewportEndX = (int)(offsetX + currentWidth + ChunkSize);

            List<int> chunksToRemove = new();
            foreach (var chunkKey in loadedChunks.Keys)
            {
                if (chunkKey + ChunkSize < viewportStartX - currentWidth || chunkKey > viewportEndX + currentWidth)
                {
                    chunksToRemove.Add(chunkKey);
                }
            }

            foreach (var key in chunksToRemove)
            {
                loadedChunks.Remove(key);
            }
        }


        private void UnloadAllChunks()
        {
            loadedChunks.Clear();
        }








        private uint[] GenerateColorLUT(double height, int x, double localWaterY, GradientConfig gradientConfig)
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


        public (uint[], uint[]) PrecomputeColors()
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

            return (skyLut, waterLut);
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