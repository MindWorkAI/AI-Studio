using System.Diagnostics;

namespace AIStudio.Tools;

public sealed class PandocPreparedProcess(ProcessStartInfo startInfo, bool isLocal)
{
    public ProcessStartInfo StartInfo => startInfo;

    public bool IsLocal => isLocal;
}