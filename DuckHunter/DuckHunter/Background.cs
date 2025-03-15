using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuckHunter
{
    class Background
    {
        private readonly MainWindow window;  // Itt nem hozunk létre új MainWindow-t, hanem paraméterként adjuk át
        private readonly ImgLoader loader = new();
        public int X { get; set; }
        public int Y { get; set; }

        public Background(MainWindow mainWindow, int x, int y)
        {
            this.X = x;
            this.Y = y;
            this.window = mainWindow;
            this.loader.LoadImage(window, "img/sky.png", true, 0, 0, x, y);
        }
    }
}
