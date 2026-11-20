using Kerlo.Core;
using Microsoft.Win32;

namespace Kerlo.Services;

public static class StartupReader
{
    private const string Run = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";

    public static IReadOnlyList<StartupItem> Read()
    {
        var entries = new List<StartupItem>();
        ReadKey(Registry.CurrentUser, Run, "Current user", entries);
        ReadKey(Registry.LocalMachine, Run, "All users", entries);
        ReadKey(Registry.LocalMachine, Run32, "All users (32-bit)", entries);
        return entries.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void ReadKey(RegistryKey hive, string keyName, string label, List<StartupItem> entries)
    {
        try
        {
            using var key = hive.OpenSubKey(keyName, false);
            if (key is null) return;
            foreach (var name in key.GetValueNames())
            {
                try
                {
                    var command = key.GetValue(name)?.ToString() ?? "";
                    entries.Add(new StartupItem(name, command, label));
                }
                catch { /* Ignore unreadable entries. */ }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (System.Security.SecurityException) { }
    }
}
