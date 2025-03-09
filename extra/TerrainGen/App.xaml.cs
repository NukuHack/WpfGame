
using System;

using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Numerics;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

using Microsoft.Win32.SafeHandles;
using System.Windows.Interop;

namespace TerrainGen
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /*
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var mainWindow = new MainWindow(DateTime.Now.Millisecond);
            mainWindow.Show();
        }
        */
    }


    public class PerlinNoise
    {
        private readonly int[] permutation = new int[512];

        public PerlinNoise(int seed)
        {
            var random = new Random(seed);

            // Initialize and shuffle the first 256 elements
            for (int i = 0; i < 256; i++)
                permutation[i] = i;

            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
            }

            // Duplicate the shuffled array to the second half
            Array.Copy(permutation, 0, permutation, 256, 256);
        }

        public double Noise1D(double x)
        {
            int X = (int)Math.Floor(x) & 255;
            x -= Math.Floor(x);
            double u = Fade(x);

            int hash = permutation[X];
            int hash2 = permutation[X + 1];

            double a = ((hash & 1) * 2 - 1) * x;
            double b = ((hash2 & 1) * 2 - 1) * (1 - x);

            return a + u * (b - a);
        }

        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    }

    public static class Color2
    {
        // ... existing ToUint method ...

        public static Color Lerp(this Color from, Color to, float t)
        {
            t = Math2.Clamp(t, 0f, 1f); // Ensure t is between 0-1

            byte r = (byte)(from.R + (to.R - from.R) * t);
            byte g = (byte)(from.G + (to.G - from.G) * t);
            byte b = (byte)(from.B + (to.B - from.B) * t);
            byte a = (byte)(from.A + (to.A - from.A) * t);

            return Color.FromArgb(a, r, g, b);
        }
    }

    public static class Math2
    {
        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
        public static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }
        public static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }

    public static class ColorExtensions
    {
        public static uint ToUint(this Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }
    }
}
