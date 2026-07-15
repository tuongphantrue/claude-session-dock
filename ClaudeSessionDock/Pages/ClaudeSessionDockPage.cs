// ClaudeSessionDockPage.cs
using System;
using System.IO;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessionDock;

internal sealed partial class ClaudeSessionDockPage : ListPage
{
    private readonly string _cachePath;

    public ClaudeSessionDockPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Claude Session";
        Name = "Open";

        _cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            "dock_status.json");
    }

    public override IListItem[] GetItems()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return
                [
                    new ListItem(new NoOpCommand())
                    {
                        Title = "No session data yet",
                        Subtitle = "Send Claude Code a message to populate this - the statusline bridge script writes on every turn.",
                    },
                ];
            }

            using var stream = File.Open(_cachePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var status = JsonSerializer.Deserialize<DockStatus>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (status is null)
            {
                return
                [
                    new ListItem(new NoOpCommand()) { Title = "Couldn't read session data" },
                ];
            }

            var updatedAtLocal = TryParseUpdatedAt(status.UpdatedAt);

            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = $"5-hour session: {status.SessionPercent}%",
                    Subtitle = FormatReset(status.SessionResetsAt),
                },
                new ListItem(new NoOpCommand())
                {
                    Title = $"Weekly limit: {status.WeeklyPercent}%",
                    Subtitle = FormatReset(status.WeeklyResetsAt),
                },
                new ListItem(new NoOpCommand())
                {
                    Title = $"Context window: {status.ContextPercent}%",
                    Subtitle = status.Model is null ? "" : $"Model: {status.Model}",
                },
                new ListItem(new NoOpCommand())
                {
                    Title = updatedAtLocal is null
                        ? "Last updated: unknown"
                        : $"Last updated: {updatedAtLocal:t} ({RelativeTime(updatedAtLocal.Value)})",
                    Subtitle = "Updates each time Claude Code sends a new statusline event.",
                },
            ];
        }
        catch (Exception ex)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Error reading Claude session data",
                    Subtitle = ex.Message,
                },
            ];
        }
    }

    private static DateTimeOffset? TryParseUpdatedAt(string? updatedAt)
    {
        return DateTimeOffset.TryParse(updatedAt, out var parsed) ? parsed.ToLocalTime() : null;
    }

    private static string RelativeTime(DateTimeOffset when)
    {
        var delta = DateTimeOffset.UtcNow - when.ToUniversalTime();
        if (delta.TotalSeconds < 60) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        return $"{(int)delta.TotalHours}h ago";
    }

    private static string FormatReset(long? epochSeconds)
    {
        if (epochSeconds is null or 0) return "Reset time unknown";

        var resetsAt = DateTimeOffset.FromUnixTimeSeconds(epochSeconds.Value).ToLocalTime();
        var remaining = resetsAt - DateTimeOffset.Now;

        if (remaining <= TimeSpan.Zero) return "Resetting now";

        return remaining.TotalHours >= 1
            ? $"Resets in {(int)remaining.TotalHours}h {remaining.Minutes}m (at {resetsAt:t})"
            : $"Resets in {remaining.Minutes}m (at {resetsAt:t})";
    }

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