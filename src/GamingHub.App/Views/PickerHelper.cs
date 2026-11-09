using Windows.Storage.Pickers;

namespace GamingHub.App.Views;

/// <summary>UI-only helper: shows OS file/folder pickers and returns the chosen path.</summary>
public static class PickerHelper
{
    public static async Task<string?> PickFileAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.Main));
        foreach (var e in extensions) picker.FileTypeFilter.Add(e);
        var f = await picker.PickSingleFileAsync();
        return f?.Path;
    }

    public static async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.Main));
        picker.FileTypeFilter.Add("*");
        var f = await picker.PickSingleFolderAsync();
        return f?.Path;
    }
}
