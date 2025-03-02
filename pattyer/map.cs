﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;


namespace pattyer
{

    public static class TmxParser
    {
        public static GameMap Parse(string filePath)
        {
            XDocument doc = XDocument.Load(filePath);
            XElement mapElement = doc.Root;

            var tilesetElement = mapElement.Element("tileset");
            string tilesetSource = tilesetElement?.Attribute("source")?.Value;

            var mapData = new GameMap
            {
                MapWidth = int.Parse(mapElement.Attribute("width").Value),
                MapHeight = int.Parse(mapElement.Attribute("height").Value),
                BackgroundImagePath = mapElement.Attribute("background")?.Value ?? "",
                Layers = new List<List<int>>()
            };

            if (!string.IsNullOrEmpty(tilesetSource))
            {
                (mapData.TileSetImage, mapData.TileWidth, mapData.TileHeight) = ParseTileset(tilesetSource);
            }

            foreach (var layerElement in mapElement.Elements("layer"))
            {
                var data = layerElement.Element("data")?.Value;
                if (!string.IsNullOrEmpty(data))
                {
                    mapData.Layers.Add(data.Split(',').Select(int.Parse).ToList());
                }
            }

            return mapData;
        }

        private static (BitmapImage, int, int) ParseTileset(string tilesetPath)
        {
            if (!File.Exists(tilesetPath))
            {
                MessageBox.Show($"Tileset file not found: {tilesetPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, 0, 0);
            }

            XDocument doc = XDocument.Load(tilesetPath);
            XElement tilesetElement = doc.Root;
            int tileWidth = int.Parse(tilesetElement.Attribute("tilewidth").Value);
            int tileHeight = int.Parse(tilesetElement.Attribute("tileheight").Value);

            var imageElement = tilesetElement.Element("image");
            string imagePath = imageElement?.Attribute("source")?.Value;

            if (string.IsNullOrEmpty(imagePath)) return (null, 0, 0);

            imagePath = Path.Combine(Path.GetDirectoryName(tilesetPath), imagePath.Replace("../", ""));
            if (!File.Exists(imagePath))
            {
                MessageBox.Show($"Tileset image file not found: {imagePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, 0, 0);
            }

            return (new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute)), tileWidth, tileHeight);
        }
    }

    public class GameMap
    {
        public int MapWidth { get; set; }
        public int MapHeight { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public BitmapImage TileSetImage { get; set; }
        public List<List<int>> Layers { get; set; } = new List<List<int>>();
        public string BackgroundImagePath { get; set; }
    }



    public partial class MainWindow : System.Windows.Window
    {

        public GameMap gameMap;
        public readonly Dictionary<int, CroppedBitmap> tileCache = new Dictionary<int, CroppedBitmap>();


        public void LoadMap(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show("Map file not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                gameMap = TmxParser.Parse(filePath);
                if (gameMap == null || gameMap.TileSetImage == null) return;
                DisplayBackground(gameMap.BackgroundImagePath);
                RedrawMap();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load map: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DisplayBackground(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;
            try
            {
                BackgroundCanvas.Background = new ImageBrush(new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute)))
                {
                    Stretch = Stretch.UniformToFill
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load background image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        public void RedrawMap()
        {
            if (gameMap == null || gameMap.Layers.Count == 0 || gameMap.TileSetImage == null) return;

            MapCanvas.Children.Clear();
            collidableTiles.Clear(); // Reset list before populating

            double scaleX = ActualWidth / (gameMap.MapWidth * gameMap.TileWidth);
            double scaleY = ActualHeight / (gameMap.MapHeight * gameMap.TileHeight);
            double scale = Math.Min(scaleX, scaleY);

            int newTileWidth = (int)(gameMap.TileWidth * scale);
            int newTileHeight = (int)(gameMap.TileHeight * scale);

            int tilesetColumns = gameMap.TileSetImage.PixelWidth / gameMap.TileWidth;

            foreach (var layer in gameMap.Layers)
            {
                for (int i = 0; i < layer.Count; i++)
                {
                    int tileIndex = layer[i];
                    if (tileIndex <= 0) continue;

                    int x = i % gameMap.MapWidth;
                    int y = i / gameMap.MapWidth;

                    var tileBitmap = GetTileBitmap(tileIndex, tilesetColumns);
                    if (tileBitmap == null) continue;

                    var tileImage = new Image
                    {
                        Source = tileBitmap,
                        Width = newTileWidth,
                        Height = newTileHeight,
                        Stretch = Stretch.Uniform
                    };

                    Canvas.SetLeft(tileImage, x * newTileWidth);
                    Canvas.SetTop(tileImage, y * newTileHeight);

                    MapCanvas.Children.Add(tileImage);

                    // Store collidable tile positions
                    if (true)
                    {
                        collidableTiles.Add(new Rect(x * newTileWidth, y * newTileHeight, newTileWidth, newTileHeight));
                    }
                }
            }
        }

        public CroppedBitmap GetTileBitmap(int gid, int tilesetColumns)
        {
            if (tileCache.ContainsKey(gid)) return tileCache[gid];

            int srcX = ((gid - 1) % tilesetColumns) * gameMap.TileWidth;
            int srcY = ((gid - 1) / tilesetColumns) * gameMap.TileHeight;

            var croppedBitmap = new CroppedBitmap(gameMap.TileSetImage, new Int32Rect(srcX, srcY, gameMap.TileWidth, gameMap.TileHeight));
            tileCache[gid] = croppedBitmap;
            return croppedBitmap;
        }

    }
}
