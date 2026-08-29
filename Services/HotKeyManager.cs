using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SCtoolGui
{
    public class HotKeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private IntPtr _handle;
        private HwndSource? _source;

        public event Action? OnHotKeyPressed;

        /// <summary>ホットキーの登録に成功したか。他アプリに取られていると false になる。</summary>
        public bool IsRegistered { get; private set; }

        public HotKeyManager(Window window, uint modifiers, uint key)
        {
            var helper = new WindowInteropHelper(window);
            _handle = helper.EnsureHandle();

            _source = HwndSource.FromHwnd(_handle);
            _source.AddHook(HwndHook);

            // RegisterHotKey は失敗しても例外を投げず false を返すため、戻り値で成否を保持する。
            IsRegistered = RegisterHotKey(_handle, HOTKEY_ID, modifiers, key);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OnHotKeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterHotKey(_handle, HOTKEY_ID);
            _source?.RemoveHook(HwndHook);
        }
    }
}