using System;
using System.Runtime.InteropServices;

namespace SCtoolGui
{
    /// <summary>
    /// SetForegroundWindow のフォアグラウンドロック回避に使う Win32 呼び出し群。
    /// ScreenCapture(対象ウィンドウの前面化)と MainWindow(自ウィンドウの前面復帰)の両方が
    /// 「現在の前面スレッドへ一時的に AttachThreadInput してから前面化する」という同じ手法を
    /// 使っていたため、ここに共通化してある。
    /// </summary>
    internal static class ForegroundWindowHelper
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        /// <summary>現在の前面ウィンドウのハンドル。</summary>
        public static IntPtr GetForeground() => GetForegroundWindow();

        /// <summary>
        /// 対象を前面化する試行を1回行う。SetForegroundWindow はOSの仕様で無視されることがあるため、
        /// 成否確認が必要な呼び出し側(対象ウィンドウの撮影など)は戻り値を待たずポーリングで確認すること。
        /// </summary>
        public static void TryForegroundOnce(IntPtr hwnd)
        {
            IntPtr fg = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
            uint thisThread = GetCurrentThreadId();

            bool attached = false;
            try
            {
                if (fgThread != thisThread && fgThread != 0)
                {
                    attached = AttachThreadInput(thisThread, fgThread, true);
                }

                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached) AttachThreadInput(thisThread, fgThread, false);
            }
        }
    }
}
