using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RoseTrap.Core.Roblox;

namespace RoseTrap.ViewModels;

public enum StepState { Waiting, Active, Done }

public sealed partial class LoadingStep(string title) : ObservableObject
{
    public string Title { get; } = title;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsActive), nameof(IsDone))] StepState _state;
    public bool IsActive => State == StepState.Active;
    public bool IsDone => State == StepState.Done;
}

/// <summary>
/// RoseTrap's loading screen for one launch. Stages come from the launcher (download, settings, start)
/// and then from Roblox's own log (joining, connected, loaded), so the bar only moves on real events.
/// </summary>
public sealed partial class LoadingSession : ObservableObject
{
    public const int Preparing = 0, Starting = 1, Joining = 2, Loading = 3;

    readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LoadingSession(bool joinsGame, long? placeId)
    {
        JoinsGame = joinsGame;
        PlaceId = placeId;
        Steps = joinsGame
            ? [new("Preparing Roblox"), new("Starting Roblox"), new("Joining server"), new("Loading game")]
            : [new("Preparing Roblox"), new("Starting Roblox")];
        Steps[0].State = StepState.Active;
    }

    public bool JoinsGame { get; }
    public bool IsPreview { get; init; }
    public long? PlaceId { get; private set; }
    public ObservableCollection<LoadingStep> Steps { get; }
    public Task Done => _done.Task;
    public bool IsDone => _done.Task.IsCompleted;

    [ObservableProperty] string _title = "Roblox";
    [ObservableProperty] string _subtitle = "Getting ready";
    [ObservableProperty] string _status = "Preparing";
    [ObservableProperty] double _progress;
    [ObservableProperty] Bitmap? _thumbnail;
    [ObservableProperty] Bitmap? _icon;
    [ObservableProperty] bool _hasGame;

    int _step;

    /// <summary>Launcher progress: downloading, applying settings, starting.</summary>
    public void Report(InstallProgress p)
    {
        if (IsDone) return;
        Status = p.Stage;
        if (p.Stage.StartsWith("Starting", StringComparison.Ordinal)) Advance(Starting, .5);
        else Progress = Math.Max(Progress, p.Fraction * Share(Preparing));
    }

    /// <summary>Roblox's process is running.</summary>
    public void Started()
    {
        if (IsDone) return;
        if (!JoinsGame) { Advance(Starting, 1); Status = "Roblox is open"; Finish(); return; }
        Advance(Joining, 0);
        Status = "Finding a server";
    }

    public void OnJoin(JoinEvent e)
    {
        if (IsDone) return;
        switch (e.Stage)
        {
            case JoinStage.Joining:
                if (e.PlaceId is { } id && PlaceId is null) PlaceId = id;
                Advance(Joining, .4);
                Status = "Joining server";
                break;
            case JoinStage.Connected:
                Advance(Loading, .2);
                Status = "Loading the game";
                break;
            case JoinStage.Loaded:
                Advance(Loading, 1);
                Status = "Ready";
                Finish();
                break;
            case JoinStage.Left:
                Finish();
                break;
        }
    }

    public void ShowGame(GameInfo g)
    {
        Title = g.Name;
        Subtitle = string.IsNullOrEmpty(g.Creator) ? "Roblox experience" : "by " + g.Creator;
        HasGame = true;
    }

    /// <summary>Closes the screen: loaded, gave up waiting, Roblox closed, or the user dismissed it.</summary>
    public void Finish()
    {
        if (IsDone) return;
        foreach (var s in Steps) if (s.State == StepState.Active) s.State = StepState.Done;
        _done.TrySetResult();
        OnPropertyChanged(nameof(IsDone));
    }

    void Advance(int step, double within)
    {
        step = Math.Min(step, Steps.Count - 1);
        if (step < _step) return;
        for (int i = 0; i < Steps.Count; i++)
            Steps[i].State = i < step ? StepState.Done : i == step ? StepState.Active : StepState.Waiting;
        _step = step;
        double start = 0;
        for (int i = 0; i < step; i++) start += Share(i);
        Progress = Math.Max(Progress, start + Share(step) * within);
    }

    // Preparing includes a possible download, so it gets the biggest part of the bar.
    double Share(int step) => JoinsGame ? new[] { .35, .15, .2, .3 }[step] : new[] { .7, .3 }[step];
}
