using ferram4;
using FerramAerospaceResearch.FARAeroComponents;
using ProceduralParts;
using RealFuels.Tanks;
using WingProcedural;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ROLib
{
    public class ModuleROMaterials : PartModule, IPartMassModifier, IPartCostModifier
    {
        private const string GroupDisplayName = "RO-Thermal_Protection";
        private const string GroupName = "ModuleROTPS";

        #region KSPFields

        [KSPField(isPersistant = true, guiName = "Core:", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string presetCoreName = "";
        [KSPField(isPersistant = true, guiName = "TPS:", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string presetSkinName = "";
        [KSPField(isPersistant = true, guiName = "TPS height (mm)", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName), 
        UI_FloatEdit(sigFigs = 2, suppressEditorShipModified = true)]
        public float tpsHeightDisplay = 1.0f;
        [KSPField(guiActiveEditor = true, guiName = "Desc", groupName = GroupName, groupDisplayName = GroupDisplayName)]
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
        


        #endregion KSPFields

        #region Private Variables

        private ModuleAblator modAblator;
        private ModuleROTank modularPart;
        private ProceduralPart proceduralPart;
        private ModuleFuelTanks moduleFuelTanks;
        private FARAeroPartModule fARAeroPartModule; 
        private FARWingAerodynamicModel fARWingModule;
        private WingProcedural.WingProcedural wingProceduralModule;
        private PresetROMatarial presetCore;
        private PresetROMatarial presetTPS;
        
        private float tpsCost = 0.0f;
        private float tpsMass = 0.0f;
        private double tpsSurfaceDensity = 0.0f; 
        private double skinIntTransferCoefficient = 0.0;
        private float moduleMass = 0.0f;
        private string ablatorResourceName;
        private string outputResourceName;
        private bool onLoadFiredInEditor;
        private double tick = 470.0;
        private float prevHeight = -10.001f;
        private double heatConductivityMult = 1f / (10.0 * PhysicsGlobals.ConductionFactor );
        private double SkinInternalConductivityMult = 1f / (10.0 * PhysicsGlobals.ConductionFactor * PhysicsGlobals.SkinInternalConductionFactor * 0.5);
        private double SkinSkinConductivityMult = 1f / (10.0 * PhysicsGlobals.ConductionFactor * PhysicsGlobals.SkinSkinConductionFactor);
        private double absorptiveConstantOrig;
        
        [SerializeField] private string[] availablePresetNamesCore = new string[] { "default" };
        [SerializeField] private string[] availablePresetNamesSkin = new string[] { "None" };
        private string PresetCore {
            get{ return presetCore.name; }
            set{ 
                if (PresetROMatarial.PresetsCore.TryGetValue(value, out PresetROMatarial preset)) {
                    presetCoreName = value;
                    presetCore = preset;
                }
                else
                    Debug.LogWarning("[ROThermal] " + part.name + " Preset Core: " + presetCoreName + " not in the List"); 
                }
        }
        private string PresetTPS {
            get{ return presetTPS.name; }
            set{ 
                if (PresetROMatarial.PresetsSkin.TryGetValue(value, out PresetROMatarial preset)) {
                    presetSkinName = value;
                    presetTPS = preset;
                }  
                else
                    Debug.LogWarning("[ROThermal] " + part.name + " Preset Skin: " + presetSkinName + " not in the List"); 
                }
        }
        
        // TODO need detailed implementation
        //
        // HeatConductivity seems to be a replacement value for thermal contact resistance in inverse
        // which then get multltiplied together
        // since heat transfer calculations usualy add them together & don't multiply them, like the game does 
        // flux: Q = U * A * ΔT     U [kW/(m²·K)]: overall heat transfer coefficient 
        //                          U = 1 /( l1/k1 + l2/k2 + 1/hc) bc. temperatures inside parts are uniform l1&2 get infinitely small
        //                          U = 1 / hc      in ksp U -> part.heatConductivity * part2.heatConductivity * global mult
        // partThermalData.localIntConduction[kW] += thermalLink.contactArea[m^2] * thermalLink.temperatureDelta[K]
        //                                       * thermalLink.remotePart.heatConductivity[Sqrt(kW/(m²·K))] * part.heatConductivity[Sqrt(kW/(m²·K))]
        //                                       * PhysicsGlobals.ConductionFactor * 10.0
        private double HeatConductivity {
            get {
                return part.partInfo.partPrefab.heatConductivity; // not corectly implemented yet
                if (presetTPS.thermalConductivity > 0) {
                    return presetTPS.thermalConductivity * heatConductivityMult;
                } else if (presetCore.thermalConductivity > 0 ) {
                    return presetCore.thermalConductivity * heatConductivityMult;
                } else {
                    return part.partInfo.partPrefab.heatConductivity;
                };
            }
        }

        // TODO need detailed implementation
        private double SkinSkinConductivity {
            get {
                if (presetTPS.skinSkinConductivity > 0) {
                    return presetTPS.skinSkinConductivity / part.heatConductivity * SkinSkinConductivityMult;
                } else if (presetCore.skinSkinConductivity > 0 ) {
                    return presetCore.skinSkinConductivity / part.heatConductivity * SkinSkinConductivityMult;
                } else {
                    return part.partInfo.partPrefab.skinSkinConductionMult;
                }
            }
        }

        #endregion Private Variables
        [KSPField] public float surfaceAreaPart = -0.1f;
        [KSPField] public float volumePart = -0.1f;
        [KSPField] public bool tpsMassIsAdditive = true;
        [KSPField] public float surfaceArea = 0.0f; // m2
        public float TPSAreaCost => presetTPS?.costPerArea ?? 1.0f;
        public float TPSAreaMult => presetTPS?.heatShieldAreaMult ?? 1.0f;

        public float CurrentDiameter => modularPart?.currentDiameter ?? 0f;
        public float LargestDiameter => modularPart?.largestDiameter ?? 0f;
        public float TotalTankLength => modularPart?.totalTankLength ?? 0f;
        public float SurfaceArea 
        {
            get
            {   
                float radArea = 0.0f;
                if (surfaceAreaPart > 0.0f) 
                {
                    Debug.Log("[ROThermal] get_SurfaceArea derived from SurfaceAreaPart Entry: " + surfaceAreaPart);
                    radArea =  surfaceAreaPart;
                }
                else if (wingProceduralModule is WingProcedural.WingProcedural & fARWingModule != null){
                    // TODO preciser calculation needed
                    Debug.Log("[ROThermal] get_SurfaceArea deriving from b9wingProceduralModule: ");
                    surfaceArea = (float)fARWingModule.S * 2 + (wingProceduralModule.sharedBaseThicknessRoot + wingProceduralModule.sharedBaseThicknessTip)
                            * Mathf.Atan((wingProceduralModule.sharedBaseWidthRoot + wingProceduralModule.sharedBaseWidthTip) / (float)fARWingModule.b_2_actual);
                            // aproximation for leading & trailing Edge
                    Debug.Log("[ROThermal] get_SurfaceArea derived from ModuleWingProcedural: " + surfaceArea);
                    radArea =  surfaceArea;
                }
                else if (modularPart is ModuleROTank)
                {
                    surfaceArea =  Mathf.PI / 2f * ((CurrentDiameter + LargestDiameter) * TotalTankLength + (CurrentDiameter * CurrentDiameter + LargestDiameter * LargestDiameter) / 2f);
                    Debug.Log("[ROThermal] get_SurfaceArea derived from ModuleROTank: " + surfaceArea);
                    radArea =  surfaceArea;
                }
                else if (proceduralPart is ProceduralPart)
                {
                    Debug.Log("[ROThermal] get_SurfaceArea deriving from ProceduralPart: ");
                    surfaceArea =  proceduralPart.SurfaceArea;
                    Debug.Log("[ROThermal] get_SurfaceArea derived from ProceduralPart: " + surfaceArea);
                    radArea =  surfaceArea;
                }
                else if (fARAeroPartModule != null) {
                    // Inconsistant results in Editor & Flight, returned results for a cylinder are much closer to a cube
                    // Procedural Tank
                    // 3x3 cylinder 42.4m^2 -> surfaceArea = 52.5300 (In Editor) 77.36965 (In Flight)
                    // 3x3x3 cube   54.0m^2 -> surfaceArea = 56.3686 (In Editor) 71.05373 (In Flight)
                    surfaceArea = (float)(fARAeroPartModule?.ProjectedAreas.totalArea ?? 0.0f);
                    if (surfaceArea > 0.0) {
                        Debug.Log("[ROThermal] get_SurfaceArea derived from fARAeroPartModule: " + surfaceArea);
                        radArea =  surfaceArea;
                    } else {
                        if (fARAeroPartModule?.ProjectedAreas == null)
                            Debug.Log("[ROThermal] get_SurfaceArea skipping fARAeroPartModule ProjectedAreas = null ");
                        else if (fARAeroPartModule?.ProjectedAreas.totalArea == null)
                            Debug.Log("[ROThermal] get_SurfaceArea skipping fARAeroPartModule totalArea = null ");
                        else
                            Debug.Log("[ROThermal] get_SurfaceArea skipping fARAeroPartModule got " + surfaceArea);
                    }
                }
                if (radArea > 0.0) {
                    //part.DragCubes.SetPartOcclusion();
                    part.DragCubes.ForceUpdate(false, true, false);
                    Debug.Log($"[ROThermal] part.DragCubes.SetPartOcclusion() ");
                    string str = $"[ROThermal] get_SurfaceArea Surface Area: " + radArea + " coverd skin: attachNodes ";
                    foreach (AttachNode nodeAttach in part.attachNodes) {
                        if (nodeAttach.attachedPart == null)
                            continue;
                        nodeAttach.attachedPart.DragCubes.ForceUpdate(false, true, false);
                        radArea -= nodeAttach.contactArea;
                        str += nodeAttach.contactArea + ", ";
                    }
                    Debug.Log($"[ROThermal] part.attachNodes ");
                    part.srfAttachNode.attachedPart?.DragCubes.SetPartOcclusion();
                    Debug.Log($"[ROThermal] part.srfAttachNode?.attachedPart.DragCubes.SetPartOcclusion() ");
                    str +=  "srfAttachNode " + part.srfAttachNode.contactArea + ", ";
                    radArea -= part.srfAttachNode?.contactArea ?? 0.0f;
                    str +=  "children ";
                    Debug.Log($"[ROThermal] part.srfAttachNode.contactArea ");
                    foreach (Part child in part.children) {
                        if (child == null)
                            continue;
                        child.DragCubes.RequestOcclusionUpdate();
                        child.DragCubes.ForceUpdate(false, true, false);
                        child.DragCubes.SetPartOcclusion();
                        str +=  child.srfAttachNode.contactArea + ", ";
                        radArea -= child.srfAttachNode.contactArea;
                    }
                    Debug.Log($"part.children ");
                    Debug.Log(str + "  Result: " +  radArea);
                    if (radArea > 0.0)
                        return radArea;
                }

                Debug.LogWarning("[ROThermal] get_SurfaceArea failed: ");
                return 0f;
            }
        }
        private float SkinHeightMaxVal 
        {
            get {
                if (presetTPS.skinHeightMax > 0.0f) {
                    return (float)presetTPS.skinHeightMax * 1000f;
                } else if (presetTPS.skinHeightMin > 0.0f) {
                    return (float)presetTPS.skinHeightMin * 1000f;
                }
                return 0;
            }
        }
        public float TPSMass => (float)(surfaceArea * tpsSurfaceDensity * TPSAreaMult / 1000f);
        public float TPSCost => surfaceArea * TPSAreaCost * TPSAreaMult;

        public float Ablator => Mathf.Round(surfaceArea * presetTPS.heatShieldAblator * 10f) / 10f;       
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

        #region Standard KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            onLoadFiredInEditor = HighLogic.LoadedSceneIsEditor;

            heatConductivityMult = 1f / (10.0 * PhysicsGlobals.ConductionFactor );
            SkinInternalConductivityMult = heatConductivityMult / ( PhysicsGlobals.SkinInternalConductionFactor * 0.5);
            SkinSkinConductivityMult = heatConductivityMult / (PhysicsGlobals.SkinSkinConductionFactor);

            Debug.Log("[ROThermal] " + part.name + " OnLoad() LoadedScene is " + HighLogic.LoadedScene);
            node.TryGetValue("TPSSurfaceArea", ref surfaceAreaPart);
            node.TryGetValue("Volume", ref volumePart);
            if (!node.TryGetValue("skinMassIsAdditive", ref tpsMassIsAdditive))
                Debug.LogWarning("[ROThermal] "+ part.name + " skinMassAdditive entry not found");
            Debug.Log("[ROThermal] " + part.name + " tpsMassAdditive " + tpsMassIsAdditive);
            
            if (node.TryGetValue("corePresets", ref availablePresetNamesCore))
                Debug.Log("[ROThermal] available presetsCore loaded");
            if (node.TryGetValue("skinPresets", ref availablePresetNamesSkin))
                Debug.Log("[ROThermal] available presetsSkin loaded");     

            node.TryGetValue("core", ref presetCoreName);
            node.TryGetValue("skin", ref presetSkinName);                      
        }
        public override void OnStart(StartState state)
        {
            Debug.Log("[ROThermal] " + part.name + " OnStart() LoadedScene is " + HighLogic.LoadedScene);
            PresetROMatarial.LoadPresets();
            if (!PresetROMatarial.Initialized)
                return;

            if (HighLogic.LoadedSceneIsEditor) {
                
                if (availablePresetNamesCore.Length > 0)
                {
                    // GameEvents.onEditorShipModified.Add(OnEditorShipModified);
                    // RP-1 allows selecting all configs but will run validation when trying to add the vessel to build queue
                    string[] unlockedPresetsName = RP1Found ? availablePresetNamesCore : GetUnlockedPresets(availablePresetNamesCore);

                    UpdatePresetsList(unlockedPresetsName, PresetType.Core);
                    if (presetCoreName == "" | presetCoreName == null)
                        presetCoreName = "default";

                    PresetCore = presetCoreName;
                    Fields[nameof(presetCoreName)].uiControlEditor.onFieldChanged = 
                    Fields[nameof(presetCoreName)].uiControlEditor.onSymmetryFieldChanged =
                        (bf, ob) => ApplyCorePreset(presetCoreName);

                    /*if (!onLoadFiredInEditor)
                    {
                        EnsureBestAvailableConfigSelected();
                    }*/
                }
                if (availablePresetNamesSkin.Length > 0)
                {
                    Debug.Log($"[ROThermal] availablePresetNamesSkin.Length > 0");
                    // GameEvents.onEditorShipModified.Add(OnEditorShipModified);
                    // RP-1 allows selecting all configs but will run validation when trying to add the vessel to build queue
                    string[] unlockedPresetsName = RP1Found ? availablePresetNamesSkin : GetUnlockedPresets(availablePresetNamesSkin);
                    UpdatePresetsList(unlockedPresetsName, PresetType.Skin);
                    if (presetSkinName == "" | presetSkinName == null)
                        presetSkinName = "None";
                    
                    PresetTPS = presetSkinName;
                    Fields[nameof(presetSkinName)].uiControlEditor.onFieldChanged =
                    Fields[nameof(presetSkinName)].uiControlEditor.onSymmetryFieldChanged =
                        (bf, ob) => ApplySkinPreset(presetSkinName);

                    Fields[nameof(tpsHeightDisplay)].uiControlEditor.onFieldChanged =
                    Fields[nameof(tpsHeightDisplay)].uiControlEditor.onSymmetryFieldChanged = OnHeightChanged;

                    this.ROLupdateUIFloatEditControl(nameof(tpsHeightDisplay), (float)presetTPS.skinHeightMin * 1000, SkinHeightMaxVal, 10f, 1f, 0.01f);
                }
            } else if (HighLogic.LoadedSceneIsFlight) {
                if (presetCoreName == "")
                    presetCoreName = "default";
                if (presetSkinName == "")
                    presetSkinName = "None";
                PresetCore = presetCoreName;
                PresetTPS = presetSkinName;
            }
        }     

        public override void OnStartFinished(StartState state)
        {
            Debug.Log($"[ROThermal] " + part.name + " OnStartFinished() LoadedScene is " + HighLogic.LoadedScene);
            base.OnStartFinished(state);

            absorptiveConstantOrig = part.absorptiveConstant;

            if (!PresetROMatarial.Initialized)
                return;

            modAblator = part.FindModuleImplementing<ModuleAblator>();
            modularPart = part?.FindModuleImplementing<ModuleROTank>();
            proceduralPart = part?.FindModuleImplementing<ProceduralPart>();
            moduleFuelTanks = part?.FindModuleImplementing<ModuleFuelTanks>();
            fARAeroPartModule = part?.FindModuleImplementing<FARAeroPartModule>();
            fARWingModule = part?.FindModuleImplementing<FARControllableSurface>();
            fARWingModule = part?.FindModuleImplementing<FARWingAerodynamicModel>();
            wingProceduralModule = part?.FindModuleImplementing<WingProcedural.WingProcedural>();

            if(HighLogic.LoadedSceneIsEditor) {
                if (modularPart is ModuleROTank)
                {
                    //TODO implement Mount Nose or core variant change update
                    modularPart.Fields[nameof(modularPart.currentDiameter)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateGeometricProperties();
                    modularPart.Fields[nameof(modularPart.currentLength)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateGeometricProperties();
                    modularPart.Fields[nameof(modularPart.currentVScale)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateGeometricProperties();
                    modularPart.Fields[nameof(modularPart.currentDiameter)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateGeometricProperties();
                    modularPart.Fields[nameof(modularPart.currentLength)].uiControlEditor.onSymmetryFieldChanged += (bf, ob) => UpdateGeometricProperties();
                    modularPart.Fields[nameof(modularPart.currentVScale)].uiControlEditor.onSymmetryFieldChanged += (bf, ob) => UpdateGeometricProperties();
                }
                if (fARWingModule != null) {
                    Debug.Log("[ROThermal] " + part.name + " fARModuleReference found " + fARWingModule.moduleName);
                    fARWingModule.Fields[nameof(fARWingModule.massMultiplier)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateGeometricProperties(); //Mass-Strength Multiplier
                    fARWingModule.Fields[nameof(fARWingModule.massMultiplier)].uiControlEditor.onSymmetryFieldChanged += (bf, ob) => UpdateGeometricProperties();
                    Debug.Log("[ROThermal] " + part.name + " fARModuleReference found 2 " + fARWingModule.moduleName);
                    fARWingModule.Fields[nameof(fARWingModule.curWingMass)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateGeometricProperties();
                    fARWingModule.Fields[nameof(fARWingModule.curWingMass)].uiControlEditor.onSymmetryFieldChanged += (bf, ob) => UpdateGeometricProperties();
                }
                if (moduleFuelTanks is ModuleFuelTanks)
                { 
                    moduleFuelTanks.Fields[nameof(moduleFuelTanks.typeDisp)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateCoreForRealfuels();
                    moduleFuelTanks.Fields[nameof(moduleFuelTanks.typeDisp)].uiControlEditor.onSymmetryFieldChanged += (bf, ob) => UpdateCoreForRealfuels();
                    Debug.Log("[ROThermal] " + part.name + " ModuleFuelTanks found " + moduleFuelTanks.name + " updating core material list");
                    UpdateCoreForRealfuels();
                } else {
                    ApplyCorePreset(presetCoreName);
                }
                ApplySkinPreset(presetSkinName);
            }
            

            if (HighLogic.LoadedSceneIsFlight) {
                ApplyCorePreset(presetCoreName);
                ApplySkinPreset(presetSkinName);
            }
        }

        // Remove after Debugging is less needed
        public override void OnUpdate() {
            if (HighLogic.LoadedSceneIsFlight) {
                if (tick % 500 == 0) {
                    DebugLog();
                }
                tick ++;
            }
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => moduleMass;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => tpsCost - defaultCost;

        #endregion Standard KSP Overrides


        #region Custom Methods


        /// <summary>
        /// Message sent from ProceduralPart or ProceduralWing when it updates.
        /// </summary>
        [KSPEvent]
        public void OnPartVolumeChanged(BaseEventDetails data)
        {
            Debug.Log("[ROThermal] OnPartVolumeChanged Message caught");
            if (!HighLogic.LoadedSceneIsEditor) return;
            UpdateGeometricProperties();
        }

        public void OnHeightChanged(BaseField bf, object ob) {
            Debug.Log($"[ROThermal] OnHeightChanged()  (float)bf.GetValue(this) " + (float)bf.GetValue(this) + " prevHeight " + prevHeight);
            if ((float)bf.GetValue(this) == prevHeight) return;

            UpdateHeight();
            ApplyThermal();
            UpdateGeometricProperties();
        }

        public void UpdateHeight() {
            if (SkinHeightMaxVal <= (float)presetTPS.skinHeightMin * 1000f)
            {
                Debug.LogWarning($"[ROThermal] Warning Preset "+ presetTPS.name + " skinHeightMax lower then skinHeightMin");
                tpsHeightDisplay = (float)presetTPS.skinHeightMin;
            }
            tpsHeightDisplay = Mathf.Max(tpsHeightDisplay, (float)presetTPS.skinHeightMin * 1000f);
            tpsHeightDisplay = Mathf.Min(tpsHeightDisplay, SkinHeightMaxVal );
            prevHeight = tpsHeightDisplay;
            this.ROLupdateUIFloatEditControl(nameof(tpsHeightDisplay), (float)presetTPS.skinHeightMin * 1000f, SkinHeightMaxVal, 10f, 1f, 0.1f);
        }

        public void ApplyCorePreset (string preset) {
            PresetCore = preset;

            // maxTemp
            if (presetCore.maxTempOverride > 0) {
                part.maxTemp = presetCore.maxTempOverride;
            } else {
                part.maxTemp = part.partInfo.partPrefab.maxTemp;
            }    
            // thermalMassModifier
            if (presetCore.specificHeatCapacity > 0) {
                part.thermalMassModifier = presetCore.specificHeatCapacity / PhysicsGlobals.StandardSpecificHeatCapacity;
            } else {
                part.thermalMassModifier = part.partInfo.partPrefab.thermalMassModifier;
            }

            ApplyThermal();

            Debug.Log($"[ROThermal] loaded preset {PresetCore} for part {part.name}");     
            UpdateGeometricProperties();
        }
        public void ApplySkinPreset (string preset) {
            PresetTPS = preset;
            UpdateHeight();
            ApplyThermal();

            // update ModuleAblator parameters, if present and used
            if (modAblator != null && !presetTPS.disableModAblator)
            {
                if (!string.IsNullOrWhiteSpace(presetTPS.AblativeResource))
                    modAblator.ablativeResource = presetTPS.AblativeResource;
                if (!string.IsNullOrWhiteSpace(presetTPS.OutputResource))
                    modAblator.outputResource = presetTPS.OutputResource;

                if (!string.IsNullOrWhiteSpace(presetTPS.NodeName))
                    modAblator.nodeName = presetTPS.NodeName;
                if (!string.IsNullOrWhiteSpace(presetTPS.CharModuleName))
                    modAblator.charModuleName = presetTPS.CharModuleName;
                if (!string.IsNullOrWhiteSpace(presetTPS.UnitsName))
                    modAblator.unitsName = presetTPS.UnitsName;

                if (presetTPS.LossExp.HasValue)
                    modAblator.lossExp = presetTPS.LossExp.Value;
                if (presetTPS.LossConst.HasValue)
                    modAblator.lossConst = presetTPS.LossConst.Value;
                if (presetTPS.PyrolysisLossFactor.HasValue)
                    modAblator.pyrolysisLossFactor = presetTPS.PyrolysisLossFactor.Value;
                if (presetTPS.AblationTempThresh.HasValue)
                    modAblator.ablationTempThresh = presetTPS.AblationTempThresh.Value;
                if (presetTPS.ReentryConductivity.HasValue)
                    modAblator.reentryConductivity = presetTPS.ReentryConductivity.Value;
                if (presetTPS.UseNode.HasValue)
                    modAblator.useNode = presetTPS.UseNode.Value;
                if (presetTPS.CharAlpha.HasValue)
                    modAblator.charAlpha = presetTPS.CharAlpha.Value;
                if (presetTPS.CharMax.HasValue)
                    modAblator.charMax = presetTPS.CharMax.Value;
                if (presetTPS.CharMin.HasValue)
                    modAblator.charMin = presetTPS.CharMin.Value;
                if (presetTPS.UseChar.HasValue)
                    modAblator.useChar = presetTPS.UseChar.Value;
                if (presetTPS.OutputMult.HasValue)
                    modAblator.outputMult = presetTPS.OutputMult.Value;
                if (presetTPS.InfoTemp.HasValue)
                    modAblator.infoTemp = presetTPS.InfoTemp.Value;
                if (presetTPS.Usekg.HasValue)
                    modAblator.usekg = presetTPS.Usekg.Value;
                if (presetTPS.NominalAmountRecip.HasValue)
                    modAblator.nominalAmountRecip = presetTPS.NominalAmountRecip.Value;
            }

            if (modAblator != null)
            {
                if (presetTPS.AblativeResource == null || ablatorResourceName !=presetTPS.AblativeResource ||
                    presetTPS.OutputResource == null || outputResourceName != presetTPS.OutputResource ||
                    presetTPS.disableModAblator)
                {
                    RemoveAblatorResources();
                }

                ablatorResourceName = presetTPS.AblativeResource;
                outputResourceName = presetTPS.OutputResource;

                modAblator.isEnabled = modAblator.enabled = !presetTPS.disableModAblator;
            }

            if (!string.IsNullOrEmpty(presetTPS.description))
            {
                Fields[nameof(description)].guiActiveEditor = true;
                description = presetTPS.description;
            }
            else
                Fields[nameof(description)].guiActiveEditor = false;

            Debug.Log($"[ROThermal] loaded preset {PresetTPS} for part {part.name}");     
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
            // heatConductivity
            part.heatConductivity = HeatConductivity;
            part.skinSkinConductionMult = SkinSkinConductivity;

            if (presetTPS.skinHeightMax > 0.0 & presetTPS.skinSpecificHeatCapacityMax > 0.0 & presetTPS.skinMassPerAreaMax > 0.0) 
            {
                double heightfactor = (tpsHeightDisplay / 1000 - presetTPS.skinHeightMin) / (presetTPS.skinHeightMax - presetTPS.skinHeightMin);

                tpsSurfaceDensity = (presetTPS.skinMassPerAreaMax - presetTPS.skinMassPerArea) * heightfactor + presetTPS.skinMassPerArea;
                part.skinMassPerArea = tpsSurfaceDensity;
                part.skinThermalMassModifier = ((presetTPS.skinSpecificHeatCapacityMax - presetTPS.skinSpecificHeatCapacity) * heightfactor + presetTPS.skinSpecificHeatCapacity)
                                                / PhysicsGlobals.StandardSpecificHeatCapacity / part.thermalMassModifier;
                skinIntTransferCoefficient = (presetTPS.skinIntTransferCoefficientMax - presetTPS.skinIntTransferCoefficient) * heightfactor + presetTPS.skinIntTransferCoefficient;
                part.skinInternalConductionMult = skinIntTransferCoefficient * SkinInternalConductivityMult / part.heatConductivity ;
            } 
            else 
            {
                // skinMassPerArea
                if (presetTPS.skinMassPerArea > 0.0) {
                    part.skinMassPerArea = presetTPS.skinMassPerArea;
                    tpsSurfaceDensity = (float)presetTPS.skinMassPerArea;
                } else if (presetCore.skinMassPerArea > 0.0 ) {
                    part.skinMassPerArea = presetCore.skinMassPerArea;
                    tpsSurfaceDensity = 0.0f;
                } else {
                    part.skinMassPerArea = part.partInfo.partPrefab.skinMassPerArea;
                    tpsSurfaceDensity = 0.0f;
                }
                // skinThermalMassModifier
                if (presetTPS.skinSpecificHeatCapacity > 0.0) {
                    part.skinThermalMassModifier = presetTPS.skinSpecificHeatCapacity / PhysicsGlobals.StandardSpecificHeatCapacity / part.thermalMassModifier;
                } else if (presetCore.skinSpecificHeatCapacity > 0.0) {
                    part.skinThermalMassModifier = presetCore.skinSpecificHeatCapacity / PhysicsGlobals.StandardSpecificHeatCapacity / part.thermalMassModifier;
                } else {
                    part.skinThermalMassModifier = 1.0f;
                }
                // skinIntTransferCoefficient
                if (presetTPS.skinIntTransferCoefficient > 0.0) {
                    skinIntTransferCoefficient = presetTPS.skinIntTransferCoefficient;
                    part.skinInternalConductionMult = skinIntTransferCoefficient * SkinInternalConductivityMult / part.heatConductivity;
                } else if (presetCore.skinIntTransferCoefficient > 0.0 ) {
                    skinIntTransferCoefficient = presetTPS.skinIntTransferCoefficient;
                    part.skinInternalConductionMult = skinIntTransferCoefficient * SkinInternalConductivityMult / part.heatConductivity;
                } else {
                    part.skinInternalConductionMult = part.partInfo.partPrefab.skinInternalConductionMult;
                    skinIntTransferCoefficient = part.partInfo.partPrefab.skinInternalConductionMult / SkinInternalConductivityMult * part.heatConductivity;
                }
            }


            // skinMaxTempOverride
            if (presetTPS.skinMaxTempOverride > 0) {
                part.skinMaxTemp = presetTPS.skinMaxTempOverride;
            } else if (presetCore.skinMaxTempOverride > 0 ) {
                part.skinMaxTemp = presetCore.skinMaxTempOverride;
            } else {
                part.skinMaxTemp = part.partInfo.partPrefab.skinMaxTemp;
            }
            // emissiveConstant
            if (presetTPS.emissiveConstantOverride > 0) {
                part.emissiveConstant = presetTPS.emissiveConstantOverride;
            } else if (presetCore.emissiveConstantOverride > 0 ) {
                part.emissiveConstant = presetCore.emissiveConstantOverride;
            } else {
                part.emissiveConstant = part.partInfo.partPrefab.emissiveConstant;
            }
            // absorptiveConstant
            if (presetTPS.absorptiveConstant > 0) {
                part.absorptiveConstant = presetTPS.absorptiveConstant;
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
            if (HighLogic.LoadedSceneIsEditor) {
                surfaceArea = SurfaceArea;
                part.radiativeArea = surfaceArea;
            }          
            tpsMass = TPSMass;
            tpsCost = TPSCost;
            if (tpsMassIsAdditive) {
                moduleMass = tpsMass;
            } else {
                moduleMass = 0.0f;
            }
            

            if ((modularPart != null || proceduralPart != null) && modAblator != null && modAblator.enabled)
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

            part.UpdateMass(); // mass = prefabMass + moduleMass;
            part.GetResourceMass(out float resourceThermalMass);

            double mult = PhysicsGlobals.StandardSpecificHeatCapacity * part.thermalMassModifier;
            part.thermalMass = part.mass * (float)mult + resourceThermalMass;
            part.skinThermalMass = (float)Math.Max(0.1, Math.Min(0.001 * part.skinMassPerArea * part.skinThermalMassModifier * surfaceArea * mult, (double)part.mass * mult * 0.5));
            part.thermalMass = Math.Max(part.thermalMass - part.skinThermalMass, 0.1);
            Debug.Log($"[ROThermal] UpdateGUI() 0.001 * part.skinMassPerArea: " + part.skinMassPerArea + " * part.skinThermalMassModifier: " + part.skinThermalMassModifier + " * surfaceArea: " + surfaceArea + " * mult: (" + PhysicsGlobals.StandardSpecificHeatCapacity + " * " + part.thermalMassModifier + ")");

            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch?.ship != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
           
            UpdateGUI();
            UpdatePAW();

            // ModuleAblator's Start runs before this PM overrides the ablator values and will precalculate some parameters.
            // Run this precalculation again after we've finished configuring everything.
            if (HighLogic.LoadedSceneIsFlight)
                modAblator?.Start();
        }

        public void UpdateGUI() {
            maxTempDisplay = "Skin: " + part.skinMaxTemp + "K / Core: " + part.maxTemp;
            thermalMassDisplay = "Skin: " + FormatThermalMass(part.skinThermalMass) + " / Core: " + FormatThermalMass(part.thermalMass);
            thermalInsulanceDisplay = KSPUtil.PrintSI(1/skinIntTransferCoefficient, "m²*K/kW", 1);
            emissiveConstantDisplay = part.emissiveConstant.ToString();
            massDisplay = "Skin " + FormatMass((float)tpsMass) + " Total: " + FormatMass(part.mass + part.GetResourceMass());
            surfaceDensityDisplay = (float)tpsSurfaceDensity;
            DebugLog();
        }

        public void DebugLog()
        {
            part.DragCubes.RequestOcclusionUpdate();
            part.DragCubes.SetPartOcclusion();
            double skinThermalMassModifier;
            if (presetTPS.skinHeightMax > 0.0 & presetTPS.skinSpecificHeatCapacityMax > 0.0 & presetTPS.skinMassPerAreaMax > 0.0) 
            {
            double heightfactor = (tpsHeightDisplay / 1000 - presetTPS.skinHeightMin) / (presetTPS.skinHeightMax - presetTPS.skinHeightMin);
            skinThermalMassModifier = (presetTPS.skinSpecificHeatCapacityMax - presetTPS.skinSpecificHeatCapacity) * heightfactor + presetTPS.skinSpecificHeatCapacity
                                                / PhysicsGlobals.StandardSpecificHeatCapacity / part.thermalMassModifier;
            } else {
               skinThermalMassModifier = presetTPS.skinSpecificHeatCapacity > 0.0 ? presetTPS.skinSpecificHeatCapacity : presetCore.skinSpecificHeatCapacity;
            }
            skinThermalMassModifier /= PhysicsGlobals.StandardSpecificHeatCapacity / part.thermalMassModifier;
            double moduleCalcArea = SurfaceArea;
            part.GetResourceMass(out float resourceThermalMass);
            double mult = PhysicsGlobals.StandardSpecificHeatCapacity * part.thermalMassModifier;
            float thermalMass = part.mass * (float)mult + resourceThermalMass;
            float skinThermalMass = (float)Math.Max(0.1, Math.Min(0.001 * part.skinMassPerArea * part.skinThermalMassModifier * surfaceArea * mult, (double)part.mass * mult * 0.5));
            thermalMass = Mathf.Max(thermalMass - skinThermalMass, 0.1f);

            Debug.Log($"[ROThermal] (" + HighLogic.LoadedScene + ") Values for " + part.name + "\n"
                    + "Core Preset: " + presetCore.name + ", Skin Preset: " +  presetTPS.name + ": " + tpsHeightDisplay + " mm\n"
                    + "TempMax: Skin: " + part.skinMaxTemp + "K / Core: " + part.maxTemp + "K\n"
                    + "ThermalMassMod Part Skin: " + part.skinThermalMassModifier + ", Core: "  + part.thermalMassModifier + "\n"
                    + "             Module Skin: " + skinThermalMassModifier + ", Core: "  + presetCore.specificHeatCapacity + "\n"
                    + "skinMassPerArea Part" + part.skinMassPerArea + ", Module " + tpsSurfaceDensity + "\n"
                    + "ConductionMult Part: Internal " + part.skinInternalConductionMult + ", SkintoSkin " + part.skinSkinConductionMult + ", Conductivity " + part.heatConductivity + "\n"
                    + "             Module: Internal " + skinIntTransferCoefficient * SkinInternalConductivityMult / part.heatConductivity + ", Skin to Skin " 
                                        + presetTPS.skinSkinConductivity + ", Conductivity " + presetCore.thermalConductivity + "\n"
                    + "emissiveConstant part " + part.emissiveConstant + ",    preset" + presetTPS.emissiveConstantOverride + "\n"
                    + "ThermalMass Part: Skin: " + FormatThermalMass((float)part.skinThermalMass) + " / Core: " + FormatThermalMass((float)part.thermalMass) + "\n"
                    + "          Module: Skin: " + FormatThermalMass(skinThermalMass) + " / Core: " + FormatThermalMass(thermalMass) + "\n"
                    + "ModuleMass (Skin) " + FormatMass(moduleMass) + ",    Total Mass: " + FormatMass(part.mass) + "\n"
                    + "SurfaceArea: part.radiativeArea " + part.radiativeArea + ", module Calc " + moduleCalcArea + ", Module surfaceArea " + surfaceArea + "\n"
                    //+ "SurfaceArea: part.exposedArea " + part.exposedArea + ", part.skinExposedArea "  + part.skinExposedArea + ", skinExposedAreaFrac " + part.skinExposedAreaFrac + "\n"
                    + "part.DragCubes->  PostOcclusionArea " + part.DragCubes.PostOcclusionArea  + ", cubeData.exposedArea "+ part.DragCubes.ExposedArea + ", Area "+ part.DragCubes.Area + "\n"
            );
        }

        public void UpdateCoreForRealfuels()
        {
            List<string> availableMaterialsNamesForFuelTank = new List<string>();
            string str = "";
            foreach (string name in availablePresetNamesCore) {
                if (PresetROMatarial.PresetsCore[name].restrictors.Contains(moduleFuelTanks.type)){
                    availableMaterialsNamesForFuelTank.Add(name);
                    str += name + ",";
                }
            }
            if (availableMaterialsNamesForFuelTank.Any()) {
                presetCoreName = availableMaterialsNamesForFuelTank[0];
                string[] strList = availableMaterialsNamesForFuelTank.ToArray();
                UpdatePresetsList(strList, PresetType.Core);
                Debug.Log("[ROThermal] UpdateFuelTankCore() " + moduleFuelTanks.type + " found in " + str 
                            + "\n\r presetCoreName set as " + availableMaterialsNamesForFuelTank[0]);
            } else {
                availableMaterialsNamesForFuelTank.Add("default");
                presetCoreName = "default";
                Debug.LogError("[ROThermal] No fitting PresetROMatarial for " + moduleFuelTanks.type + " found in " + part.name);   
            }
            ApplyCorePreset(presetCoreName);
        }

        public string[] GetUnlockedPresets(string[] all)
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER &&
                HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
            {
                Debug.Log($"[ROThermal] All presets unlocked");
                return all;
            }

            var unlocked = new List<string>();
            foreach (string s in all)
            {
                if (IsConfigUnlocked(s))
                {
                    Debug.Log($"[ROThermal] preset {s} is unlocked");
                    unlocked.AddUnique(s);
                }
            }

            if (unlocked.Count == 0)
                unlocked.Add("default");

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
            Debug.Log($"[ROThermal] available {type} presets on part {part.name}: " + string.Join(",", uiControlEditor.options));
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
                presetCoreName = bestConfigMatch;
                ApplyCorePreset(presetCoreName);
            }
        }

        public void UpdatePAW()
        {
            foreach (UIPartActionWindow window in UIPartActionController.Instance.windows)
            {
                if (window.part == this.part)
                {
                    window.displayDirty = true;
                }
            }
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
        public static string FormatMass(float mass) => mass < 1.0f ? KSPUtil.PrintSI(mass * 1e6, "g", 4) : KSPUtil.PrintSI(mass, "t", 4);
        public static string FormatThermalMass(float thermalmass) => KSPUtil.PrintSI(thermalmass * 1e3, "J/K", 4);
        public static string FormatThermalMass(double thermalmass) => KSPUtil.PrintSI(thermalmass * 1e3, "J/K", 4);

        #endregion Custom Methods
    }
}
