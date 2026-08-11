namespace SCtoolGui
{
    public enum PreviewMode { Horizontal, Vertical }

    public enum AutoSwitchDecision { None, Prompt, Switch }

    /// <summary>プレビューの向き判定と自動切替の決定を行う純粋ロジック。</summary>
    public static class PreviewOrientationLogic
    {
        public static PreviewMode DetectImageOrientation(double width, double height)
            => height > width ? PreviewMode.Vertical : PreviewMode.Horizontal;

        public static AutoSwitchDecision Decide(string autoSwitchSetting, PreviewMode current, PreviewMode image)
        {
            if (autoSwitchSetting == "Off") return AutoSwitchDecision.None;
            if (current == image) return AutoSwitchDecision.None;
            if (autoSwitchSetting == "Force") return AutoSwitchDecision.Switch;
            if (autoSwitchSetting == "Prompt") return AutoSwitchDecision.Prompt;
            return AutoSwitchDecision.None;
        }
    }
}
