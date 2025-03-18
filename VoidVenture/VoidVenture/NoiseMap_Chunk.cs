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
        public uint[,] PixelData { get; }

        public TerrainChunk(int startX, int startY, int width, int height)
        {
            StartX = startX;
            StartY = startY;
            Width = width;
            Height = height;
            PixelData = new uint[height, width];
        }
    }

    public class StaticChunk
    {
        public int StartX { get; }
        public int StartY { get; }
        public int Width { get; }
        public int Height { get; }
        public uint Color { get; }

        public StaticChunk(int startX, int startY, int width, int height, uint color)
        {
            StartX = startX;
            StartY = startY;
            Width = width;
            Height = height;
            Color = color;
        }
    }



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {

        private int ChunkSize = 256;
        private int RenderBuffer = 2;
        private Dictionary<(int, int), object> _chunks = new();
        private object _renderLock = new();


        public void RenderTerrainChunks()
        {
            PrecomputeGradientColors2();
            UnloadOutOfRangeChunks();
            BeginBitmapRender();
            ComputeNoiseValues();

            var viewport = CalculateViewportBounds();
            var chunkRange = CalculateChunkRange(viewport);

            Parallel.For(chunkRange.Item1, chunkRange.Item2 + 1, xChunk =>
            {
                int startX = xChunk * ChunkSize;
                GenerateChunk(startX, 0, ChunkSize, (int)ActualHeight);
                RenderChunk(startX, 0);
            });

            _terrainBitmap.WritePixels(
                new Int32Rect(0, 0, (int)ActualWidth, (int)ActualHeight),
                _pixelBuffer,
                (int)ActualWidth * 4,
                0);
        }

        private void GenerateChunk(int startX, int startY, int width, int height)
        {
            var key = (startX, startY);
            if (_chunks.ContainsKey(key)) return;

            if (IsStaticArea(startX, startY))
            {
                _chunks[key] = new StaticChunk(
                    startX,
                    startY,
                    width,
                    height,
                    GetStaticColor(startX, startY));
            }
            else
            {
                var chunk = new TerrainChunk(startX, startY, width, height);
                GenerateTerrain(chunk);
                _chunks[key] = chunk;
            }
        }

        private void RenderChunk(int startX, int startY)
        {
            if (!_chunks.TryGetValue((startX, startY), out var chunk)) return;

            lock (_renderLock)
            {
                switch (chunk)
                {
                    case TerrainChunk tc:
                        RenderTerrainChunk(tc);
                        break;
                    case StaticChunk sc:
                        RenderStaticChunk(sc);
                        break;
                }
            }
        }

        private void RenderTerrainChunk(TerrainChunk chunk)
        {
            var viewport = CalculateViewportBounds();
            int startX = Math.Max(chunk.StartX, viewport.Item1);
            int endX = Math.Min(chunk.StartX + chunk.Width, viewport.Item2);
            int startY = Math.Max(chunk.StartY, viewport.Item3);
            int endY = Math.Min(chunk.StartY + chunk.Height, viewport.Item4);

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    if (IsInView(x, y))
                    {
                        int bufferIndex = GetBufferIndex(x, y);
                        _pixelBuffer[bufferIndex] = chunk.PixelData[y - chunk.StartY, x - chunk.StartX];
                    }
                }
            }
        }

        private void RenderStaticChunk(StaticChunk chunk)
        {
            var viewport = CalculateViewportBounds();
            int startX = Math.Max(chunk.StartX, viewport.Item1);
            int endX = Math.Min(chunk.StartX + chunk.Width, viewport.Item2);
            int startY = Math.Max(chunk.StartY, viewport.Item3);
            int endY = Math.Min(chunk.StartY + chunk.Height, viewport.Item4);

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    if (IsInView(x, y))
                    {
                        int bufferIndex = GetBufferIndex(x, y);
                        _pixelBuffer[bufferIndex] = chunk.Color;
                    }
                }
            }
        }

        private (int, int, int, int) CalculateViewportBounds()
        {
            int left = (int)(offsetX - RenderBuffer * ChunkSize * Scale);
            int right = (int)(offsetX + ActualWidth * Scale + RenderBuffer * ChunkSize * Scale);
            int top = (int)(offsetY - RenderBuffer * ChunkSize * Scale);
            int bottom = (int)(offsetY + ActualHeight * Scale + RenderBuffer * ChunkSize * Scale);
            return (left, right, top, bottom);
        }

        private (int, int) CalculateChunkRange((int, int, int, int) viewport)
        {
            int firstChunkX = viewport.Item1 / ChunkSize;
            int lastChunkX = (viewport.Item2 - 1) / ChunkSize;
            return (firstChunkX, lastChunkX);
        }

        private bool IsStaticArea(int startX, int startY)
        {
            var viewport = CalculateViewportBounds();
            return startX < viewport.Item1 - ChunkSize ||
                   startX > viewport.Item2 + ChunkSize ||
                   startY < viewport.Item3 - ChunkSize ||
                   startY > viewport.Item4 + ChunkSize;
        }

        private uint GetStaticColor(int x, int y)
        {
            return y < offsetY ? skyColorArgb : undergroundColorArgb;
        }

        private bool IsInView(int x, int y)
        {
            var viewport = CalculateViewportBounds();
            return x >= viewport.Item1 && x < viewport.Item2 &&
                   y >= viewport.Item3 && y < viewport.Item4;
        }

        private int GetBufferIndex(int x, int y)
        {
            int localX = (int)((x - offsetX) / Scale);
            int localY = (int)((y - offsetY) / Scale);
            return localY * (int)ActualWidth + localX;
        }

        public void UnloadAllChunks()
        {
            _chunks.Clear();
        }

        private void UnloadOutOfRangeChunks()
        {
            var viewport = CalculateViewportBounds();
            var removalList = new List<(int, int)>();

            foreach (var (x, y) in _chunks.Keys)
            {
                if (x < viewport.Item1 - ChunkSize * RenderBuffer ||
                    x > viewport.Item2 + ChunkSize * RenderBuffer ||
                    y < viewport.Item3 - ChunkSize * RenderBuffer ||
                    y > viewport.Item4 + ChunkSize * RenderBuffer)
                {
                    removalList.Add((x, y));
                }
            }

            foreach (var key in removalList)
            {
                _chunks.Remove(key);
            }
        }





        private void PrecomputeGradientColors2()
        {
            // Sky gradient
            SkyLut = new uint[currentHeight];
            double invHeight = 1.0 / currentHeight;

            for (int y = 0; y < currentHeight; y++)
            {
                double factor = Math.Pow(1 - y * invHeight, 0.8);
                byte r = (byte)(SkyColor.R * factor);
                byte g = (byte)(SkyColor.G * factor);
                byte b = (byte)(SkyColor.B * factor);
                SkyLut[y] = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
            }

            // Water gradient
            WaterLut = new uint[51]; // 0-50 depth
            for (int d = 0; d <= 50; d++)
            {
                double ratio = d / 50.0;
                byte r = (byte)(WaterColor.R + (WaterDeepColor.R - WaterColor.R) * ratio);
                byte g = (byte)(WaterColor.G + (WaterDeepColor.G - WaterColor.G) * ratio);
                byte b = (byte)(WaterColor.B + (WaterDeepColor.B - WaterColor.B) * ratio);
                WaterLut[d] = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
            }
        }

        private uint[] GenerateColorLUT2(double terrainHeight, int globalX, double waterY)
        {
            uint[] lut = new uint[currentHeight];
            double invGrass = 1.0 / (gradientConfig.GrassDepth * Scale);
            double invDirt = 1.0 / ((gradientConfig.DirtDepth - gradientConfig.GrassDepth) * Scale);
            double invStone = 1.0 / ((currentHeight - gradientConfig.DirtDepth) * Scale);

            int waterThreshold = (int)(waterY - 5);

            for (int y = 0; y < currentHeight; y++)
            {
                double delta = y - terrainHeight;
                bool isUnderwater = y > waterThreshold;

                // Layer blending factors
                double grassFactor = Math.Clamp(delta * invGrass, 0, 1);
                double dirtFactor = Math.Clamp((delta - gradientConfig.GrassDepth) * invDirt, 0, 1);
                double stoneFactor = Math.Clamp((delta - gradientConfig.DirtDepth) * invStone, 0, 1);

                // Base color selection
                Color baseColor = isUnderwater
                    ? GetWaterColor(y, waterThreshold)
                    : GetSkyColor(y);

                // Color blending calculation
                byte r = (byte)(
                    baseColor.R * (1 - grassFactor) +
                    gradientConfig.DirtColor.R * grassFactor * (1 - dirtFactor) +
                    gradientConfig.StoneColor.R * dirtFactor * (1 - stoneFactor) +
                    gradientConfig.StoneColor.R * stoneFactor);

                byte g = (byte)(
                    baseColor.G * (1 - grassFactor) +
                    gradientConfig.DirtColor.G * grassFactor * (1 - dirtFactor) +
                    gradientConfig.StoneColor.G * dirtFactor * (1 - stoneFactor) +
                    gradientConfig.StoneColor.G * stoneFactor);

                byte b = (byte)(
                    baseColor.B * (1 - grassFactor) +
                    gradientConfig.DirtColor.B * grassFactor * (1 - dirtFactor) +
                    gradientConfig.StoneColor.B * dirtFactor * (1 - stoneFactor) +
                    gradientConfig.StoneColor.B * stoneFactor);

                lut[y] = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
            }

            return lut;
        }

        private Color GetSkyColor(int y)
        {
            return Color.FromArgb(255,
                (byte)(SkyLut[y] >> 16),
                (byte)(SkyLut[y] >> 8),
                (byte)(SkyLut[y]));
        }

        private Color GetWaterColor(int y, int waterThreshold)
        {
            int depth = Math.Max(0, Math.Min(50, y - waterThreshold));
            return Color.FromArgb(255,
                (byte)(WaterLut[depth] >> 16),
                (byte)(WaterLut[depth] >> 8),
                (byte)(WaterLut[depth]));
        }

        private void GenerateTerrain(TerrainChunk chunk)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                int globalX = chunk.StartX + x;

                // Boundary check
                if (globalX < 0 || globalX >= columnHeights.Length)
                {
                    Array.Clear(chunk.PixelData, x * chunk.Height, chunk.Height);
                    continue;
                }

                // Get precomputed values
                double terrainHeight = columnHeights[globalX];
                double waterY = waterDepthLUT[globalX];

                // Generate color LUT for this column
                uint[] colorLut = GenerateColorLUT2(terrainHeight, globalX, waterY);

                // Populate chunk pixels
                for (int y = 0; y < chunk.Height; y++)
                {
                    chunk.PixelData[y, x] = colorLut[y];
                }
            }
        }



    }


}
