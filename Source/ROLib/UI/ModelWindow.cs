using System;
using UnityEngine;

namespace ROLib
{
    class ModelWindow : AbstractWindow
    {
        Vector2 selectModelScroll;
        private readonly ModuleROTank pm;
        private readonly ROLModelModule<ModuleROTank> module;
        private readonly ModelDefinitionLayoutOptions[] def;
        private string modelName;

        public ModelWindow (ModuleROTank m, ROLModelModule<ModuleROTank> mod, ModelDefinitionLayoutOptions[] d, string name) :
            base(new Guid(), $"ROTanks {name} Selection", new Rect(300, 300, 400, 600))
        {
            selectModelScroll = new Vector2();
            pm = m;
            module = mod;
            def = d;
        }
        
        private void UpdateModelSelections()
        {
            foreach (ModelDefinitionLayoutOptions options in def)
            {
                
                if (RenderToggleButton($"{options.definition.title}", options.definition.name == module.modelName))
                {
                    modelName = options.definition.name;
                    SelectCurrentModel(modelName);
                }
            }
        }

        private void SelectCurrentModel(string model)
        {
            //ROLLog.debug($"module.name: {module.name}");
            BaseField fld;
            string oldModel;
            switch (module.name)
            {
                case "ModuleROTank-Mount":
                    fld = pm.Fields[nameof(pm.currentMount)];
                    oldModel = pm.currentMount;
                    break;
                case "ModuleROTank-Nose":
                    fld = pm.Fields[nameof(pm.currentNose)];
                    oldModel = pm.currentNose;
                    break;
                default:
                    fld = pm.Fields[nameof(pm.currentCore)];
                    oldModel = pm.currentCore;
                    break;
            }
            //ROLLog.debug($"oldModel: {oldModel}");
            //ROLLog.debug($"Set the value of the Current Model to: {model}");
            //ROLLog.debug($"fld: {fld.guiName}");
            fld.SetValue(model, pm);
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldModel);
            fld.uiControlEditor.onSymmetryFieldChanged.Invoke(fld, oldModel);
            MonoUtilities.RefreshContextWindows(pm.part);
        }

        public void SelectModel()
        {
            selectModelScroll = GUILayout.BeginScrollView((selectModelScroll));
            UpdateModelSelections();
            GUILayout.EndScrollView();
        }

        public override void Window(int id)
        {
            //ROLLog.debug("ModelWindow: DrawWindow()");
            GUI.skin = HighLogic.Skin;
            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.BeginVertical(GUILayout.Width(250), GUILayout.Height(500));
                    SelectModel();
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
                //ROLLog.debug("ModelWindow: End DrawWindow()");
            }
        }
    }
}
