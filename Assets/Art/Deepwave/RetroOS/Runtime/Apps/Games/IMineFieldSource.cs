using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Games
{
    // Board size for Mines, wherever the program was configured - as an asset
    // dragged into a profile, or as a component in the scene.
    public interface IMineFieldSource
    {
        int Columns { get; }
        int Rows { get; }
        int Mines { get; }
    }
}
