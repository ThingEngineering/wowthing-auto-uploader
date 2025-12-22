using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace WowthingAutoUploader;

public partial class MainWindow : Window
{
    private UploaderConfig? _config;

#if DEBUG
    private static readonly string[] UploadHosts = ["https://localhost:55501", "https://wowthing.org"];
#else
    private static readonly string[] UploadHosts = ["https://wowthing.org"];
#endif

    public MainWindow()
    {
        InitializeComponent();

        LoadConfig();
    }

    private static string ConfigDir =>
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WowthingAutoUploader");

    private static string ConfigFile => Path.Join(ConfigDir, "config.json");

    private void LoadConfig()
    {
        try
        {
            string text = File.ReadAllText(ConfigFile);
            _config = JsonSerializer.Deserialize<UploaderConfig>(text);
        }
        catch
        {
            // ignored
        }

        if (_config == null)
        {
            _config = new UploaderConfig();
            SaveConfig();
        }

        ApiKey.Text = _config.ApiKey;
        WowFolder.Text = _config.WowFolder;
    }

    private void SaveConfig()
    {
        if (!Directory.Exists(ConfigDir))
        {
            Directory.CreateDirectory(ConfigDir);
        }

        // try
        // {
        string json = JsonSerializer.Serialize(_config);
        File.WriteAllText(ConfigFile, json);
        // }
        // catch
        // {
        //     // ruh roh
        // }
    }

    private async void WowFolder_OnTapped(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is { StorageProvider.CanPickFolder: true })
        {
            var options = new FolderPickerOpenOptions
            {
                Title = "Select your World of Warcraft folder",
                AllowMultiple = false
            };
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Any())
            {
                var folder = folders[0];
                string folderPath = folder.Path.LocalPath;
                WowFolder.Text = folderPath;
                _config!.WowFolder = folderPath;
                SaveConfig();
            }
        }
    }

    private void ApiKey_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _config!.ApiKey = ApiKey.Text;
        SaveConfig();
    }

    // TODO: window move/resize => save location/size
    // https://github.com/AvaloniaUI/Avalonia/discussions/7836
}
