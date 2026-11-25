using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using RoseTrap.Core.Shaders;
using RoseTrap.Services;

namespace RoseTrap.ViewModels;

/// <summary>Shaders tab: colour grading presets, sliders, live preview and skyboxes.</summary>
public sealed partial class MainViewModel
{
    public ShaderService Shader { get; } = new();

    public static readonly string[] ShaderPresetNames = [.. ShaderPresets.All.Select(p => p.Name), ShaderPresets.Custom];
    public static readonly string[] SkyboxNames = [.. Skyboxes.All.Select(p => p.Name)];

    Core.Settings.ShaderSettings Sh => _s.Shaders;
    ColorGrade Grade => Sh.Grade;

    public bool ShadersSupported => ShaderService.IsSupported;
    public bool ShadersUnsupported => !ShaderService.IsSupported;

    void InitShaders()
    {
        SyncShaderService();
        Shader.Start();
        RenderPreview();
    }

    void SyncShaderService()
    {
        Shader.Enabled = Sh.Enabled;
        Shader.OnlyWhenRobloxFocused = Sh.OnlyWhenRobloxFocused;
        Shader.Grade = Grade;
        Shader.Update();
    }

    public bool ShadersEnabled
    {
        get => Sh.Enabled;
        set { Sh.Enabled = value; Changed(); SyncShaderService(); }
    }

    public bool OnlyWhenRobloxFocused
    {
        get => Sh.OnlyWhenRobloxFocused;
        set { Sh.OnlyWhenRobloxFocused = value; Changed(); SyncShaderService(); }
    }

    public int ShaderPresetIndex
    {
        get { int i = Array.IndexOf(ShaderPresetNames, Sh.Preset); return i < 0 ? ShaderPresetNames.Length - 1 : i; }
        set
        {
            if (value < 0 || value >= ShaderPresetNames.Length) return;
            Sh.Preset = ShaderPresetNames[value];
            if (ShaderPresets.Find(Sh.Preset) is { } p) SetGrade(p.Grade, keepPreset: true);
            Changed();
            OnPropertyChanged(nameof(ShaderDescription));
        }
    }

    public string ShaderDescription =>
        ShaderPresets.Find(Sh.Preset)?.Description ?? "Your own mix. Pick a preset to start over from it.";

    void SetGrade(ColorGrade g, bool keepPreset = false)
    {
        Sh.Grade = g;
        if (!keepPreset && Sh.Preset != ShaderPresets.Custom)
        {
            Sh.Preset = ShaderPresets.Custom;
            OnPropertyChanged(nameof(ShaderPresetIndex));
            OnPropertyChanged(nameof(ShaderDescription));
        }
        foreach (var n in GradeProps) OnPropertyChanged(n);
        _saveTimer.Change(400, Timeout.Infinite);
        SyncShaderService();
        RenderPreview();
    }

    static readonly string[] GradeProps =
    [
        nameof(Exposure), nameof(ExposureLabel), nameof(Brightness), nameof(BrightnessLabel), nameof(Contrast), nameof(ContrastLabel),
        nameof(Saturation), nameof(SaturationLabel), nameof(Temperature), nameof(TemperatureLabel), nameof(Tint), nameof(TintLabel),
        nameof(Fade), nameof(FadeLabel),
    ];

