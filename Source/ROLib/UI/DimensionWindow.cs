using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

namespace ROLib
{
    public class DimensionWindow : AbstractWindow
    {
        public enum Unit { Meter, Foot, Inch };
        public static Dictionary<Unit, string> UnitAbbreviations = new Dictionary<Unit, string>() {
            { Unit.Meter, "m" },
            { Unit.Foot, "ft" },
            { Unit.Inch, "in" },
        };
        private double ConvertUnit(Unit from, Unit to, double val)
        {
            return (from, to) switch
            {
                (Unit.Meter, Unit.Foot) => val / 0.3048,
                (Unit.Meter, Unit.Inch) => val / 0.3048 * 12d,
                (Unit.Foot, Unit.Meter) => val * 0.3048,
                (Unit.Foot, Unit.Inch) => val * 12d,
                (Unit.Inch, Unit.Meter) => val * 0.0254,
                (Unit.Inch, Unit.Foot) => val / 12d,
                _ => val,
            };
        }

        private string TrimmedDecimal(double num, int maxDecimalPlaces = 1) =>
            num.ToString($"N{maxDecimalPlaces}").TrimEnd('0').TrimEnd('.');


        ModuleROTank module;

        double diameter;
        Unit inputUnit = Unit.Meter;
        double diameterFeet => ConvertUnit(Unit.Meter, Unit.Foot, diameter);
        double diameterInches => ConvertUnit(Unit.Meter, Unit.Inch, diameter);
        private double diameterInputUnit => inputUnit switch
        {
            Unit.Meter => diameter,
            Unit.Foot => diameterFeet,
            Unit.Inch => diameterInches,
            _ => throw new Exception(),
        };

        public override Rect InitialPosition => new Rect(300, 300, 400, 600);

        public override string Title => "ROTanks Diameter Selection";

        const int ApplicationPrecision = 3;
        string diameterBuf;
        private void ResetDiameterBuf() => diameterBuf = TrimmedDecimal(diameterInputUnit, ApplicationPrecision);

        List<(string File, string Name, double Diameter)> presets = new List<(string, string, double)>();
        Vector2 presetScroll = new Vector2();
        bool presetCreateMode = false;
        bool presetDeleteMode = false;
        bool showPresetNameOnly = true;
        string presetNameBuf = "";

        static Dictionary<string, double> DiameterRescaleFactors = new Dictionary<string, double>()
        {
            { "3/4 of", 0.75 },
            { "4/3 of", 4d / 3d },
            { "2/3 of", 2d / 3d },
            { "3/2 of", 1.5 },
            { "1/2 of", 0.5 },
            { "2x",     2d },
            { "1/3 of", 1d / 3d },
            { "3x",     3d },
        };

        public void InitForModule(ModuleROTank mod)
        {
            module = mod;
            diameter = mod.currentDiameter;
            ResetDiameterBuf();
            LoadPresetsFromConfigs();
        }

        private void LoadPresetsFromConfigs()
        {
            presets.Clear();
            foreach (string file in Directory.GetFiles($"{KSPUtil.ApplicationRootPath}GameData/ROLib/PluginData/", "*.cfg"))
            {
                var node = ConfigNode.Load(file);
                presets.Add((
                    file,
                    node.GetValue("name"),
                    double.Parse(node.GetValue("diameter"))));
            }
            presets.Sort((a, b) => a.Diameter.CompareTo(b.Diameter));
        }

        private string PresetDescription(string name, double diam) => showPresetNameOnly
            ? name
            : $"{name} [{diam:N1}m / {ConvertUnit(Unit.Meter, Unit.Foot, diam):N1}ft]";

