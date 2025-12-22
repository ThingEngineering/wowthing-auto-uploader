using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace WowthingAutoUploader;

public partial class MainWindow : Window
{
    private FileSystemWatcher _watcher = null!;
    private UploaderConfig _config = null!;

#if DEBUG
    private static readonly string[] UploadHosts = ["https://localhost:55501", "https://wowthing.org"];
#else
    private static readonly string[] UploadHosts = ["https://wowthing.org"];
#endif

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        Output.Text = "";

        await LoadConfig();
        await WatchFiles();
    }

    private static string ConfigDir =>
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WowthingAutoUploader");

    private static string ConfigFile => Path.Join(ConfigDir, "config.json");

    private async Task LoadConfig()
    {
        await Log("Loading config from {0}", ConfigFile);

        try
        {
            string text = await File.ReadAllTextAsync(ConfigFile);
            _config = JsonSerializer.Deserialize<UploaderConfig>(text) ?? throw new InvalidOperationException();
        }
        catch
        {
            await Log("ERROR: unable to load config, recreating");
            _config = new UploaderConfig();
            await SaveConfig();
        }

        ApiKey.Text = _config.ApiKey;
        WowFolder.Text = _config.WowFolder;
    }

    private async Task SaveConfig()
    {
        if (!Directory.Exists(ConfigDir))
        {
            Directory.CreateDirectory(ConfigDir);
        }

        // try
        // {
        string json = JsonSerializer.Serialize(_config);
        await File.WriteAllTextAsync(ConfigFile, json);
        // }
        // catch
        // {
        //     // ruh roh
        // }
    }

    private async Task WatchFiles()
    {
        string wtfPath = Path.Join(_config.WowFolder, "_retail_", "WTF", "Account");
        if (!Directory.Exists(wtfPath))
        {
            await Log("ERROR: invalid WoW folder");
            return;
        }

        _watcher = new FileSystemWatcher(wtfPath);

        _watcher.Filter = "WoWthing_Collector.lua";
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.IncludeSubdirectories = true;

        _watcher.Changed += Watcher_OnChanged;
        _watcher.EnableRaisingEvents = true;

        await Log("Watching {0}", wtfPath);
    }

    private void Watcher_OnChanged(object sender, FileSystemEventArgs e)
    {
        string shorterPath = Path.GetRelativePath(_config.WowFolder, e.FullPath);
        Log("File changed: {0}", shorterPath).RunSynchronously();
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
                _config.WowFolder = folderPath;
                await SaveConfig();
            }
        }
    }

    private void ApiKey_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _config.ApiKey = ApiKey.Text ?? "";
        Task.Run(SaveConfig);
    }

    // TODO: window move/resize => save location/size
    // https://github.com/AvaloniaUI/Avalonia/discussions/7836

    private async Task Log(string text, params string[] args)
    {
        if (args.Length > 0)
        {
            text = string.Format(text, args);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!string.IsNullOrEmpty(Output.Text))
            {
                Output.Text += Environment.NewLine;
            }

            Output.Text += $"[{DateTime.Now:HH:mm:ss}] {text}";
        });
    }
}
