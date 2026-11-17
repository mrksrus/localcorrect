using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LocalCorrect.App.Win32;

/// <summary>
/// A job object that Windows closes when this app ends, however it ends, and that kills every process in it.
/// So a crashed or killed LocalCorrect cannot leave a 1 GB java.exe behind that also locks the install folder.
/// </summary>
internal static class ChildProcessJob
{
    static nint _job;

    public static void Add(Process process)
    {
        if (_job == 0) _job = Create();
        if (!NativeMethods.AssignProcessToJobObject(_job, process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    // The handle stays open for the life of the process on purpose: closing it is what kills the children.
    static nint Create()
    {
        var job = NativeMethods.CreateJobObject(0, null);
        if (job == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION { LimitFlags = NativeMethods.JobObjectLimitKillOnJobClose };
        if (!NativeMethods.SetInformationJobObject(job, NativeMethods.JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return job;
    }
}