        private void DrawControls()
        {
            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Diameter:", boldLblStyle, GUILayout.Width(90));
                    diameterBuf = GUILayout.TextField(diameterBuf);
                    GUILayout.Label(UnitAbbreviations[inputUnit], GUILayout.Width(18));

                    if (double.TryParse(diameterBuf, out double parsed))
                        diameter = inputUnit switch
                        {
                            Unit.Meter => parsed,
                            Unit.Foot => ConvertUnit(Unit.Foot, Unit.Meter, parsed),
                            Unit.Inch => ConvertUnit(Unit.Inch, Unit.Meter, parsed),
                            _ => throw new Exception(),
                        };
                    else if (diameterBuf != "")
                        ScreenMessages.PostScreenMessage("Cannot parse entered diameter.", 0.2f, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
                }

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("", GUILayout.Width(90));
                    GUILayout.Label(inputUnit switch
                    {
                        Unit.Meter => $"= {TrimmedDecimal(diameterFeet)}ft = {TrimmedDecimal(diameterInches)}in",
                        Unit.Foot => $"= {TrimmedDecimal(diameterInches)}in = {TrimmedDecimal(diameter)}m",
                        Unit.Inch => $"= {TrimmedDecimal(diameterFeet)}ft = {TrimmedDecimal(diameter)}m",
                        _ => throw new Exception(),
                    });
                }

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Unit:", boldLblStyle, GUILayout.Width(80));
                    var prevUnit = inputUnit;
                    inputUnit = RenderRadioSelectors(inputUnit, UnitAbbreviations, GUILayout.Width(50));
                    if (prevUnit != inputUnit) ResetDiameterBuf();
                }
            }

            IEnumerable<Action> drawRescaleButtons = DiameterRescaleFactors
                .Select(kvp => (Action)(() =>
                {
                    string desc = kvp.Key;
                    double mult = kvp.Value;
                    if (GUILayout.Button($"{desc} current", GUILayout.Width(125)))
                    {
                        diameter *= mult;
                        ResetDiameterBuf();
                    }
                }))
                .Append(() =>
                {
                    if (GUILayout.Button("Currently applied", GUILayout.Width(125)))
                    {
                        diameter = module.currentDiameter;
                        ResetDiameterBuf();
                    }
                });
            RenderGrid(2, drawRescaleButtons);

            // Only allow the button to be clicked if the input value is different from the applied value.
            GUI.enabled = Math.Abs((diameter - module.currentDiameter) / diameter) > 0.0005;
            if (GUILayout.Button("Apply diameter"))
            {
                if (diameter >= 0.1)
                {
                    ApplyDiameter();
                    ResetDiameterBuf();
                }
                else
                    ScreenMessages.PostScreenMessage("The entered diameter is too small, please enter a new value.", 5, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (RenderToggleButton("Create preset...", presetCreateMode))
            {
                if (presetCreateMode)
                    presetCreateMode = false;
                else if (diameter >= 0.1)
                {
                    presetNameBuf = $"{TrimmedDecimal(diameterInputUnit, 1)} {UnitAbbreviations[inputUnit]}";
                    presetCreateMode = true;
                }
                else
                    ScreenMessages.PostScreenMessage("Cannot create preset; diameter is too small.", 5, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
            }

            if (presetCreateMode)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Name:", GUILayout.Width(90));
                    presetNameBuf = GUILayout.TextField(presetNameBuf);
                }
                GUI.enabled = presetNameBuf != "";
                if (GUILayout.Button("Save preset"))
                {
                    ConfigNode config = new ConfigNode();
                    config.AddValue("name", presetNameBuf);
                    config.AddValue("diameter", diameter);

                    var newFile = string.Concat(presetNameBuf.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
                    config.Save($"{KSPUtil.ApplicationRootPath}GameData/ROLib/PluginData/{newFile}.cfg");

                    ScreenMessages.PostScreenMessage("Preset Saved. You can overwrite it by creating a new preset with the same name.", 5, ScreenMessageStyle.UPPER_CENTER, Color.green);

                    LoadPresetsFromConfigs();
                    presetCreateMode = false;
                }
            }

            // If there are no presets, do not activate the Delete button.
            GUI.enabled = presets.Count > 0;
            presetDeleteMode = GUILayout.Toggle(presetDeleteMode, "Delete presets...");
        }

        private void DrawPresets()
        {
            using (new GUILayout.VerticalScope())
            {
                presetScroll = GUILayout.BeginScrollView(presetScroll);
                foreach (var (file, name, diam) in presets)
                {
                    if (presetDeleteMode)
                    {
                        if (GUILayout.Button($"Delete {PresetDescription(name, diam)}"))
                        {
                            File.Delete(file);
                            LoadPresetsFromConfigs();
                        }
                    }
                    else if (GUILayout.Button(PresetDescription(name, diam)))
                    {
                        diameter = diam;
                        ApplyDiameter();
                        ResetDiameterBuf();
                    }
                }
                GUILayout.EndScrollView();

                showPresetNameOnly = GUILayout.Toggle(showPresetNameOnly, "Show preset names only");
            }
        }

        private void ApplyDiameter()
        {
            double oldDiameter = module.currentDiameter;
            module.currentDiameter = (float)Math.Round(diameter, ApplicationPrecision);
            BaseField fld = module.Fields[nameof(module.currentDiameter)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldDiameter);
            MonoUtilities.RefreshContextWindows(module.part);
        }

        public override void Window(int id)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope(GUILayout.Width(250)))
                {
                    DrawControls();
                }
                using (new GUILayout.VerticalScope(GUILayout.Width(250)))
                {
                    DrawPresets();
                }
            }
            base.Window(id);
        }
    }
}
