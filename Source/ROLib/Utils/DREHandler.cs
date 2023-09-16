using System;
using System.Linq;
using UnityEngine;

namespace ROLib
{
	public static class DREHandler
    {
        // add support for maxOperationalTemp
        // hook into DRE and set maxOperationalTemp = maxTemp + 1
        // same for maxSkinTemp
        // DRE should automatically adjust it since maxOpTemp > maxTemp
        private static bool? _isInstalled = null;

        public static bool Found
        {
            get
            {
                if (!_isInstalled.HasValue)
                {
                    _isInstalled = AssemblyLoader.loadedAssemblies.Any(
                        a => string.Equals(a.name, "DeadlyReentry", StringComparison.OrdinalIgnoreCase));
                    
                    if(_isInstalled.Value)
                        Debug.Log("[ROHeatshields] Deadly Reentry detected");
                }
                return _isInstalled.Value;
            }
        }

        public static void SetOperationalTemps(Part part, double maxTemp, double skinMaxTemp)
        {
            PartModule pm = part.Modules.GetModule("ModuleAeroReentry");

            if (pm is null)
                return;

            //we set them to T+0.1 so that DRE notices that maxOpTemp > maxTemp
            //and adjustes maxTemp accordingly. 
            pm.Fields.SetValue("maxOperationalTemp", maxTemp + 0.1f);
            pm.Fields.SetValue("skinMaxOperationalTemp", skinMaxTemp + 0.1f);
        }
    }
}