    static string Signed(double v, string f = "0.00") => (v >= 0 ? "+" : "") + v.ToString(f, CultureInfo.InvariantCulture);
    static string Pct(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    public double Exposure { get => Grade.Exposure; set => SetGrade(Grade with { Exposure = Math.Round(value, 2) }); }
    public string ExposureLabel => Signed(Grade.Exposure) + " EV";
    public double Brightness { get => Grade.Brightness; set => SetGrade(Grade with { Brightness = Math.Round(value, 2) }); }
    public string BrightnessLabel => Signed(Grade.Brightness);
    public double Contrast { get => Grade.Contrast; set => SetGrade(Grade with { Contrast = Math.Round(value, 2) }); }
    public string ContrastLabel => Pct(Grade.Contrast);
    public double Saturation { get => Grade.Saturation; set => SetGrade(Grade with { Saturation = Math.Round(value, 2) }); }
    public string SaturationLabel => Pct(Grade.Saturation);
    public double Temperature { get => Grade.Temperature; set => SetGrade(Grade with { Temperature = Math.Round(value, 2) }); }
    public string TemperatureLabel => Grade.Temperature == 0 ? "Neutral" : Signed(Grade.Temperature) + (Grade.Temperature > 0 ? " warm" : " cool");
    public double Tint { get => Grade.Tint; set => SetGrade(Grade with { Tint = Math.Round(value, 2) }); }
    public string TintLabel => Grade.Tint == 0 ? "Neutral" : Signed(Grade.Tint) + (Grade.Tint > 0 ? " pink" : " green");
    public double Fade { get => Grade.Fade; set => SetGrade(Grade with { Fade = Math.Round(value, 2) }); }
    public string FadeLabel => Grade.Fade.ToString("0.00", CultureInfo.InvariantCulture);

    [RelayCommand]
    void ResetGrade()
    {
        Sh.Preset = "Off";
        SetGrade(ColorGrade.Neutral, keepPreset: true);
        OnPropertyChanged(nameof(ShaderPresetIndex));
        OnPropertyChanged(nameof(ShaderDescription));
    }

    [RelayCommand]
    void TestShaderOnScreen()
    {
        if (!ShaderService.IsSupported) { Toast?.Invoke("Screen shaders need Windows 8 or newer."); return; }
        Shader.Test(Grade, TimeSpan.FromSeconds(5));
        Toast?.Invoke(Shader.LastApplyOk ? "Showing the look on your whole screen for 5 seconds" : "Windows didn't allow the colour filter");
    }

    // ---------- skybox ----------

    public int SkyboxIndex
    {
        get => Math.Max(0, Array.IndexOf(SkyboxNames, Sh.Skybox));
        set
        {
            if (value < 0 || value >= SkyboxNames.Length) return;
            Sh.Skybox = SkyboxNames[value];
            Changed();
            OnPropertyChanged(nameof(SkyboxDescription));
            RenderSkyPreview();
            RenderPreview();
        }
    }

    public string SkyboxDescription => Skyboxes.Find(Sh.Skybox)?.Description ?? "";

    Bitmap? _skyPreview;
    public Bitmap? SkyPreview { get => _skyPreview; private set => SetProperty(ref _skyPreview, value); }

    byte[]? _skyRgb; // 0..255 RGB of the back face at preview size, used as the preview scene's sky
    const int PreviewW = 480, PreviewH = 220;

    void RenderSkyPreview()
    {
        _skyRgb = null;
        if (Skyboxes.Find(Sh.Skybox) is not { } p || p.Name == Skyboxes.Default) { SkyPreview = null; return; }
        var png = Skyboxes.RenderFace(p, "bk", 160);
        SkyPreview = new Bitmap(new MemoryStream(png));
        _skyRgb = Skyboxes.RenderRgb(p, "bk", PreviewW);
    }

    // ---------- live preview ----------

    WriteableBitmap? _preview;
    public WriteableBitmap? Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    byte[]? _scene;

    /// <summary>Grades the sample scene with exactly the matrix Windows will use.</summary>
    public unsafe void RenderPreview()
    {
        _scene ??= PreviewScene.Build(PreviewW, PreviewH, null);
        if (_skyRgb != null) _scene = PreviewScene.Build(PreviewW, PreviewH, _skyRgb);
        var m = Grade.Matrix();
        var bmp = new WriteableBitmap(new PixelSize(PreviewW, PreviewH), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var fb = bmp.Lock())
        {
            var dst = (byte*)fb.Address;
            for (int y = 0; y < PreviewH; y++)
            {
                byte* row = dst + y * fb.RowBytes;
                for (int x = 0; x < PreviewW; x++)
                {
                    int i = (y * PreviewW + x) * 3;
                    var (r, g, b) = ColorGrade.Apply(m, _scene[i] / 255.0, _scene[i + 1] / 255.0, _scene[i + 2] / 255.0);
                    row[x * 4] = (byte)(b * 255);
                    row[x * 4 + 1] = (byte)(g * 255);
                    row[x * 4 + 2] = (byte)(r * 255);
                    row[x * 4 + 3] = 255;
                }
            }
        }
        Preview = bmp;
    }
}
