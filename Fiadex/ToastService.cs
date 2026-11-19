using System.Collections.ObjectModel;
using System.Windows;

namespace Fiadex;

public sealed class ToastService
{
    public ObservableCollection<ToastMessage> Messages { get; } = [];

    public void Show(string text, bool isError = false)
    {
        var toast = new ToastMessage(text, isError);
        Application.Current?.Dispatcher.Invoke(() => Messages.Add(toast));
        _ = RemoveAfterDelayAsync(toast);
    }

    private async Task RemoveAfterDelayAsync(ToastMessage toast)
    {
        await Task.Delay(4500);
        Application.Current?.Dispatcher.Invoke(() => Messages.Remove(toast));
    }
}
