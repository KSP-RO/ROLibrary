using ferram4;
using FerramAerospaceResearch.FARAeroComponents;
using KSP.Localization;
using RP0;
using RealFuels.Tanks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace ROLib
{
    public class ModuleROMaterials : PartModule, IPartMassModifier, IPartCostModifier
    {
        
        #region Display

        private const string GroupDisplayName = "RO-Thermal_Protection";
        private const string GroupName = "ModuleROMaterials";

        [KSPField(isPersistant = true, guiName = "Core", guiActiveEditor = false, groupName = GroupName, groupDisplayName = GroupDisplayName), 
         UI_ChooseOption(scene = UI_Scene.Editor, suppressEditorShipModified = true)]
        public string presetCoreName = "";
        [KSPField(isPersistant = true, guiName = "Core", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string presetCoreNameAltDispl = "";
        [KSPField(isPersistant = false, guiName = "TPS", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName), 
         UI_ChooseOption(scene = UI_Scene.Editor, suppressEditorShipModified = true)]
        public string presetSkinName = "";
        [KSPField(isPersistant = true, guiName = "TPS height (mm)", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName), 
         UI_FloatEdit(sigFigs = 1, suppressEditorShipModified = true)]
        public float tpsHeightDisplay = 0.0f;
        [KSPField(isPersistant = false, guiName = "Desc", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string description = "";
        [KSPField(guiActiveEditor = true, guiName = "Temp", guiUnits = "K", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string maxTempDisplay = "";
        [KSPField(guiActiveEditor = true, guiName = "Heat Capacity",  groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string thermalMassDisplay = "";
        [KSPField(guiActiveEditor = true, guiName = "Thermal Insulance" , groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string thermalInsulanceDisplay = "";
        [KSPField(guiActiveEditor = true, guiName = "Emissivity",  groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string emissiveConstantDisplay = "";
        [KSPField(guiActiveEditor = true, guiName = "Mass", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public String massDisplay = "";
        [KSPField(guiActiveEditor = true, guiName = "Skin Density", guiFormat = "F3", guiUnits = "kg/m²",  groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public float surfaceDensityDisplay = 0.0f;

        [KSPField(isPersistant = false, guiName = "Max Temp", guiActive = true, guiActiveEditor = false, guiActiveUnfocused = false)]
        public string FlightDisplay = "";


        private const string GroupDisplayNameDebug = "RO-Materials Debug";
        private const string GroupNameDebug = "ModuleROMaterialsDebug";
        
        [KSPField(guiName = "Peak Temp Skin/Core",  groupName = GroupNameDebug, groupDisplayName = GroupDisplayNameDebug
                    , guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false)]
        public string tempPeakText;
        [KSPField(guiName = "Emissivity",  groupName = GroupNameDebug, groupDisplayName = GroupDisplayNameDebug
                    , guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false)]
        public string emissiveConstantText;
        [KSPField(guiName = "SkinHeatCap",  groupName = GroupNameDebug, groupDisplayName = GroupDisplayNameDebug
                    , guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false)]
        public string skinHeatCapText;
        [KSPField(guiName = "InternalCondMult",  groupName = GroupNameDebug, groupDisplayName = GroupDisplayNameDebug
                    , guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false)]
        public string skinInternalConductionMultText;
        [KSPField(guiName = "heatConductivity",  groupName = GroupNameDebug, groupDisplayName = GroupDisplayNameDebug
                    , guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false)]
        public string heatConductivityText;

        #endregion Display

        #region Private Variables

        private ModuleAblator modAblator;
        private ModuleFuelTanks moduleFuelTanks;
        private FARAeroPartModule fARAeroPartModule; 
        private FARWingAerodynamicModel fARWingModule;
        private ModuleTagList CCTagListModule;
        private PresetROMatarial presetCore;
        private PresetROMatarial presetSkin;

        private const string reentryTag = "Reentry";
        private bool reentryByDefault = false;
        private float tpsCost = 0.0f;
        private float tpsMass = 0.0f;
        private double tpsSurfaceDensity = 0.0f; 
        private double skinIntTransferCoefficient = 0.0;
        private float moduleMass = 0.0f;
        private string ablatorResourceName;
        private string outputResourceName;
        private bool onLoadFiredInEditor;
        private bool ignoreSurfaceAttach = true; // ignore all surface attached parts/childern when subtracting surface area
        private string[] ignoredNodes = new string[] {}; // ignored Nodes when subtracting surface area
        private float prevHeight = -10.001f;
        private double heatConductivityDivGlobal => 1.0 / (45 * 10.0 * PhysicsGlobals.ConductionFactor);
        private double SkinInternalConductivityDivGlobal => 1.0 / (PhysicsGlobals.SkinInternalConductionFactor * 0.5 * PhysicsGlobals.ConductionFactor * 10.0 * part.heatConductivity);
        private double SkinSkinConductivityDivGlobal => 1.0 / (10.0 * PhysicsGlobals.ConductionFactor * PhysicsGlobals.SkinSkinConductionFactor);
        private double SkinThermalMassModifierDiv => 1.0 / (PhysicsGlobals.StandardSpecificHeatCapacity * part.thermalMassModifier);
        private double SkinThermalMassModifierMult => PhysicsGlobals.StandardSpecificHeatCapacity * part.thermalMassModifier;
        private double absorptiveConstantOrig;
        private double[][] thermalPropertiesSkin;
        private double[][] thermalPropertiesCore;
        private int thermalPropertyRowsSkin = 1000;
        private int thermalPropertyRowsCore = 1000;
        private bool hasThermalProperties = false;
        private bool pawOpen = false;
        private double peakTempSkin = 0.0;
        private double peakTempCore;
        private double nextUpdateUpSkin = double.MaxValue;
        private double nextUpdateDownSkin = double.MinValue;
        private double nextUpdateUpCore = double.MaxValue;
        private double nextUpdateDownCore = double.MinValue;
        private int indexSkin = 0;
        private int indexCore = 0;

        private static bool? _RP1Found = null;
        public static bool RP1Found
        {
            get
            {
                if (!_RP1Found.HasValue)
                {
                    var assembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "RP0")?.assembly;
                    _RP1Found = assembly != null;
                }
                return _RP1Found.Value;
            }
        }
        
        [SerializeField] private string[] availablePresetNamesCore = new string[] {};
        [SerializeField] private string[] availablePresetNamesSkin = new string[] {};

        #endregion Private Variables
        [KSPField] public float surfaceAreaCfg = -0.1f;
        [KSPField] public float volumePart = -0.1f;
        [KSPField] public bool tpsMassIsAdditive = true;
        [KSPField] public double surfaceArea = 0.0; // m2
        [KSPField] public double surfaceAreaCovered = 0.0; // m2
        [Persistent] public string coreCfg = "";
        [Persistent] public string skinCfg = "";
        [KSPField] public string skinReferenceZeroPoint = null;
        [KSPField] public double surfaceDensityZeroPoint = -1.0;
        [KSPField] public float costPerAreaZeroPoint = -1.0f;
        [KSPField] public bool coreSelectable = false;
        [KSPField] public double coreThermalMassFraction = 1.0;
        [Persistent] public float skinHeightCfg = -1.0f;
        public float TPSAreaCost => presetSkin?.costPerArea ?? 1.0f;
        public float TPSAreaMult => presetSkin?.heatShieldAreaMult ?? 1.0f;

        public string PresetCore {
            get{ return presetCore.name; }
            set{ 
                if (PresetROMatarial.PresetsCore.TryGetValue(value, out PresetROMatarial preset)) {
                    presetCoreName = value;
                    presetCoreNameAltDispl = value;
                    presetCore = preset;
                }
                else if (coreCfg != "" && PresetROMatarial.PresetsSkin.TryGetValue(coreCfg, out preset))
                {
                    Debug.LogError($"[ROThermal] " + part.name + " Preset " + presetCoreName + " config not available, falling back to" + coreCfg);
                    presetCoreName = coreCfg;
                    presetCoreNameAltDispl = coreCfg;
                    presetCore = preset;
                }
                else
                {
                    Debug.LogError($"[ROThermal] " + part.name + " Preset " + presetCoreName + " config not available, falling back to default");
                    PresetROMatarial.PresetsSkin.TryGetValue("default", out preset);
                    presetCoreName = "default";
                    presetCoreNameAltDispl = "default";
                    presetCore = preset;
                }
            }
        }
        public string PresetTPS {
            get{ return presetSkin.name; }
            set{ 
                if (PresetROMatarial.PresetsSkin.TryGetValue(value, out PresetROMatarial preset)) {
                    presetSkinName = value;
                    presetSkin = preset;
                }  
                else if (skinCfg != "" && PresetROMatarial.PresetsSkin.TryGetValue(skinCfg, out preset))
                {
                    Debug.LogError($"[ROThermal] " + part.name + " Preset " + presetSkinName + " config not available, falling back to" + skinCfg);
                    presetSkinName = value;
                    presetSkin = preset;
                }
                else
                {
                    Debug.LogError($"[ROThermal] " + part.name + " Preset " + presetSkinName + " config not available, falling back to None");
                    PresetROMatarial.PresetsSkin.TryGetValue("None", out preset);
                    presetSkinName = value;
                    presetSkin = preset;
                }
            }      
        }

        public bool UpdateSurfaceArea()
        {
            if (surfaceAreaCfg > 0.0f) 
            {
                if (surfaceAreaCovered == surfaceAreaCfg)
                    return false;
                Debug.Log("[ROThermal] get_SurfaceArea derived from SurfaceAreaPart Entry: " + surfaceAreaCfg);
                surfaceAreaCovered =  surfaceAreaCfg;
            }
            else if (fARAeroPartModule != null) 
            {   
                double frac = surfaceArea / fARAeroPartModule?.ProjectedAreas.totalArea ?? surfaceArea;
                if (frac > 1.01 || frac < 0.99) 
                {
                    surfaceArea = fARAeroPartModule.ProjectedAreas.totalArea;
                    if (surfaceArea > 0.0)
                    {
                        /// No need to subtract occluded areas, fARAeroPartModule takes care of that
                        Debug.Log("[ROThermal] get_SurfaceArea fARAeroPartModule totalArea = " + surfaceArea);
                        return true;
                    }
                }
                else 
                {
                    Debug.Log("[ROThermal] get_SurfaceArea No significant change in surface area base " + surfaceArea + " new " + (fARAeroPartModule?.ProjectedAreas.totalArea ?? 0.0));
                }

                if (fARAeroPartModule?.ProjectedAreas == null)
                    Debug.Log("[ROThermal] get_SurfaceArea skipping fARAeroPartModule ProjectedAreas = null ");
                else if (fARAeroPartModule?.ProjectedAreas.totalArea == null)
                    Debug.Log("[ROThermal] get_SurfaceArea skipping fARAeroPartModule totalArea = null ");
                return false;
            }
            /// decrease surface area based on contact area of attached nodes & surface attached parts
            if (surfaceAreaCovered > 0.0) 
            {
                //part.DragCubes.SetPartOcclusion();
                part.DragCubes.ForceUpdate(false, true, false);
                //Debug.Log($"[ROThermal] part.DragCubes.SetPartOcclusion() ");
                string str = $"[ROThermal] get_SurfaceArea Surface Area: " + surfaceAreaCovered + " coverd skin: attachNodes ";
                foreach (AttachNode nodeAttach in part.attachNodes) 
                {
                    if (nodeAttach.attachedPart == null | ignoredNodes.Contains(nodeAttach.id))
                        continue;
                    nodeAttach.attachedPart.DragCubes.ForceUpdate(false, true, false);
                    surfaceAreaCovered -= nodeAttach.contactArea;
                    str += nodeAttach.contactArea + ", ";
                }
                part.srfAttachNode.attachedPart?.DragCubes.SetPartOcclusion();
                str +=  "srfAttachNode " + part.srfAttachNode.contactArea + ", ";
                surfaceAreaCovered -= part.srfAttachNode?.contactArea ?? 0.0f;
                if (!ignoreSurfaceAttach)
                {   
                    str +=  "children ";
                    Debug.Log($"[ROThermal] part.srfAttachNode.contactArea ");
                    foreach (Part child in part.children) 
                    {
                        if (child == null)
                            continue;
                        child.DragCubes.RequestOcclusionUpdate();
                        child.DragCubes.ForceUpdate(false, true, false);
                        child.DragCubes.SetPartOcclusion();
                        str +=  child.srfAttachNode.contactArea + ", ";
                        surfaceAreaCovered -= child.srfAttachNode.contactArea;
                    }
                }
            }
            if (surfaceAreaCovered > 0.0)
            {
                surfaceArea = surfaceAreaCovered;
                return true;
            } 
            else
            {
                Debug.LogWarning("[ROThermal] get_SurfaceArea failed: Area=" + surfaceAreaCovered);
                return false;
            }
        }


        // TODO need detailed implementation
        //
        // HeatConductivity seems to be a replacement value for thermal contact resistance in inverse
        // which then get multltiplied together
        // since heat transfer calculations usualy add them together & don't multiply them, like the game does 
        // flux: Q = U * A * ΔT     U [kW/(m²·K)]: overall heat transfer coefficient 
        //                          U = 1 /( l1/k1 + l2/k2 + 1/hc) bc. temperatures inside parts are uniform l1&2 get infinitely small
        //                          U = hc      in ksp U -> part.heatConductivity * part2.heatConductivity * global mult
        //                          Al->Al@1atm ~2200 hc
        // partThermalData.localIntConduction[kW] += thermalLink.contactArea[m^2] * thermalLink.temperatureDelta[K]
        //                                       * thermalLink.remotePart.heatConductivity[Sqrt(kW/(m²·K))] * part.heatConductivity[Sqrt(kW/(m²·K))]
        //                                       * PhysicsGlobals.ConductionFactor * 10.0

        // TODO need detailed implementation
        private double SkinSkinConductivity {
            get {
                if (presetSkin.skinSkinConductivity > 0) {
                    return presetSkin.skinSkinConductivity / part.heatConductivity * SkinSkinConductivityDivGlobal;
                } else if (presetCore.skinSkinConductivity > 0 ) {
                    return presetCore.skinSkinConductivity / part.heatConductivity * SkinSkinConductivityDivGlobal;
                } else {
                    return part.partInfo.partPrefab.skinSkinConductionMult;
                }
            }
        }
        
        public float TPSMass => (float)(surfaceArea * tpsSurfaceDensity) * TPSAreaMult / 1000f;
        public float TPSMassRef => (float)(surfaceArea * (tpsSurfaceDensity - surfaceDensityZeroPoint)) * TPSAreaMult / 1000f;
        public float TPSCost => (float)surfaceArea * (TPSAreaCost - costPerAreaZeroPoint ) * TPSAreaMult;

        public float Ablator => Mathf.Round((float)surfaceArea * presetSkin.heatShieldAblator * 10f) / 10f;       
       

        #region Standard KSP Overrides


        public void FixedUpdate()
        {
            if (hasThermalProperties && HighLogic.LoadedSceneIsFlight) 
            {
                if (part.skinTemperature > nextUpdateUpSkin) 
                {  
                    nextUpdateDownSkin = thermalPropertiesSkin[indexSkin][0];
                    indexSkin ++;
                    if (indexSkin >= thermalPropertyRowsSkin) 
                    {
                        indexSkin --;
                        nextUpdateUpSkin = double.PositiveInfinity;
                        return;
                    }
                    nextUpdateUpSkin = thermalPropertiesSkin[indexSkin][0];
                    Debug.Log("[ROThermal] "+ part.name + " moving temperature values up, between " +  nextUpdateDownSkin + "-" + nextUpdateUpSkin);

                    part.skinThermalMassModifier = thermalPropertiesSkin[indexSkin][1];
                    part.skinInternalConductionMult = thermalPropertiesSkin[indexSkin][2];
                    part.emissiveConstant = thermalPropertiesSkin[indexSkin][3];
                    //part.ptd.emissScalar = part.emissiveConstant * PhysicsGlobals.RadiationFactor * 0.001;
                    if (pawOpen && PhysicsGlobals.ThermalDataDisplay)
                        UpdateFlightDebug();
                } 
                else if (part.skinTemperature < nextUpdateDownSkin) 
                {
                    nextUpdateUpSkin = thermalPropertiesSkin[indexSkin][0];
                    indexSkin --;
                    if (indexSkin < 0) 
                    {
                        indexSkin = 0;
                        nextUpdateDownSkin = -1;
                        return;
                    }
                    nextUpdateDownSkin = thermalPropertiesSkin[indexSkin][0];
                    Debug.Log("[ROThermal] "+ part.name + " moving temperature values down, between " +  nextUpdateDownSkin + "-" + nextUpdateUpSkin);

                    part.skinThermalMassModifier = thermalPropertiesSkin[indexSkin][1];
                    part.skinInternalConductionMult = thermalPropertiesSkin[indexSkin][2];
                    part.emissiveConstant = thermalPropertiesSkin[indexSkin][3];
                    //part.ptd.emissScalar = part.emissiveConstant * PhysicsGlobals.RadiationFactor * 0.001;
                    if (pawOpen && PhysicsGlobals.ThermalDataDisplay)
                        UpdateFlightDebug();
                }
                if (part.temperature > nextUpdateUpCore) 
                {
                    nextUpdateDownCore = thermalPropertiesCore[indexCore][0];
                    indexCore ++;
                    if (indexCore >= thermalPropertyRowsCore) 
                    {
                        indexCore --;
                        nextUpdateUpCore = double.PositiveInfinity;
                        return;
                    }
                    nextUpdateUpCore = thermalPropertiesCore[indexCore][0];
                    part.thermalMassModifier = thermalPropertiesCore[indexCore][1]; 
                    Debug.Log("[ROThermal] "+ part.name + " moving Core temperature values up, between " +  nextUpdateDownCore + "-" + nextUpdateUpCore);
                }
                else if (part.temperature < nextUpdateDownCore) 
                {
                    nextUpdateUpCore = thermalPropertiesCore[indexCore][0];
                    indexCore --;
                    if (indexCore < 0) 
                    {
                        indexCore = 0;
                        nextUpdateDownCore = -1;
                        return;
                    }
                    nextUpdateDownCore = thermalPropertiesCore[indexCore][0];
                    part.thermalMassModifier = thermalPropertiesCore[indexCore][1];
                    Debug.Log("[ROThermal] "+ part.name + " moving Core temperature values up, between " +  nextUpdateDownCore + "-" + nextUpdateUpCore);
                }
            } 
            if (pawOpen && PhysicsGlobals.ThermalDataDisplay && HighLogic.LoadedSceneIsFlight)
            {
                if (part.temperature > peakTempCore || part.skinTemperature > peakTempSkin) 
                {
                    peakTempCore = part.temperature;
                    peakTempSkin = part.skinTemperature;
                    tempPeakText = Localizer.Format("<<1>>/<<2>> K", peakTempSkin.ToString("F1"),  peakTempCore.ToString("F1"));
                }     
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            onLoadFiredInEditor = HighLogic.LoadedSceneIsEditor;

            node.TryGetValue("TPSSurfaceArea", ref surfaceAreaCfg);
            node.TryGetValue("Volume", ref volumePart);
            
            if (!node.TryGetValue("skinMassIsAdditive", ref tpsMassIsAdditive))
                Debug.Log("[ROThermal] "+ part.name + " skinMassAdditive entry not found");
            if (node.TryGetValue("corePresets", ref availablePresetNamesCore))
                Debug.Log("[ROThermal] available presetsCore loaded");
            if (node.TryGetValue("skinPresets", ref availablePresetNamesSkin))
                Debug.Log("[ROThermal] available presetsSkin loaded");  
            node.TryGetValue("ignoreNodes", ref ignoredNodes);
            node.TryGetValue("ignoreSurfaceAttach", ref ignoreSurfaceAttach);

            node.TryGetValue("core", ref coreCfg);
            node.TryGetValue("coreSelectable", ref coreSelectable);
            node.TryGetValue("coreThermalMassFraction", ref coreThermalMassFraction);
            node.TryGetValue("skin", ref skinCfg);
            node.TryGetValue("skinHeight", ref skinHeightCfg);

            node.TryGetValue("skinZeroPoint", ref skinReferenceZeroPoint);
            node.TryGetValue("skinMassPerAreaZeroPoint", ref surfaceDensityZeroPoint);
            node.TryGetValue("skinCostPerAreaZeroPoint", ref costPerAreaZeroPoint);           

            ensurePresetIsInList(ref availablePresetNamesCore, coreCfg);
            ensurePresetIsInList(ref availablePresetNamesSkin, skinCfg);

            if (coreSelectable)
            {
                Fields[nameof(presetCoreName)].guiActiveEditor = true;
                Fields[nameof(presetCoreName)].uiControlEditor.controlEnabled = true;
                Fields[nameof(presetCoreNameAltDispl)].guiActiveEditor = false;
            }
            else
            {
                Fields[nameof(presetCoreName)].guiActiveEditor = false;
                Fields[nameof(presetCoreName)].uiControlEditor.controlEnabled = false;
                Fields[nameof(presetCoreNameAltDispl)].guiActiveEditor = true;
            }
        }

        public override void OnStart(StartState state)
        {
            GameEvents.onPartActionUIDismiss.Add(OnPartActionUIDismiss);
            GameEvents.onPartActionUIShown.Add(OnPartActionUIShown);
            
            PresetROMatarial.LoadPresets();
            if (!PresetROMatarial.Initialized) {
                Debug.Log($"[ROThermal] OnStart Presets could not be initialized" + part.name + " LoadedScene is " + HighLogic.LoadedScene);
                return;
            }
            if (skinReferenceZeroPoint != null)
            {
                if (PresetROMatarial.PresetsSkin.TryGetValue(skinReferenceZeroPoint, out PresetROMatarial preset))
                {
                    if (surfaceDensityZeroPoint < 0.0)
                        surfaceDensityZeroPoint = preset.skinMassPerArea;
                    if (costPerAreaZeroPoint < 0.0f)
                        costPerAreaZeroPoint = preset.costPerArea;
                }
                else
                    Debug.LogWarning($"[ROThermal] skinZeroPoint " + skinReferenceZeroPoint + "not present in presets list");
            }
            if (surfaceDensityZeroPoint < 0.0)
                surfaceDensityZeroPoint = 0.0;
            if (costPerAreaZeroPoint < 0.0)
                costPerAreaZeroPoint = 0.0f;
                

            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
            {
                if (presetCoreName != "") 
                    PresetCore = presetCoreName;
                else if (coreCfg != "")
                    PresetCore = coreCfg;
                else
                    PresetCore = "default";

                if (presetSkinName != "") 
                    PresetTPS = presetSkinName;
                else if (skinCfg != "")
                    PresetTPS = skinCfg;
                else
                    PresetTPS = "None";
            }


            if (HighLogic.LoadedSceneIsEditor) 
            {         
                if (availablePresetNamesCore.Length > 0)
                { 
                    // RP-1 allows selecting all configs but will run validation when trying to add the vessel to build queue
                    string[] unlockedPresetsName = RP1Found ? availablePresetNamesCore : GetUnlockedPresets(availablePresetNamesCore);
                    UpdatePresetsList(unlockedPresetsName, PresetType.Core);
                }          

                Fields[nameof(presetCoreName)].uiControlEditor.onFieldChanged = 
                Fields[nameof(presetCoreName)].uiControlEditor.onSymmetryFieldChanged =
                    (bf, ob) => ApplyCorePreset(presetCoreName);

                if (availablePresetNamesSkin.Length > 0)
                {
                    // GameEvents.onEditorShipModified.Add(OnEditorShipModified);
                    // RP-1 allows selecting all configs but will run validation when trying to add the vessel to build queue
                    string[] unlockedPresetsName = RP1Found ? availablePresetNamesSkin : GetUnlockedPresets(availablePresetNamesSkin);
                    UpdatePresetsList(unlockedPresetsName, PresetType.Skin);
                }
                
                Fields[nameof(presetSkinName)].uiControlEditor.onFieldChanged =
                Fields[nameof(presetSkinName)].uiControlEditor.onSymmetryFieldChanged =
                    (bf, ob) => ApplySkinPreset(presetSkinName);
                Fields[nameof(tpsHeightDisplay)].uiControlEditor.onFieldChanged =
                Fields[nameof(tpsHeightDisplay)].uiControlEditor.onSymmetryFieldChanged = 
                    (bf, ob) => OnHeightChanged((float)bf.GetValue(this));
                
                this.ROLupdateUIFloatEditControl(nameof(tpsHeightDisplay), presetSkin.skinHeightMin, presetSkin.SkinHeightMaxVal, 10f, 1f, 0.05f);
            }
        } 

        public override void OnStartFinished(StartState state)
        {
            Debug.Log($"[ROThermal] OnStartFinished "  + part.name + " Scene: " + HighLogic.LoadedScene);
            base.OnStartFinished(state);
            absorptiveConstantOrig = part.absorptiveConstant;

            if (!PresetROMatarial.Initialized)
            {
                Debug.Log($"[ROThermal] OnStartFinished Presets are not initialized" + part.name + " LoadedScene is " + HighLogic.LoadedScene);
                return;
            }

            modAblator = part?.FindModuleImplementing<ModuleAblator>();
            moduleFuelTanks = part?.FindModuleImplementing<ModuleFuelTanks>();
            fARAeroPartModule = part?.FindModuleImplementing<FARAeroPartModule>();
            CCTagListModule = part?.FindModuleImplementing<ModuleTagList>();
            fARWingModule = part?.FindModuleImplementing<FARControllableSurface>();
            fARWingModule = part?.FindModuleImplementing<FARWingAerodynamicModel>();

            if(HighLogic.LoadedSceneIsEditor) 
            {
                if (moduleFuelTanks is ModuleFuelTanks)
                { 
                    /// ModuleFuelTanks changes TankType & mass on Update()
                    moduleFuelTanks.Fields[nameof(moduleFuelTanks.typeDisp)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateCoreForRealfuels(true);
                    moduleFuelTanks.Fields[nameof(moduleFuelTanks.typeDisp)].uiControlEditor.onSymmetryFieldChanged += (bf, ob) => UpdateCoreForRealfuels(true);
                    GameEvents.onPartResourceListChange.Add(OnPartResourceListChange);
                    Debug.Log("[ROThermal] " + part.name + " ModuleFuelTanks found " + moduleFuelTanks.name + " updating core material list");
                    UpdateCoreForRealfuels(false);
                }

                ApplyCorePreset(presetCoreName);
                ApplySkinPreset(presetSkinName);
            }
            if(CCTagListModule is ModuleTagList && CCTagListModule.tags.Contains(reentryTag))
            {
                reentryByDefault = true;
            }

            if (HighLogic.LoadedSceneIsFlight) 
            {
                ApplyCorePreset(presetCoreName);
                if (presetCore.hasCVS) 
                {
                    LoadThermalPropertiesArrayCore();
                    for (int i = 0; i < thermalPropertiesCore.Length - 1; i++)
                    {
                        if (thermalPropertiesCore[i][0] >= 250.0) {
                            indexCore = i;
                            nextUpdateDownCore = thermalPropertiesCore[i-1][0];
                            nextUpdateUpCore = thermalPropertiesCore[i][0];
                            break;
                        }
                    }
                    part.thermalMassModifier = thermalPropertiesCore[indexCore][1];
                    hasThermalProperties = true;
                    Debug.Log($"[ROThermal] OnStartFinished thermalPropertiesCore array temperature set to " + thermalPropertiesCore[indexCore][0] + " part " + part);
                }

                ApplySkinPreset(presetSkinName);    
                if (presetSkin.hasCVS) 
                {
                    LoadThermalPropertiesArraySkin();
                    for (int i = 0; i < thermalPropertiesSkin.Length - 1; i++)
                    {
                        if (thermalPropertiesSkin[i][0] >= 250.0) {
                            indexSkin = i;
                            nextUpdateDownSkin = thermalPropertiesSkin[i-1][0];
                            nextUpdateUpSkin = thermalPropertiesSkin[i][0];
                            break;
                        }
                    }
                    part.skinInternalConductionMult = thermalPropertiesSkin[indexCore][2];
                    part.skinThermalMassModifier = thermalPropertiesSkin[indexCore][1];
                    part.emissiveConstant = thermalPropertiesSkin[indexCore][3];
                    hasThermalProperties = true;
                    Debug.Log($"[ROThermal] OnStartFinished thermalPropertiesSkin array temperature set to " + thermalPropertiesSkin[indexSkin][0] + " part " + part);
                }
                DebugLog();
            }
        }
        
        private void OnDestroy()
        {
            GameEvents.onPartActionUIDismiss.Remove(OnPartActionUIDismiss);
            GameEvents.onPartActionUIShown.Remove(OnPartActionUIShown);
            if (moduleFuelTanks is ModuleFuelTanks && HighLogic.LoadedSceneIsEditor)
                GameEvents.onPartResourceListChange.Remove(OnPartResourceListChange);
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => moduleMass;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => tpsCost - defaultCost;

        #endregion Standard KSP Overrides

        #region Custom Methods



        public void OnHeightChanged(float height) 
        {
            if (height == prevHeight) return;
            prevHeight = Mathf.Clamp(tpsHeightDisplay, presetSkin.skinHeightMin, presetSkin.SkinHeightMaxVal);
            tpsHeightDisplay = prevHeight;

            //UpdateHeight();
            ApplyThermal();
            UpdateGeometricProperties();
        }

        public void ApplyCorePreset (string preset) {
            PresetCore = preset;

            if (fARWingModule != null)
            {
                //fARWingModule.wingBaseMassMultiplier = ;
            }

            // maxTemp
            if (presetCore.maxTempOverride > 0) {
                part.maxTemp = presetCore.maxTempOverride;
            } else {
                part.maxTemp = part.partInfo.partPrefab.maxTemp;
            }    
            // thermalMassModifier
            if (presetCore.specificHeatCapacity > 0) {
                part.thermalMassModifier = presetCore.specificHeatCapacity * coreThermalMassFraction / PhysicsGlobals.StandardSpecificHeatCapacity;
            } else {
                part.thermalMassModifier = part.partInfo.partPrefab.thermalMassModifier;
            }
            // heatConductivity
            if (presetCore.thermalConductivity > 0 ) {
                part.heatConductivity = presetCore.thermalConductivity * heatConductivityDivGlobal;
            } else {
                part.heatConductivity = part.partInfo.partPrefab.heatConductivity;
            };

            ApplyThermal();
            CCTagUpdate(presetCore);

            Debug.Log($"[ROThermal] applied preset {PresetCore} for core of {part.name}");     
            UpdateGeometricProperties();
        }
        public void ApplySkinPreset (string preset) {
            PresetTPS = preset;

            float heightMax = presetSkin.SkinHeightMaxVal; 
            float heightMin = presetSkin.skinHeightMin;
            this.ROLupdateUIFloatEditControl(nameof(tpsHeightDisplay), heightMin, heightMax, 10f, 1f, 0.05f);
            prevHeight = Mathf.Clamp(tpsHeightDisplay, heightMin, heightMax);
            tpsHeightDisplay = prevHeight;

            ApplyThermal();
            CCTagUpdate(presetSkin);

            // update ModuleAblator parameters, if present and used
            if (modAblator != null && !presetSkin.disableModAblator)
            {
                if (!string.IsNullOrWhiteSpace(presetSkin.AblativeResource))
                    modAblator.ablativeResource = presetSkin.AblativeResource;
                if (!string.IsNullOrWhiteSpace(presetSkin.OutputResource))
                    modAblator.outputResource = presetSkin.OutputResource;

                if (!string.IsNullOrWhiteSpace(presetSkin.NodeName))
                    modAblator.nodeName = presetSkin.NodeName;
                if (!string.IsNullOrWhiteSpace(presetSkin.CharModuleName))
                    modAblator.charModuleName = presetSkin.CharModuleName;
                if (!string.IsNullOrWhiteSpace(presetSkin.UnitsName))
                    modAblator.unitsName = presetSkin.UnitsName;

                if (presetSkin.LossExp.HasValue)
                    modAblator.lossExp = presetSkin.LossExp.Value;
                if (presetSkin.LossConst.HasValue)
                    modAblator.lossConst = presetSkin.LossConst.Value;
                if (presetSkin.PyrolysisLossFactor.HasValue)
                    modAblator.pyrolysisLossFactor = presetSkin.PyrolysisLossFactor.Value;
                if (presetSkin.AblationTempThresh.HasValue)
                    modAblator.ablationTempThresh = presetSkin.AblationTempThresh.Value;
                if (presetSkin.ReentryConductivity.HasValue)
                    modAblator.reentryConductivity = presetSkin.ReentryConductivity.Value;
                if (presetSkin.UseNode.HasValue)
                    modAblator.useNode = presetSkin.UseNode.Value;
                if (presetSkin.CharAlpha.HasValue)
                    modAblator.charAlpha = presetSkin.CharAlpha.Value;
                if (presetSkin.CharMax.HasValue)
                    modAblator.charMax = presetSkin.CharMax.Value;
                if (presetSkin.CharMin.HasValue)
                    modAblator.charMin = presetSkin.CharMin.Value;
                if (presetSkin.UseChar.HasValue)
                    modAblator.useChar = presetSkin.UseChar.Value;
                if (presetSkin.OutputMult.HasValue)
                    modAblator.outputMult = presetSkin.OutputMult.Value;
                if (presetSkin.InfoTemp.HasValue)
                    modAblator.infoTemp = presetSkin.InfoTemp.Value;
                if (presetSkin.Usekg.HasValue)
                    modAblator.usekg = presetSkin.Usekg.Value;
                if (presetSkin.NominalAmountRecip.HasValue)
                    modAblator.nominalAmountRecip = presetSkin.NominalAmountRecip.Value;
            }

            if (modAblator != null)
            {
                if (presetSkin.AblativeResource == null || ablatorResourceName !=presetSkin.AblativeResource ||
                    presetSkin.OutputResource == null || outputResourceName != presetSkin.OutputResource ||
                    presetSkin.disableModAblator)
                {
                    RemoveAblatorResources();
                }

                ablatorResourceName = presetSkin.AblativeResource;
                outputResourceName = presetSkin.OutputResource;

                modAblator.isEnabled = modAblator.enabled = !presetSkin.disableModAblator;
            }

            if (!string.IsNullOrEmpty(presetSkin.description))
            {
                Fields[nameof(description)].guiActiveEditor = true;
                description = presetSkin.description;
            }
            else
                Fields[nameof(description)].guiActiveEditor = false;

            Debug.Log($"[ROThermal] applied preset {PresetTPS} for Skin of {part.name}");     
            UpdateGeometricProperties();
        }

        public void ApplyThermal()
        // part.skinInternalConductionMult = skinIntTransferCoefficient * [1 / conductionMult] / part.heatConductivity
        //
        // skinInteralConductionFlux[kJ/s] = InternalConductivity[kJ/s-K] * FluxExponent[] * dT[K]
        // InternalConductivity[kJ/s-K]    = part.skinExposedAreaFrac[] * part.radiativeArea[m^2] * part.skinInternalConductionMult[kJ/(s*m^2*K)] * conductionMult[] * part.heatConductivity
        // conductionMult                  = PhysicsGlobals.SkinInternalConductionFactor * 0.5 * PhysicsGlobals.ConductionFactor * 10
        // skinIntTransferCoefficient[kW/(m^2*K)] = 1 / ThermalResistance [(m^2*K)/kW]
        //
        {
            if (presetSkin.skinHeightMax > 0.0 && presetSkin.skinSpecificHeatCapacityMax > 0.0 && presetSkin.skinMassPerAreaMax > 0.0) 
            {
                double heightfactor = 0;
                if (presetSkin.skinHeightMax != presetSkin.skinHeightMin)
                {
                    heightfactor = (tpsHeightDisplay - presetSkin.skinHeightMin) / (presetSkin.skinHeightMax - presetSkin.skinHeightMin);
                }

                tpsSurfaceDensity = (presetSkin.skinMassPerAreaMax - presetSkin.skinMassPerArea) * heightfactor + presetSkin.skinMassPerArea;
                part.skinMassPerArea = tpsSurfaceDensity;
                part.skinThermalMassModifier = ((presetSkin.skinSpecificHeatCapacityMax - presetSkin.skinSpecificHeatCapacity) * heightfactor + presetSkin.skinSpecificHeatCapacity)
                                                * SkinThermalMassModifierDiv;
                skinIntTransferCoefficient = (presetSkin.skinIntTransferCoefficientMax - presetSkin.skinIntTransferCoefficient) * heightfactor + presetSkin.skinIntTransferCoefficient;
                part.skinInternalConductionMult = skinIntTransferCoefficient * SkinInternalConductivityDivGlobal ;
            } 
            else 
            {
                // skinMassPerArea
                if (presetSkin.skinMassPerArea > 0.0) {
                    part.skinMassPerArea = presetSkin.skinMassPerArea;
                    tpsSurfaceDensity = (float)presetSkin.skinMassPerArea;
                } else if (presetCore.skinMassPerArea > 0.0 ) {
                    part.skinMassPerArea = presetCore.skinMassPerArea;
                    tpsSurfaceDensity = 0.0f;
                } else {
                    part.skinMassPerArea = part.partInfo.partPrefab.skinMassPerArea;
                    tpsSurfaceDensity = 0.0f;
                }
                // skinThermalMassModifier
                if (presetSkin.skinSpecificHeatCapacity > 0.0) {
                    part.skinThermalMassModifier = presetSkin.skinSpecificHeatCapacity * SkinThermalMassModifierDiv;
                } else if (presetCore.skinSpecificHeatCapacity > 0.0) {
                    part.skinThermalMassModifier = presetCore.skinSpecificHeatCapacity * SkinThermalMassModifierDiv;
                } else {
                    part.skinThermalMassModifier = part.partInfo.partPrefab.skinThermalMassModifier;
                }
                // skinIntTransferCoefficient
                if (presetSkin.skinIntTransferCoefficient != double.PositiveInfinity | presetSkin.skinIntTransferCoefficient  > 0.0) {
                    skinIntTransferCoefficient = presetSkin.skinIntTransferCoefficient;
                    part.skinInternalConductionMult = skinIntTransferCoefficient * SkinInternalConductivityDivGlobal;
                } else if (presetCore.skinIntTransferCoefficient > 0.0 ) {
                    skinIntTransferCoefficient = presetCore.skinIntTransferCoefficient;
                    part.skinInternalConductionMult = skinIntTransferCoefficient * SkinInternalConductivityDivGlobal;
                } else {
                    part.skinInternalConductionMult = part.partInfo.partPrefab.skinInternalConductionMult;
                    skinIntTransferCoefficient = part.partInfo.partPrefab.skinInternalConductionMult;
                }
            }
            part.skinSkinConductionMult = part.skinInternalConductionMult;


            // skinMaxTempOverride
            if (presetSkin.skinMaxTempOverride > 0) {
                part.skinMaxTemp = presetSkin.skinMaxTempOverride;
            } else if (presetCore.skinMaxTempOverride > 0 ) {
                part.skinMaxTemp = presetCore.skinMaxTempOverride;
            } else {
                part.skinMaxTemp = part.partInfo.partPrefab.skinMaxTemp;
            }
            // emissiveConstant
            if (presetSkin.emissiveConstantOverride > 0) {
                part.emissiveConstant = presetSkin.emissiveConstantOverride;
            } else if (presetCore.emissiveConstantOverride > 0 ) {
                part.emissiveConstant = presetCore.emissiveConstantOverride;
            } else {
                part.emissiveConstant = part.partInfo.partPrefab.emissiveConstant;
            }
            // absorptiveConstant
            if (presetSkin.absorptiveConstant > 0) {
                part.absorptiveConstant = presetSkin.absorptiveConstant;
            } else if (presetCore.absorptiveConstant > 0 ) {
                part.absorptiveConstant = presetCore.absorptiveConstant;
            } else {
                part.absorptiveConstant = absorptiveConstantOrig;
            }

            //prevent DRE from ruining everything
            if (DREHandler.Found && HighLogic.LoadedSceneIsFlight)
                DREHandler.SetOperationalTemps(part, part.maxTemp, part.skinMaxTemp);
        }

        public void UpdateGeometricProperties()
        {
            if (HighLogic.LoadedSceneIsEditor) 
            {
                part.radiativeArea = surfaceArea;
            }
            tpsMass = TPSMass;
            tpsCost = TPSCost;
            if (tpsMassIsAdditive) 
            {
                float tpsMassRef = TPSMassRef;
                if (moduleMass != tpsMassRef)
                {
                    moduleMass = tpsMassRef;

                    // lets us update Engineer's Report without triggering re-voxelizaion
                    Debug.Log($"[ROThermal] OnGUIStageSequenceModified Fired");
                    GameEvents.StageManager.OnGUIStageSequenceModified.Fire();
                    part.UpdateMass();
                }    
            } 
            else 
            {
                moduleMass = 0.0f;
            }
            
            if (modAblator != null && modAblator.enabled)
            {
                if (ablatorResourceName != null)
                {
                    var ab = EnsureAblatorResource(ablatorResourceName);
                    double ratio = ab.maxAmount > 0 ? ab.amount / ab.maxAmount : 1.0;
                    ab.maxAmount = Ablator;
                    ab.amount = Math.Min(ratio * ab.maxAmount, ab.maxAmount);
                }

                if (outputResourceName != null)
                {
                    var ca = EnsureAblatorResource(outputResourceName);
                    ca.maxAmount = Ablator;
                    ca.amount = 0;
                }
            }

            UpdateGUI();

            // ModuleAblator's Start runs before this PM overrides the ablator values and will precalculate some parameters.
            // Run this precalculation again after we've finished configuring everything.
            if (HighLogic.LoadedSceneIsFlight)
                modAblator?.Start();
        }
        public void LoadThermalPropertiesArrayCore() 
        {
            thermalPropertyRowsCore = presetCore.thermalPropMin.Length;
            int columns = presetCore.thermalPropMin[0].Length;

            try
            {
                thermalPropertiesCore = new double[thermalPropertyRowsCore][];
                for (int r = 0; r < thermalPropertyRowsCore; r++) 
                {
                    thermalPropertiesCore[r] = new double [columns];
                    thermalPropertiesCore[r][0] = presetCore.thermalPropMin[r][0];
                    thermalPropertiesCore[r][1] = presetCore.thermalPropMin[r][1] * coreThermalMassFraction / PhysicsGlobals.StandardSpecificHeatCapacity;
                    //thermalPropertiesCore[r][2] = presetCore.thermalPropMin[r][2]
                    //thermalPropertiesCore[r][3] = presetCore.thermalPropMin[r][3];
                }
                Debug.Log($"[ROThermal] LoadThermalPropertiesArray() Array[{thermalPropertyRowsCore}, {columns}] {presetCore} for part {part.name}");   
            }
            catch(IndexOutOfRangeException e)
            {
                Debug.LogError("[ROThermal] IndexOutOfRangeException for: thermalPropertiesCore " + presetCore.name + "# rows: " + thermalPropertyRowsSkin + " #columns: " + columns);
                throw new IndexOutOfRangeException("",e);
            }
        }
        public void LoadThermalPropertiesArraySkin() 
        {
            double skinInternalConductivityDivChache = SkinInternalConductivityDivGlobal;
            thermalPropertyRowsSkin = presetSkin.thermalPropMin.Length;
            int columns = presetSkin.thermalPropMin[0].Length;

            double heightfactor = 0;
            if (presetSkin.skinHeightMax != presetSkin.skinHeightMin)
            {
                heightfactor = (tpsHeightDisplay - presetSkin.skinHeightMin) / (presetSkin.skinHeightMax - presetSkin.skinHeightMin);
            }

            try
            {
                thermalPropertiesSkin = new double [thermalPropertyRowsSkin][];
                for (int r = 0; r < thermalPropertyRowsSkin; r++) 
                {
                    thermalPropertiesSkin[r] = new double[columns];
                    thermalPropertiesSkin[r][0] = presetSkin.thermalPropMin[r][0];
                    thermalPropertiesSkin[r][1] = ((presetSkin.thermalPropMax[r][1] - presetSkin.thermalPropMin[r][1]) * heightfactor + presetSkin.thermalPropMin[r][1])
                                                    * SkinThermalMassModifierDiv;
                    thermalPropertiesSkin[r][2] = ((presetSkin.thermalPropMax[r][2] - presetSkin.thermalPropMin[r][2]) * heightfactor + presetSkin.thermalPropMin[r][2])
                                                    * skinInternalConductivityDivChache;
                    thermalPropertiesSkin[r][3] = presetSkin.thermalPropMin[r][3];
                    //presetTPS.array[i, 2] *= SkinInternalConductivityDivGlobal; // *=1/6
                }
                Debug.Log($"[ROThermal] LoadThermalPropertiesArray() Array[{thermalPropertyRowsSkin}, {columns}] {PresetTPS} for part {part.name}");
            }
            catch(IndexOutOfRangeException e)
            {
                Debug.LogError("[ROThermal] IndexOutOfRangeException for: thermalPropertiesSkin " + presetSkin.name + "# rows: " + thermalPropertyRowsSkin + " #columns: " + columns);
                throw new IndexOutOfRangeException("",e);
            }
        }

        public void CCTagUpdate(PresetROMatarial preset ) 
        {
            if (!(CCTagListModule is ModuleTagList))
                return;
            
            if(preset.name == "None")
            {
                if (reentryByDefault)
                    CCTagListModule.tags.AddUnique(reentryTag);
                else if (!reentryByDefault && CCTagListModule.tags.Contains(reentryTag))
                    CCTagListModule.tags.Remove(reentryTag);

                CCTagListModule.tags.Sort();
            }
            else if(preset.reentryTag == reentryTag &&  !CCTagListModule.tags.Contains(reentryTag))
            {
                CCTagListModule.tags.Add(preset.reentryTag);
                CCTagListModule.tags.Sort();

            }
            else if(preset.reentryTag != reentryTag &&  CCTagListModule.tags.Contains(reentryTag))
            {
                CCTagListModule.tags.Remove(preset.reentryTag);
                CCTagListModule.tags.Sort();

            }

        }

        public void UpdateCoreForRealfuels(bool applyPreset)
        {
            List<string> availableMaterialsNamesForFuelTank = new List<string>();
            string logStr = "";
            foreach (string name in availablePresetNamesCore) {
                if (PresetROMatarial.PresetsCore.TryGetValue(name, out PresetROMatarial preset)) {
                    if (preset.restrictors.Contains(moduleFuelTanks.type)){
                    availableMaterialsNamesForFuelTank.Add(name);
                    logStr += name + ", ";
                    }
                }
                else {
                    Debug.LogWarning($"[ROThermal] preset \"{name}\" in corePresets not found");
                }
                
            }
            if (availableMaterialsNamesForFuelTank.Any()) 
            {
                string[] strList = availableMaterialsNamesForFuelTank.ToArray();
                UpdatePresetsList(strList, PresetType.Core);
                if (!availableMaterialsNamesForFuelTank.Contains(presetCoreName))
                {
                    if (availableMaterialsNamesForFuelTank.Contains(coreCfg))
                        presetCoreName = coreCfg;
                    else
                        presetCoreName = availableMaterialsNamesForFuelTank[0];
                    if (applyPreset)
                        ApplyCorePreset(presetCoreName);
                }
                Debug.Log($"[ROThermal] UpdateFuelTankCore() " + moduleFuelTanks.type + " found in " + logStr 
                            + "\n\r presetCoreName set as " + availableMaterialsNamesForFuelTank[0]);
            } else {
                Debug.Log("[ROThermal] No fitting PresetROMatarial for " + moduleFuelTanks.type + " found in " + part.name);   
            }
        }

        public string[] GetUnlockedPresets(string[] all)
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
            {
                Debug.Log($"[ROThermal] All presets unlocked");
                return all;
            }

            var unlocked = new List<string>();
            foreach (string s in all)
            {
                if (IsConfigUnlocked(s))
                {
                    unlocked.AddUnique(s);
                }
            }
            Debug.Log($"[ROThermal] presets {unlocked} are unlocked");

            return unlocked.ToArray();
        }

        public bool IsConfigUnlocked(string configName)
        {
            if (!PartUpgradeManager.Handler.CanHaveUpgrades()) return true;

            PartUpgradeHandler.Upgrade upgd = PartUpgradeManager.Handler.GetUpgrade(configName);
            if (upgd == null) return true;

            if (PartUpgradeManager.Handler.IsEnabled(configName)) return true;

            if (upgd.entryCost < 1.1 && PartUpgradeManager.Handler.IsAvailableToUnlock(configName) &&
                PurchaseConfig(upgd))
            {
                return true;
            }

            return false;
        }

        public bool PurchaseConfig(PartUpgradeHandler.Upgrade upgd)
        {
            if (Funding.CanAfford(upgd.entryCost))
            {
                PartUpgradeManager.Handler.SetUnlocked(upgd.name, true);
                GameEvents.OnPartUpgradePurchased.Fire(upgd);
                return true;
            }

            return false;
        }

        private void UpdatePresetsList(string[] presetNames, PresetType type)
        {
            BaseField bf;

            if (type == PresetType.Core){
                bf = Fields[nameof(presetCoreName)];
                if (!Fields[nameof(presetCoreName)].guiActiveEditor) {
                    return;
                }               
            } else {
                bf = Fields[nameof(presetSkinName)];
            }

            var dispValues = RP1Found && HighLogic.LoadedScene != GameScenes.LOADING ?
                presetNames.Select(p => ConstructColoredPresetTitle(p)).ToArray() : presetNames;
            if (presetNames.Length == 0)
            {
                presetNames = dispValues = new string[] { "NONE" };
            }
            var uiControlEditor = bf.uiControlEditor as UI_ChooseOption;
            uiControlEditor.options = presetNames;
            uiControlEditor.display = dispValues;  
        }

        private string ConstructColoredPresetTitle(string presetName)
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING)
                return presetName;

            string partTech = part.partInfo.TechRequired;
            if (string.IsNullOrEmpty(partTech) || ResearchAndDevelopment.GetTechnologyState(partTech) != RDTech.State.Available)
                return $"<color=orange>{presetName}</color>";

            PartUpgradeHandler.Upgrade upgrade = PartUpgradeManager.Handler.GetUpgrade(presetName);
            bool isTechAvailable = upgrade == null || ResearchAndDevelopment.GetTechnologyState(upgrade.techRequired) == RDTech.State.Available;
            return isTechAvailable ? presetName : $"<color=orange>{presetName}</color>";
        }

        private void EnsureBestAvailableConfigSelected()
        {
            if (IsConfigUnlocked(presetCoreName)) return;

            string bestConfigMatch = null;
            for (int i = availablePresetNamesCore.IndexOf(presetCoreName) - 1; i >= 0; i--)
            {
                bestConfigMatch = availablePresetNamesCore[i];
                if (IsConfigUnlocked(bestConfigMatch)) break;
            }

            if (bestConfigMatch != null)
            {
                PresetCore = bestConfigMatch;
                ApplyCorePreset(presetCoreName);
            }
        }

        public void UpdatePAW()
        {
            if (UIPartActionController.Instance?.GetItem(part) is UIPartActionWindow paw)
                paw.displayDirty = true;
        }

        private void RemoveAblatorResources()
        {
            if (ablatorResourceName != null)
            {
                part.Resources.Remove(ablatorResourceName);
            }

            if (outputResourceName != null)
            {
                part.Resources.Remove(outputResourceName);
            }
        }

        private PartResource EnsureAblatorResource(string name)
        {
            PartResource res = part.Resources[name];
            if (res == null)
            {
                PartResourceDefinition resDef = PartResourceLibrary.Instance.GetDefinition(name);
                if (resDef == null)
                {
                    Debug.LogError($"[ROThermal] Resource {name} not found!");
                    return null;
                }

                res = new PartResource(part);
                res.resourceName = name;
                res.SetInfo(resDef);
                res._flowState = true;
                res.isTweakable = resDef.isTweakable;
                res.isVisible = resDef.isVisible;
                res.hideFlow = false;
                res._flowMode = PartResource.FlowMode.Both;
                part.Resources.dict.Add(resDef.id, res);
            }

            return res;
        }

        void ensurePresetIsInList (ref string [] list, string preset) {
            if(!list.Contains(preset))
            {
                int i = list.Length;
                Array.Resize(ref list, i + 1);
                list[i] = preset;
            }
        }
        #endregion Custom Methods

        #region Game Events
        public void OnPartActionUIShown (UIPartActionWindow window, Part p) 
        {
            if (PhysicsGlobals.ThermalDataDisplay && HighLogic.LoadedSceneIsFlight) 
            {
                foreach (BaseField field in Fields)
                {
                    if (field.group.name == GroupNameDebug) 
                    {
                        field.guiActive = true;
                    }
                }
                UpdateFlightDebug();
            } 
            else
            {
                foreach (BaseField field in Fields)
                {
                    if (field.group.name == GroupNameDebug) 
                    {
                        field.guiActive = false;
                    }
                }
            }
            pawOpen = true;
        }

        public void OnPartActionUIDismiss(Part p)
        {
            pawOpen = false;
        }

        public void OnPartResourceListChange(Part dPart)
        {
            Debug.Log($"[ROThermal] onPartResourceListChange Part {part} Message caught.");
            UpdateGUI();
        }

        #endregion Game Events

        /// <summary>
        /// Message sent from ProceduralPart, ProceduralWing or ModuleROTanks when it updates.
        /// </summary>
        [KSPEvent]
        public void OnPartVolumeChanged(BaseEventDetails data)
        {
            Debug.Log($"[ROThermal] OnPartVolumeChanged Message caught");
            if (!HighLogic.LoadedSceneIsEditor) return;
            UpdateGeometricProperties();
        }

        /// <summary>
        /// Message sent from FAR via Harmony Patch.
        /// </summary>
        [KSPEvent]
        public void OnVoxelizationComplete(BaseEventDetails data)
        {
            Debug.Log($"[ROThermal] VoxelizationComplete Message caught");
            if (UpdateSurfaceArea())
                UpdateGeometricProperties();
        }
        

        /// <summary>
        /// Called from RP0KCT
        /// </summary>
        /// <param name="validationError"></param>
        /// <param name="canBeResolved"></param>
        /// <param name="costToResolve"></param>
        /// <returns></returns>
        public virtual bool Validate(out string validationError, out bool canBeResolved, out float costToResolve, out string techToResolve)
        {
            validationError = null;
            canBeResolved = false;
            costToResolve = 0;
            techToResolve = null;

            if (IsConfigUnlocked(presetCoreName)) return true;

            PartUpgradeHandler.Upgrade upgd = PartUpgradeManager.Handler.GetUpgrade(presetCoreName);
            if (upgd != null)
                techToResolve = upgd.techRequired;
            if (PartUpgradeManager.Handler.IsAvailableToUnlock(presetCoreName))
            {
                canBeResolved = true;
                costToResolve = upgd.entryCost;
                validationError = $"purchase config {upgd.title}";
            }
            else
            {
                validationError = $"unlock tech {ResearchAndDevelopment.GetTechnologyTitle(upgd.techRequired)}";
            }

            return false;
        }

        /// <summary>
        /// Called from RP0KCT
        /// </summary>
        /// <returns></returns>
        public virtual bool ResolveValidationError()
        {
            PartUpgradeHandler.Upgrade upgd = PartUpgradeManager.Handler.GetUpgrade(presetCoreName);
            if (upgd == null) return false;

            return PurchaseConfig(upgd);
        }

        public void UpdateGUI() 
        {
            float resourceMass = part.GetResourceMass(out double resourceThermalMass);

            double mult = PhysicsGlobals.StandardSpecificHeatCapacity * part.thermalMassModifier;
            double massSkin = part.skinMassPerArea * surfaceArea * 0.001;
            double skinThermalMass = (float)Math.Max(0.1, massSkin * part.skinThermalMassModifier * mult);
            double coreThermalMass =  Math.Max((double)part.mass - massSkin, 0.001) * mult + resourceThermalMass;
            //Debug.Log($"[ROThermal] UpdateGUI() skinThermalMass = " + skinThermalMass + "= 0.001 * part.skinMassPerArea: " + part.skinMassPerArea + " * part.skinThermalMassModifier: " + part.skinThermalMassModifier + " * surfaceArea: " + surfaceArea + " * mult: (" + PhysicsGlobals.StandardSpecificHeatCapacity + " * " + part.thermalMassModifier + ")");

            maxTempDisplay = "Skin: " + String.Format("{0:0.}", part.skinMaxTemp) + "K / Core: " + String.Format("{0:0.}", part.maxTemp) ;
            thermalMassDisplay = "Skin: " + FormatThermalMass(skinThermalMass) + " / Core: " + FormatThermalMass(coreThermalMass);
            //Debug.Log($"[ROThermal] UpdateGUI() thermalInsulance: skinIntTransferCoefficient " + skinIntTransferCoefficient + " presetCore.skinIntTransferCoefficient " + presetCore.skinIntTransferCoefficient );
            thermalInsulanceDisplay = KSPUtil.PrintSI(1.0/skinIntTransferCoefficient, "m²*K/kW", 4);
            emissiveConstantDisplay = part.emissiveConstant.ToString("F2");
            massDisplay = "Skin " + FormatMass((float)tpsMass) + " Total: " + FormatMass(part.mass + resourceMass);
            surfaceDensityDisplay = (float)tpsSurfaceDensity;

            FlightDisplay = "" + part.skinMaxTemp + "/" + part.maxTemp + "\nSkin: " + PresetTPS  + " " + tpsHeightDisplay + "mm\nCore: " + PresetCore ;
            //if (HighLogic.LoadedSceneIsEditor)
                //DebugLog();
            
            UpdatePAW();
        }
        public static string FormatMass(float mass) => mass < 1.0f ? KSPUtil.PrintSI(mass * 1e6, "g", 4) : KSPUtil.PrintSI(mass, "t", 4);
        public static string FormatThermalMass(float thermalmass) => KSPUtil.PrintSI(thermalmass * 1e3, "J/K", 4);
        public static string FormatThermalMass(double thermalmass) => KSPUtil.PrintSI(thermalmass * 1e3, "J/K", 4);
        public void UpdateFlightDebug()
        {
                emissiveConstantText = part.emissiveConstant.ToString("F3");
                skinHeatCapText = (part.skinThermalMassModifier * SkinThermalMassModifierMult).ToString("F1") + " J*kg/K";
                skinInternalConductionMultText = part.skinInternalConductionMult.ToString("F6");
                heatConductivityText = part.heatConductivity.ToString("F6");
        }
        public void DebugLog()
        {
            //part.DragCubes.RequestOcclusionUpdate();
            //part.DragCubes.SetPartOcclusion();
            double skinThermalMassModifier;
            if (presetSkin.skinHeightMax > 0.0 && presetSkin.skinSpecificHeatCapacityMax > 0.0 && presetSkin.skinMassPerAreaMax > 0.0) 
            {
                
                double heightfactor = 0;
                if (presetSkin.skinHeightMax != presetSkin.skinHeightMin)
                {
                    heightfactor = (tpsHeightDisplay - presetSkin.skinHeightMin) / (presetSkin.skinHeightMax - presetSkin.skinHeightMin);
                }
                skinThermalMassModifier = (presetSkin.skinSpecificHeatCapacityMax - presetSkin.skinSpecificHeatCapacity) * heightfactor + presetSkin.skinSpecificHeatCapacity
                                                    * SkinThermalMassModifierDiv;
            } else 
            {
               skinThermalMassModifier = presetSkin.skinSpecificHeatCapacity > 0.0 ? presetSkin.skinSpecificHeatCapacity : presetCore.skinSpecificHeatCapacity;
            }
            skinThermalMassModifier *= SkinThermalMassModifierDiv;
            part.GetResourceMass(out float resourceThermalMass);
            double mult = SkinThermalMassModifierMult;
            float thermalMass = part.mass * (float)mult + resourceThermalMass;
            float skinThermalMass = (float)Math.Max(0.1, Math.Min(0.001 * part.skinMassPerArea * part.skinThermalMassModifier * surfaceArea * mult, (double)part.mass * mult * 0.5));
            thermalMass = Mathf.Max(thermalMass - skinThermalMass, 0.1f);

            Debug.Log($"[ROThermal] DebugLog (" + HighLogic.LoadedScene + ") Values for " + part.name + "\n"
                    + "Core Preset: " + presetCore.name + ", Skin Preset: " +  presetSkin.name + ": " + tpsHeightDisplay + " mm\n"
                    + "TempMax: Skin: " + part.skinMaxTemp + "K / Core: " + part.maxTemp + "K\n"
                    + "ThermalMassMod Part Skin: " + part.skinThermalMassModifier + ", Core: "  + part.thermalMassModifier + "\n"
                    + "             Module Skin: " + skinThermalMassModifier + ", Core: "  + presetCore.specificHeatCapacity + "\n"
                    + "skinMassPerArea Part" + part.skinMassPerArea + ", Module " + tpsSurfaceDensity + "\n"
                    + "ConductionMult Part: Internal " + part.skinInternalConductionMult + ", SkintoSkin " + part.skinSkinConductionMult + ", Conductivity " + part.heatConductivity + "\n"
                    + "             Module: Internal " + skinIntTransferCoefficient * SkinInternalConductivityDivGlobal + ", Skin to Skin " 
                                        + presetSkin.skinSkinConductivity + ", Conductivity " + presetCore.thermalConductivity + "\n"
                    + "emissiveConstant part " + part.emissiveConstant + ",    preset" + presetSkin.emissiveConstantOverride + "\n"
                    + "ThermalMass Part: Skin: " + FormatThermalMass((float)part.skinThermalMass) + " / Core: " + FormatThermalMass((float)part.thermalMass) + "\n"
                    + "          Module: Skin: " + FormatThermalMass(skinThermalMass) + " / Core: " + FormatThermalMass(thermalMass) + "\n"
                    + "ModuleMass (Skin) " + FormatMass(moduleMass) + ",    Total Mass: " + FormatMass(part.mass) + "\n"
                    + "SurfaceArea: part.radiativeArea " + part.radiativeArea + ", get_SurfaceArea" + surfaceArea + "far" + fARAeroPartModule?.ProjectedAreas.totalArea + "\n"
                    //+ "SurfaceArea: part.exposedArea " + part.exposedArea + ", part.skinExposedArea "  + part.skinExposedArea + ", skinExposedAreaFrac " + part.skinExposedAreaFrac + "\n"
                    + "part.DragCubes->  PostOcclusionArea " + part.DragCubes.PostOcclusionArea  + ", cubeData.exposedArea "+ part.DragCubes.ExposedArea + ", Area "+ part.DragCubes.Area + "\n"
            );
        }  
    }
}
