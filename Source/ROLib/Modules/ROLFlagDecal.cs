using System;
using UnityEngine;
using KSPShaderTools;

namespace ROLib
{
    public class ROLFlagDecal : PartModule, IPartGeometryUpdated
    {

        [KSPField]
        public String transformName = String.Empty;

        [KSPField(isPersistant = true)]
        public bool flagEnabled = true;

        [KSPEvent(guiName = "Toggle Flag Visibility", guiActiveEditor = true)]
        public void ToggleFlagEvent() => OnFlagToggled(true);

        private void OnFlagToggled(bool updateSymmetry)
        {
            flagEnabled = !flagEnabled;
            UpdateFlagTransform();
            if (updateSymmetry)
            {
                int index = part.Modules.IndexOf(this);
                foreach (Part p in part.symmetryCounterparts) { ((ROLFlagDecal)p.Modules[index]).OnFlagToggled(false); }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            UpdateFlagTransform();
            GameEvents.onMissionFlagSelect.Add(OnFlagChanged);
        }

        private void OnDestroy() => GameEvents.onMissionFlagSelect.Remove(OnFlagChanged);

        public void OnFlagChanged(string flagUrl) => UpdateFlagTransform();

        //IPartGeometryUpdated callback method
        public void geometryUpdated(Part part)
        {
            if (part == this.part)
            {
                UpdateFlagTransform();
            }
        }

        public void UpdateFlagTransform()
        {
            string textureName = part.flagURL;
            if (HighLogic.LoadedSceneIsEditor && string.IsNullOrEmpty(textureName)) { textureName = EditorLogic.FlagURL; }
            if (string.IsNullOrEmpty(textureName) && HighLogic.CurrentGame!=null) { textureName = HighLogic.CurrentGame.flagURL; }
            foreach (Transform t in part.FindModelTransforms(transformName))
            {
                if (t.GetComponent<Renderer>() is Renderer r)
                {
                    r.enabled = flagEnabled && !string.IsNullOrEmpty(textureName);
                    if (r.enabled)
                        r.material.mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
                }
            }
        }
    }
}
