using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace VoidVenture
{


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

        public static uint ToUint(this Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
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



    public class TerrainColorGenerator
    {
        private static Random random = new Random();

        public static List<Color> GenerateColors(int count = 6)
        {
            var colors = new List<Color>();

            for (int i = 0; i < count; i++)
            {
                // Get biome parameters based on elevation level (index)
                var (hueMin, hueMax, satMin, satMax, lightMin, lightMax) = GetBiomeParams(i, count);

                // Generate random HSL within biome constraints
                double h = RandomRange(hueMin, hueMax);
                double s = RandomRange(satMin, satMax);
                double l = RandomRange(lightMin, lightMax);

                // Convert to RGB and add to list
                colors.Add(HslToRgb(h, s, l));
            }

            return colors;
        }

        private static (double, double, double, double, double, double) GetBiomeParams(int index, int total)
        {
            double segment = (double)index / (total - 1); // 0 (low elevation) to 1 (high)

            // Define biome templates as tuples: (hueMin, hueMax, satMin, satMax, lightMin, lightMax)
            var biomeTemplates = new List<(double, double, double, double, double, double)>
        {
            (180, 240, 10, 30, 10, 30),   // Deep water (dark blue-gray)
            (200, 270, 15, 40, 20, 40),   // Shallow water (dark cyan-blue)
            (30, 90, 10, 30, 30, 50),     // Sand/beach (dark yellow-brown)
            (40, 100, 15, 40, 40, 60),    // Desert (dark warm brown)
            (60, 160, 10, 30, 30, 50),    // Grassland/forest (dark green-gray)
            (80, 180, 10, 30, 40, 60),    // Swamp/marsh (dark olive-green)
            (10, 70, 5, 25, 20, 40),      // Mountain rock (dark gray-brown)
            (0, 40, 5, 25, 30, 50),       // Volcanic rock (dark red-brown)
            (0, 360, 0, 10, 60, 80),      // Snow (very desaturated white)
            (200, 300, 5, 20, 50, 70)     // Ice (dark blue-gray)
        };

            // Randomly select a biome template
            var randomTemplate = biomeTemplates[random.Next(biomeTemplates.Count)];

            // Adjust lightness based on elevation segment for progression
            var (hueMin, hueMax, satMin, satMax, lightMin, lightMax) = randomTemplate;
            lightMin += segment * 30; // Increase lightness at higher elevations (but still darker overall)
            lightMax += segment * 30;

            return (hueMin, hueMax, satMin, satMax, lightMin, lightMax);
        }

        private static double RandomRange(double min, double max)
        {
            return min + (random.NextDouble() * (max - min));
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l / 100; // Achromatic
            }
            else
            {
                double q = l < 50 ? l * (1 + s / 100) : l + s - (l * s) / 100;
                double p = (2 * l - q) / 100;
                h /= 360;
                double tR = h + 1.0 / 3;
                double tG = h;
                double tB = h - 1.0 / 3;

                r = HueToRgb(p, q / 100, tR);
                g = HueToRgb(p, q / 100, tG);
                b = HueToRgb(p, q / 100, tB);
            }

            return Color.FromArgb(
                (byte)(255),
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
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
}
