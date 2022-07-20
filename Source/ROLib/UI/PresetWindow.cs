using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

namespace ROLib
{
    public class PresetWindow : AbstractWindow
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
        private string TrimmedNumber(double num, int maxDecimalPlaces = 0) =>
            num.ToString($"N{maxDecimalPlaces}");


        ModuleROTank pm;

        string psName = "(None Selected)";
        string psVariant = "";
        string psCore = "";
        string psNose = "";
        string psMount = "";
        string psCoreTex = "";
        string psNoseTex = "";
        string psMountTex = "";
        string psNoseRot = "";
        string psNoseVS = "";
        string psMountRot = "";
        string psMountVS = "";

        double diameter;
        double length;
        double noseRot;
        double mountRot;
        double noseVS;
        double mountVS;
        Unit inputUnit = Unit.Meter;
        double diameterFeet => ConvertUnit(Unit.Meter, Unit.Foot, diameter);
        double lengthFeet => ConvertUnit(Unit.Meter, Unit.Foot, length);
        double diameterInches => ConvertUnit(Unit.Meter, Unit.Inch, diameter);
        double lengthInches => ConvertUnit(Unit.Meter, Unit.Inch, length);
        private double diameterInputUnit => inputUnit switch
        {
            Unit.Meter => diameter,
            Unit.Foot => diameterFeet,
            Unit.Inch => diameterInches,
            _ => throw new Exception(),
        };
        private double lengthInputUnit => inputUnit switch
        {
            Unit.Meter => length,
            Unit.Foot => lengthFeet,
            Unit.Inch => lengthInches,
            _ => throw new Exception(),
        };
        const int ApplicationPrecision = 3;
        string diameterBuf;
        string lengthBuf;
        private void ResetDiameterBuf() => diameterBuf = TrimmedDecimal(diameterInputUnit, ApplicationPrecision);
        private void ResetLengthBuf() => lengthBuf = TrimmedDecimal(lengthInputUnit, ApplicationPrecision);

        List<PresetSelection> presets = new List<PresetSelection>();
        Vector2 presetScroll = new Vector2();
        bool presetCreateMode = false;
        bool presetDeleteMode = false;
        string presetNameBuf = "";

        public class PresetSelection
        {
            public PresetSelection() { }
            public PresetSelection(ConfigNode node, string file)
            {
                FileName = file;
                Name = node.GetValue("name");
                Variant = node.GetValue("variant");
                Core = node.GetValue("core");
                Nose = node.GetValue("nose");
                Mount = node.GetValue("mount");
                CoreTexture = node.GetValue("coreTexture");
                NoseTexture = node.GetValue("noseTexture");
                MountTexture = node.GetValue("mountTexture");
                Diameter = double.Parse(node.GetValue("diameter"));
                Length = double.Parse(node.GetValue("length"));
                NoseRotation = double.Parse(node.GetValue("noseRotation"));
                NoseVScale = double.Parse(node.GetValue("noseVScale"));
                MountRotation = double.Parse(node.GetValue("mountRotation"));
                MountVScale = double.Parse(node.GetValue("mountVScale"));
            }
            
            public string FileName { get; set; }
            public string Name { get; set; }
            public string Variant { get; set; }
            public string Core { get; set; }
            public string Nose { get; set; }
            public string Mount { get; set; }
            public string CoreTexture { get; set; }
            public string NoseTexture { get; set; }
            public string MountTexture { get; set; }
            public double Diameter { get; set; }
            public double Length { get; set; }
            public double NoseRotation { get; set; }
            public double NoseVScale { get; set; }
            public double MountRotation { get; set; }
            public double MountVScale { get; set; }
        }

        static Dictionary<string, double> DiameterRescaleFactors = new Dictionary<string, double>()
        {
            { "3/4 of", 0.75 },
            { "4/3 of", 4d / 3d },
            { "2/3 of", 2d / 3d },
            { "3/2 of", 1.5 },
            { "1/2 of", 0.5 },
            { "2x",     2 },
            { "1/3 of", 1d / 3d },
            { "3x",     3 },
        };

        public PresetWindow (ModuleROTank m) :
            base(Guid.NewGuid(), "ROTanks Preset Selection", new Rect(300, 300, 600, 600))
        {
            pm = m;
            psVariant = m.currentVariant;
            psCore = m.currentCore;
            psNose = m.currentNose;
            psMount = m.currentMount;
            psCoreTex = m.currentCoreTexture;
            psNoseTex = m.currentNoseTexture;
            psMountTex = m.currentMountTexture;
            diameter = m.currentDiameter;
            length = m.currentLength;
            psNoseRot = TrimmedNumber(m.currentNoseRotation);
            psMountRot = TrimmedNumber(m.currentMountRotation);
            psNoseVS = TrimmedDecimal(m.currentNoseVScale, ApplicationPrecision);
            psMountVS = TrimmedDecimal(m.currentMountVScale, ApplicationPrecision);

            ResetDiameterBuf();
            ResetLengthBuf();
            LoadPresetsFromConfigs();
        }


