using System;
using System.Globalization;
using UnityEngine;

namespace ROLib
{
    public class ModelSelectionGUI : AbstractWindow
    {
        private string diameterStr;
        private float diameter;
        private ModuleROTank ROTank;
        private static ModuleTab currentTab;

        public override Rect InitialPosition => new Rect(300, 300, 800, 600);

        public override string Title => "ROTanks Model Selection";

        private enum ModuleTab
        {
            Core,
            Nose,
            Mount
        };

        public void InitForModule(ModuleROTank m)
        {
            ROTank = m;
            diameterStr = m.currentDiameter.ToString("N3");
        }

        private void UpdateSelectedModule()
        {
            GUILayout.BeginHorizontal();
            if (ShouldShowTab(ModuleTab.Core) && RenderToggleButton("Core", currentTab == ModuleTab.Core))
                SwitchTab(ModuleTab.Core);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (ShouldShowTab(ModuleTab.Nose) && RenderToggleButton("Nose", currentTab == ModuleTab.Nose))
                SwitchTab(ModuleTab.Nose);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (ShouldShowTab(ModuleTab.Mount) && RenderToggleButton("Mount", currentTab == ModuleTab.Mount))
                SwitchTab(ModuleTab.Mount);
            GUILayout.EndHorizontal();
        }

        private void DimensionsPanel()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Diameter: ", boldLblStyle, GUILayout.Width(80));
            if (GUILayout.Button("<<", GUILayout.Width(20)))
            {
                diameter -= 1.0f;
                diameterStr = diameter.ToString("N3");
            }
            if (GUILayout.Button("<", GUILayout.Width(20)))
            {
                diameter -= 0.1f;
                diameterStr = diameter.ToString("N3");
            }

            diameterStr = GUILayout.TextField(diameterStr, GUILayout.Width(50));
            ROTank.currentDiameter = diameter = (float)Math.Round(float.Parse(diameterStr.Substring(diameterStr.Length - 1), CultureInfo.InvariantCulture.NumberFormat), 3);
            
            if (GUILayout.Button(">", GUILayout.Width(20)))
            {
                diameter += 0.1f;
                diameterStr = diameter.ToString("N3");
            }
            if (GUILayout.Button(">>", GUILayout.Width(20)))
            {
                diameter += 1.0f;
                diameterStr = diameter.ToString("N3");
            }
            GUILayout.EndHorizontal();
        }

        private bool ShouldShowTab(ModuleTab tab)
        {
            switch (tab)
            {
                case ModuleTab.Core:
                case ModuleTab.Nose:
                    return ROTank.noseDefs.Length > 0;
                case ModuleTab.Mount:
                    return ROTank.mountDefs.Length > 0;
                default:
                    return true;
            }
        }

        private static void SwitchTab(ModuleTab newTab)
        {
            if (newTab == currentTab) return;
            currentTab = newTab;
        }
        
        public override void Window(int id)
        {
            GUI.skin = HighLogic.Skin;
            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.BeginVertical(GUILayout.Width(220), GUILayout.Height(600));
                    UpdateSelectedModule();
                    DimensionsPanel();
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
            }
        }
    }
}