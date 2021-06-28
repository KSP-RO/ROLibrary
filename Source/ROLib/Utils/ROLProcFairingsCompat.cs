using System.Reflection;
using UnityEngine;
using Keramzit;

namespace ROLib
{
    public static class ROLProcFairingsCompat
    {
        public static ProceduralFairingBase GetPFBModule(Part part)
        {
            ROLLog.debug($"GetPFBModule()");
            if (!part.Modules.Contains(nameof(ProceduralFairingBase))) return null;
            return (ProceduralFairingBase) part.Modules[nameof(ProceduralFairingBase)];
        }

        /// <summary>
        /// Hides items from the PF PAW that are not needed because they are handled by ROLib.
        /// </summary>
        public static void HidePAW(ProceduralFairingBase pfb)
        {
            ROLLog.debug($"HidePAW()");
            pfb.Fields[nameof(pfb.baseSize)].guiActiveEditor = false;
            ROLLog.debug($"pfb.Fields[nameof(pfb.baseSize)]: {pfb.Fields[nameof(pfb.baseSize)].name}");
            ROLLog.debug($"pfb.Fields[nameof(pfb.baseSize)].guiActiveEditor: {pfb.Fields[nameof(pfb.baseSize)].guiActiveEditor}");
        }

        public static void SetBaseSize(ProceduralFairingBase fb, float oldDiam, float newDiam)
        {
            ROLLog.debug("SetBaseSize");
            var fld = fb.Fields["baseSize"];
            ROLLog.debug($"fld: {fld}");
            fld.SetValue(newDiam, fb);
            fld.uiControlEditor.onFieldChanged.Invoke(fld, oldDiam);
            
            if (fb.part.symmetryCounterparts.Count > 0)
            {
                foreach (var p in fb.part.symmetryCounterparts)
                {
                    ProceduralFairingBase f = (ProceduralFairingBase)p.Modules["ProceduralFairingBase"];
                    fld = f.Fields["baseSize"];
                    fld.SetValue(newDiam, f);
                    fld.uiControlEditor.onFieldChanged.Invoke(fld, oldDiam);
                }
            }
        }
        
        /*
        public static void SetBaseSize(ProceduralFairingBase fairing, float diam)
        {
            var pai = fairing.Fields[nameof(fairing.baseSize)].uiControlEditor.partActionItem;
            ROLLog.debug($"pai: {pai}");
            if (pai is UIPartActionFieldItem item)
            {
                ROLLog.debug("Invoke SetFieldValue");
                item.GetType().GetMethod("SetFieldValue ", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(diam, null);
            }

            if (fairing.part.symmetryCounterparts.Count > 0)
            {
                foreach (var p in fairing.part.symmetryCounterparts)
                {
                    pai = fairing.Fields[nameof(fairing.baseSize)].uiControlEditor.partActionItem;
                    ROLLog.debug($"pai: {pai}");
                    if (pai is UIPartActionFieldItem uipafi)
                    {
                        ROLLog.debug("Invoke SetFieldValue");
                        uipafi.GetType().GetMethod("SetFieldValue ", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.Invoke(diam, null);
                    }
                }
            }
        }
        */
    }
}
