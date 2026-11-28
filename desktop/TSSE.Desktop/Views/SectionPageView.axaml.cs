using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TSSE.Desktop.ViewModels;
using TSSE.Savegames.Diagnostics;

namespace TSSE.Desktop.Views;

public partial class SectionPageView : UserControl
{
    public SectionPageView()
    {
        InitializeComponent();
    }

    private async void OnInspectSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SectionViewModel section || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        try
        {
            IReadOnlyList<IStorageFile> selected = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions { AllowMultiple = false, Title = section.FixtureDiagnostics.ImportLabel });
            if (selected.Count == 0)
                return;

            await using Stream input = await selected[0].OpenReadAsync();
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            try
            {
                int read;
                while ((read = await input.ReadAsync(chunk)) != 0)
                {
                    if (buffer.Length + read > SiiSaveContainerDecoder.MaximumFileBytes)
                    {
                        section.FixtureDiagnostics.ReportImportTooLarge();
                        return;
                    }
                    await buffer.WriteAsync(chunk.AsMemory(0, read));
                }

                byte[] bytes = buffer.ToArray();
                try
                {
                    section.FixtureDiagnostics.InspectSelectedBytes(bytes);
                }
                finally
                {
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
                }
            }
            finally
            {
                if (buffer.TryGetBuffer(out ArraySegment<byte> backing) && backing.Array is not null)
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(backing.Array);
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(chunk);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            section.FixtureDiagnostics.ReportImportReadFailure();
        }
    }
}
