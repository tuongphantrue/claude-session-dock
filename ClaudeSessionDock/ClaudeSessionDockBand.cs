// ClaudeSessionDockBand.cs
//
// Holds four short-lived dock items (session, weekly, context, updated),
// refreshed on a shared timer from dock_status.json, and manually
// refreshable by clicking any one of them.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessionDock;

public sealed partial class ClaudeSessionDockBand : IDisposable
{
    private readonly string _cachePath;
    private readonly Timer _timer;

    private readonly ListItem _sessionItem;
    private readonly ListItem _weeklyItem;
    private readonly ListItem _contextItem;
    private readonly ListItem _updatedItem;

    public IListItem[] Items => [_sessionItem, _weeklyItem, _contextItem, _updatedItem];

    public ClaudeSessionDockBand()
    {
        _cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            "dock_status.json");

        // All four items trigger the same manual refresh - clicking any one
        // of them re-reads the cache file immediately instead of waiting up
        // to 15s for the timer.
        var refreshCommand = new RefreshCommand(this);

        _sessionItem = new ListItem(refreshCommand) { Title = "5h --%", Icon = new IconInfo("\uE945") };
        _weeklyItem = new ListItem(refreshCommand) { Title = "Wk --%" };
        _contextItem = new ListItem(refreshCommand) { Title = "Ctx --%" };
        _updatedItem = new ListItem(refreshCommand) { Title = "Upd --:--" };

        Refresh(null);
        _timer = new Timer(Refresh, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    private void Refresh(object? state)
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                _sessionItem.Title = "No data";
                return;
            }

            using var stream = File.Open(_cachePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var status = JsonSerializer.Deserialize<DockStatus>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (status is null)
            {
                return;
            }

            var resetIn = FormatResetCountdown(status.SessionResetsAt);
            var updatedAtLocal = TryParseUpdatedAt(status.UpdatedAt);

            _sessionItem.Title = $"5h {status.SessionPercent}%";
            _sessionItem.Subtitle = resetIn;
            _sessionItem.Icon = status.SessionPercent switch
            {
                >= 90 => new IconInfo("\uEA6A"),
                >= 70 => new IconInfo("\uE7BA"),
                _ => new IconInfo("\uE945"),
            };

            _weeklyItem.Title = $"Wk {status.WeeklyPercent}%";
            _weeklyItem.Subtitle = FormatResetCountdown(status.WeeklyResetsAt);

            _contextItem.Title = $"Ctx {status.ContextPercent}%";
            _contextItem.Subtitle = status.Model is null ? "" : status.Model;

            _updatedItem.Title = updatedAtLocal is null ? "Upd --:--" : $"Upd {updatedAtLocal:t}";
            _updatedItem.Subtitle = "Statusline event";
        }
        catch (Exception ex)
        {
            _sessionItem.Title = "Error";
            _sessionItem.Subtitle = ex.GetType().Name;
        }
    }

    private static DateTimeOffset? TryParseUpdatedAt(string? updatedAt) =>
        DateTimeOffset.TryParse(updatedAt, out var parsed) ? parsed.ToLocalTime() : null;

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
            ? $"resets {(int)remaining.TotalHours}h{remaining.Minutes}m"
            : $"resets {remaining.Minutes}m";
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

    // Manually re-reads dock_status.json when clicked, instead of waiting
    // for the 15s polling timer.
    private sealed partial class RefreshCommand : InvokableCommand
    {
        private readonly ClaudeSessionDockBand _band;

        public RefreshCommand(ClaudeSessionDockBand band)
        {
            _band = band;
        }

        public override string Name => "Refresh Claude session stats";

        public override ICommandResult Invoke()
        {
            _band.Refresh(null);
            return CommandResult.KeepOpen();
        }
    }
}