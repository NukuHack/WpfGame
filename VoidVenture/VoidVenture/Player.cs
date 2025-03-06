using System;

using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
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


namespace VoidVenture
{
    public static class MathUtils
    {
        // Clamp function for integers and doubles
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));
        public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(value, max));
    }


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

        public Player(double x, double y, double speed, int width, int height)
        {
            X = TargetX = x;
            Y = TargetY = y;
            Speed = speed;
            Width = width;
            Height = height;
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

            nextTargetX = MathUtils.Clamp(nextTargetX, 0, canvasWidth - Width);
            nextTargetY = MathUtils.Clamp(nextTargetY, 0, canvasHeight - Height);

            Rect newPlayerBounds = new Rect(nextTargetX, nextTargetY, Width, Height);

            if (!CheckCollision(newPlayerBounds, collidableTiles))
            {
                TargetX = nextTargetX;
                TargetY = nextTargetY;
                UpdateRotation(direction);
            }
        }



        public void ApplyGravity(double gravity)
        {
            // Apply gravity to TargetY
            TargetY += gravity;
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

        private bool CheckCollision(Rect playerBounds, List<Rect> collidableTiles)
        {
            foreach (Rect tile in collidableTiles)
            {
                if (playerBounds.IntersectsWith(tile))
                {
                    return true; // Collision detected
                }
            }
            return false;
        }




        private void UpdateRotation(Direction direction)
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

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
        None
    }


    public partial class MainWindow : System.Windows.Window
    {
        public Player player;
        public MatrixTransform playerTransform; // Use MatrixTransform instead of TransformGroup
        public Image playerImage;

        public readonly double _gravity = 5;
        public double gravity = 5;

        public List<Rect> collidableTiles = new List<Rect>();





        public void UpdatePlayerPosition()
        {
            if (playerTransform != null && player != null)
            {
                double offsetX = 0, offsetY = 0;

                switch (player.Rotation)
                {
                    case 90:  // Facing down
                        offsetX = (player.Height - player.Width) / 2;
                        offsetY = (player.Width - player.Height) / 2;
                        break;
                    case 270: // Facing up
                        offsetX = (player.Height - player.Width) / 2;
                        offsetY = (player.Width - player.Height) / 2;
                        break;
                    case 180: // Facing left
                        offsetX = player.Width - player.Height / 2;
                        offsetY = player.Height / 2;
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
                gravity = _gravity / 2;
            else
                gravity = _gravity;
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

                // Create the player object
                player = new Player(GameCanvas.ActualWidth / 2, GameCanvas.ActualHeight * 0.2, 20, 46, 84);

                if (!DORecolorPlayer)
                {
                    // Load the player image
                    BitmapImage playerImageSource = new BitmapImage();
                    playerImageSource.BeginInit();
                    playerImageSource.UriSource = new Uri(PlayerImagePath, UriKind.RelativeOrAbsolute);
                    playerImageSource.CacheOption = BitmapCacheOption.OnLoad; // Ensure the image is fully loaded
                    playerImageSource.EndInit();

                    // Create the player image element
                    playerImage = new Image
                    {
                        Source = playerImageSource,
                        Width = player.Width,
                        Height = player.Height,
                        RenderTransformOrigin = new Point(0, 0) // Set origin for rotation
                    };
                }
                else
                {
                    // Create the recolored image
                    var playerRecolored = RecolorImage(PlayerImagePath);

                    // Create the player image element
                    playerImage = new Image
                    {
                        Source = playerRecolored,
                        Width = player.Width,
                        Height = player.Height,
                        RenderTransformOrigin = new Point(0, 0), // Set origin for rotation
                    };

                }

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


        public void Player_RePos()
        {
            if (player != null)
            {
                player.X = player.TargetX = GameCanvas.ActualWidth / 2; // Reset target position
                player.Y = player.TargetY = GameCanvas.ActualHeight / 2;
                UpdatePlayerPosition();
            }
        }



    }
}
