﻿using System;
using System.Windows;
using System.Windows.Media;
using System.Configuration;
using System.Data;

namespace VoidVenture
{

    public static class MathHelper
    {
        public static float Lerp(float a, float b, float t)
        {
            // Clamp t between 0 and 1 to ensure valid interpolation
            t = Math.Clamp(t, 0f, 1f);
            return a + (b - a) * t;
        }

    }


    public static class Color2
    {
        // ... existing ToUint method ...

        public static Color Lerp(this Color from, Color to, float t)
        {
            t = Math.Clamp(t, 0f, 1f); // Ensure t is between 0-1

            byte r = (byte)(from.R + (to.R - from.R) * t);
            byte g = (byte)(from.G + (to.G - from.G) * t);
            byte b = (byte)(from.B + (to.B - from.B) * t);
            byte a = (byte)(from.A + (to.A - from.A) * t);

            return Color.FromArgb(a, r, g, b);
        }

        private static uint LerpColor(uint from, uint to, float t)
        {
            int fromR = (int)((from >> 16) & 0xFF);
            int fromG = (int)((from >> 8) & 0xFF);
            int fromB = (int)(from & 0xFF);

            int toR = (int)((to >> 16) & 0xFF);
            int toG = (int)((to >> 8) & 0xFF);
            int toB = (int)(to & 0xFF);

            int r = (int)(fromR + (toR - fromR) * t);
            int g = (int)(fromG + (toG - fromG) * t);
            int b = (int)(fromB + (toB - fromB) * t);

            return (uint)((255 << 24) | (r << 16) | (g << 8) | b);
        }

        public static uint ToUint(this Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
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



    public partial class App : System.Windows.Application
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