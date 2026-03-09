using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Core;

public static class PngWriter
{
    private const double TemperatureMin = 0.0;
    private const double TemperatureMax = 1000.0;

    public static void SaveHeatmapPng(string path, double[] field, int nx, int ny)
    {
        byte[] pngBytes = CreateHeatmapPng(field, nx, ny);
        File.WriteAllBytes(path, pngBytes);
    }

    public static byte[] CreateHeatmapPng(double[] field, int nx, int ny)
    {
        using var img = new Image<Rgba32>(nx, ny);

        for (int j = 0; j < ny; j++)
        {
            int row = j * nx;
            int y = (ny - 1) - j;

            for (int i = 0; i < nx; i++)
            {
                double t = field[row + i];
                img[i, y] = MapToRgba(t);
            }
        }

        using var stream = new MemoryStream();
        img.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static Rgba32 MapToRgba(double value)
    {
        double x = (value - TemperatureMin) / (TemperatureMax - TemperatureMin);
        x = Math.Clamp(x, 0.0, 1.0);

        double r, g, b;

        if (x < 0.25)
        {
            double t = x / 0.25;
            r = 0; g = t; b = 1;
        }
        else if (x < 0.5)
        {
            double t = (x - 0.25) / 0.25;
            r = 0; g = 1; b = 1 - t;
        }
        else if (x < 0.75)
        {
            double t = (x - 0.5) / 0.25;
            r = t; g = 1; b = 0;
        }
        else
        {
            double t = (x - 0.75) / 0.25;
            r = 1; g = 1 - t; b = 0;
        }

        byte R = (byte)Math.Clamp((int)Math.Round(r * 255), 0, 255);
        byte G = (byte)Math.Clamp((int)Math.Round(g * 255), 0, 255);
        byte B = (byte)Math.Clamp((int)Math.Round(b * 255), 0, 255);

        return new Rgba32(R, G, B, 255);
    }
}
