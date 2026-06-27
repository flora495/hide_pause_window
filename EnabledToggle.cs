using JumpKing.PauseMenu.BT.Actions;

namespace HidePauseWindow
{
    public sealed class EnabledToggle : ITextToggle
    {
        public EnabledToggle()
            : base(ModEntry.Settings.IsEnabled)
        {
        }

        protected override string GetName()
        {
            return "Enabled";
        }

        protected override void OnToggle()
        {
            ModEntry.Settings.IsEnabled = base.toggle;
            ModEntry.SaveSettings();
        }
    }
}
