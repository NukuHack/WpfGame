using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace rnd
{
    public partial class MainWindow : Window
    {
        private double scrollX = 0;
        private double scrollY = 0;
        private ImageBrush _tileBrush;
        private Rectangle _tileRectangle;
        private int CellSize = 200;
        private int TileWorldSize;
        private double[] sinX, cosY;
        private double k;
        private double _zoomFactor = 1.0;
        private const double ZoomSensitivity = 1.2;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += (sx, ex) => { MainWindow_Loaded(); };
        }

        private void MainWindow_SizeChanged(EventArgs e){
            UpdateTileSize();

            RenderMap();
        }

        private void MainWindow_Loaded()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            timer.Tick += (x, y) =>
            {
                timer.Stop();
                this.SizeChanged += (s,e) => { MainWindow_SizeChanged(e); };
                this.KeyDown += (s, e) => { Window_KeyDown(e.Key); };
                this.MouseWheel += (s, e) => { Window_MouseWheel(e); };
                UpdateTileSize();
            };
            timer.Start();
        }

        private void Window_KeyDown(Key key)
        {
            double delta = 50 / _zoomFactor;
            switch (key)
            {
                case Key.W: ScrollMap(0, -delta); break;
                case Key.S: ScrollMap(0, delta); break;
                case Key.A: ScrollMap(-delta, 0); break;
                case Key.D: ScrollMap(delta, 0); break;
            }
        }

        private void Window_MouseWheel(MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(MyCanvas);
            double oldZoom = _zoomFactor;
            _zoomFactor *= e.Delta > 0 ? ZoomSensitivity : 1 / ZoomSensitivity;
            if (_zoomFactor >= 100)
            {
                _zoomFactor = oldZoom;
                return;
            }
            else if (_zoomFactor <= 0.01)
            {
                _zoomFactor = oldZoom;
                return;
            }

            double originalX = (mousePos.X / oldZoom) + scrollX;
            double originalY = (mousePos.Y / oldZoom) + scrollY;

            scrollX = originalX - (mousePos.X / _zoomFactor);
            scrollY = originalY - (mousePos.Y / _zoomFactor);

            RenderMap();
        }

        private void ScrollMap(double deltaX, double deltaY)
        {
            scrollX += deltaX;
            scrollY += deltaY;
            RenderMap();
        }

        private void RenderMap()
        {
            double offsetX = scrollX % TileWorldSize;
            double offsetY = scrollY % TileWorldSize;

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(_zoomFactor, _zoomFactor));
            transformGroup.Children.Add(new TranslateTransform(-offsetX * _zoomFactor, -offsetY * _zoomFactor));
            _tileBrush.Transform = transformGroup;
        }

        private void UpdateTileSize()
        {
            MyCanvas.Width = (int)this.ActualWidth;
            MyCanvas.Height = (int)this.ActualHeight;
            TileWorldSize = (int)(MyCanvas.ActualHeight / CellSize) * CellSize;
            sinX = new double[TileWorldSize];
            cosY = new double[TileWorldSize];
            k = (2 * Math.PI) / TileWorldSize;

            for (int i = 0; i < TileWorldSize; i++)
            {
                sinX[i] = Math.Sin(i * k);
                cosY[i] = Math.Cos(i * k);
            }

            _tileBrush = CreateTileBrush(CellSize);
            _tileRectangle = new Rectangle { Fill = _tileBrush };
            _tileRectangle.Width = (int)this.ActualWidth;
            _tileRectangle.Height = (int)this.ActualHeight;
            MyCanvas.Children.Clear();
            MyCanvas.Children.Add(_tileRectangle);
        }

        private ImageBrush CreateTileBrush(int cellSize)
        {
            int pixelSize = (int)TileWorldSize;
            int cellsPerSide = (int)(pixelSize / cellSize);

            var writeableBitmap = new WriteableBitmap(pixelSize, pixelSize, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new uint[pixelSize * pixelSize];

            Parallel.For(0, cellsPerSide, cellY =>
            {
                int startY = cellY * cellSize;
                int endY = startY + cellSize;

                for (int cellX = 0; cellX < cellsPerSide; cellX++)
                {
                    // Calculate color for this cell
                    int centerX = cellX * cellSize + cellSize / 2;
                    int centerY = cellY * cellSize + cellSize / 2;

                    double red = (Math.Sin(centerX * k) + 1) / 2;
                    double green = (Math.Cos(centerY * k) + 1) / 2;
                    double blue = (Math.Sin((centerX + centerY) * k * 0.7) + 1) / 2;

                    // Apply gamma correction
                    red = Math.Pow(red, 2.2);
                    green = Math.Pow(green, 2.2);
                    blue = Math.Pow(blue, 2.2);

                    byte r = (byte)(red * 255);
                    byte g = (byte)(green * 255);
                    byte b = (byte)(blue * 255);
                    uint color = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | (uint)b;

                    // Fill entire cell with this color
                    int startX = cellX * cellSize;
                    int endX = startX + cellSize;

                    for (int y = startY; y < endY && y < pixelSize; y++)
                    {
                        int rowOffset = y * pixelSize;
                        for (int x = startX; x < endX && x < pixelSize; x++)
                        {
                            pixels[rowOffset + x] = color;
                        }
                    }
                }
            });

            writeableBitmap.WritePixels(
                new Int32Rect(0, 0, pixelSize, pixelSize),
                pixels,
                pixelSize * 4,
                0);

            return new ImageBrush(writeableBitmap)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, cellSize, cellSize),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None
            };
        }
    }
}