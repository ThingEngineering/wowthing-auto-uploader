# WowthingAutoUploader

A rewrite of [WoWthing Sync](https://github.com/ThingEngineering/wowthing-sync) in modern C#.

- uses [Avalonia UI](https://avaloniaui.net/), cross-platform UI toolkit - Microsoft really likes throwing out
  their current UI toolkit and starting something new every few years and I don't want to deal with it
- uses [Velopack](https://velopack.io/), relatively new cross-platform install/update solution 

## But Why?

There are multiple reasons that Sync is no longer viable:

- targets an ancient version of .NET (4.x)
- uses WinForms for UI, ancient _and_ horrible to work with
- no installer (fixable)
- no automatic updates (fixable)
- only runs on Windows (not really fixable)

## Publishing

- `dotnet install -g vpk`
- `dotnet publish --self-contained -r win-x64 -o .\publish`
- `vpk pack --packId "ThingEngineering.WowthingAutoUploader" --packVersion 0.0.1 --packDir .\publish --mainExe WowthingAutoUploader.exe`
- Upload files from `Release` to ??? (TODO: set up something to host these files)
