using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SCtoolGui
{
    /// <summary>
    /// プロセスの昇格（管理者）状態の判定と、自己昇格による再起動を行う。
    ///
    /// 通常は asInvoker で起動し、管理者権限で動く対象ウィンドウを扱う時だけ
    /// このクラスで昇格を判定・実行する（普段は UAC を出さないため）。
    /// </summary>
    public static class ProcessElevation
    {
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass,
            out uint TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint TOKEN_QUERY = 0x0008;
        private const int TokenElevation = 20; // TOKEN_INFORMATION_CLASS.TokenElevation

        /// <summary>現在のプロセス（自分自身）が昇格（管理者）で動いているか。</summary>
        public static bool IsCurrentProcessElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// 指定 PID のプロセスが昇格（管理者）で動いているか。
        /// プロセスやトークンを開けず判定できない場合は、相手が上位権限の可能性が高いため true を返す。
        /// </summary>
        public static bool IsProcessElevated(uint pid)
        {
            if (pid == 0) return false;

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return true; // 開けない＝上位権限の可能性が高い

            try
            {
                if (!OpenProcessToken(hProcess, TOKEN_QUERY, out IntPtr hToken))
                    return true; // トークンを開けない＝上位権限の可能性が高い

                try
                {
                    if (GetTokenInformation(hToken, TokenElevation, out uint elevated, sizeof(uint), out _))
                        return elevated != 0;
                    return false; // 取得できたが不明 → 昇格なし扱い
                }
                finally { CloseHandle(hToken); }
            }
            finally { CloseHandle(hProcess); }
        }

        /// <summary>指定ウィンドウのプロセスが昇格（管理者）で動いているか。</summary>
        public static bool IsWindowProcessElevated(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            GetWindowThreadProcessId(hWnd, out uint pid);
            return IsProcessElevated(pid);
        }

        /// <summary>
        /// 自分自身を管理者として起動し直す。UAC をキャンセルされた等で起動できなければ false。
        /// 成功時（true）は、呼び出し側で速やかにアプリを終了すること。
        /// </summary>
        public static bool RelaunchAsAdmin()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return false;

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true, // runas には ShellExecute が必要
                    Verb = "runas",
                });
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false; // UAC キャンセル(1223)など
            }
        }
    }
}
