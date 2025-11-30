# WowthingAutoUploader

Blah blah uploads. Avalonia. Velopack.

## Publishing

- `dotnet install -g vpk`
- `dotnet publish --self-contained -r win-x64 -o .\publish`
- `vpk pack --packId "ThingEngineering.WowthingAutoUploader" --packVersion 0.0.1 --packDir .\publish --mainExe WowthingAutoUploader.exe`
- Upload files from `Release` to ?somewhere?
