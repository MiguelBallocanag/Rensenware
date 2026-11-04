namespace Deepwave.RetroOS.Core.Terminal
{
    // Sink that shell and commands write to.
    public interface ITerminalOutput
    {
        void WriteLine(string line = "");
        void Clear();
    }
}
