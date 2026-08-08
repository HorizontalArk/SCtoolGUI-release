using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SCtoolGui
{
    public class WindowItem : IWindowSnapshot
    {
        /// <summary>ウィンドウタイトル。アプリの状態で変化しうるため、識別子には使わない。</summary>
        public string Title { get; set; } = string.Empty;

        public IntPtr Handle { get; set; }

        /// <summary>実行ファイルのフルパス。取得できなかった場合は空。ターゲットの永続的な識別子。</summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>実行ファイル名（拡張子なし）。表示名の初期値に使う。</summary>
        public string ProcessName =>
            string.IsNullOrEmpty(ExecutablePath) ? string.Empty : Path.GetFileNameWithoutExtension(ExecutablePath);
    }

    public static class WindowManager
    {
        // ★修正: SetLastError = true を追加し、OSからエラーコードを受け取れるようにする
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        public static List<WindowItem> GetTargetWindows()
        {
            var windows = new List<WindowItem>();

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    int length = GetWindowTextLength(hWnd);
                    if (length > 0)
                    {
                        var builder = new StringBuilder(length + 1);
                        GetWindowText(hWnd, builder, builder.Capacity);
                        string title = builder.ToString();

                        if (!string.IsNullOrEmpty(title) && title != "SCtool" && title != "詳細設定")
                        {
                            windows.Add(new WindowItem
                            {
                                Title = title,
                                Handle = hWnd,
                                ExecutablePath = GetExecutablePath(hWnd),
                            });
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        // ★修正: void から bool に変更し、命令がOSに通ったかどうかを返すようにする
        public static bool SetAlwaysOnTop(IntPtr handle, bool isTopmost)
        {
            if (handle == IntPtr.Zero) return false;
            return SetWindowPos(handle, isTopmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }

        public static bool IsWindowMinimized(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return false;
            return IsIconic(handle);
        }

        /// <summary>ハンドルが今も生きているウィンドウを指しているか。</summary>
        public static bool IsWindowAlive(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return false;
            return IsWindow(handle);
        }

        /// <summary>
        /// ウィンドウを所有するプロセスの実行ファイルパスを返す。
        /// 権限不足などで取得できない場合は空文字を返す（呼び出し側はタイトル照合へフォールバックする）。
        /// </summary>
        private static string GetExecutablePath(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return string.Empty;

            // MainModule と違い、権限の強いプロセスや32/64bit混在でも取得できることが多い
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return string.Empty;

            try
            {
                uint capacity = 1024;
                var buffer = new StringBuilder((int)capacity);
                return QueryFullProcessImageName(hProcess, 0, buffer, ref capacity)
                    ? buffer.ToString()
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
    }
}