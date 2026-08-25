namespace SCtoolGui
{
    /// <summary>
    /// ウィンドウの位置保存で、現在の状態（通常/最大化）に応じて
    /// どの座標を保存すべきかを決める純粋ロジック。
    /// 最大化中は RestoreBounds（元の位置）を保存し、maximized フラグを立てる。
    /// </summary>
    public static class WindowPlacementLogic
    {
        /// <summary>
        /// 保存すべき (left, top, maximized) を返す。
        /// isMaximized が true なら restore 値＋maximized=true、false なら normal 値＋maximized=false。
        /// </summary>
        public static (double left, double top, bool maximized) Resolve(
            bool isMaximized,
            double normalLeft, double normalTop,
            double restoreLeft, double restoreTop)
        {
            return isMaximized
                ? (restoreLeft, restoreTop, true)
                : (normalLeft, normalTop, false);
        }
    }
}
