// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessionDock;

public partial class ClaudeSessionDockCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ClaudeSessionDockBand _dockBand = new();

    public ClaudeSessionDockCommandsProvider()
    {
        DisplayName = "Claude Session";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands = [_dockBand];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override ICommandItem[]? GetDockBands()
    {
        return [_dockBand];
    }
}