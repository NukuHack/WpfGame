﻿using System;

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
using System.Diagnostics.Eventing.Reader;


namespace VoidVenture
{
    public class Player
    {
        public double X { get; set; } // Current position
        public double Y { get; set; }
        public double TargetX { get; set; } // Target position
        public double TargetY { get; set; }
        public double Speed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Rotation { get; set; } = 0; // New property for rotation
        public bool isOnGround { get; set; } = false;


        // Image and collision properties
        public BitmapSource Image { get; set; }
        public MainWindow window { get; set; }
        public WriteableBitmap WriteableBitmap { get; set; }
        public List<Point> OctagonPoints { get; set; }
        public (int Y, int MinX, int MaxX) TopEdge { get; set; }
        public (int Y, int MinX, int MaxX) BottomEdge { get; set; }
        public (int X, int MinY, int MaxY) LeftEdge { get; set; }
        public (int X, int MinY, int MaxY) RightEdge { get; set; }

        public Player(MainWindow window, BitmapImage image, double x, double y, double speed, int width, int height)
        {
            X = TargetX = x;
            Y = TargetY = y;
            Speed = speed;
            Width = width;
            Height = height;
            this.window = window;

            Image = image;

            WriteableBitmap = new WriteableBitmap(
                image.PixelWidth,
                image.PixelHeight,
                image.DpiX,
                image.DpiY,
                PixelFormats.Pbgra32,
                null);

            int stride = Image.PixelWidth * 4;
            byte[] pixels = new byte[Image.PixelHeight * stride];
            image.CopyPixels(pixels, stride, 0);
            WriteableBitmap.WritePixels(new Int32Rect(0, 0, Image.PixelWidth, Image.PixelHeight), pixels, stride, 0);

            CalculateCollisionGeometry();
        }

        public void SetTargetPosition(Direction direction, double canvasWidth, double canvasHeight, List<Rect> collidableTiles)
        {
            double nextTargetX = TargetX;
            double nextTargetY = TargetY;

            switch (direction)
            {
                case Direction.Up: nextTargetY -= Speed; break;
                case Direction.Down: nextTargetY += Speed; break;
                case Direction.Left: nextTargetX -= Speed; break;
                case Direction.Right: nextTargetX += Speed; break;
            }

            nextTargetX = Math2.Clamp(nextTargetX, 0, canvasWidth - Width);
            nextTargetY = Math2.Clamp(nextTargetY, 0, canvasHeight - Height);

            Rect newPlayerBounds = new Rect(nextTargetX, nextTargetY, Width, Height);

            if (!CheckCollision(newPlayerBounds, collidableTiles))
            {
                TargetX = nextTargetX;
                TargetY = nextTargetY;
            }
        }


        public void CalculateCollisionGeometry()
        {
            int stride = WriteableBitmap.BackBufferStride;
            byte[] pixels = new byte[Image.PixelHeight * stride];
            WriteableBitmap.CopyPixels(pixels, stride, 0);

            TopEdge = FindTopEdge(pixels, Image.PixelWidth, Image.PixelHeight, stride);
            BottomEdge = FindBottomEdge(pixels, Image.PixelWidth, Image.PixelHeight, stride);
            LeftEdge = FindLeftEdge(pixels, Image.PixelWidth, Image.PixelHeight, stride);
            RightEdge = FindRightEdge(pixels, Image.PixelWidth, Image.PixelHeight, stride);

            // Calculate scale factors based on player size vs image size
            double scaleX = (double)Width / Image.PixelWidth;
            double scaleY = (double)Height / Image.PixelHeight;

            OctagonPoints = new List<Point>
            {
                new Point(TopEdge.MinX * scaleX, TopEdge.Y * scaleY),
                new Point(TopEdge.MaxX * scaleX, TopEdge.Y * scaleY),
                new Point(RightEdge.X * scaleX, RightEdge.MinY * scaleY),
                new Point(RightEdge.X * scaleX, RightEdge.MaxY * scaleY),
                new Point(BottomEdge.MaxX * scaleX, BottomEdge.Y * scaleY),
                new Point(BottomEdge.MinX * scaleX, BottomEdge.Y * scaleY),
                new Point(LeftEdge.X * scaleX, LeftEdge.MaxY * scaleY),
                new Point(LeftEdge.X * scaleX, LeftEdge.MinY * scaleY)
            };
        }


        public void ApplyGravity(double gravity)
        {
            // Apply gravity to TargetY
            if (!isOnGround)
            {
                TargetY += gravity;
            }
        }

