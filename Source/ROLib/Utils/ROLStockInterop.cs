using System.Collections.Generic;
using UnityEngine;

namespace ROLib
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ROLStockInterop : MonoBehaviour
    {
        private static readonly List<Part> dragCubeUpdateParts = new List<Part>();
        private static readonly List<Part> delayedUpdateDragCubeParts = new List<Part>();
        private static readonly List<Part> FARUpdateParts = new List<Part>();

        private static bool fireEditorEvent = false;

        public static ROLStockInterop INSTANCE;

        public void Start()
        {
            INSTANCE = this;
            KSPShaderTools.TexturesUnlimitedLoader.addPostLoadCallback(KSPShaderToolsPostLoad);
            GameObject.DontDestroyOnLoad(this);
            MonoBehaviour.print("ROLStockInterop Start");
        }

        public static void FireEditorUpdate()
        {
            fireEditorEvent = HighLogic.LoadedSceneIsEditor;
        }

        public void FixedUpdate()
        {
            if (!(HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                return;
            }

            foreach (Part p in dragCubeUpdateParts)
            {
                Debug.LogWarning("[ROLibrary] ROLStockInterop.FixedUpdate() found legacy request to update drag cubes!");
                UpdatePartDragCube(p);
            }
            foreach (Part p in FARUpdateParts)
            {
                Debug.LogWarning("[ROLibrary] ROLStockInterop.FixedUpdate() found legacy request to update FAR!");
                p.SendMessage("GeometryPartModuleRebuildMeshData");
            }
            dragCubeUpdateParts.Clear();
            FARUpdateParts.Clear();
        }

        public void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor && fireEditorEvent)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
            fireEditorEvent = false;

            //if (HighLogic.LoadedSceneIsEditor && Input.GetKey(KeyCode.U))
            //{
            //    MonoBehaviour.print("Recolor part pick!");
            //    EditorLogic el = EditorLogic.fetch;
            //    Camera c = el.editorCamera;
            //    Ray ray = c.ScreenPointToRay(Input.mousePosition);
            //    RaycastHit hit;
            //    int layerMask = 0 | 1 | 1<<2;
            //    if (Physics.Raycast(ray, out hit, 1000f, layerMask))
            //    {
            //        Part p = hit.collider.gameObject.GetComponentUpwards<Part>();
            //        MonoBehaviour.print("Picked Part: " + p);
            //    }
            //}
        }

        //called from the ModuleManagerPostLoad() callback for KSPShaderTools
        public void KSPShaderToolsPostLoad()
        {
            MonoBehaviour.print("Reloading config databases (fuel types, model data, etc...)");
            //FuelTypes.INSTANCE.loadConfigData();
            //VolumeContainerLoader.loadConfigData();//needs to be loaded after fuel types
            ROLModelLayout.load();
            ROLModelData.loadConfigData();
        }

        private static void SeatFirstCollider(Part part)
        {
            Collider[] colliders = part.gameObject.GetComponentsInChildren<Collider>();
            int len = colliders.Length;
            for (int i = 0; i < len; i++)
            {
                if (colliders[i].isTrigger) { continue; }
                if (colliders[i].GetType() == typeof(WheelCollider)) { continue; }
                part.collider = colliders[i];
                break;
            }
        }

        public static void UpdatePartDragCube(Part part)
        {
            DragCube newDefaultCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
            newDefaultCube.Weight = 1f;
            newDefaultCube.Name = "Default";
            part.DragCubes.ClearCubes();
            part.DragCubes.Cubes.Add(newDefaultCube);
            part.DragCubes.ResetCubeWeights();
            part.DragCubes.ForceUpdate(true, true, false);
            if (part.collider == null) SeatFirstCollider(part);
        }

        public static void UpdateEngineThrust(ModuleEngines engine, float minThrust, float maxThrust)
        {
            engine.minThrust = minThrust;
            engine.maxThrust = maxThrust;
            ConfigNode updateNode = new ConfigNode("MODULE");
            updateNode.AddValue("maxThrust", engine.maxThrust);
            updateNode.AddValue("minThrust", engine.minThrust);
            engine.OnLoad(updateNode);
        }

        public static void UpdatePartHighlighting(Part part)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }//noop on prefabs
            if (part.HighlightRenderer != null)
            {
                part.HighlightRenderer.Clear();
                if (part.transform.ROLFindRecursive("model") is Transform model)
                {
                    Renderer[] renders = model.GetComponentsInChildren<Renderer>(false);
                    part.HighlightRenderer.AddRange(renders);
                }
                part.RefreshHighlighter();
            }
        }
    }
}
