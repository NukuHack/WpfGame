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

        public List<Rect> collidableTiles = new List<Rect>();
        public readonly Dictionary<int, CroppedBitmap> tileCache = new Dictionary<int, CroppedBitmap>();

        public GameMap gameMap;


        public void LoadTileMap(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"Map file not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                gameMap = TmxParser.Parse(filePath);
                if (gameMap == null || gameMap.TileSetImage == null) return;
                DisplayBackgroundFromTile(gameMap.BackgroundImagePath);
                RedrawTileMap();
            }
            catch (Exception ex)
            {
                ErrorMessage(ex, "Failed to load map");
            }
        }

        public void DisplayBackgroundFromTile(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;
            try
            {
                BackgroundCanvas.Background = new ImageBrush(new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute)))
                {
                    Stretch = Stretch.UniformToFill
                };
                if (DORecolorBackground)
                {
                    var recoloredBitmap = RecolorImage(imagePath);
                    BackgroundCanvas.Background = new ImageBrush(recoloredBitmap)
                    {
                        Stretch = Stretch.UniformToFill
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load background image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public void RedrawTileMap()
        {
            if (gameMap == null || gameMap.Layers.Count == 0 || gameMap.TileSetImage == null) return;

            MapCanvas.Children.Clear();
            collidableTiles.Clear();

            // Calculate scale to fit within the canvas
            double scaleX = ActualWidth / (gameMap.MapWidth * gameMap.TileWidth);
            double scaleY = ActualHeight / (gameMap.MapHeight * gameMap.TileHeight);
            double scale = Math.Min(scaleX, scaleY);

            // Calculate scaled tile dimensions
            int newTileWidth = (int)(gameMap.TileWidth * scale);
            int newTileHeight = (int)(gameMap.TileHeight * scale);

            // Calculate total scaled map dimensions
            double scaledMapWidth = gameMap.MapWidth * newTileWidth;
            double scaledMapHeight = gameMap.MapHeight * newTileHeight;

            // Calculate centering offset
            double offsetX = (ActualWidth - scaledMapWidth) / 2;
            double offsetY = (ActualHeight - scaledMapHeight) / 2;

            int tilesetColumns = gameMap.TileSetImage.PixelWidth / gameMap.TileWidth;

            foreach (var layer in gameMap.Layers)
            {
                for (int i = 0; i < layer.Count; i++)
                {
                    int tileIndex = layer[i];
                    if (tileIndex == 0) continue;

                    // Calculate tile position in map coordinates
                    int tileX = i % gameMap.MapWidth;
                    int tileY = i / gameMap.MapWidth;

                    // Get tile bitmap
                    var tileBitmap = GetTileBitmap(tileIndex, tilesetColumns);
                    if (tileBitmap == null) continue;

                    // Create image element
                    var tileImage = new Image
                    {
                        Source = tileBitmap,
                        Width = newTileWidth,
                        Height = newTileHeight,
                        Stretch = Stretch.Uniform
                    };

                    // Position the tile with centering offset
                    Canvas.SetLeft(tileImage, tileX * newTileWidth + offsetX);
                    Canvas.SetTop(tileImage, tileY * newTileHeight + offsetY);

                    // Add to canvas
                    MapCanvas.Children.Add(tileImage);

                    // Store collidable tiles with proper coordinates
                    collidableTiles.Add(new Rect(
                        tileX * newTileWidth + offsetX,
                        tileY * newTileHeight + offsetY,
                        newTileWidth,
                        newTileHeight));
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