// ClaudeSessionDockBand.cs
//
// A Command Palette Dock band that shows your Claude Code 5-hour session
// usage (and weekly usage as a tooltip/subtitle). Modeled on the pattern
// Microsoft uses for the built-in "Time & Date" NowDockBand: a lightweight
// item that refreshes itself on a timer and exposes itself via
// ICommandProvider3.GetDockBands().

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessionDock;

public sealed partial class ClaudeSessionDockBand : CommandItem, IDisposable
{
    private readonly string _cachePath;
    private readonly Timer _timer;

    public ClaudeSessionDockBand() : base(new ClaudeSessionDockPage())
    {
        _cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            "dock_status.json");

        Title = "Claude";
        Subtitle = "Session: --%";
        Icon = new IconInfo("\uE945"); // placeholder glyph; swap for a Claude icon asset

        Refresh(null);

        // Poll every 15s. The cache file itself only changes when Claude Code
        // sends you a new statusline update (i.e. after each turn), so this
        // is just cheap enough to feel live without hammering disk I/O.
        _timer = new Timer(Refresh, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    private void Refresh(object? state)
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                Subtitle = "No session data yet";
                return;
            }

            using var stream = File.Open(_cachePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // PropertyNameCaseInsensitive is required: dock_status.json uses
            // camelCase (written by the PowerShell bridge script's ConvertTo-Json),
            // but these C# properties are PascalCase. Without this, every field
            // silently deserializes to its default (0 / null) instead of erroring -
            // confirmed as the cause of the band showing "0%" for everything.
            var status = JsonSerializer.Deserialize<DockStatus>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (status is null)
            {
                return;
            }

            var resetIn = FormatResetCountdown(status.SessionResetsAt);

            // Compact form to match the other taskbar tiles (CPU/Memory/etc.):
            // just "N%" in Title. Full breakdown lives in Subtitle for
            // Default (non-compact) dock size or on hover.
            Title = $"{status.SessionPercent}%";
            Subtitle = $"Claude session: {status.SessionPercent}% used ({resetIn})  |  weekly: {status.WeeklyPercent}%";

            // Swap icon glyph as usage climbs, mirroring the
            // green/yellow/red convention used by the terminal statuslines.
            Icon = status.SessionPercent switch
            {
                >= 90 => new IconInfo("\uEA6A"), // warning glyph
                >= 70 => new IconInfo("\uE7BA"), // caution glyph
                _ => new IconInfo("\uE945"),      // normal glyph
            };
        }
        catch (Exception ex)
        {
            // Surface parse/read errors instead of silently going stale forever -
            // this is what would have hidden the epoch-seconds mismatch bug.
            Subtitle = $"Claude Dock error: {ex.GetType().Name}";
        }
    }

    private static string ProgressBar(int percent, int width = 10)
    {
        var filled = (int)Math.Round(percent / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);
        return new string('#', filled) + new string('.', width - filled);
    }

    // sessionResetsAt / weeklyResetsAt come through as Unix epoch seconds
    // (e.g. 1784097000), not ISO date strings - confirmed from the actual
    // dock_status.json payload. Convert and format as a countdown.
    private static string FormatResetCountdown(long? epochSeconds)
    {
        if (epochSeconds is null or 0)
        {
            return "reset --";
        }

        var resetsAt = DateTimeOffset.FromUnixTimeSeconds(epochSeconds.Value);
        var remaining = resetsAt - DateTimeOffset.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            return "resetting";
        }

        return remaining.TotalHours >= 1
            ? $"resets in {(int)remaining.TotalHours}h{remaining.Minutes}m"
            : $"resets in {remaining.Minutes}m";
    }

    public void Dispose() => _timer.Dispose();

    private sealed class DockStatus
    {
        public string? Model { get; set; }
        public int ContextPercent { get; set; }
        public int SessionPercent { get; set; }
        public long? SessionResetsAt { get; set; }
        public int WeeklyPercent { get; set; }
        public long? WeeklyResetsAt { get; set; }
        public string? UpdatedAt { get; set; }
    }
}