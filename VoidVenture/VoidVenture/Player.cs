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
using System.Windows.Shapes;
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

    public class Player
    {

        // Position & Movement
        public double X { get; set; }
        public double Y { get; set; }
        public Vector Velocity { get; set; } = new Vector(0, 0);
        public double Speed { get; set; }
        public bool isOnGround { get; set; } = false;
        public double Rotation { get; set; } = 0;
        public double Friction { get; set; } = 0.85;


        // Dimensions
        public readonly double OriginWidth, OriginHeight;
        public double Width, Height;

        // Collision
        public Rect CollisionBounds => new(X, Y, Width, Height);

        // Visuals
        public MainWindow _window;

        private const double BaseScaleFactor = 0.15;


        public Player(MainWindow window, double x, double y, double speed, int width, int height)
        {
            _window = window;
            X = x;
            Y = y;
            Speed = speed;
            OriginWidth = width;
            OriginHeight = height;
            Width = OriginWidth * BaseScaleFactor * _window.Scale;
            Height = OriginHeight * BaseScaleFactor * _window.Scale;
        }

        public void Update(
            double gravity, List<Rect>? collidableTiles,
            double[]? heightMap, bool doCollCheck = true)
        {
            ApplyGravity(gravity);
            Move();
            if (doCollCheck)
                HandleCollisions(collidableTiles, heightMap);
            UpdateTransform();
        }

        public void ApplyGravity(double gravity)
        {
            // Apply gravity to TargetY
            if (!isOnGround)
            {
                Velocity = new Vector(Velocity.X, Velocity.Y + gravity * (_window.Scale));
            }
        }

        private void Move()
        {
            X += Velocity.X; // Horizontal movement
            Y += Velocity.Y; // Vertical movement

            if (!_window.DO.UseNoiseTerrain)
                ClampPosition();
            else
                ClampVelocity();

            if (_window.DO.UseNoiseTerrain)
                HandleEdgeMovement();

            // Apply friction conditionally
            Velocity = new Vector(Velocity.X * Friction, Velocity.Y * Friction);
        }


            private void HandleEdgeMovement()
            {
                double edge = 0.95;
                if (X + Width >= _window.currentWidth * edge)
                {
                    double correction = X + Width - _window.currentWidth * edge;
                    _window.MoveOffset(Direction.Right, correction);
                    X -= correction;
                }
                else if (X <= _window.currentWidth * (1 - edge))
                {
                    double correction = _window.currentWidth * (1 - edge) - X;
                    _window.MoveOffset(Direction.Left, correction);
                    X += correction;
                }

                if (Y + Height >= _window.currentHeight * edge)
                {// double it for bottom (because it looks bad othervise)
                    double correction = Y + Height - _window.currentHeight * edge;
                    _window.MoveOffset(Direction.Down, correction);
                    Y -= correction;
                }
                else if (Y <= _window.currentHeight * (1 - edge))
                {
                    double correction = _window.currentHeight * (1 - edge) - Y;
                    _window.MoveOffset(Direction.Up, correction);
                    Y += correction;
                }
            }

        private void ClampPosition()
        {
            double edge = 0.95;
            X = Math.Clamp(X, _window.currentWidth * (1 - edge), _window.currentWidth* edge);
            Y = Math.Clamp(Y, _window.currentHeight * (1 - edge), _window.currentHeight * edge);
        }
        private void ClampVelocity()
        {
            double edge = 0.95;
            double leftEdge = _window.currentWidth * (1 - edge);
            double rightEdge = _window.currentWidth * edge;
            double maxVelRight = rightEdge - X; // Max rightward velocity
            double maxVelLeft = X - leftEdge;   // Max leftward velocity
            double minY = Y - _window.currentHeight * (1 - edge);
            double maxY = _window.currentHeight * edge - Y;

            Velocity = new Vector(
                Math.Clamp(Velocity.X, -maxVelLeft, maxVelRight), // Allow negative (left) velocity
                Math.Clamp(Velocity.Y, -minY/2, maxY)
            );
        }

        private void HandleCollisions(List<Rect>? collidableTiles, double[]? heightMap)
        {
            if (!_window.DO.UseNoiseTerrain)
            {
                if (collidableTiles == null || collidableTiles.Count == 0)
                {
                    Console.WriteLine("No collidable tiles provided for collision detection.");
                    return;
                }

                ResolveTileCollision(collidableTiles);
            }
            else // Use heightmap-based terrain collision
            {
                if (heightMap == null || heightMap.Length == 0)
                {
                    Console.WriteLine("No heightMap provided for collision detection.");
                    return;
                }

                ResolveHeightmapCollision(heightMap);
            }
        }

        public void ResolveTileCollision(List<Rect> collidableTiles)
        {
            var bounds = CollisionBounds;

            // Reset isOnGround at the start of the frame
            isOnGround = false;

            foreach (var tile in collidableTiles)
            {
                if (!bounds.IntersectsWith(tile)) continue;


                float overlapX = (float)Math.Min(bounds.Right - tile.Left, tile.Right - bounds.Left);
                float overlapY = (float)Math.Min(bounds.Bottom - tile.Top, tile.Bottom - bounds.Top);

                if (overlapX < overlapY)
                {
                    // Horizontal collision
                    if (Velocity.X > 0 && bounds.Right > tile.Left)
                        X = tile.Left - Width;
                    else if (Velocity.X < 0 && bounds.Left < tile.Right)
                        X = tile.Right;
                    Velocity = new Vector(0, Velocity.Y); // Reset X velocity
                }
                else
                {
                    // Vertical collision
                    if (Velocity.Y > 0 && bounds.Bottom > tile.Top)
                    {
                        Y = tile.Top - Height;
                        isOnGround = true;
                    }
                    else if (Velocity.Y < 0 && bounds.Top < tile.Bottom)
                        Y = tile.Bottom;

                    Velocity = new Vector(Velocity.X, 0);
                }
            }
        }


        public void ResolveHeightmapCollision(double[] heightMap)
        {
            // Reset isOnGround at the start of the frame
            isOnGround = false;

            for (int column = (int)X; column < X + Width; column++)
            {
                if (column < 0 || column >= heightMap.Length) continue;

                double terrainHeight = heightMap[column];
                if (Y + Height >= terrainHeight)
                {
                    Y = terrainHeight - Height;
                    Velocity = new Vector(Velocity.X * Friction, 0);
                    isOnGround = true;
                }
            }
        }

        private static readonly Dictionary<Direction, Vector> DirectionVectors =
            new()
            {
                { Direction.Left, new Vector(-1, 0) },
                { Direction.Right, new Vector(1, 0) },
                { Direction.Up, new Vector(0, -2) },
                { Direction.Down, new Vector(0, 1) }
            };

        public void SetMovementDirection(Direction direction)
        {
            if (DirectionVectors.TryGetValue(direction, out Vector velocityAdjustment))
            {

                if (direction == Direction.Up && isOnGround)
                {
                    Velocity = new Vector(Velocity.X, -Speed * 3 * _window.Scale);
                    isOnGround = false;
                    return;
                }
                Velocity = new Vector(
                    Velocity.X + velocityAdjustment.X * Speed * _window.Scale,
                    Velocity.Y + velocityAdjustment.Y * Speed * _window.Scale
                );
            }
            UpdateRotation(direction);
        }


        private static readonly Dictionary<Direction, double> RotationAngles =
            new()
        {
            { Direction.Left, 180 },
            { Direction.Right, 0 },
            { Direction.Up, 270 },
            { Direction.Down, 90 }
        };

        public void UpdateRotation(Direction direction)
        {
            Rotation = RotationAngles.ContainsKey(direction) ? RotationAngles[direction] : 0;
        }

        private void UpdateTransform()
        {
            // Calculate new dimensions based on scale
            this.Width = OriginWidth * BaseScaleFactor * _window.Scale;
            this.Height = OriginHeight * BaseScaleFactor * _window.Scale;

            // Create transformation matrix
            Matrix matrix = new();

            // Apply scaling based on conditions
            if (Rotation >= 180)
            {
                if (_window.DO.UseNoiseTerrain)
                    matrix.ScaleAt(-BaseScaleFactor * _window.Scale, BaseScaleFactor * _window.Scale, Width, 0);
                else
                    matrix.ScaleAt(-1, 1, Width, 0);
            }
            else if (_window.DO.UseNoiseTerrain)
                matrix.ScaleAt(BaseScaleFactor * _window.Scale, BaseScaleFactor * _window.Scale, 0, 0);

            // Apply position translation
            matrix.Translate(X, Y);

            // Apply the transformation to the render transform
            if (_window.playerImage.RenderTransform is MatrixTransform transform)
                transform.Matrix = matrix;

            // Force UI update
            _window.GameCanvas.InvalidateVisual();
        }




    }


    public partial class MainWindow : System.Windows.Window
    {
        public Player player;
        public System.Windows.Controls.Image playerImage;
        public ScaleTransform playerTransform;

        public readonly double _gravity = 0.5; // gravity by default
        private double gravity = 0.5; // gravity what is chaged by outside stuffs like the hover mode



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
                BitmapImage bitmapImage = new();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(openFileDialog.FileName, UriKind.RelativeOrAbsolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                bitmapImage.EndInit();

                player = new Player(player._window, player.X, player.Y, player.Speed, bitmapImage.PixelWidth, bitmapImage.PixelHeight);
                //playerImage.Source = player.Image;


            }
        }


        public void AddPlayerDynamically(byte[] playerImageData)
        {
            if (GameCanvas == null || GameCanvas.ActualWidth <= 0 || GameCanvas.ActualHeight <= 0)
            {
                MessageBox.Show("GameCanvas is not ready. Cannot add player.", "Error");
                return;
            }

            try
            {
                BitmapImage bitmapImage = new();

                // Allow the user to select an image file
                var openFileDialog = new OpenFileDialog { Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*" };
                if (DO.SelectPlayerManually && openFileDialog.ShowDialog() == true)
                {
                    var PlayerImagePath = openFileDialog.FileName;
                    // Load the image from the selected file
                    bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(PlayerImagePath, UriKind.RelativeOrAbsolute);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                    bitmapImage.EndInit();
                }
                else
                {
                    // Use the provided byte array to create the BitmapImage
                    if (playerImageData == null || playerImageData.Length == 0)
                    {
                        throw new ArgumentException("Player image data is null or empty.");
                    }

                    bitmapImage = ConvertByteArrayToBitmapImage(playerImageData);
                }

                // Initialize the player object with the loaded image
                player = new Player(this, GameCanvas.ActualWidth * 0.5, GameCanvas.ActualHeight * 0.3, 1, bitmapImage.PixelWidth, bitmapImage.PixelHeight);

                // Initialize the player image in the UI
                PlayerImageInitialize(playerImageData);

                // Create the matrix transform for combined translation, rotation, and scale
                playerImage.RenderTransform = new MatrixTransform();

                // Add the player image to the canvas
                GameCanvas.Children.Add(playerImage);
            }
            catch (Exception ex)
            {
                ErrorMessage(ex, "Failed to load player image");
            }
        }



        public void PlayerImageInitialize(byte[] playerImageRaw, bool doReplace = false)
        {
            if (playerImageRaw == null || playerImageRaw.Length == 0)
            {
                throw new ArgumentException("Player image data is null or empty.");
            }

            if (!DO.RecolorPlayer)
            {
                // Load the player image from the raw byte array
                BitmapImage bitmapImage = ConvertByteArrayToBitmapImage(playerImageRaw);

                if (!doReplace)
                {
                    // Create a new Image control if not replacing
                    playerImage = new System.Windows.Controls.Image
                    {
                        Source = bitmapImage,
                        Width = player.OriginWidth,
                        Height = player.OriginHeight,
                        RenderTransformOrigin = new System.Windows.Point(0, 0)
                    };
                }
                else
                {
                    // Replace the source of the existing Image control
                    playerImage.Source = bitmapImage;
                }
            }
            else
            {
                // Recolor the image
                var playerRecolored = RecolorImage(playerImageRaw);

                if (!doReplace)
                {
                    // Create a new Image control if not replacing
                    playerImage = new System.Windows.Controls.Image
                    {
                        Source = playerRecolored,
                        Width = player.OriginWidth,
                        Height = player.OriginHeight,
                        RenderTransformOrigin = new System.Windows.Point(0, 0)
                    };
                }
            }
        }


        public void PlayerImageInitialize(string playerImageUri, bool doReplace = false)
        {
            if (!DO.RecolorPlayer)
            {
                // Load the player image
                BitmapImage playerImageSource = new();
                playerImageSource.BeginInit();
                playerImageSource.UriSource = new Uri(playerImageUri, UriKind.RelativeOrAbsolute);
                playerImageSource.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                playerImageSource.EndInit();
                if (!doReplace)
                    // Create the player image element
                    playerImage = new System.Windows.Controls.Image
                    {
                        Source = playerImageSource,
                        Width = player.OriginWidth,
                        Height = player.OriginHeight,
                        RenderTransformOrigin = new System.Windows.Point(0, 0)
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
                        Width = player.OriginWidth,
                        Height = player.OriginHeight,
                        RenderTransformOrigin = new System.Windows.Point(0, 0)
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




    }
}