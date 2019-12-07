using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Globalization;

namespace ROLib
{
    public class DimensionWindow : AbstractWindow
    {
        Vector2 presetScroll;
        ModuleROTank module;
        string nameString, diameterString;
        float curDiameter, diameterMeters, diameterFeet, diameter, tempDiameter = 0.0f;
        bool feet, deleteEnabled = false;
        string[] files;
        SortedList<string, float> sortedFiles = new SortedList<string, float>();
        string file;
        string deleteFile = "";
        string deleteFileName = "";

        public DimensionWindow (ModuleROTank m) :
            base (new Guid(), "ROTanks Dimension Selection", new Rect (300, 300, 400, 600))
        {
            presetScroll = new Vector2();
            module = m;
            nameString = "";
            diameterString = m.currentDiameter.ToString("N3");
            UpdatePresetList();
        }

        private void UpdatePresetList()
        {
            files = Directory.GetFiles($"{KSPUtil.ApplicationRootPath}GameData/ROLib/PluginData/", "*.cfg");
            NumericComparer ns = new NumericComparer();
            Array.Sort(files, ns);
        }

        public void CreateNew()
        {
            //ROLLog.debug("TankDimensionGUI: CreateNew()");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name: ", boldLblStyle, GUILayout.Width(100));
            nameString = GUILayout.TextField(nameString);
            //ROLLog.debug("TankDimensionGUI: CreateNew().nameString " + nameString);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Diameter: ", boldLblStyle, GUILayout.Width(100));
            diameterString = GUILayout.TextField(diameterString);
            //diameterString = Regex.Replace(diameterString, @"[^0-9\.?]", "");
            //ROLLog.debug("TankDimensionGUI: CreateNew().diameterString " + diameterString);
            GUILayout.EndHorizontal();

            feet = GUILayout.Toggle(feet, "Use Feet?");
            //ROLLog.debug("TankDimensionGUI: CreateNew().feet " + feet);

            if (!feet)
            {
                diameter = diameterMeters = (float)Math.Round(float.Parse(diameterString, CultureInfo.InvariantCulture.NumberFormat), 3);
                diameterFeet = (float)Math.Round(diameterMeters * 3.28084f, 3);
                //ROLLog.debug("TankDimensionGUI: CreateNew() Not Inches diameterMeters " + diameterMeters);
            }
            else
            {
                diameterFeet = (float)Math.Round(float.Parse(diameterString, CultureInfo.InvariantCulture.NumberFormat), 3);
                diameter = diameterMeters = (float)Math.Round(diameterFeet * 0.3048f, 3);
                //ROLLog.debug("TankDimensionGUI: CreateNew() Inches diameterMeters " + diameterMeters);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Meters: " + diameterMeters.ToString("N3"), GUILayout.Width(100));
            //ROLLog.debug("TankDimensionGUI: CreateNew() Meters: " + diameterMeters.ToString("N3"));
            GUILayout.Label("Feet: " + diameterFeet.ToString("N3"), GUILayout.Width(100));
            //ROLLog.debug("TankDimensionGUI: CreateNew() Feet: " + diameterFeet.ToString("N3"));
            GUILayout.EndHorizontal();

            if (feet)
            {
                curDiameter = module.currentDiameter * 3.281f;
            }
            else
            {
                curDiameter = module.currentDiameter;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1:2 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(0.5f, "1-2 of ");
            }
            if (GUILayout.Button("1:3 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(0.33f, "1-3 of ");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("2:3 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(0.66f, "2-3 of ");
            }
            if (GUILayout.Button("1:4 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(0.25f, "1-4 of ");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("3:4 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(0.75f, "3-4 of ");
            }
            if (GUILayout.Button("2:1 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(2f, "2-1 of ");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("3:1 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(3f, "3-1 of ");
            }
            if (GUILayout.Button("3:2 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(1.5f, "3-2 of ");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("4:3 of Current", GUILayout.Width(125)))
            {
                CalculateDiameter(1.33f, "4-3 of ");
            }
            if (GUILayout.Button("Set to Current", GUILayout.Width(125)))
            {
                diameterString = curDiameter.ToString("N3");
                nameString = "";
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Set Diameter", HighLogic.Skin.button))
            {
                //ROLLog.debug("TankDimensionGUI: Set Diameter Button Made");
                if (diameterMeters > 0.1)
                {
                    // Set currentDiameter of ROTank
                    SetCurrentDiameter(diameter);
                }
                else
                {
                    ScreenMessages.PostScreenMessage("The entered diameter is invalid. Please enter a new value.", 5, ScreenMessageStyle.UPPER_CENTER);
                }
            }

            // If there is no name entered or diameter set, do not activate the button
            if (nameString == "" || diameterMeters < 0.1)
            {
                GUI.enabled = false;
                //ROLLog.debug("TankDimensionGUI: Save as Preset Not Enabled");
            }
            if (GUILayout.Button("Save as Preset"))
            {
                //ROLLog.debug("TankDimensionGUI: Save as Preset Button Made");
                var newName = nameString;
                var newDiameter = diameterMeters.ToString("N3");
                ConfigNode config = new ConfigNode(newName);
                config.AddValue("name", newName);
                config.AddValue("diameter", newDiameter);
                config.Save(KSPUtil.ApplicationRootPath + "GameData/ROLib/PluginData/" + newName + ".cfg");
                ScreenMessages.PostScreenMessage("Preset Saved. You can edit the preset later by using the same name in the Tank Dimension UI.", 5, ScreenMessageStyle.UPPER_CENTER);
                UpdatePresetList();
            }

            // If there are no presets, do not activate the Delete button
            if (files.Length == 0)
            {
                GUI.enabled = false;
                //ROLLog.debug("TankDimensionGUI: No Files - Delete Not Enabled");
            }
            else
            {
                GUI.enabled = true;
                //ROLLog.debug("TankDimensionGUI: Delete Enabled");
            }

            // If the button has currently been set for deletion, then toggle it
            if (deleteEnabled)
            {
                if (GUILayout.Button("Disable Deletion"))
                {
                    deleteEnabled = false;
                    //ROLLog.debug("TankDimensionGUI: UnDelete Not Enabled");
                }
            }
            else
            {
                if (GUILayout.Button("Enable Deletion"))
                {
                    deleteEnabled = true;
                    //ROLLog.debug("TankDimensionGUI: Delete Enabled");
                }
            }

            //ROLLog.debug("TankDimensionGUI: End CreateNew()");
        }

        private void CalculateDiameter(float f, string s)
        {
            tempDiameter = (float)Math.Round(curDiameter * f, 3);
            diameterString = tempDiameter.ToString("N3");
            string feetOrInches = "m";
            if (feet)
            {
                feetOrInches = " ft";
            }
            nameString = s + diameterString + feetOrInches;
        }

        public void CreatePresets()
        {
            //ROLLog.debug("TankDimensionGUI: CreatePresets()");
            presetScroll = GUILayout.BeginScrollView(presetScroll);

            //ROLLog.debug("TankDimensionGUI: ForEach through Files.");
            foreach (string f in files)
            {
                file = f;
                //ROLLog.debug("TankDimensionGUI: Load ConfigNode");
                ConfigNode config = ConfigNode.Load(file);

                // If the player is deleting the files, append the names
                if (deleteEnabled)
                {
                    if (GUILayout.Button($"Delete {config.GetValue("name")} [{config.GetValue("diameter")}m]"))
                    {
                        deleteFile = file;
                        deleteFileName = config.GetValue("name");
                        File.Delete(deleteFile);
                        UpdatePresetList();
                    }
                }
                else
                {
                    if (GUILayout.Button($"{config.GetValue("name")} [{config.GetValue("diameter")} m]"))
                    {
                        // Set currentDiameter of ROTank
                        diameter = ROLUtils.safeParseFloat(config.GetValue("diameter"));
                        nameString = config.GetValue("name");
                        diameterString = diameter.ToString("N3");
                        feet = false;
                        SetCurrentDiameter(diameter);
                    }
                }
            }
            GUILayout.EndScrollView();
            //ROLLog.debug("TankDimensionGUI: Ending CreatePresets()");
        }

        public void SetCurrentDiameter(float f)
        {
            module.currentDiameter = f;

            module.updateModulePositions();
            module.updateDimensions();
            module.updateAttachNodes(false);
            module.updateAvailableVariants();
            if (module.scaleMass)
            {
                module.updateMass();
            }
            if (module.scaleCost)
            {
                module.updateCost();
            }

            UpdatePartActionWindow();
            ROLLog.log("ModuleROTank - Diameter set to: " + f);
        }

        private void UpdatePartActionWindow()
        {
            UIPartActionWindow window = UIPartActionController.Instance?.GetItem(module.part, false);
            if (window is null) return;

            window.ClearList();
            window.displayDirty = true;
        }

        public override void Window(int id)
        {
            //ROLLog.debug("TankDimensionGUI: DrawWindow()");
            GUI.skin = HighLogic.Skin;
            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.BeginVertical(GUILayout.Width(250));
                    CreateNew();
                }
                finally
                {
                    GUILayout.EndVertical();
                }

                try
                {
                    GUILayout.BeginVertical(GUILayout.Width(200));
                    CreatePresets();
                }
                finally
                {
                    GUILayout.EndVertical();
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
                GUI.DragWindow();
                base.Window(id);
                //ROLLog.debug("TankDimensionGUI: End DrawWindow()");
            }
        }
    }
}
