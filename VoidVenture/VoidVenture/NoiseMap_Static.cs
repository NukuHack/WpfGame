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





    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {



        private void RenderTerrainWhole()
        {
            BeginTerrainGenerating();

            // Render terrain in parallel
            Parallel.For(0, currentWidth, x =>
            {
                double terrainHeight = columnHeights[x];
                double localWaterY = waterDepthLUT[x];
                uint[] gradientMap = GenerateColorLUT(terrainHeight, x, localWaterY);

                int baseIndex = x; // Base index for this column
                for (int y = 0; y < currentHeight; y++)
                {
                    uint color;
                    if (y < terrainHeight)
                    {
                        if (y > localWaterY)
                            color = WaterLut[(int)Math.Min(y - localWaterY, 50)];
                        else
                            color = SkyLut[y];
                    }
                    else
                    {
                        if (y < terrainHeight - DirtDepth - 5 && y > terrainHeight * 1.5)
                            color = GroundColorArgb;
                        else
                            color = gradientMap[y];
                    }

                    // Store the color in the 1D array
                    _pixelBuffer[baseIndex] = color;
                    baseIndex += currentWidth; // Move to the next row in the same column
                }
            });


            UpdateTerrainDisplay();
        }
    }
}