        public void UpdatePosition(List<Rect> collidableTiles)
        {
            // Predict the next position
            double nextX = X + (TargetX - X) * 0.1; // Smoothing factor
            double nextY = Y + (TargetY - Y) * 0.1;

            Rect newPlayerBounds = new Rect(nextX, nextY, Width, Height);

            // Check collision before updating the position
            if (!CheckCollision(newPlayerBounds, collidableTiles))
            {
                isOnGround = false;
                X = nextX;
                Y = nextY;
            }
            else
            {
                // If collision detected, stop movement in that direction
                TargetX = X;
                TargetY = Y;
            }
        }

        public bool CheckCollision(Rect playerBounds, List<Rect> collidableTiles)
        {
            if (!window.DOUseNoise)
            {
                foreach (Rect tile in collidableTiles)
                {
                    if (playerBounds.IntersectsWith(tile))
                    {
                        if (playerBounds.Bottom == tile.Top + 0.1)
                            isOnGround = true;
                        return true; // Collision detected
                    }
                }
                return false;
            }
            else
            {
                if (window.columnHeights == null || window.columnHeights.Length == 0)
                    return false;

                foreach (var point in OctagonPoints)
                {
                    double worldX = X + point.X;
                    double worldY = Y + point.Y;
                    int x = (int)worldX; // Convert to integer column index

                    // Check if out of terrain bounds
                    if (x < 0 || x >= window.columnHeights.Length)
                        return true;

                    // Collision occurs if the player's Y is below the terrain's height at this X
                    if (worldY > window.columnHeights[x])
                        return true;
                }
                return false;
            }
        }


        public (int y, int minX, int maxX) FindTopEdge(byte[] pixels, int width, int height, int stride)
        {
            for (int y = 0; y < height; y++)
            {
                (int minX, int maxX) = GetRowMinMax(pixels, width, stride, y);
                if (minX != -1) return (y, minX, maxX);
            }
            return (-1, -1, -1);
        }

        public (int y, int minX, int maxX) FindBottomEdge(byte[] pixels, int width, int height, int stride)
        {
            for (int y = height - 1; y >= 0; y--)
            {
                (int minX, int maxX) = GetRowMinMax(pixels, width, stride, y);
                if (minX != -1) return (y, minX, maxX);
            }
            return (-1, -1, -1);
        }

        public (int x, int minY, int maxY) FindLeftEdge(byte[] pixels, int width, int height, int stride)
        {
            for (int x = 0; x < width; x++)
            {
                (int minY, int maxY) = GetColumnMinMax(pixels, height, stride, x);
                if (minY != -1) return (x, minY, maxY);
            }
            return (-1, -1, -1);
        }

