using System.Collections.ObjectModel;
using System.Windows;

namespace Fiadex;

public sealed class ToastService
{
    private const int MaxVisibleToasts = 3;

    public ObservableCollection<ToastMessage> Messages { get; } = [];

    public void Show(string text, bool isError = false)
    {
        var toast = new ToastMessage(text, isError);
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Messages.Add(toast);
            while (Messages.Count > MaxVisibleToasts)
            {
                Messages.RemoveAt(0);
            }
        });
        _ = RemoveAfterDelayAsync(toast);
    }

    private async Task RemoveAfterDelayAsync(ToastMessage toast)
    {
        await Task.Delay(4500);
        Application.Current?.Dispatcher.Invoke(() => Messages.Remove(toast));
    }
}
