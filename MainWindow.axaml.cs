using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using UrlCombineLib;

namespace WowthingAutoUploader;

public partial class MainWindow : Window
{
#if DEBUG
    private static readonly string[] UploadHosts = ["https://localhost:55501", "https://wowthing.org"];
#else
    private static readonly string[] UploadHosts = ["https://wowthing.org"];
#endif

    // dev has an invalid certificate
#if DEBUG
    private readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
    });
#else
    private readonly HttpClient _httpClient = new();
#endif

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentQueue<string> _uploadQueue = new();
    private readonly Timer _uploadTimer = new(500);
    private readonly TimeSpan _waitInterval = TimeSpan.FromSeconds(2);

    private bool _isUploading;
    private FileSystemWatcher _watcher = null!;
    private UploaderConfig _config = null!;

    private static string ConfigDir =>
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WowthingAutoUploader");

    private static string ConfigFile => Path.Join(ConfigDir, "config.json");

    private string WtfAccountPath => Path.Join(_config.WowFolder, "_retail_", "WTF", "Account");

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;

        // HttpClient
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WowthingAutoUploader");
        _httpClient.Timeout = TimeSpan.FromSeconds(20);

        // Timer
        _uploadTimer.Elapsed += UploadTimer_Elapsed;
        _uploadTimer.Enabled = true;
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        Output.Text = "";

        await LoadConfig();
        await WatchFiles();
    }

    private void Log(string text, params string[] args)
    {
        Task.Run(async () => await LogAsync(text, args));
    }

    private async Task LogAsync(string text, params string[] args)
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

    private async Task LoadConfig()
    {
        await LogAsync("Loading config from {0}", ConfigFile);

        try
        {
            string text = await File.ReadAllTextAsync(ConfigFile);
            _config = JsonSerializer.Deserialize<UploaderConfig>(text) ?? throw new InvalidOperationException();
        }
        catch
        {
            await LogAsync("ERROR: unable to load config, recreating");
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
        if (!Directory.Exists(WtfAccountPath))
        {
            await LogAsync("ERROR: invalid WoW folder");
            return;
        }

        _watcher = new FileSystemWatcher(WtfAccountPath);

        _watcher.Filter = "WoWthing_Collector.lua";
        _watcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite |
                                NotifyFilters.Size;
        _watcher.IncludeSubdirectories = true;

        _watcher.Changed += Watcher_OnChanged;
        _watcher.Created += Watcher_OnChanged;
        _watcher.Renamed += Watcher_OnChanged;
        _watcher.EnableRaisingEvents = true;

        await LogAsync("Watching {0}", WtfAccountPath);
    }

    private void Watcher_OnChanged(object sender, FileSystemEventArgs e)
    {
#if DEBUG
        Log("File changed: {0}", Path.GetRelativePath(WtfAccountPath, e.FullPath));
#endif

        Task.Run(async () =>
        {
            await Task.Delay(_waitInterval);
            _uploadQueue.Enqueue(e.FullPath);
        });
    }

    private void UploadTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isUploading || !_uploadQueue.TryDequeue(out string filePath))
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            return;
        }

        // TODO: mtime tracking
        // var mtime = File.GetLastWriteTimeUtc(filePath);

        _isUploading = true;
        Task.Run(() => Upload(filePath));
    }

    private async Task Upload(string filePath)
    {
        var upload = new ApiUpload
        {
            ApiKey = _config.ApiKey,
            LuaFile = await File.ReadAllTextAsync(filePath, Encoding.UTF8)
        };
        string json = JsonSerializer.Serialize(upload, _jsonOptions);
        var content = new CompressedContent(new StringContent(json, Encoding.UTF8, "application/json"));

        string relativePath = Path.GetRelativePath(WtfAccountPath, filePath);
        await LogAsync("Uploading {0}...", relativePath);

        foreach (string uploadHost in UploadHosts)
        {
            string url = UrlCombine.Combine(uploadHost, "/api/upload");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    await LogAsync("Uploaded {0}", relativePath);
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    await LogAsync("Upload failed: {0}", errorMessage);
                }

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                await LogAsync("EXCEPTION: {0}", e.Message);
            }
        }

        _isUploading = false;
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

    public class ApiUpload
    {
        public string ApiKey { get; set; }
        public string LuaFile { get; set; }
    }
}
