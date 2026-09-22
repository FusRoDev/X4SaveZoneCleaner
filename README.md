# X4 Save Zone Cleaner

A small Windows application for safely inspecting X4: Foundations save files for extremely displaced zones and selectively removing chosen complete zone XML blocks.

> **Warning:** This is an invasive repair tool. Removing a zone block removes the entire XML subtree beneath it, which may include ships, stations, and other objects contained in that zone. Use it at your own risk, select blocks carefully, and keep independent backups of your saves.  

## Features

- Reads regular XML saves and GZip-compressed saves (`.gz` or `.xml.gz`). Compression is detected from the file contents, not only from the file name.
- Analyses `component` elements with `class="zone"` and reports zones where X, Y, or Z exceeds the configurable threshold (default: 1,000,000 m).
- Displays the zone code, zone ID, position, and count of contained ships and stations. Contents of nested zones are not counted twice.
- Lets you preview the complete XML blocks selected for removal before making any change.
- Provides checkboxes and an explicit confirmation before an output file is written.
- Removes only the selected complete `component class="zone"` elements from a copy of the XML tree, then validates the resulting XML tree before writing it.
- Optionally, it also creates a byte-for-byte source backup next to the output file.

## Usage

1. Install the **.NET 10 SDK** if it is not already installed.
2. Download or clone this repository.
3. Double-click `run.cmd`.
4. The application will be built automatically and the GUI will start.
5. Select **Open save file...** and choose a save file.
6. Adjust the threshold if needed, then select **Analyse**.
7. Review the reported zones and select only the zone blocks you intend to remove.
8. Optionally, use **Preview selected XML...** to inspect the exact complete XML blocks that would be removed.
9. Select **Remove selected zone blocks...**, review the confirmation, and choose a new output file name.

The original file remains unchanged.

## Requirements

- Windows 10 or Windows 11
- .NET 10 SDK

Download the official .NET 10 SDK from Microsoft:  
https://dotnet.microsoft.com/download/dotnet/10.0

For a normal 64-bit Windows PC, select the **Windows x64 SDK**.

You do **not** need to install the .NET Runtime separately. The SDK includes everything required to build and run the application.
