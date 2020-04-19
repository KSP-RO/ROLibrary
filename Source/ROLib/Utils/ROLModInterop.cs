using System;
using UnityEngine;
using System.Reflection;
using KSPShaderTools;
using System.Linq;

namespace ROLib
{
    public static class ROLModInterop
    {
        private static bool initialized = false;
        private static bool installedFAR = false;
        private static bool installedRF = false;
        private static bool installedMFT = false;
        private static bool installedSolverEngines = false;

        public static void UpdateResourceVolume(Part part)
        {
            float totalVolume = 0;
            foreach (IContainerVolumeContributor contrib in part.FindModulesImplementing<IContainerVolumeContributor>())
            {
                foreach (ContainerContribution c in contrib.getContainerContributions())
                {
                    totalVolume += c.containerVolume;
                }
            }
            RealFuelsVolumeUpdate(part, totalVolume);
        }

        /// <summary>
        /// Updates part highlight renderer list, sends message to ModuleROTFlagDecal to update its renderer,
        ///  sends message to FAR to update voxels, or if createDefaultCube==true will re-render the 'default' stock drag cube for the part<para/>
        /// Should be called anytime the model geometry in a part is changed -- either models added/deleted, procedural meshes updated.  Other methods exist for pure drag-cube updating in ROLStockInterop.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="createDefaultCube"></param>
        public static void OnPartGeometryUpdate(Part part, bool createDefaultCube)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }//noop on prefabs
            ROLStockInterop.UpdatePartHighlighting(part);
            part.airlock = LocateAirlock(part);
            PartGeometryUpdate(part);
            if (createDefaultCube && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                ROLStockInterop.UpdatePartDragCube(part);
            }
            if (IsFARInstalled())
            {
                //FARdebug(part);
                part.SendMessage("GeometryPartModuleRebuildMeshData");
            }
            if (HighLogic.LoadedSceneIsEditor && part.parent==null && part!=EditorLogic.RootPart)//likely the part under the cursor; this fixes problems with modular parts not wanting to attach to stuff
            {
                part.gameObject.SetLayerRecursive(1, 2097152);//1<<21 = Part Triggers get skipped by the relayering (hatches, ladders, ??)
            }
        }

        public static void OnPartTextureUpdated(Part part)
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                TextureCallbacks.onTextureSetChanged(part);
            }
        }

        private static void PartGeometryUpdate(Part part)
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                TextureCallbacks.onPartModelChanged(part);
            }
        }

        private static Transform LocateAirlock(Part part)
        {
            foreach (Collider coll in part.GetComponentsInChildren<Collider>())
            {
                if (coll.gameObject.tag == "Airlock")
                {
                    return coll.transform;
                }
            }
            return null;
        }

        private static void FARdebug(Part part)
        {
            MonoBehaviour.print($"FAR DEBUG FOR PART: {part}");
            foreach (MeshFilter mf in part.GetComponentsInChildren<MeshFilter>())
            {
                MonoBehaviour.print($"FAR debug data || go: {mf.gameObject} || mf: {mf} || mesh: {mf.mesh} || sharedMesh: {mf.sharedMesh}");
            }
            MonoBehaviour.print("-------------------------------------------------------------------------");
        }

        public static bool RealFuelsVolumeUpdate(Part part, float liters)
        {
            if (!IsRFInstalled() && !IsMFTInstalled())
            {
                MonoBehaviour.print($"ERROR: Config for {part} is set to use RF/MFT, but neither RF nor MFT is installed, cannot update part volumes through them.  Please check your configs and/or patches for errors.");
                return false;
            }
            string targ = IsRFInstalled() ? "RealFuels.Tanks.ModuleFuelTanks,RealFuels" : "RealFuels.Tanks.ModuleFuelTanks,modularFuelTanks";
            if (Type.GetType(targ) is Type moduleFuelTank && getModuleFuelTanks(part) is PartModule pm)
            {
                MethodInfo mi = moduleFuelTank.GetMethod("ChangeTotalVolume");
                double volumeLiters = liters;
                mi.Invoke(pm, new object[] { volumeLiters, false });
                MethodInfo mi2 = moduleFuelTank.GetMethod("CalculateMass");
                mi2.Invoke(pm, new object[] { });
                UpdatePartResourceDisplay(part);
                MonoBehaviour.print($"ROTModInterop - Set RF/MFT total tank volume to: {volumeLiters} Liters for part: {part.name}");
                return true;
            } else
            {
                MonoBehaviour.print($"ERROR! Could not find ModuleFuelTank in part {part} for RealFuels/MFT");
                return false;
            }
        }

        public static PartModule getModuleFuelTanks(Part part)
        {
            string targ = IsRFInstalled() ? "RealFuels.Tanks.ModuleFuelTanks,RealFuels" :
                          IsMFTInstalled() ? "RealFuels.Tanks.ModuleFuelTanks,modularFuelTanks" :
                          string.Empty;
            if (Type.GetType(targ) is Type moduleFuelTank)
            {
                return part.GetComponent(moduleFuelTank) as PartModule;
            } else
            {
                MonoBehaviour.print($"ERROR: getModuleFuelTanks for {part} looking for {targ} but failed to find the PartModule.");
                return null;
            }
        }

        public static bool HasModuleFuelTanks(Part part) => getModuleFuelTanks(part) != null;

        public static bool hasModuleEngineConfigs(Part part) =>
            Type.GetType("RealFuels.ModuleEngineConfigs,RealFuels") is Type type && part.GetComponent(type) is PartModule;

        public static PropertyInfo getSolverEngineTempProperty()
        {
            if (!IsSolverEnginesInstalled()) return null;

            string targ = "SolverEngines.ModuleEnginesSolver,SolverEngines";
            if (Type.GetType(targ) is Type t)
                return t.GetProperty("GetEngineTemp");
            else
                return null;
        }

        public static ModuleEngines getSolverEngineModule(Part part, string engineID)
        {
            if (!IsSolverEnginesInstalled()) return null;

            string targ = "SolverEngines.ModuleEnginesSolver,SolverEngines";
            if (Type.GetType(targ) is Type moduleSolverEngine)
            {
                ModuleEngines engine = Array.ConvertAll(part.GetComponents(moduleSolverEngine), x => (ModuleEngines) x)
                    .FirstOrDefault(x => x.engineID == engineID);

                engine ??= (ModuleEngines) part.GetComponent(moduleSolverEngine);
                return engine;
            }
            else
            {
                MonoBehaviour.print($"ERROR: getSolverEngineModule for {part} looking for {targ} but failed to find the PartModule.");
                return null;
            }
        }

        private static void Init()
        {
            initialized = true;
            installedFAR = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "FerramAerospaceResearch");
            installedRF = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "RealFuels");
            installedMFT = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "modularFuelTanks");
            installedSolverEngines = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "SolverEngines");
        }

        public static bool IsFARInstalled()
        {
            if (!initialized) Init();
            return installedFAR;
        }

        public static bool IsRFInstalled()
        {
            if (!initialized) Init();
            return installedRF;
        }

        public static bool IsMFTInstalled()
        {
            if (!initialized) Init();
            return installedMFT;
        }

        public static bool IsSolverEnginesInstalled()
        {
            if(!initialized) Init();
            return installedSolverEngines;
        }

        public static void UpdatePartResourceDisplay(Part part)
        {
            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch == null) { return; }
            if (HighLogic.LoadedSceneIsFlight && FlightDriver.fetch == null) { return; }
            try
            {
                if (UIPartActionController.Instance != null)
                {
                    UIPartActionWindow window = UIPartActionController.Instance.GetItem(part);
                    if (window != null) { window.displayDirty = true; }
                }
            }
            catch (Exception e)
            {
                MonoBehaviour.print("ERROR: Caught exception while updating part resource display: " + e.Message);
            }
        }
    }
}
