using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using ParadoxModTool.Notes;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Anhänge von Meldungen anzeigen: Vorschaubilder (nur Discord- und GitHub-Adressen, siehe <see cref="BugAttachment.CanPreview"/>), sonst Links.
/// Bilder werden erst beim Anzeigen geladen, höchstens 10 MB, und für die Sitzung im Speicher gehalten; auf die Platte kommt nichts.
/// </summary>
internal static class AttachmentPreview
{
    private const long MaxBytes = 10 * 1024 * 1024;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Geladene Bilder je Adresse ohne Parameter (Discord signiert dieselbe Datei bei jedem Abruf neu).</summary>
    private static readonly Dictionary<string, BitmapSource> Cache = new(StringComparer.Ordinal);

    /// <summary>Leiste mit allen Anhängen; <see langword="null"/> ohne Anhänge.</summary>
    public static FrameworkElement? Create(IReadOnlyList<BugAttachment>? attachments, Window owner)
    {
        if (attachments is not { Count: > 0 })
        {
            return null;
        }

        var panel = new Controls.WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        foreach (var attachment in attachments)
        {
            panel.Children.Add(attachment.IsImage && BugAttachment.CanPreview(attachment.Url) ? Thumbnail(attachment, owner) : Link(attachment));
        }

        return panel;
    }

    /// <summary>Öffnet eine https-Adresse im Browser; andere Adressen werden ignoriert.</summary>
    public static void OpenInBrowser(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Kein Browser: nichts zu tun.
        }
    }

    /// <summary>Lädt ein Bild (aus dem Speicher oder übers Netz); <see langword="null"/>, wenn es nicht ladbar ist.</summary>
    public static async Task<BitmapSource?> LoadAsync(string url)
    {
        var key = url.Split('?')[0];
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!BugAttachment.CanPreview(url))
        {
            return null;
        }

        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            buffer.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = buffer;
            image.EndInit();
            image.Freeze();
            Cache[key] = image;
            return image;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static FrameworkElement Thumbnail(BugAttachment attachment, Window owner)
    {
        var holder = new Controls.Button
        {
            Width = 168,
            Height = 128,
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(2),
            ToolTip = attachment.FileName,
            Content = new Controls.TextBlock { Text = attachment.FileName, TextWrapping = TextWrapping.Wrap, FontSize = 11 },
        };
        holder.Click += (_, _) => new ImageViewerWindow(attachment) { Owner = owner }.Show();
        _ = FillAsync(holder, attachment);
        return holder;
    }

    private static async Task FillAsync(Controls.Button holder, BugAttachment attachment)
    {
        var image = await LoadAsync(attachment.Url);
        holder.Content = image is null
            ? new Controls.TextBlock { Text = attachment.FileName + "\n" + BugTexts.WindowImageFailed, TextWrapping = TextWrapping.Wrap, FontSize = 11 }
            : new Controls.Image { Source = image, Stretch = System.Windows.Media.Stretch.Uniform };
    }

    private static FrameworkElement Link(BugAttachment attachment)
    {
        var link = new Controls.Button
        {
            Content = new Controls.TextBlock { Text = "📎 " + attachment.FileName, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 260 },
            ToolTip = attachment.Url,
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 6, 6),
            VerticalAlignment = VerticalAlignment.Top,
        };
        link.Click += (_, _) => OpenInBrowser(attachment.Url);
        return link;
    }
}

/// <summary>Ein Anhang groß, an die Fenstergröße angepasst, mit "Im Browser öffnen".</summary>
internal sealed class ImageViewerWindow : Window
{
    public ImageViewerWindow(BugAttachment attachment)
    {
        Title = BugTexts.ImageTitle(attachment.FileName);
        Width = 1000;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Controls.DockPanel { Margin = new Thickness(8) };
        var open = new Controls.Button { Content = BugTexts.WindowOpenInBrowser, Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
        open.Click += (_, _) => AttachmentPreview.OpenInBrowser(attachment.Url);
        Controls.DockPanel.SetDock(open, Controls.Dock.Top);
        root.Children.Add(open);
        var content = new Controls.ContentControl();
        root.Children.Add(content);
        Content = root;

        Loaded += async (_, _) =>
        {
            var image = await AttachmentPreview.LoadAsync(attachment.Url);
            content.Content = image is null
                ? new Controls.TextBlock { Text = BugTexts.WindowImageFailed, TextWrapping = TextWrapping.Wrap }
                : new Controls.Image { Source = image, Stretch = System.Windows.Media.Stretch.Uniform, StretchDirection = Controls.StretchDirection.DownOnly };
        };
    }
}
