// this is just a try to optimize the color gradient (cool looking coloring) from the complex.cs to be actually usable and don't burn my pc to ashes

// Precompute noise values and kernel if radius is fixed
double[] precomputedNoise1 = new double[width];
double[] precomputedNoise2 = new double[width];
for (int x = 0; x < width; x++) {
    precomputedNoise1[x] = noiseGenerator.Noise1D(x * 0.1) * 10;
    precomputedNoise2[x] = (noiseGenerator.Noise1D(x * 0.05) + 1) * 0.1;
}

// Precompute Gaussian kernel if radius is fixed
int smoothRadius = 3; // Example fixed radius
double[] gaussianKernel = GenerateGaussianKernel(smoothRadius);

// Main rendering loop
Parallel.For(0, height, y => {
    int stride = y * width;
    double waterY = height - WaterLevel;
    double invHeight = 1.0 / height;

    for (int x = 0; x < width; x++) {
        double baseH = smoothHeights[x];
        double terrainH = baseH + precomputedNoise1[x];
        
        double delta = terrainH - y;
        double gBlend = Math.Clamp((delta + GrassDepth) / (GrassDepth * 2), 0, 1);
        double dBlend = Math.Clamp((delta + DirtDepth) / (DirtDepth * 2), 0, 1);
        double sBlend = 1 - y * invHeight;

        if (y < terrainH) {
            double cloud = precomputedNoise2[x];
            byte alpha = (byte)(255 * Math.Min(0.3 + cloud, 1));
            double skyF = 1 - y * invHeight;
            
            byte r = (byte)(SkyColor.R * skyF + cloud * 50);
            byte g = (byte)(SkyColor.G * skyF + cloud * 30);
            byte b = (byte)(SkyColor.B * skyF + cloud * 20);
            
            pixels[stride + x] = (uint)(alpha << 24 | r << 16 | g << 8 | b);
        } else {
            if (y > waterY) {
                double depth = Math.Min((y - waterY) / 50, 1);
                byte r = (byte)(WaterColor.R * (1 - depth) + SandColor.R * depth);
                byte g = (byte)(WaterColor.G * (1 - depth) + SandColor.G * depth);
                byte b = (byte)(WaterColor.B * (1 - depth) + SandColor.B * depth);
                pixels[stride + x] = 0xFF000000 | (uint)(r << 16 | g << 8 | b);
            } else {
                byte r = (byte)(GrassColor.R * gBlend + DirtColor.R * dBlend + StoneColor.R * sBlend);
                byte g = (byte)(GrassColor.G * gBlend + DirtColor.G * dBlend + StoneColor.G * sBlend);
                byte b = (byte)(GrassColor.B * gBlend + DirtColor.B * dBlend + StoneColor.B * sBlend);
                pixels[stride + x] = 0xFF000000 | (uint)(r << 16 | g << 8 | b);
            }
        }
    }
});

terrainBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
terrainImage.Source = terrainBitmap;


// Optimized Gaussian smooth with precomputed kernel
private double[] GaussianSmooth(double[] input, double[] kernel, int radius) {
    double[] output = new double[input.Length];
    for (int i = 0; i < input.Length; i++) {
        double sum = 0, wSum = 0;
        for (int j = -radius; j <= radius; j++) {
            int idx = Math.Clamp(i + j, 0, input.Length - 1);
            double w = kernel[Math.Abs(j)];
            sum += input[idx] * w;
            wSum += w;
        }
        output[i] = sum / wSum;
    }
    return output;
}
