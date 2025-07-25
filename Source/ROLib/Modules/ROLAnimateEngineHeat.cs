using ROUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ROLib
{
    public class ROLAnimateEngineHeat : PartModule
    {

        //amount of 'heat' added per second at full throttle
        [KSPField]
        public double heatOutput = 300;

        //amount of heat dissipated per second, adjusted by the heatDissipationCurve below
        [KSPField]
        public double heatDissipation = 100;

        //point at which the object will begin to glow
        [KSPField]
        public double draperPoint = 400;

        //maximum amount of heat allowed in this engine
        //will reach max glow at this temp, and begin dissipating even faster past this point
        [KSPField]
        public double maxHeat = 2400;

        //maxStoredHeat
        //storedHeat will not go beyond this, sets retention period for maximum glow
        [KSPField]
        public double maxStoredHeat = 3600;

        //curve to adjust heat dissipation; should generally expel heat faster when hotter
        [KSPField]
        public HermiteCurve heatDissipationCurve = new HermiteCurve();

        //the heat-output curve for an engine (varies with thrust/throttle), in case it is not linear
        [KSPField]
        public HermiteCurve heatAccumulationCurve = new HermiteCurve();

        [KSPField]
        public HermiteCurve redCurve = new HermiteCurve();

        [KSPField]
        public HermiteCurve blueCurve = new HermiteCurve();

        [KSPField]
        public HermiteCurve greenCurve = new HermiteCurve();

        [KSPField]
        public string engineID = "Engine";

        [KSPField]
        public string meshName = string.Empty;

        [KSPField(isPersistant = true)]
        public double currentHeat = 0;

        [KSPField]
        public bool useThrottle;

        int shaderEmissiveID;

        private ModuleEngines engineModule;

        private Renderer[] animatedRenderers;

        private Color emissiveColor = new Color(0f, 0f, 0f, 1f);

        private Func<double> getSolverChamberTemp;

        public bool useSolverEngines => getSolverChamberTemp != null;

        public override void OnAwake()
        {
            heatDissipationCurve.AddKey(0f, 0.2f);
            heatDissipationCurve.AddKey(1f, 1f);

            heatAccumulationCurve.AddKey(0f, 0f);
            heatAccumulationCurve.AddKey(1f, 1f);
            
            redCurve.AddKey(0f, 0f);
            redCurve.AddKey(1f, 1f);

            blueCurve.AddKey(0f, 0f);
            blueCurve.AddKey(1f, 1f);

            greenCurve.AddKey(0f, 0f);
            greenCurve.AddKey(1f, 1f);

            shaderEmissiveID = Shader.PropertyToID("_EmissiveColor");
        }

        public override void OnStart(StartState state)
        {
            locateAnimatedTransforms();
            locateEngineModule();
            if(ROLModInterop.IsSolverEnginesInstalled() && !useThrottle)
                locateSolverEngines();
            else if (!ROLModInterop.IsSolverEnginesInstalled() && !useThrottle && HighLogic.LoadedSceneIsFlight)
                StartCoroutine(updateHeatCalc());
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
                updateHeatAnim();
        }

        private void locateEngineModule()
        {
            engineModule = null;
            engineModule = part.GetComponents<ModuleEngines>().FirstOrDefault(x => x.engineID == engineID);
            if (engineModule == null)
                print($"ERROR: Could not locate engine by ID: {engineID} for part: {part} for ROLAnimateEngineHeat.  This will cause errors during gameplay.  Setting engine to first engine module (if present)");

            engineModule ??= part.GetComponent<ModuleEngines>();
        }

        private void locateAnimatedTransforms()
        {
            List<Renderer> renderers = new List<Renderer>();
            Transform[] animatedTransforms = part.transform.ROLFindChildren(meshName);
            foreach (var animatedTransform in animatedTransforms)
            {
                renderers.AddRange(animatedTransform.GetComponentsInChildren<Renderer>(false));
            }
            animatedRenderers = renderers.ToArray();
            if (animatedRenderers == null || animatedRenderers.Length == 0) { print("ERROR: Could not locate any emissive meshes for name: " + meshName); }
        }

        private void locateSolverEngines()
        {
            if (ROLModInterop.getSolverEngineModule(part, engineID) is ModuleEngines solverEnginesModule)
            {
                MethodInfo getter = ROLModInterop.getSolverEngineTempProperty().GetGetMethod();

                getSolverChamberTemp = (Func<double>) Delegate.CreateDelegate(typeof(Func<double>), solverEnginesModule, getter);
            }
        }

        private IEnumerator updateHeatCalc()
        {
            while (true)
            {
                yield return new WaitForFixedUpdate();

                //add heat from engine
                if (engineModule.EngineIgnited && !engineModule.flameout && engineModule.currentThrottle > 0)
                {
                    double throttle = engineModule.currentThrottle;
                    double heatIn = heatAccumulationCurve.Evaluate(throttle) * heatOutput * TimeWarp.fixedDeltaTime;
                    currentHeat += heatIn;
                }

                //dissipate heat
                double heatPercent = currentHeat / maxHeat;
                if (currentHeat > 0)
                {
                    double heatOut = heatDissipationCurve.Evaluate(heatPercent) * heatDissipation * TimeWarp.fixedDeltaTime;
                    if (heatOut > currentHeat) { heatOut = currentHeat; }
                    currentHeat -= heatOut;
                }
                if (currentHeat > maxStoredHeat) { currentHeat = maxStoredHeat; }
            }
        }

        private void updateHeatAnim()
        {
            if (engineModule == null) { return; }

            double emissivePercent = 0f;

            if (!useThrottle && !useSolverEngines)
            {
                double mhd = maxHeat - draperPoint;
                double chd = currentHeat - draperPoint;

                if (chd < 0f) { chd = 0f; }
                emissivePercent = chd / mhd;
            }
            else if (!useThrottle && useSolverEngines)
            {
                double mhd = maxHeat - draperPoint;
                double chamberTemp = getSolverChamberTemp();
                double chd = chamberTemp - draperPoint;

                if (chd < 0f) { chd = 0f; }
                emissivePercent = chd / mhd;
            }
            else
            {
                emissivePercent = engineModule.currentThrottle;
            }


            if (emissivePercent > 1f) { emissivePercent = 1f; }
            emissiveColor.r = (float)redCurve.Evaluate(emissivePercent);
            emissiveColor.g = (float)greenCurve.Evaluate(emissivePercent);
            emissiveColor.b = (float)blueCurve.Evaluate(emissivePercent);
            setEmissiveColors();
        }

        private void setEmissiveColors()
        {
            if (animatedRenderers != null)
            {
                bool rebuild = false;
                foreach(var renderer in animatedRenderers)
                {
                    if (renderer == null)
                    {
                        rebuild = true;
                        continue;
                    }
                    renderer.sharedMaterial.SetColor(shaderEmissiveID, emissiveColor);
                }
                if (rebuild)
                {
                    animatedRenderers = null;
                    locateAnimatedTransforms();
                }
            }
        }

    }
}