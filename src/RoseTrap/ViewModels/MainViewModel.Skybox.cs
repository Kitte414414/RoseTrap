using Avalonia.Media.Imaging;
using RoseTrap.Core.Shaders;

namespace RoseTrap.ViewModels;

/// <summary>Skybox picker (Mods tab).</summary>
public sealed partial class MainViewModel
{
    public static readonly string[] SkyboxNames = [.. Skyboxes.All.Select(p => p.Name)];

    Core.Settings.ShaderSettings Sky => _s.Shaders;

    public int SkyboxIndex
    {
        get => Math.Max(0, Array.IndexOf(SkyboxNames, Sky.Skybox));
        set
        {
            if (value < 0 || value >= SkyboxNames.Length) return;
            Sky.Skybox = SkyboxNames[value];
            Changed();
            OnPropertyChanged(nameof(SkyboxDescription));
            _skyPreview = null;
            OnPropertyChanged(nameof(SkyPreview));
        }
    }

    public string SkyboxDescription => Skyboxes.Find(Sky.Skybox)?.Description ?? "";

    Bitmap? _skyPreview;
    /// <summary>Rendered on first use only, so the picture costs nothing until the Mods tab is opened.</summary>
    public Bitmap? SkyPreview
    {
        get
        {
            if (_skyPreview != null || Skyboxes.Find(Sky.Skybox) is not { } p || p.Name == Skyboxes.Default) return _skyPreview;
            return _skyPreview = new Bitmap(new MemoryStream(Skyboxes.RenderFace(p, "bk", 160)));
        }
    }
}
