﻿using System;
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
        public float heatOutput = 300;

        //amount of heat dissipated per second, adjusted by the heatDissipationCurve below
        [KSPField]
        public float heatDissipation = 100;

        //point at which the object will begin to glow
        [KSPField]
        public float draperPoint = 400;

        //maximum amount of heat allowed in this engine
        //will reach max glow at this temp, and begin dissipating even faster past this point
        [KSPField]
        public float maxHeat = 2400;

        //maxStoredHeat
        //storedHeat will not go beyond this, sets retention period for maximum glow
        [KSPField]
        public float maxStoredHeat = 3600;

        //curve to adjust heat dissipation; should generally expel heat faster when hotter
        [KSPField]
        public FloatCurve heatDissipationCurve = new FloatCurve();

        //the heat-output curve for an engine (varies with thrust/throttle), in case it is not linear
        [KSPField]
        public FloatCurve heatAccumulationCurve = new FloatCurve();

        [KSPField]
        public FloatCurve redCurve = new FloatCurve();

        [KSPField]
        public FloatCurve blueCurve = new FloatCurve();

        [KSPField]
        public FloatCurve greenCurve = new FloatCurve();

        [KSPField]
        public string engineID = "Engine";

        [KSPField]
        public string meshName = string.Empty;

        [KSPField(isPersistant = true)]
        public float currentHeat = 0;

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
            heatDissipationCurve.Add(0f, 0.2f);
            heatDissipationCurve.Add(1f, 1f);

            heatAccumulationCurve.Add(0f, 0f);
            heatAccumulationCurve.Add(1f, 1f);
            
            redCurve.Add(0f, 0f);
            redCurve.Add(1f, 1f);

            blueCurve.Add(0f, 0f);
            blueCurve.Add(1f, 1f);

            greenCurve.Add(0f, 0f);
            greenCurve.Add(1f, 1f);

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
                    float throttle = engineModule.currentThrottle;
                    float heatIn = heatAccumulationCurve.Evaluate(throttle) * heatOutput * TimeWarp.fixedDeltaTime;
                    currentHeat += heatIn;
                }

                //dissipate heat
                float heatPercent = currentHeat / maxHeat;
                if (currentHeat > 0f)
                {
                    float heatOut = heatDissipationCurve.Evaluate(heatPercent) * heatDissipation * TimeWarp.fixedDeltaTime;
                    if (heatOut > currentHeat) { heatOut = currentHeat; }
                    currentHeat -= heatOut;
                }
                if (currentHeat > maxStoredHeat) { currentHeat = maxStoredHeat; }
            }
        }

        private void updateHeatAnim()
        {
            if (engineModule == null) { return; }

            float emissivePercent = 0f;

            if (!useThrottle && !useSolverEngines)
            {
                float mhd = maxHeat - draperPoint;
                float chd = currentHeat - draperPoint;

                if (chd < 0f) { chd = 0f; }
                emissivePercent = chd / mhd;
            }
            else if (!useThrottle && useSolverEngines)
            {
                float mhd = maxHeat - draperPoint;
                float chamberTemp = (float) getSolverChamberTemp();
                float chd = chamberTemp - draperPoint;

                if (chd < 0f) { chd = 0f; }
                emissivePercent = chd / mhd;
            }
            else
            {
                emissivePercent = engineModule.currentThrottle;
            }


            if (emissivePercent > 1f) { emissivePercent = 1f; }
            emissiveColor.r = redCurve.Evaluate(emissivePercent);
            emissiveColor.g = greenCurve.Evaluate(emissivePercent);
            emissiveColor.b = blueCurve.Evaluate(emissivePercent);
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