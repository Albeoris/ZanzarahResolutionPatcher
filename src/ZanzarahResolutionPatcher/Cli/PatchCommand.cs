using System.ComponentModel;
using Spectre.Console.Cli;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Cli;

[Description("Patch hardcoded resolutions in Zanzarah: The Hidden Portal.")]
public sealed class PatchCommand : Command<PatchCommandSettings>
{
    protected override int Execute(
        CommandContext context,
        PatchCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var options = new PatchOptionsFactory().Create(settings);
        return PatchApplication.CreateDefault().Run(options);
    }
}
