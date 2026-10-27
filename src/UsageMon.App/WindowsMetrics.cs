using System.Runtime.InteropServices;

namespace UsageMon.App;

public sealed class WindowsMetrics
{
    private ulong lastIdle, lastKernel, lastUser;
    private bool sampled;

    public (double? Cpu, double? Ram) Sample()
    {
        double? cpu = null, ram = null;
        if (GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            var idle = idleTime.Value;
            var kernel = kernelTime.Value;
            var user = userTime.Value;
            if (sampled && kernel >= lastKernel && user >= lastUser && idle >= lastIdle)
            {
                var total = kernel - lastKernel + user - lastUser;
                if (total > 0) cpu = Math.Clamp(100.0 * (total - Math.Min(total, idle - lastIdle)) / total, 0, 100);
            }
            (lastIdle, lastKernel, lastUser, sampled) = (idle, kernel, user, true);
        }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (GlobalMemoryStatusEx(ref memory) && memory.TotalPhysical > 0)
            ram = 100.0 * (memory.TotalPhysical - memory.AvailablePhysical) / memory.TotalPhysical;
        return (cpu, ram);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low, High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