        public (int x, int minY, int maxY) FindRightEdge(byte[] pixels, int width, int height, int stride)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                (int minY, int maxY) = GetColumnMinMax(pixels, height, stride, x);
                if (minY != -1) return (x, minY, maxY);
            }
            return (-1, -1, -1);
        }

        public (int minX, int maxX) GetRowMinMax(byte[] pixels, int width, int stride, int y)
        {
            int minX = -1, maxX = -1;
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * stride + x * 4 + 3] > 0)
                {
                    if (minX == -1 || x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
            }
            return (minX, maxX);
        }

        public (int minY, int maxY) GetColumnMinMax(byte[] pixels, int height, int stride, int x)
        {
            int minY = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                if (pixels[y * stride + x * 4 + 3] > 0)
                {
                    if (minY == -1 || y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            return (minY, maxY);
        }



        public void UpdateRotation(Direction direction)
        {
            // Determine rotation angle based on movement direction
            switch (direction)
            {
                case Direction.Left:
                    Rotation = 180; // Rotate 180 degrees
                    break;
                case Direction.Right:
                    Rotation = 0; // No rotation
                    break;
                case Direction.Up:
                    Rotation = 270; // Rotate 270 degrees (facing up)
                    break;
                case Direction.Down:
                    Rotation = 90; // Rotate 90 degrees (facing down)
                    break;
                default:
                    Rotation = 0; // Reset rotation if no movement
                    break;
            }
        }
    }


    public partial class MainWindow : System.Windows.Window
    {
        public Player player;
        public MatrixTransform playerTransform; // Use MatrixTransform instead of TransformGroup
        public System.Windows.Controls.Image playerImage;

        public readonly double _gravity = 5; // gravity by default
        public double gravity = 5; // gravity what is chaged by outside stuffs like the hover mode





        public void UpdatePlayerPosition()
        {
            if (playerTransform != null && player != null)
            {
                double offsetX = 0, offsetY = 0;

                switch (player.Rotation)
                {
                    case 90:  // Facing down
                        offsetX = (player.Height - player.Width) / 2;
                        offsetY = + player.Height/ 2 - player.Width / 2;
                        break;
                    case 270: // Facing up
                        offsetX = (player.Height - player.Width) / 2;
                        offsetY = - player.Height/2 + player.Width/2;
                        break;
                    case 180: // Facing left
                        offsetX = player.Width - player.Height / 2;
                        offsetY = 0;
                        break;
                    case 0:   // Facing right (default)
                    default:
                        offsetX = 0;
                        offsetY = 0;
                        break;
                }

                // Create a matrix for combined translation and rotation
                Matrix matrix = new Matrix();

                // Apply rotation around the center of the player
                matrix.RotateAt(player.Rotation, player.Width / 2, player.Height / 2);

                // Apply translation
                matrix.Translate(player.X + offsetX, player.Y + offsetY);

                // Set the matrix to the player's transform
                playerTransform.Matrix = matrix;

                GameCanvas.UpdateLayout();
            }
        }

        public void Hover(bool ease)
        {
            if (ease)
                gravity = _gravity * 0.25;
            else
                gravity = _gravity;
        }


        public void ReLoadImage()
        {
            var openFileDialog = new OpenFileDialog { Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                PlayerImageInitialize(openFileDialog.FileName, true);
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(openFileDialog.FileName, UriKind.RelativeOrAbsolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                bitmapImage.EndInit();
                player.Image = bitmapImage;

            }
        }

        public void AddPlayerDynamically(string PlayerImagePath)
        {
            if (GameCanvas == null || GameCanvas.ActualWidth <= 0 || GameCanvas.ActualHeight <= 0)
            {
                MessageBox.Show("GameCanvas is not ready. Cannot add player.", "Error");
                return;
            }

            try
            {
                if (DOSelectPlayerManually)
                {

                    var openFileDialog = new OpenFileDialog { Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*" };
                    if (openFileDialog.ShowDialog() == true)
                        PlayerImagePath = openFileDialog.FileName;

                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(PlayerImagePath, UriKind.RelativeOrAbsolute);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                    bitmapImage.EndInit();
                    player = new Player(this, bitmapImage, GameCanvas.ActualWidth * 0.5, GameCanvas.ActualHeight * 0.2, 20, 46, 84);
                    //playerImage.Source = player.Image;

                }
                else
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(PlayerImagePath, UriKind.RelativeOrAbsolute);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                    bitmapImage.EndInit();
                    player = new Player(this, bitmapImage, GameCanvas.ActualWidth* 0.5, GameCanvas.ActualHeight * 0.2, 20, 46, 84);
                    //playerImage.Source = player.Image;

                }

                PlayerImageInitialize(PlayerImagePath);

                // Create the matrix transform for combined translation and rotation
                playerTransform = new MatrixTransform();
                playerImage.RenderTransform = playerTransform;

                // Add the player image to the canvas
                GameCanvas.Children.Add(playerImage);

                //Console.WriteLine($"Player added successfully. Position: X={player.X}, Y={player.Y}");
            }
            catch (Exception ex)
            {
                ErrorMessage(ex, "Failed to load player image");
            }
        }

        public void PlayerImageInitialize(string playerImageUri, bool doReplace = false)
        {
            if (!DORecolorPlayer)
            {
                // Load the player image
                BitmapImage playerImageSource = new BitmapImage();
                playerImageSource.BeginInit();
                playerImageSource.UriSource = new Uri(playerImageUri, UriKind.RelativeOrAbsolute);
                playerImageSource.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                playerImageSource.EndInit();
                if (!doReplace)
                    // Create the player image element
                    playerImage = new System.Windows.Controls.Image
                    {
                        Source = playerImageSource,
                        Width = player.Width,
                        Height = player.Height,
                        RenderTransformOrigin = new Point(0, 0) // Set origin for rotation
                    };
                else
                    playerImage.Source = playerImageSource;
            }
            else
            {
                // Create the recolored image
                var playerRecolored = RecolorImage(playerImageUri);

                if (!doReplace)
                    // Create the player image element
                    playerImage = new System.Windows.Controls.Image
                    {
                        Source = playerRecolored,
                        Width = player.Width,
                        Height = player.Height,
                        RenderTransformOrigin = new Point(0, 0), // Set origin for rotation
                    };
                else
                    playerImage.Source = playerRecolored;

            }
        }


        public void Player_RePos()
        {
            if (player != null)
            {
                player.X = player.TargetX = GameCanvas.ActualWidth * 0.5; // Reset target position
                player.Y = player.TargetY = GameCanvas.ActualHeight * 0.2;
                UpdatePlayerPosition();
            }
        }



    }
}
