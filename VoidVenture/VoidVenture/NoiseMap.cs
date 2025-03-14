using System;

using System.IO;
using System.Text;
using System.Linq;
//using System.Drawing;
using System.Xml.Linq;
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

        private const int ChunkSize = 256; // Fixed chunk size
        private readonly Dictionary<int, TerrainChunk> loadedChunks = new Dictionary<int, TerrainChunk>();
        private readonly object renderLock = new object();
        private readonly HashSet<int> activeChunks = new HashSet<int>();




        public void InitializeNoiseMap()
        {
            SetupTerrainMoveWithRightClick();
            SetupGetInfoFromTerrainOnLeftClick();
            SetupTerrainScalingWithScrolling();

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

            if (DOUseChunkGen)
            {
                RenderTerrainInChunks(gradientConfig, skyLut, waterLut);
            }
            else
            {

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
            // Write pixels to the bitmap
            terrainBitmap.WritePixels(new Int32Rect(0, 0, currentWidth, currentHeight), pixels, currentWidth * 4, 0);
            terrainImage.Source = terrainBitmap;

            // Update transform
            terrainScaleTransform.ScaleX = currentWidth / (double)terrainImage.Source.Width;
            terrainScaleTransform.ScaleY = currentHeight / (double)terrainImage.Source.Height;
        }




        private void RenderTerrainInChunks(GradientConfig gradientConfig, uint[] skyLut, uint[] waterLut)
        {
            int numChunks = (currentWidth + ChunkSize - 1) / ChunkSize;

            double viewportStartX = offsetX;
            double viewportEndX = offsetX + currentWidth;

            int startChunkIndex = (int)Math.Max(0, viewportStartX / ChunkSize);
            int endChunkIndex = (int)Math.Min(numChunks - 1, viewportEndX / ChunkSize);

            Parallel.For(startChunkIndex, endChunkIndex + 1, chunkIndex =>
            {
                int startX = chunkIndex * ChunkSize;
                int width = Math.Min(ChunkSize, currentWidth - startX);

                lock (activeChunks)
                {
                    activeChunks.Add(startX); // Mark chunk as active
                }

                if (!loadedChunks.ContainsKey(startX))
                {
                    GenerateChunk(startX, width, gradientConfig, skyLut, waterLut);
                }

                RenderChunk(loadedChunks[startX]);

                lock (activeChunks)
                {
                    activeChunks.Remove(startX); // Mark chunk as inactive
                }
            });

            UnloadUnusedChunks();
        }

        private void RenderChunk(TerrainChunk chunk)
        {
            lock (renderLock)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    int globalX = chunk.StartX + x; // Global X position
                    int baseIndex = globalX; // Base index for this column

                    for (int y = 0; y < currentHeight; y++)
                    {
                        uint color = chunk.Pixels[y, x];
                        pixels[baseIndex] = color;
                        baseIndex += currentWidth; // Move to the next row
                    }
                }
            }
        }


        private void UnloadUnusedChunks()
        {

            double viewportStartX = offsetX;
            double viewportEndX = offsetX + currentWidth;

            List<int> chunksToRemove = new List<int>();

            lock (loadedChunks)
            {
                foreach (var chunkKey in loadedChunks.Keys)
                {
                    if ((chunkKey + ChunkSize <= viewportStartX || chunkKey >= viewportEndX) &&
                        !activeChunks.Contains(chunkKey))
                    {
                        chunksToRemove.Add(chunkKey);
                    }
                }

                foreach (var key in chunksToRemove)
                {
                    loadedChunks.Remove(key);
                }
            }
        }


        private void GenerateChunk(int startX, int width, GradientConfig gradientConfig, uint[] skyLut, uint[] waterLut)
        {
            TerrainChunk chunk = new TerrainChunk(startX, width, currentHeight);

            for (int x = 0; x < width; x++)
            {
                int globalX = startX + x;

                double terrainHeight = columnHeights[globalX];
                double localWaterY = waterDepthLUT[globalX];

                uint[] gradientMap = GenerateColorLUT(terrainHeight, globalX, localWaterY, gradientConfig);
                uint stoneColorArgb = 0xFF000000 | ((uint)StoneColor.R << 16) | ((uint)StoneColor.G << 8) | StoneColor.B;

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

                    chunk.Pixels[y, x] = color;
                }

                chunk.Heights[x] = terrainHeight;
            }

            lock (loadedChunks)
            {
                loadedChunks[startX] = chunk;
            }
        }





        private uint[] GenerateColorLUT(double height, int x, double localWaterY, GradientConfig gradientConfig)
        {
            uint[] lut = new uint[currentHeight];

            // Precompute constants to avoid recalculating them in every iteration
            double invGrass = 1.0 / (GrassDepth * Scale);
            double invDirt = 1.0 / ((DirtDepth - GrassDepth) * Scale);
            double invStone = 1.0 / ((currentWidth - DirtDepth) * Scale);

            // Precompute water level check
            int waterThreshold = (int)(localWaterY - 5);

            for (int y = 0; y < currentHeight; y++)
            {
                double delta = y - height;

                // Clamp values directly without using Math2.Clamp (faster inline implementation)
                double grass = Math.Max(0, Math.Min(delta * invGrass, 1));
                double dirt = Math.Max(0, Math.Min((delta - GrassDepth) * invDirt, 1));
                double stone = Math.Max(0, Math.Min((delta - DirtDepth) * invStone, 1));

                // Determine if sunk based on precomputed threshold
                bool sunk = y > waterThreshold;

                // Precompute base colors once
                byte baseR = sunk ? gradientConfig.SandColor.R : gradientConfig.GrassColor.R;
                byte baseG = sunk ? gradientConfig.SandColor.G : gradientConfig.GrassColor.G;
                byte baseB = sunk ? gradientConfig.SandColor.B : gradientConfig.GrassColor.B;

                // Compute RGB values efficiently
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
            // this used to contain the current height but decided to just add scrolling - scaling and remove the current height from the calculation

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
                // this used to contain the current height but decided to just add scrolling - scaling and remove the current height from the calculation
                if (DODebug)
                {
                    noiseDebug[x, 0] = noiseValue;
                    noiseDebug[x, 1] = normalizedValue;
                }
            });
        }


    }



}