using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using RoseTrap.ViewModels;

namespace RoseTrap.Views;

public partial class LoadingWindow : Window
{
    public LoadingWindow()
    {
        InitializeComponent();
        HideButton.Click += (_, _) => Session?.Finish();
        Card.PointerPressed += (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); };
        DataContextChanged += async (_, _) =>
        {
            if (Session is not { } s) return;
            await s.Done;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Card.Opacity = 0;
                await Task.Delay(380);
                Close();
            });
        };
    }

    public LoadingSession? Session => DataContext as LoadingSession;
}