        private void LoadPresetsFromConfigs()
        {
            presets.Clear();
            foreach (string file in Directory.GetFiles($"{KSPUtil.ApplicationRootPath}GameData/ROLib/PluginData/Models", "*.cfg"))
            {
                var node = ConfigNode.Load(file);
                presets.Add(new PresetSelection(node, file));
            }
            presets.Sort((a, b) => a.Name.CompareTo(b.Name));
        }

        private void DrawControls()
        {
            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Name: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(psName, GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Variant: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(pm.currentVariant, GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Core: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(pm.currentCore, GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Nose: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(pm.currentNose, GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Mount: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(pm.currentMount, GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Core Tex: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(pm.currentCoreTexture, GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Nose Tex: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(pm.currentNoseTexture, GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Mount Tex: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(pm.currentMountTexture, GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Nose Rot.: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(TrimmedNumber(pm.currentNoseRotation), GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Mount Rot.: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(TrimmedNumber(pm.currentMountRotation), GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Nose VScale: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(TrimmedDecimal(pm.currentNoseVScale, ApplicationPrecision), GUILayout.Width(170));
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Mount VScale: ", boldLblStyle, GUILayout.Width(130));
                    GUILayout.Label(TrimmedDecimal(pm.currentMountVScale, ApplicationPrecision), GUILayout.Width(170));
                }

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Length:", boldLblStyle, GUILayout.Width(90));
                    lengthBuf = GUILayout.TextField(lengthBuf);
                    GUILayout.Label(UnitAbbreviations[inputUnit], GUILayout.Width(18));

                    if (double.TryParse(lengthBuf, out double parsed))
                        length = inputUnit switch
                        {
                            Unit.Meter => parsed,
                            Unit.Foot => ConvertUnit(Unit.Foot, Unit.Meter, parsed),
                            Unit.Inch => ConvertUnit(Unit.Inch, Unit.Meter, parsed),
                            _ => throw new Exception(),
                        };
                    else if (lengthBuf != "")
                        ScreenMessages.PostScreenMessage("Cannot parse entered length.", 0.2f, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
                }

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("", GUILayout.Width(90));
                    GUILayout.Label(inputUnit switch
                    {
                        Unit.Meter => $"= {TrimmedDecimal(lengthFeet)}ft = {TrimmedDecimal(lengthInches)}in",
                        Unit.Foot => $"= {TrimmedDecimal(lengthInches)}in = {TrimmedDecimal(length)}m",
                        Unit.Inch => $"= {TrimmedDecimal(lengthFeet)}ft = {TrimmedDecimal(length)}m",
                        _ => throw new Exception(),
                    });
                }

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

            IEnumerable<Action> drawRescaleValues = DiameterRescaleFactors
                .Select(kvp => (Action)(() =>
                {
                    string desc = kvp.Key;
                    double mult = kvp.Value;
                    ROLLog.debug($"Key: {desc}, Value: {mult}");
                    GUILayout.Label($"{desc} current: ", boldLblStyle, GUILayout.Width(100));
                    GUILayout.Label($"{TrimmedDecimal(diameter * mult, ApplicationPrecision)}", GUILayout.Width(40));
                }));
            RenderGrid(2, drawRescaleValues);

            // Only allow the button to be clicked if the input value is different from the applied value.
            GUI.enabled = Math.Abs((length - pm.currentLength) / length) > 0.0005 || Math.Abs((diameter - pm.currentDiameter) / diameter) > 0.0005;
            if (GUILayout.Button("Apply Length & Diameter"))
            {
                if (length >= pm.minLength)
                {
                    ApplyLength();
                    ResetLengthBuf();
                }
                else
                    ScreenMessages.PostScreenMessage("The entered length is too small, please enter a new value.", 5, ScreenMessageStyle.UPPER_CENTER, Color.yellow);

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
                    presetNameBuf = "New Preset";
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
                    config.AddValue("variant", pm.currentVariant);
                    config.AddValue("core", pm.currentCore);
                    config.AddValue("nose", pm.currentNose);
                    config.AddValue("mount", pm.currentMount);
                    config.AddValue("coreTexture", pm.currentCoreTexture);
                    config.AddValue("noseTexture", pm.currentNoseTexture);
                    config.AddValue("mountTexture", pm.currentMountTexture);
                    config.AddValue("diameter", pm.currentDiameter);
                    config.AddValue("length", pm.currentLength);
                    config.AddValue("noseRotation", pm.currentNoseRotation);
                    config.AddValue("noseVScale", pm.currentNoseVScale);
                    config.AddValue("mountRotation", pm.currentMountRotation);
                    config.AddValue("mountVScale", pm.currentMountVScale);

                    var newFile = string.Concat(presetNameBuf.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
                    config.Save($"{KSPUtil.ApplicationRootPath}GameData/ROLib/PluginData/Models/{newFile}.cfg");

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
                foreach (var ps in presets)
                {
                    if (presetDeleteMode)
                    {
                        if (GUILayout.Button($"Delete {ps.Name}"))
                        {
                            File.Delete(ps.FileName);
                            LoadPresetsFromConfigs();
                        }
                    }
                    else if (GUILayout.Button(ps.Name))
                    {
                        psName = ps.Name;
                        psVariant = ps.Variant;
                        psCore = ps.Core;
                        psNose = ps.Nose;
                        psMount = ps.Mount;
                        psCoreTex = ps.CoreTexture;
                        psNoseTex = ps.NoseTexture;
                        psMountTex = ps.MountTexture;
                        diameter = ps.Diameter;
                        length = ps.Length;
                        noseRot = ps.NoseRotation;
                        mountRot = ps.MountRotation;
                        noseVS = ps.NoseVScale;
                        mountVS = ps.MountVScale;
                        psNoseRot = TrimmedDecimal(ps.NoseRotation, ApplicationPrecision);
                        psMountRot = TrimmedDecimal(ps.MountRotation, ApplicationPrecision);
                        psNoseVS = TrimmedDecimal(ps.NoseVScale, ApplicationPrecision);
                        psMountVS = TrimmedDecimal(ps.MountVScale, ApplicationPrecision);
                        ApplyPresetData();
                    }
                }
                GUILayout.EndScrollView();
            }
        }

        public void UpdateLength()
        {
            length = pm.currentLength;
            ResetLengthBuf();
        }
        public void UpdateDiameter()
        {
            diameter = pm.currentDiameter;
            ResetDiameterBuf();
        }

        private void ApplyDiameter()
        {
            double oldDiameter = pm.currentDiameter;
            pm.currentDiameter = (float)Math.Round(diameter, ApplicationPrecision);
            BaseField fld = pm.Fields[nameof(pm.currentDiameter)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldDiameter);
            MonoUtilities.RefreshContextWindows(pm.part);
        }

        private void ApplyLength()
        {
            double oldLength = pm.currentLength;
            pm.currentLength = (float)Math.Round(length, ApplicationPrecision);
            BaseField fld = pm.Fields[nameof(pm.currentLength)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldLength);
            MonoUtilities.RefreshContextWindows(pm.part);
        }

        private void ApplyPresetData()
        {
            //ApplyVariant();
            ApplyCoreModel();
            ApplyNoseModel();
            ApplyMountModel();
            ApplyDiameter();
            ApplyLength();
            ResetDiameterBuf();
            ResetLengthBuf();
        }

        private void ApplyVariant()
        {
            string oldVariant = pm.currentVariant;
            pm.currentVariant = psVariant;
            BaseField fld = pm.Fields[nameof(pm.currentVariant)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldVariant);
        }

        private void ApplyCoreModel()
        {
            string oldCore = pm.currentCore;
            pm.currentCore = psCore;
            BaseField fld = pm.Fields[nameof(pm.currentCore)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldCore);
            psCore = pm.currentCore;

            oldCore = pm.currentCoreTexture;
            pm.currentCoreTexture = psCoreTex;
            fld = pm.Fields[nameof(pm.currentCoreTexture)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldCore);
            psCoreTex = pm.currentCoreTexture;
        }

        private void ApplyNoseModel()
        {
            string oldNose = pm.currentNose;
            pm.currentNose = psNose;
            BaseField fld = pm.Fields[nameof(pm.currentNose)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldNose);
            psNose = pm.currentNose;

            oldNose = pm.currentNoseTexture;
            pm.currentNoseTexture = psNoseTex;
            fld = pm.Fields[nameof(pm.currentNoseTexture)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldNose);
            psNoseTex = pm.currentNoseTexture;

            double dOldNose = pm.currentNoseRotation;
            pm.currentNoseRotation = (float)Math.Round(noseRot, 0);
            fld = pm.Fields[nameof(pm.currentNoseRotation)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, dOldNose);

            dOldNose = pm.currentNoseVScale;
            pm.currentNoseVScale = (float)Math.Round(noseVS, ApplicationPrecision);
            fld = pm.Fields[nameof(pm.currentNoseVScale)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, dOldNose);

        }

        private void ApplyMountModel()
        {
            string oldMount = pm.currentMount;
            pm.currentMount = psMount;
            BaseField fld = pm.Fields[nameof(pm.currentMount)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldMount);
            psMount = pm.currentMount;

            oldMount = pm.currentMountTexture;
            pm.currentMountTexture = psMountTex;
            fld = pm.Fields[nameof(pm.currentMountTexture)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldMount);
            psMountTex = pm.currentMountTexture;

            double dOldMount = pm.currentMountRotation;
            pm.currentMountRotation = (float)Math.Round(mountRot, 0);
            fld = pm.Fields[nameof(pm.currentMountRotation)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, dOldMount);

            dOldMount = pm.currentMountVScale;
            pm.currentMountVScale = (float)Math.Round(mountVS, ApplicationPrecision);
            fld = pm.Fields[nameof(pm.currentMountVScale)];
            fld.uiControlEditor.onFieldChanged.Invoke(fld, dOldMount);
        }

        public override void Window(int id)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope(GUILayout.Width(310)))
                {
                    DrawControls();
                }
                using (new GUILayout.VerticalScope(GUILayout.Width(280)))
                {
                    DrawPresets();
                }
            }
            base.Window(id);
        }
    }
}
