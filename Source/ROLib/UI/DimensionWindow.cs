using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

namespace ROLib
{
    public class DimensionWindow : AbstractWindow
    {
        ModuleROTank module;

        private float MetersToFeet(float m) => (float)Math.Round(m * 3.28084f, 3);
        private float FeetToMeters(float ft) => (float)Math.Round(ft * 0.3048f, 3);

        float diameter;
        bool useFeet = false;
        float diameterFeet => MetersToFeet(diameter);
        string diameterBuf;

        List<(string File, string Name, float Diameter)> presets = new List<(string, string, float)>();
        Vector2 presetScroll = new Vector2();
        bool presetCreateMode = false;
        bool presetDeleteMode = false;
        bool showPresetNameOnly = true;
        string presetNameBuf = "";

        static Dictionary<string, float> DiameterRescaleFactors = new Dictionary<string, float>()
        {
            ["1/4 of"] = 0.25f,
            ["1/3 of"] = 1f / 3f,
            ["1/2 of"] = 0.5f,
            ["2/3 of"] = 2f / 3f,
            ["3/4 of"] = 0.75f,
            ["3/2 of"] = 1.5f,
            ["4/3 of"] = 4f / 3f,
            ["2x"] = 2f,
            ["3x"] = 3f,
        };

        public DimensionWindow(ModuleROTank mod) :
            base(Guid.NewGuid(), "ROTanks Diameter Selection", new Rect(300, 300, 400, 600))
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
                    (float)Math.Round(float.Parse(node.GetValue("diameter")), 3)));
            }
            presets.Sort((a, b) => a.Diameter.CompareTo(b.Diameter));
        }

        private string PresetDescription(string name, float diam) => showPresetNameOnly ? name : $"{name} [{diam:N1}m / {MetersToFeet(diam):N1}ft]";

        private void ResetDiameterBuf() => diameterBuf = (useFeet ? diameterFeet : diameter).ToString("N3");

        private void DrawControls()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Diameter: ", boldLblStyle, GUILayout.Width(100));
                using (new GUILayout.VerticalScope())
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        diameterBuf = GUILayout.TextField(diameterBuf);
                        GUILayout.Label(useFeet ? "ft" : "m", GUILayout.Width(18));

                        if (float.TryParse(diameterBuf, out float parsed))
                            diameter = useFeet ? FeetToMeters(parsed) : (float)Math.Round(parsed, 3);
                        else if (diameterBuf != "")
                            ScreenMessages.PostScreenMessage("Cannot parse entered diameter.", 0.2f, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
                    }
                    GUILayout.Label(useFeet ? $"= {diameter:N3} m" : $"= {diameterFeet:N3} ft");
                }
            }

            var prevUseFeet = useFeet;
            useFeet = GUILayout.Toggle(useFeet, "Input in feet");
            if (prevUseFeet != useFeet) ResetDiameterBuf();

            IEnumerable<Action> drawRescaleButtons = DiameterRescaleFactors
                .Select(kvp => (Action)(() =>
                {
                    string desc = kvp.Key;
                    float mult = kvp.Value;
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
            GUI.enabled = Mathf.Abs((diameter - module.currentDiameter) / diameter) > 0.0005;
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

            if (RenderToggleButton("Create preset...", presetCreateMode))
            {
                if (presetCreateMode)
                    presetCreateMode = false;
                else if (diameter >= 0.1)
                {
                    presetNameBuf = useFeet ? $"{diameterFeet:N1} ft" : $"{diameter:N1} m";
                    presetCreateMode = true;
                }
                else
                    ScreenMessages.PostScreenMessage("Cannot create preset; diameter is too small.", 5, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
            }

            if (presetCreateMode)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Name:", GUILayout.Width(100));
                    presetNameBuf = GUILayout.TextField(presetNameBuf);
                }
                GUI.enabled = presetNameBuf != "";
                if (GUILayout.Button("Save preset"))
                {
                    ConfigNode config = new ConfigNode();
                    config.AddValue("name", presetNameBuf);
                    config.AddValue("diameter", diameter.ToString("N3"));

                    var newFile = string.Concat(presetNameBuf.Select(c => Char.IsLetterOrDigit(c) ? c : '-'));
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
            float oldDiameter = module.currentDiameter;
            module.currentDiameter = diameter;
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
