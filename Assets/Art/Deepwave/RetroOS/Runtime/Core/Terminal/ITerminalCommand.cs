namespace Deepwave.RetroOS.Core.Terminal
{
    // One executable command word.
    public interface ITerminalCommand
    {
        string Name { get; }
        string Help { get; }
        void Execute(string[] args, ShellContext ctx);
    }
}
