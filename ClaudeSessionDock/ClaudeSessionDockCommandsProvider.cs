using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessionDock;

public partial class ClaudeSessionDockCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ClaudeSessionDockBand _dockStats = new();

    public ClaudeSessionDockCommandsProvider()
    {
        DisplayName = "Claude Session";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands = [new CommandItem(new ClaudeSessionDockPage()) { Title = DisplayName }];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands()
    {
        var band = new WrappedDockItem(
            _dockStats.Items,
            "com.tuongphantrue.claudesessiondock.band",
            "Claude Session");

        return [band];
    }
}