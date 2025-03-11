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
using System.Diagnostics.Eventing.Reader;
using static System.Net.Mime.MediaTypeNames;


namespace VoidVenture
{

    public class Player
    {

        // Position & Movement
        public double X { get; set; }
        public double Y { get; set; }
        public Vector Velocity { get; set; } = new Vector(0, 0);
        public double Speed { get; set; }
        public bool isOnGround { get; set; } = false;
        public double Rotation { get; set; } = 0;
        public double friction { get; set; } = 0.85;
        

        // Dimensions
        public int Width { get; set; }
        public int Height { get; set; }

        // Collision
        private Rect CollisionBounds => new Rect(X, Y, Width, Height);
        private readonly List<Rect> _collidableTiles;

        // Visuals
        public BitmapSource Image { get; set; }
        private readonly WriteableBitmap _writeableBitmap;
        private readonly MainWindow _window;
        public List<Point> OctagonPoints { get; private set; }



        public Player(MainWindow window, BitmapImage image, double x, double y, double speed, int width, int height)
        {
            _window = window;
            X = x;
            Y = y;
            Speed = speed;
            Width = width;
            Height = height;
            Image = image;

            _writeableBitmap = new WriteableBitmap(
                image.PixelWidth,
                image.PixelHeight,
                image.DpiX,
                image.DpiY,
                PixelFormats.Pbgra32,
                null);

            byte[] pixels = new byte[image.PixelHeight * _writeableBitmap.BackBufferStride];
            image.CopyPixels(pixels, _writeableBitmap.BackBufferStride, 0);
            _writeableBitmap.WritePixels(new Int32Rect(0, 0, image.PixelWidth, image.PixelHeight), pixels, _writeableBitmap.BackBufferStride, 0);

            _collidableTiles = new List<Rect>();
            CalculateCollisionGeometry();
        }

        public void Update(double gravity, List<Rect> collidableTiles = null, double[] heightMap = null)
        {
            ApplyGravity(gravity);
            Move();
            HandleCollisions(collidableTiles, heightMap);
            UpdateTransform();
        }

        public void ApplyGravity(double gravity)
        {
            // Apply gravity to TargetY
            if (!isOnGround)
            {
                Velocity = new Vector(Velocity.X, Velocity.Y + gravity);
            }
        }

        private void Move()
        {
            // Horizontal movement
            X += Velocity.X;
            // Vertical movement
            Y += Velocity.Y;

            HandleCollisions();

            // should make this more smooth
            // make bigger jumps if the player moves more
            if (X>= _window.ActualWidth * 0.75)
            {
                _window.MoveOffset(Direction.Right);
                X -= 10;
            }
            else if (X <= _window.ActualWidth * 0.25)
            {
                _window.MoveOffset(Direction.Left);
                X += 10;
            }

            if (Y >= _window.ActualHeight * 0.75)
            {
                _window.MoveOffset(Direction.Down);
                Y -= 10;
            }
            else if (Y <= _window.ActualHeight * 0.25)
            {
                _window.MoveOffset(Direction.Up);
                Y += 10;
            }

            // Apply friction after collision resolution
            Velocity = new Vector(Velocity.X * friction, Velocity.Y * friction);
        }

        private void HandleCollisions(List<Rect> collidableTiles = null, double[] heightMap = null)
        {

            var bounds = CollisionBounds;

            if (!_window.DOUseNoise)
            {
                if (!(collidableTiles is null) && collidableTiles.Count > 0)
                {
                    foreach (var tile in _collidableTiles)
                    {
                        if (!bounds.IntersectsWith(tile)) continue;

                        // -y 
                        if (Velocity.Y > 0 && bounds.Bottom > tile.Top)
                        {
                            Y = tile.Top - Height;
                            Velocity = new Vector(Velocity.X, 0);
                            isOnGround = true;
                        }
                        else if (Velocity.Y < 0 && bounds.Top < tile.Bottom)
                        {
                            Y = tile.Bottom;
                            Velocity = new Vector(Velocity.X, 0);
                        }

                        // -x
                        if (Velocity.X > 0 && bounds.Right > tile.Left)
                        {
                            X = tile.Left - Width;
                            Velocity = new Vector(0, Velocity.Y);
                        }
                        else if (Velocity.X < 0 && bounds.Left < tile.Right)
                        {
                            X = tile.Right;
                            Velocity = new Vector(0, Velocity.Y);
                        }
                    }

                }
                else
                {
                    throw new Exception("Collision list not provided");
                    //return true;
                }
            }
            else // Use heightmap-based terrain collision
                {
                    if (heightMap == null || heightMap.Length == 0)
                        return;

                    double maxPenetration = 0;
                    bool hasCollision = false;

                    foreach (var point in OctagonPoints)
                    {
                        double worldX = X + point.X;
                        double worldY = Y + point.Y;
                        int column = (int)worldX; // Column index in terrain heightmap

                        // Handle out-of-bounds X positions
                        if (column < 0 || column >= heightMap.Length)
                        {
                            // Clamp player to valid terrain bounds
                            X = Math2.Clamp(X, -point.X, heightMap.Length - 1 - point.X);
                            worldX = X + point.X;
                            column = (int)worldX;
                        }

                        double terrainHeight = heightMap[column];

                        // Check for collision (player is below terrain)
                        if (worldY > terrainHeight)
                        {
                            double penetration = worldY - terrainHeight;
                            if (penetration > maxPenetration)
                            {
                                maxPenetration = penetration;
                                hasCollision = true;
                            }
                        }
                    }

                    if (hasCollision)
                    {
                        // Resolve collision by moving player up
                        Y -= maxPenetration;
                        Velocity = new Vector(Velocity.X * friction, Velocity.Y*(1-friction)); // Stop vertical movement
                        isOnGround = true;
                    }
                    else
                    {
                        isOnGround = false; // No ground contact
                    }
                }


            // Update bounds after collision resolution
            bounds = new Rect(X, Y, Width, Height);
        }

