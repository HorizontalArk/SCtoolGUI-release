using System;
using Velopack;

namespace SCtoolGui
{
    /// <summary>
    /// アプリのエントリポイント。Velopack のインストール/更新フックは
    /// WPF が立ち上がる前に処理しないといけないため、ここで最初に Run する。
    /// フック実行時（インストール直後など）は Velopack 側が処理して即終了する。
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
