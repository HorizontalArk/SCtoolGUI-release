using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SCtoolGui
{
    /// <summary>
    /// 多重起動を防ぐための単一起動ガード。
    /// 名前付き Mutex で「起動中か」を判定する。管理者として再起動する時は、
    /// 新しいインスタンスがすぐに取得できるよう <see cref="Release"/> で先に手放す。
    /// </summary>
    public static class SingleInstance
    {
        private const string MutexName = "SCtoolGui_SingleInstance_v1";
        private static Mutex? _mutex;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        /// <summary>単一起動の権利を取得できたら true。既に起動中なら false。</summary>
        public static bool TryAcquire()
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }
            return true;
        }

        /// <summary>権利を手放す。管理者として再起動する直前に呼ぶ。</summary>
        public static void Release()
        {
            try { _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
            _mutex = null;
        }

        /// <summary>既に起動しているウィンドウ（タイトル "SCtool"）を前面に出す。</summary>
        public static void ActivateExisting()
        {
            IntPtr h = FindWindow(null, "SCtool");
            if (h != IntPtr.Zero)
            {
                ShowWindow(h, SW_RESTORE);
                SetForegroundWindow(h);
            }
        }
    }
}
