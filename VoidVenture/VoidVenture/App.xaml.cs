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

//special
using System.Configuration;
using System.Data;

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


    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
        None
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
