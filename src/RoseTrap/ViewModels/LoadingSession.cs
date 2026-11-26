using CommunityToolkit.Mvvm.ComponentModel;
using RoseTrap.Core.Roblox;

namespace RoseTrap.ViewModels;

/// <summary>
/// RoseTrap's loading panel for one launch. The bar moves on real events only: the launcher's own steps
/// (download, settings, start) and then Roblox's log (joining, connected, loaded). Between events it creeps
/// slowly toward the next stage so it never looks frozen, but it never reaches a stage it hasn't seen.
/// </summary>
public sealed partial class LoadingSession : ObservableObject
{
    enum Stage { Preparing, Starting, Joining, Loading }

    readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Stage _stage;
    double _ceiling;

    public LoadingSession(bool joinsGame, long? placeId)
    {
        JoinsGame = joinsGame;
        PlaceId = placeId;
        _ceiling = End(Stage.Preparing) - .03;
    }

    public bool JoinsGame { get; }
    public bool IsPreview { get; init; }
    public long? PlaceId { get; private set; }
    public Task Done => _done.Task;
    public bool IsDone => _done.Task.IsCompleted;

    [ObservableProperty] string _heading = "Loading";
    [ObservableProperty] string _detail = "Getting Roblox ready";
    /// <summary>"Jailbreak · by Badimo" once the game is known.</summary>
    [ObservableProperty] string? _game;
    /// <summary>A warning or the game preset, shown small under the bar.</summary>
    [ObservableProperty] string? _note;
    [ObservableProperty] bool _failed;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Percent))] double _progress;

    public string Percent => $"{Math.Round(Progress * 100)}%";

    /// <summary>Launcher progress: downloading, applying settings, starting.</summary>
    public void Report(InstallProgress p)
    {
        if (IsDone) return;
        if (p.Stage.StartsWith("Starting", StringComparison.Ordinal)) { Advance(Stage.Starting, .5); Detail = "Starting Roblox"; return; }
        Detail = p.Stage;
        Bump(Start(Stage.Preparing) + p.Fraction * (End(Stage.Preparing) - Start(Stage.Preparing)) * .95);
    }

    /// <summary>Roblox's process is running.</summary>
    public void Started()
    {
        if (IsDone) return;
        if (!JoinsGame) { Advance(Stage.Starting, 1); Heading = "Ready"; Detail = "Roblox is open"; Finish(); return; }
        Advance(Stage.Joining, 0);
        Detail = "Finding a server";
    }

    public void OnJoin(JoinEvent e)
    {
        if (IsDone) return;
        switch (e.Stage)
        {
            case JoinStage.Joining:
                if (e.PlaceId is { } id && PlaceId is null) PlaceId = id;
                Advance(Stage.Joining, .4);
                Detail = "Joining server";
                break;
            case JoinStage.Connected:
                Advance(Stage.Loading, .15);
                Detail = "Loading the game";
                break;
            case JoinStage.Loaded:
                Advance(Stage.Loading, 1);
                Heading = "Ready";
                Detail = "Have fun";
                Finish();
                break;
            case JoinStage.Left:
                Finish();
                break;
        }
    }

    public void ShowGame(GameInfo g) =>
        Game = string.IsNullOrEmpty(g.Creator) ? g.Name : $"{g.Name} · by {g.Creator}";

    public void Fail(string message)
    {
        if (IsDone) return;
        Failed = true;
        Heading = "Couldn't start Roblox";
        Detail = message;
    }

    /// <summary>Called a few times a second by the panel; eases toward the current stage's end.</summary>
    public void Creep()
    {
        if (IsDone || Failed || Progress >= _ceiling) return;
        Bump(Progress + Math.Max(.0005, (_ceiling - Progress) * .02));
    }

    /// <summary>Closes the panel: loaded, gave up waiting, Roblox closed, or the user dismissed it.</summary>
    public void Finish()
    {
        if (IsDone) return;
        _done.TrySetResult();
        OnPropertyChanged(nameof(IsDone));
    }

    void Advance(Stage stage, double within)
    {
        if (!JoinsGame && stage > Stage.Starting) stage = Stage.Starting;
        if (stage < _stage) return;
        _stage = stage;
        _ceiling = End(stage) - .02;
        Bump(Start(stage) + (End(stage) - Start(stage)) * within);
    }

    void Bump(double p) => Progress = Math.Clamp(Math.Max(Progress, p), 0, 1);

    // Preparing includes a possible download, so it gets the biggest share of the bar.
    static readonly double[] GameShares = [.35, .15, .2, .3], AppShares = [.7, .3];
    double[] Shares => JoinsGame ? GameShares : AppShares;
    double Start(Stage s) => Shares.Take((int)s).Sum();
    double End(Stage s) => Start(s) + Shares[(int)s];
}
