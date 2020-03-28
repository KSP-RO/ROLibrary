using System;

namespace ROLib
{
    public class ROLGameSettings : GameParameters.CustomParameterNode
    {

        [GameParameters.CustomParameterUI("Persistent Recolor Selections", toolTip = "If true, custom recolor selections will persist across texture set changes.")]
        public bool persistRecolorSelections = false;

        [GameParameters.CustomParameterUI("Enable Verbose Logging", toolTip = "Additional Verbose logging can help when troubleshooting, but also reduces performance.")]
        public bool enabledDebugLogging = false;
        
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

        public static bool persistRecolor()
        {
            if (HighLogic.CurrentGame != null)
            {
                return HighLogic.CurrentGame.Parameters.CustomParams<ROLGameSettings>().persistRecolorSelections;
            }
            return false;
        }

        public static bool LoggingEnabled
        {
            get
            {
                if (HighLogic.CurrentGame != null)
                {
                    return HighLogic.CurrentGame.Parameters.CustomParams<ROLGameSettings>().enabledDebugLogging;
                }
                return false;
            }
        }

    }
}
