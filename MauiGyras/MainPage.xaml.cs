using System.Diagnostics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;

namespace MauiGyras;

public partial class MainPage : ContentPage
{
    // --- Input ---
    bool useSensors = true; // en Windows no hará nada
    float accelX, accelY;
    float gyroZ;

    // --- Estado nave ---
    float shipX, shipY;
    float vx, vy;                 // velocidad (inercia)
    float targetAx, targetAy;     // “aceleración objetivo” (por drag)
    float shipAngle;              // opcional (por ahora 0)

    // --- Loop ---
    readonly Stopwatch stopwatch = new();
    bool isRunning;

    // --- Estrellas ---
    readonly Random rng = new();
    Star[] stars = Array.Empty<Star>();

    // Ajustes “feel”
    const float AccelFactor = 1800f;   // fuerza del input
    const float Damping = 0.92f;       // rozamiento (0.85–0.98)
    const float MaxSpeed = 1200f;

    public MainPage()
    {
        InitializeComponent();

        canvasView.PaintSurface += CanvasView_PaintSurface;

        // Control por arrastre (Pan)
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += Pan_PanUpdated;
        canvasView.GestureRecognizers.Add(pan);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (isRunning) return;
        isRunning = true;
        stopwatch.Restart();

        // 60 FPS aprox
        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), () =>
        {
            if (!isRunning) return false;
            canvasView.InvalidateSurface();
            return true;
        });
        StartSensorsIfAvailable();

    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        isRunning = false;
        stopwatch.Stop();
        StopSensorsIfRunning();

    }

    void StartSensorsIfAvailable()
    {
        if (!useSensors) return;

        if (Accelerometer.Default.IsSupported && !Accelerometer.Default.IsMonitoring)
        {
            Accelerometer.Default.ReadingChanged += Accelerometer_ReadingChanged;
            Accelerometer.Default.Start(SensorSpeed.Game);
        }

        if (Gyroscope.Default.IsSupported && !Gyroscope.Default.IsMonitoring)
        {
            Gyroscope.Default.ReadingChanged += Gyroscope_ReadingChanged;
            Gyroscope.Default.Start(SensorSpeed.Game);
        }
    }

    void StopSensorsIfRunning()
    {
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

    void Accelerometer_ReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        accelX = (float)e.Reading.Acceleration.X;
        accelY = (float)e.Reading.Acceleration.Y;
    }

    void Gyroscope_ReadingChanged(object? sender, GyroscopeChangedEventArgs e)
    {
        gyroZ = (float)e.Reading.AngularVelocity.Z;
    }


    private void Reset_Clicked(object sender, EventArgs e)
    {
        vx = vy = 0;
        targetAx = targetAy = 0;
    }

    private void Sensors_Clicked(object sender, EventArgs e)
    {
        useSensors = !useSensors;

        if (useSensors)
            StartSensorsIfAvailable();
        else
            StopSensorsIfRunning();
    }

    // Drag: cuanto más lejos del centro, más empuja
    private void Pan_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType is GestureStatus.Canceled or GestureStatus.Completed)
        {
            targetAx = targetAy = 0;
            return;
        }

        // Convertimos el drag en un vector [-1..1]
        // (no tenemos coordenadas absolutas fáciles aquí, así que usamos el delta acumulado)
        var nx = (float)Math.Clamp(e.TotalX / 200.0, -1.0, 1.0);
        var ny = (float)Math.Clamp(e.TotalY / 200.0, -1.0, 1.0);

        targetAx = nx;
        targetAy = ny;
    }

    private void CanvasView_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Black);

        var w = e.Info.Width;
        var h = e.Info.Height;

        // init posiciones al arrancar
        if (shipX == 0 && shipY == 0)
        {
            shipX = w / 2f;
            shipY = h / 2f;
        }

        // init estrellas
        if (stars.Length == 0)
            stars = CreateStars(180, w, h);

        // delta time (segundos)
        var dt = (float)stopwatch.Elapsed.TotalSeconds;
        stopwatch.Restart();
        dt = Math.Clamp(dt, 0.0f, 0.05f);

        // --- Update estrellas ---
        UpdateStars(dt, w, h);

        // --- Física nave (inercia) ---

        float inputX, inputY;

        // Si hay sensores (Android/iOS físico), usamos acelerómetro.
        // Si no, usamos drag (Windows).
        if (useSensors && Accelerometer.Default.IsSupported)
        {
            inputX = -accelX;
            inputY = accelY; // invertir para que “arriba” sea arriba en pantalla
        }
        else
        {
            inputX = targetAx;
            inputY = targetAy;
        }

        var ax = inputX * AccelFactor;
        var ay = inputY * AccelFactor;


        vx += ax * dt;
        vy += ay * dt;

        // rozamiento
        vx *= (float)Math.Pow(Damping, dt * 60f);
        vy *= (float)Math.Pow(Damping, dt * 60f);

        // clamp velocidad
        vx = Math.Clamp(vx, -MaxSpeed, MaxSpeed);
        vy = Math.Clamp(vy, -MaxSpeed, MaxSpeed);

        shipX += vx * dt;
        shipY += vy * dt;

        // límites de pantalla
        shipX = Math.Clamp(shipX, 30, w - 30);
        shipY = Math.Clamp(shipY, 30, h - 30);

        // --- Draw estrellas + nave ---
        DrawStars(canvas);

        canvas.Save();
        if (useSensors && Gyroscope.Default.IsSupported)
        {
            var angleDegrees = gyroZ * 10f;
            canvas.RotateDegrees(angleDegrees, w / 2f, h / 2f);
        }

        DrawSpaceship(canvas, shipX, shipY);
        canvas.Restore();


        // HUD
        var shownX = (useSensors && Accelerometer.Default.IsSupported) ? inputX : targetAx;
        var shownY = (useSensors && Accelerometer.Default.IsSupported) ? inputY : targetAy;
        lblHud.Text = $"vx={vx:0} vy={vy:0}  input=({shownX:0.00},{shownY:0.00})  sensors={(useSensors ? "ON" : "OFF")}";

    }

    // ---------- Estrellas ----------
    private Star[] CreateStars(int count, int w, int h)
    {
        var arr = new Star[count];
        for (int i = 0; i < count; i++)
        {
            // 3 capas con distinta velocidad
            var layer = rng.Next(0, 3); // 0..2
            arr[i] = new Star
            {
                X = rng.NextSingle() * w,
                Y = rng.NextSingle() * h,
                Size = layer == 0 ? 1.5f : layer == 1 ? 2.5f : 3.5f,
                Speed = layer == 0 ? 120f : layer == 1 ? 220f : 360f,
            };
        }
        return arr;
    }

    private void UpdateStars(float dt, int w, int h)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].Y += stars[i].Speed * dt;

            if (stars[i].Y > h + 10)
            {
                stars[i].Y = -10;
                stars[i].X = rng.NextSingle() * w;
            }
        }
    }

    private void DrawStars(SKCanvas canvas)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };

        foreach (var s in stars)
            canvas.DrawCircle(s.X, s.Y, s.Size, paint);
    }

    private struct Star
    {
        public float X, Y;
        public float Size;
        public float Speed;
    }

    // ---------- Nave ----------
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

        // “fuego” según velocidad (simple)
        paint.Color = SKColors.OrangeRed;
        canvas.DrawCircle(x - 12, y + 25, 6, paint);
        canvas.DrawCircle(x + 12, y + 25, 6, paint);
    }
}
