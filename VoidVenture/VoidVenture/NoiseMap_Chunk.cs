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
        public int StartY { get; }
        public int Width { get; }
        public int Height { get; }
        public uint[,] Pixels { get; }
        public double[] Heights { get; }

        public TerrainChunk(int startX, int width, int height)
        {
            StartX = startX;
            Width = width;
            Height = height;
            Pixels = new uint[height, width];
            Heights = new double[width];
        }
    }

    public class StaticChunk
    {
        public int StartX { get; }
        public int Width { get; }
        public uint Color { get; }
        public bool IsSky { get; } // New property to indicate if the chunk is for the sky or ground

        public StaticChunk(int startX, int width, uint color, bool isSky)
        {
            StartX = startX;
            Width = width;
            Color = color;
            IsSky = isSky;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {

        private const int StaticChunkHeight = 1000;
        private const int ChunkSize = 64; // Fixed chunk size
        private readonly Dictionary<int, TerrainChunk> loadedTerrainChunks = new();
        private readonly Dictionary<int, StaticChunk> loadedStaticChunks = new();
        private readonly object renderLock = new object();



        private void RenderTerrainChunks()
        {
            BeginTerrainGenerating();

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


            double viewportStartX = offsetX;
            double viewportEndX = offsetX + currentWidth;

            int firstChunkIndex = (int)Math.Floor(viewportStartX / ChunkSize);
            int lastChunkIndex = (int)Math.Floor((viewportEndX - 1) / ChunkSize);
            int numChunks = lastChunkIndex - firstChunkIndex + 1;

            Parallel.For(0, numChunks, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, chunkIndex =>
            {
                int startX = firstChunkIndex * ChunkSize + chunkIndex * ChunkSize;
                if (!loadedTerrainChunks.ContainsKey(startX))
                    GenerateChunks(startX, ChunkSize);
            });

            // Unload and render after all chunks are generated
            UnloadUnusedChunks();
            RenderAllChunks();


            UpdateTerrainDisplay();
        }





        private void GenerateChunks(int startX, int width)
        {
            GenerateDynamicChunk(startX, width);
            GenerateStaticChunk(startX, width, true);  // Sky
            GenerateStaticChunk(startX, width, false); // Ground
        }

        private void GenerateStaticChunk(int startX, int width, bool isSky)
        {
            uint color = isSky ? SkyColorArgb : GroundColorArgb;
            var chunk = new StaticChunk(startX, width, color, isSky);

            int key = (startX << 1) | (isSky ? 1 : 0); // Unique key
            lock (loadedStaticChunks) { loadedStaticChunks[key] = chunk; };
        }

        private void GenerateDynamicChunk(int startX, int width)
        {
            TerrainChunk chunk = new TerrainChunk(startX, width, StaticChunkHeight);
            for (int x = chunk.StartX; x < chunk.StartX + chunk.Width; x++)
            {
                if (x > currentWidth-1||x<0) continue;
                int localX = x - chunk.StartX; // Calculate local x within the chunk
                double terrainHeight = columnHeights[x];
                double localWaterY = waterDepthLUT[x];
                uint[] gradientMap = GenerateColorLUT(terrainHeight, x, localWaterY);
                for (int y = 0; y < chunk.Height; y++)
                {
                    if (y > gradientMap.Length - 1) continue;
                    uint color = GroundColorArgb;
                    if (y < terrainHeight)
                    {
                        if (y > localWaterY)
                            color = WaterLut[(int)Math.Min(y - localWaterY, 50)];
                        else
                            color = SkyLut[y];
                    }
                    else
                    {
                        if (y < terrainHeight - DirtDepth - 5 && y > terrainHeight * 1.5)
                            color = GroundColorArgb;
                        else
                            color = gradientMap[y];
                    }
                    // Use localX instead of x here
                    chunk.Pixels[y, localX] = color; // Fix: localX instead of x
                }
                // Use localX here as well
                chunk.Heights[localX] = terrainHeight; // Fix: localX instead of x
            }
            lock (loadedTerrainChunks)
            {
                loadedTerrainChunks[chunk.StartX] = chunk;
            }
        }

        private void RenderAllChunks()
        {
            lock (loadedTerrainChunks)
            {
                foreach (var chunk in loadedTerrainChunks)
                    RenderDynamicChunk(chunk.Value);
            }
            lock (loadedStaticChunks)
            {
                foreach (var chunk in loadedStaticChunks)
                    RenderStaticChunk(chunk.Value);
            }
        }

        private void RenderDynamicChunk(TerrainChunk chunk)
        {
            lock (renderLock)
            {
                // Parallelize x-loop
                Parallel.For(0, chunk.Width, x =>
                {
                    int globalX = chunk.StartX + x;
                    int viewportX = globalX - (int)offsetX;
                    if (viewportX < 0 || viewportX >= currentWidth) return;
                    int baseIndex = viewportX;
                    for (int y = 0; y < currentHeight; y++)
                    {
                        int chunkY = y + (int)offsetY;
                        if (chunkY >= 0 && chunkY < chunk.Height)
                        {
                            _pixelBuffer[baseIndex] = chunk.Pixels[chunkY, x];
                        }
                        baseIndex += currentWidth;
                    }
                });
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
                        if (chunk.IsSky)
                        {
                            // Only render sky chunks above the terrain
                            int chunkY = y + (int)offsetY;
                            if (chunkY < 0)
                            {
                                _pixelBuffer[baseIndex] = chunk.Color;
                            }
                        }
                        else
                        {
                            // Only render ground chunks below the terrain
                            int chunkY = y + (int)offsetY;
                            if (chunkY >= StaticChunkHeight)
                            {
                                _pixelBuffer[baseIndex] = chunk.Color;
                            }
                        }
                        baseIndex += currentWidth;
                    }
                }
            }
        }

        private void UnloadUnusedChunks()
        {
            List<int> terrainKeysToRemove = new();
            lock (loadedTerrainChunks)
            {
                foreach (var key in loadedTerrainChunks.Keys.ToList())
                {
                    if (IsChunkOutsideViewport(key)) terrainKeysToRemove.Add(key);
                }
                foreach (var key in terrainKeysToRemove) loadedTerrainChunks.Remove(key);
            }

            List<int> staticKeysToRemove = new();
            lock (loadedStaticChunks)
            {
                foreach (var key in loadedStaticChunks.Keys.ToList())
                {
                    if (IsChunkOutsideViewport(key)) staticKeysToRemove.Add(key);
                }
                foreach (var key in staticKeysToRemove) loadedStaticChunks.Remove(key);
            }
        }

        private bool IsChunkOutsideViewport(int startX)
        {
            int viewportStartX = (int)(offsetX - ChunkSize);
            int viewportEndX = (int)(offsetX + currentWidth + ChunkSize);
            int unloadThreshold = ChunkSize * 4;

            return startX > viewportEndX + unloadThreshold ||
                   startX + ChunkSize < viewportStartX - unloadThreshold;
        }


        private void UnloadAllChunks()
        {
            loadedStaticChunks.Clear();
            loadedTerrainChunks.Clear();
        }



    }

}