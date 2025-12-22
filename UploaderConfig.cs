using System.Collections.Generic;

namespace WowthingAutoUploader;

public class UploaderConfig
{
    public bool StartMinimized { get; set; } = false;
    public int WindowHeight { get; set; } = 400;
    public int WindowWidth { get; set; } = 400;
    public int WindowX { get; set; } = 100;
    public int WindowY { get; set; } = 100;
    public string ApiKey { get; set; } = string.Empty;
    public string WowFolder { get; set; } = string.Empty;

    public Dictionary<string, int> LastUploaded { get; set; } = new();
}
