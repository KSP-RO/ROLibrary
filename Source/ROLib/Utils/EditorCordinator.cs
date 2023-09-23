using FerramAerospaceResearch.FARAeroComponents;
using FerramAerospaceResearch.FARGUI.FAREditorGUI;
using System.Collections.Generic;
using UnityEngine;


namespace ROLib
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorCordinator : MonoBehaviour
    {
        public static EditorCordinator Instance { get; private set; }
        private EditorGUI farEditorGUI;
        private bool farVoxelUpdateQueLast = false;
        private int waitForFAR = 0;
        private int tryCount = 0;
        

        private void OnStart()
        {
            if (Instance == null)
            {
                Instance = this;
                Debug.Log($"[ROLib EditorCordinator] Instance created");
            }
            else
            {
                Destroy(this);
                return;
            }
        }

        private void OnStartFinished() 
        {
            farEditorGUI = EditorGUI.Instance;
            farVoxelUpdateQueLast = farEditorGUI.VoxelizationUpdateQueued;
        }

        // Workaround to check voxelization status of Vessel in Editor
        // Checks if the farEditorGUI changes the VoxelizationUpdateQueued Variable, subsequently updating all Vessel voxel and therefore the Surface Area.
        private void FixedUpdate()
        {
            if (EditorLogic.RootPart != null)
            {
                if (farEditorGUI != null)
                {
                    // wait for timer before checking voxelization completion again
                    if (waitForFAR > 1)
                    {
                        waitForFAR --;
                        return;
                    }
                    if (farVoxelUpdateQueLast == true & farEditorGUI.VoxelizationUpdateQueued == false | waitForFAR > 0)
                    {
                        waitForFAR = 0;
                        List<Part> partsList = EditorLogic.SortedShipList; 
                        foreach (Part part in partsList)
                        {
                            if (part?.HasModuleImplementing<FARAeroPartModule>() == true 
                                && part?.FindModuleImplementing<ModuleROMaterials>() is ModuleROMaterials b)
                            {
                                // compere change in ProjectedAreas.totalArea
                                if(!b.TrySurfaceAreaUpdate())
                                {  
                                    waitForFAR = 10;
                                    tryCount += 10;
                                    //EditorGUI.RequestUpdateVoxel();
                                }
                            }
                        }
                        if (waitForFAR == 0 | tryCount >= 410)
                        {
                            waitForFAR = 0;
                            tryCount = 0;
                        }
                        farVoxelUpdateQueLast = farEditorGUI.VoxelizationUpdateQueued;
                    }
                    else if (farVoxelUpdateQueLast != farEditorGUI.VoxelizationUpdateQueued)
                        farVoxelUpdateQueLast = farEditorGUI.VoxelizationUpdateQueued;
                }
                else
                {
                    Debug.Log($"[ROLib EditorCordinator] failed if check (farEditorGUI != null)");
                    farEditorGUI = EditorGUI.Instance;
                }
            }
        }
    }
}