        public void SetMovementDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.Left:
                    Velocity = new Vector(Velocity.X - Speed, Velocity.Y);
                    break;
                case Direction.Right:
                    Velocity = new Vector(Velocity.X + Speed, Velocity.Y);
                    break;
                case Direction.Up when isOnGround:
                    Velocity = new Vector(Velocity.X, Velocity.Y - Speed * 3); // Jump impulse
                    isOnGround = false;
                    break;
                case Direction.Up:
                    Velocity = new Vector(Velocity.X, Velocity.Y - Speed); // Jump impulse
                    isOnGround = false;
                    break;
                case Direction.Down:
                    Velocity = new Vector(Velocity.X, Velocity.Y + Speed); // Jump impulse
                    break;
            }
        }


        private void UpdateTransform()
        {
            // Create transformation matrix
            Matrix matrix = new Matrix();

            // Apply rotation around center
            matrix.RotateAt(Rotation, Width / 2, Height / 2);

            // Apply position translation
            matrix.Translate(X, Y);

            // Apply to render transform
            if (_window.playerImage.RenderTransform is MatrixTransform transform)
            {
                transform.Matrix = matrix;
            }

            // Force UI update
            _window.GameCanvas.InvalidateVisual();
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

        private void CalculateCollisionGeometry()
        {
            // Get pixel data from writeable bitmap
            int stride = _writeableBitmap.BackBufferStride;
            byte[] pixels = new byte[_writeableBitmap.PixelHeight * stride];
            _writeableBitmap.CopyPixels(pixels, stride, 0);

            // Calculate scale factors for collision points
            double scaleX = (double)Width / _writeableBitmap.PixelWidth;
            double scaleY = (double)Height / _writeableBitmap.PixelHeight;

            // Find edges in original image coordinates
            var topEdge = _window.FindTopEdge(pixels, _writeableBitmap.PixelWidth,
                                     _writeableBitmap.PixelHeight, stride);
            var bottomEdge = _window.FindBottomEdge(pixels, _writeableBitmap.PixelWidth,
                                           _writeableBitmap.PixelHeight, stride);
            var leftEdge = _window.FindLeftEdge(pixels, _writeableBitmap.PixelWidth,
                                       _writeableBitmap.PixelHeight, stride);
            var rightEdge = _window.FindRightEdge(pixels, _writeableBitmap.PixelWidth,
                                         _writeableBitmap.PixelHeight, stride);

            // Convert to world-space coordinates
            OctagonPoints = new List<Point>
                {
                    new Point(topEdge.minX * scaleX, topEdge.y * scaleY),
                    new Point(topEdge.maxX * scaleX, topEdge.y * scaleY),
                    new Point(rightEdge.x * scaleX, rightEdge.minY * scaleY),
                    new Point(rightEdge.x * scaleX, rightEdge.maxY * scaleY),
                    new Point(bottomEdge.maxX * scaleX, bottomEdge.y * scaleY),
                    new Point(bottomEdge.minX * scaleX, bottomEdge.y * scaleY),
                    new Point(leftEdge.x * scaleX, leftEdge.maxY * scaleY),
                    new Point(leftEdge.x * scaleX, leftEdge.minY * scaleY)
                };
        }




    }


    public partial class MainWindow : System.Windows.Window
    {
        public Player player;
        public System.Windows.Controls.Image playerImage;

        public readonly double _gravity = 0.3; // gravity by default
        private double gravity = 0.3; // gravity what is chaged by outside stuffs like the hover mode

        

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

                player = new Player(this, bitmapImage, player.X, player.Y, 10, 46, 84);
                //playerImage.Source = player.Image;

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
                BitmapImage bitmapImage = new BitmapImage();
                if (DOSelectPlayerManually)
                {

                    var openFileDialog = new OpenFileDialog { Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*" };
                    if (openFileDialog.ShowDialog() == true)
                        PlayerImagePath = openFileDialog.FileName;

                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(PlayerImagePath, UriKind.RelativeOrAbsolute);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                    bitmapImage.EndInit();

                }
                else
                {
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(PlayerImagePath, UriKind.RelativeOrAbsolute);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                    bitmapImage.EndInit();
                }

                player = new Player(this, bitmapImage, GameCanvas.ActualWidth * 0.5, GameCanvas.ActualHeight * 0.3, 10, 46, 84);
                //playerImage.Source = player.Image;

                PlayerImageInitialize(PlayerImagePath);

                // Create the matrix transform for combined translation and rotation
                playerImage.RenderTransform = new MatrixTransform();

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
                player.X = GameCanvas.ActualWidth * 0.5; // Reset target position
                player.Y = GameCanvas.ActualHeight * 0.3;
                player.Velocity = new Vector(0, 0);
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


    }
}
