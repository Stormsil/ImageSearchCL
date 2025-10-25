using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ImageSearchCL.Demo;

/// <summary>
/// Утилита для поиска окон по имени процесса или заголовку
/// </summary>
public static class WindowFinder
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Ищет окно Notepad (notepad.exe)
    /// </summary>
    /// <returns>Handle окна или IntPtr.Zero если не найдено</returns>
    public static IntPtr FindNotepad()
    {
        return FindWindowByProcessName("notepad");
    }

    /// <summary>
    /// Ищет окно по имени процесса
    /// </summary>
    /// <param name="processName">Имя процесса без .exe (например, "notepad")</param>
    /// <returns>Handle окна или IntPtr.Zero если не найдено</returns>
    public static IntPtr FindWindowByProcessName(string processName)
    {
        IntPtr foundWindow = IntPtr.Zero;

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true; // Пропускаем невидимые окна

            GetWindowThreadProcessId(hWnd, out uint processId);

            try
            {
                var process = Process.GetProcessById((int)processId);
                if (process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                {
                    foundWindow = hWnd;
                    return false; // Останавливаем перебор
                }
            }
            catch
            {
                // Игнорируем ошибки доступа к процессу
            }

            return true; // Продолжаем перебор
        }, IntPtr.Zero);

        return foundWindow;
    }

    /// <summary>
    /// Получает заголовок окна
    /// </summary>
    public static string GetWindowTitle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return string.Empty;

        int length = GetWindowTextLength(hWnd);
        if (length == 0)
            return string.Empty;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Выводит список всех запущенных окон Notepad
    /// </summary>
    public static List<(IntPtr Handle, string Title)> FindAllNotepadWindows()
    {
        var windows = new List<(IntPtr, string)>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out uint processId);

            try
            {
                var process = Process.GetProcessById((int)processId);
                if (process.ProcessName.Equals("notepad", StringComparison.OrdinalIgnoreCase))
                {
                    string title = GetWindowTitle(hWnd);
                    windows.Add((hWnd, title));
                }
            }
            catch
            {
                // Игнорируем ошибки доступа к процессу
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }
}
