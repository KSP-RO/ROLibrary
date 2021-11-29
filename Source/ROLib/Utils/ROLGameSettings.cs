using System;

namespace ROLib
{
    public class ROLGameSettings : GameParameters.CustomParameterNode
    {

        [GameParameters.CustomParameterUI("Persistent Recolor Selections", toolTip = "If true, custom recolor selections will persist across texture set changes.")]
        public bool persistRecolorSelections = false;

        [GameParameters.CustomParameterUI("Flag Decal Enabled", toolTip = "If selected, the Flag Decal will default to enabled on models where it is present.")]
        public bool enableFlagDecalDefault = true;

        [GameParameters.CustomParameterUI("ROTanks Keyboard Shortcuts", toolTip = "When enabled, N toggles the diameter selector window and J toggles the recolor window when hovering over an ROTank.")]
        public bool enableROTanksEditorShortcuts = true;

        public override string Section { get { return "RO Mods Settings"; } }

        public override int SectionOrder { get { return 1; } }

        public override string Title { get { return "RO Mods Options"; } }

        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

        public override bool HasPresets { get { return true; } }

        public override string DisplaySection
        {
            get
            {
                return "ROMods";
            }
        }

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    break;
                case GameParameters.Preset.Normal:
                    break;
                case GameParameters.Preset.Moderate:
                    break;
                case GameParameters.Preset.Hard:
                    break;
                case GameParameters.Preset.Custom:
                    break;
                default:
                    break;
            }
        }

        public static bool PersistRecolor()
        {
            if (HighLogic.CurrentGame != null)
            {
                return HighLogic.CurrentGame.Parameters.CustomParams<ROLGameSettings>().persistRecolorSelections;
            }
            return false;
        }

        public static bool FlagDecalDefault()
        {
            if (HighLogic.CurrentGame != null)
            {
                return HighLogic.CurrentGame.Parameters.CustomParams<ROLGameSettings>().enableFlagDecalDefault;
            }

            return false;
        }

        public static bool ROTanksEditorShortcuts()
        {
            if (HighLogic.CurrentGame != null)
                return HighLogic.CurrentGame.Parameters.CustomParams<ROLGameSettings>().enableROTanksEditorShortcuts;
            return false;
        }
    }
}
