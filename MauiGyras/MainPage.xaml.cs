using Microsoft.Maui.Devices.Sensors;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace MauiGyras;

public partial class MainPage : ContentPage
{
    float shipX, shipY;

    // ✅ Sensores (tienen que ser campos de la clase)
    float accelX;
    float accelY;
    float gyroZ;

    public MainPage()
    {
        InitializeComponent();
        canvasView.PaintSurface += CanvasView_PaintSurface;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (Accelerometer.Default.IsSupported)
        {
            Accelerometer.Default.ReadingChanged += Accelerometer_ReadingChanged;
            Accelerometer.Default.Start(SensorSpeed.Game);
        }

        if (Gyroscope.Default.IsSupported)
        {
            Gyroscope.Default.ReadingChanged += Gyroscope_ReadingChanged;
            Gyroscope.Default.Start(SensorSpeed.Game);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (Accelerometer.Default.IsMonitoring)
        {
            Accelerometer.Default.Stop();
            Accelerometer.Default.ReadingChanged -= Accelerometer_ReadingChanged;
        }

        if (Gyroscope.Default.IsMonitoring)
        {
            Gyroscope.Default.Stop();
            Gyroscope.Default.ReadingChanged -= Gyroscope_ReadingChanged;
        }
    }

    private void Accelerometer_ReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        accelX = (float)e.Reading.Acceleration.X;
        accelY = (float)e.Reading.Acceleration.Y;

        MainThread.BeginInvokeOnMainThread(() => canvasView.InvalidateSurface());
    }

    private void Gyroscope_ReadingChanged(object? sender, GyroscopeChangedEventArgs e)
    {
        gyroZ = (float)e.Reading.AngularVelocity.Z;
        MainThread.BeginInvokeOnMainThread(() => canvasView.InvalidateSurface());
    }

    private void CanvasView_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Black);

        var w = e.Info.Width;
        var h = e.Info.Height;

        if (shipX == 0 && shipY == 0)
        {
            shipX = w / 2f;
            shipY = h / 2f;
        }

        // ✅ Mover nave con acelerómetro (ajusta speed a gusto)
        var speed = 35f;
        shipX += accelX * speed;
        shipY -= accelY * speed;

        shipX = Math.Clamp(shipX, 30, w - 30);
        shipY = Math.Clamp(shipY, 30, h - 30);

        // ✅ (Opcional) rotación ligera con giroscopio
        canvas.Save();
        var angleDegrees = gyroZ * 10f;
        canvas.RotateDegrees(angleDegrees, w / 2f, h / 2f);

        DrawSpaceship(canvas, shipX, shipY);

        canvas.Restore();
    }

    private static void DrawSpaceship(SKCanvas canvas, float x, float y)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        paint.Color = SKColors.DeepSkyBlue;
        var path = new SKPath();
        path.MoveTo(x, y - 40);
        path.LineTo(x - 25, y + 20);
        path.LineTo(x + 25, y + 20);
        path.Close();
        canvas.DrawPath(path, paint);

        paint.Color = SKColors.Yellow;
        canvas.DrawCircle(x, y - 10, 8, paint);

        paint.Color = SKColors.Red;
        canvas.DrawCircle(x - 12, y + 25, 6, paint);
        canvas.DrawCircle(x + 12, y + 25, 6, paint);
    }
}
