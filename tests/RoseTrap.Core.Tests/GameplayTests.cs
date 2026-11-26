using RoseTrap.Core.Roblox;
using RoseTrap.Core.Settings;
using Xunit;

namespace RoseTrap.Core.Tests;

public class GameplayStoreTests
{
    // Trimmed from a real GlobalBasicSettings_13.xml.
    const string Xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <roblox xmlns:xmime="http://www.w3.org/2005/05/xmlmime" version="4">
          <External>null</External>
          <Item class="UserGameSettings" referent="RBX0">
            <Properties>
              <bool name="CameraYInverted">false</bool>
              <token name="ComputerCameraMovementMode">0</token>
              <token name="ComputerMovementMode">0</token>
              <int name="FramerateCap">60</int>
              <bool name="Fullscreen">true</bool>
              <float name="MasterVolume">0.5</float>
              <float name="MouseSensitivity">0.80000001</float>
              <Vector2 name="MouseSensitivityFirstPerson"><X>0.8</X><Y>0.8</Y></Vector2>
              <bool name="PerformanceStatsVisible">false</bool>
              <float name="PreferredTransparency">1</float>
              <bool name="ReducedMotion">false</bool>
            </Properties>
          </Item>
        </roblox>
        """;

    [Fact]
    public void ReadsRobloxValues()
    {
        using var t = new TempDir();
        var file = t.File("GlobalBasicSettings_13.xml");
        File.WriteAllText(file, Xml);
        var g = GameplayStore.Read(file)!;
        Assert.Equal(.8, g.MouseSensitivity, 4);
        Assert.Equal(.5, g.Volume, 4);
        Assert.True(g.Fullscreen);
        Assert.Equal(1, g.UiTransparency);
        Assert.Equal(CameraMovement.Default, g.CameraMode);
    }

    [Fact]
    public void WritesRoundTripAndKeepsOtherSettings()
    {
        using var t = new TempDir();
        var file = t.File("GlobalBasicSettings_13.xml");
        File.WriteAllText(file, Xml);
        var want = new GameplaySettings
        {
            MouseSensitivity = 2.35, InvertCamera = true, CameraMode = CameraMovement.CameraToggle, Movement = MovementMode.ClickToMove,
            Volume = .3, Fullscreen = false, PerformanceStats = true, ReducedMotion = true, UiTransparency = .4,
        };
        Assert.True(GameplayStore.Write(file, want));
        Assert.Equal(want, GameplayStore.Read(file));

        var text = File.ReadAllText(file);
        Assert.Contains("<int name=\"FramerateCap\">60</int>", text);
        Assert.Contains("<token name=\"ComputerCameraMovementMode\">4</token>", text);
        Assert.Contains("<Vector2 name=\"MouseSensitivityFirstPerson\"><X>2.35</X><Y>2.35</Y></Vector2>", text);
        Assert.Contains("<Vector2 name=\"MouseSensitivityThirdPerson\"><X>2.35</X><Y>2.35</Y></Vector2>", text);
        Assert.Equal(60, GlobalBasicSettings.ReadInt(file, "FramerateCap"));
    }

    [Fact]
    public void ClampsAndSkipsMissingFile()
    {
        using var t = new TempDir();
        var file = t.File("GlobalBasicSettings_13.xml");
        Assert.Null(GameplayStore.Read(file));
        Assert.False(GameplayStore.Write(file, new GameplaySettings()));
        File.WriteAllText(file, Xml);
        GameplayStore.Write(file, new GameplaySettings { MouseSensitivity = 50, Volume = -1 });
        var g = GameplayStore.Read(file)!;
        Assert.Equal(4, g.MouseSensitivity);
        Assert.Equal(0, g.Volume);
    }

    [Fact]
    public void LauncherWritesPendingGameplayOnlyWhenFileExists()
    {
        using var t = new TempDir();
        var paths = new AppPaths(t.Path);
        var s = new AppSettings();
        s.Launcher.PendingGameplay = new GameplaySettings { Volume = .2 };
        // Mirrors the launcher's step: missing file keeps the change pending.
        Assert.False(GameplayStore.Write(paths.GlobalBasicSettings, s.Launcher.PendingGameplay));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GlobalBasicSettings)!);
        File.WriteAllText(paths.GlobalBasicSettings, Xml);
        Assert.True(GameplayStore.Write(paths.GlobalBasicSettings, s.Launcher.PendingGameplay));
        Assert.Equal(.2, GameplayStore.Read(paths.GlobalBasicSettings)!.Volume, 4);
    }
}

public class RobloxLogTests
{
    const string Joining = "2026-11-20T18:01:02.345Z,12.345,1a2b,6 [FLog::Output] ! Joining game 'a1b2c3d4-0000-4000-8000-123456789abc' place 606849621 at 128.116.1.2";
    const string Replicator = "2026-11-20T18:01:03.000Z,13.0,1a2b,6 [FLog::Network] Replicator created: 0x1234";
    const string Loaded = "2026-11-20T18:01:09.000Z,19.0,1a2b,6 [FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:606849621";

    [Fact]
    public void ParsesJoinMilestones()
    {
        Assert.Equal(new JoinEvent(JoinStage.Joining, 606849621, "128.116.1.2"), RobloxLog.Parse(Joining));
        Assert.Equal(JoinStage.Connected, RobloxLog.Parse(Replicator)!.Stage);
        Assert.Equal(JoinStage.Loaded, RobloxLog.Parse(Loaded)!.Stage);
        Assert.Equal(JoinStage.Left, RobloxLog.Parse("x [FLog::SingleSurfaceApp] leaveUGCGameInternal")!.Stage);
        Assert.Null(RobloxLog.Parse("2026 [FLog::Output] Settings date header was not a valid date."));
    }

    [Fact]
    public async Task FollowsALogThatIsStillBeingWritten()
    {
        using var t = new TempDir();
        var started = DateTime.UtcNow;
        File.WriteAllText(t.File("0.123_20261120T180100Z_Studio_1a2b_last.log"), Loaded + "\n"); // not a player log
        var log = t.File("0.695_20261120T180101Z_Player_1A2B3_last.log");
        var events = new List<JoinEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var watch = RobloxLog.WatchAsync(t.Path, started, e => { lock (events) events.Add(e); }, cts.Token);

        await using (var w = new StreamWriter(new FileStream(log, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true })
        {
            await w.WriteAsync(Joining + "\n" + Replicator[..20]);
            await Task.Delay(700);
            await w.WriteAsync(Replicator[20..] + "\n" + Loaded + "\n");
            while (!cts.IsCancellationRequested) { lock (events) if (events.Count >= 3) break; await Task.Delay(50); }
        }
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => watch);
        Assert.Equal([JoinStage.Joining, JoinStage.Connected, JoinStage.Loaded], events.Select(e => e.Stage));
    }

    [Fact]
    public void IgnoresLogsFromBeforeTheLaunch()
    {
        using var t = new TempDir();
        var old = t.File("old_Player_last.log");
        File.WriteAllText(old, "");
        File.SetCreationTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        Assert.Null(RobloxLog.FindLog(t.Path, DateTime.UtcNow));
        Assert.Null(RobloxLog.FindLog(t.File("missing"), DateTime.UtcNow));
    }
}
