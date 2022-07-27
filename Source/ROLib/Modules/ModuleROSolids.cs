using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPShaderTools;

namespace ROLib
{
    internal class ModuleROSolids : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
    {
        private const string GroupDisplayName = "RO-Solids";
        private const string GroupName = "ModuleROSolids";

        #region KSPFields

        [KSPField] public float diameterLargeStep = 0.1f;
        [KSPField] public float diameterSmallStep = 0.1f;
        [KSPField] public float diameterSlideStep = 0.001f;
        [KSPField] public float minDiameter = 0.1f;
        [KSPField] public float maxDiameter = 100.0f;
        [KSPField] public float minLength = 0.1f;
        [KSPField] public float maxLength = 100.0f;
        [KSPField] public float actualHeight = 0.0f;

        [KSPField] public float volumeScalingPower = 3f;
        [KSPField] public float massScalingPower = 3f;
        [KSPField] public bool enableVScale = true;
        [KSPField] public bool enableNoseVScale = true;
        [KSPField] public bool enableMountVScale = false;
        [KSPField] public bool lengthWidth = false;
        [KSPField] public bool scaleMass = false;
        [KSPField] public bool scaleCost = false;

        [KSPField] public string coreManagedNodes = string.Empty;
        [KSPField] public string noseManagedNodes = string.Empty;
        [KSPField] public string mountManagedNodes = string.Empty;
        [KSPField] public string noseInterstageNode = "topinterstage";
        [KSPField] public string mountInterstageNode = "bottominterstage";
        [KSPField] public bool validateNose = true;
        [KSPField] public bool validateMount = true;

        [KSPField] public float designSafetyFactor = 1.1f;
        [KSPField] public float MEOP = 0f; // Maximum Expected Operating Pressure
        [KSPField] public bool hasGimbal = false;
        [KSPField] public string engineThrustTransform = "thrustTransform";
        [KSPField] public string gimbalTransform = "gimbalTransform";
        [KSPField] public string engineThrustSource = "CORE";
        [KSPField] public string engineTransformSource = "CORE";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Diameter", guiUnits = "m", groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentDiameter = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Length", guiUnits = "m", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentLength = 1.0f;

        [KSPEvent(guiName = "Open Diameter Selection", guiActiveEditor = true, groupName = GroupName)]
        public void OpenTankDimensionGUIEvent() => EditDimensions();

        [KSPEvent(guiName = "Open Preset Selection", guiActiveEditor = true, groupName = GroupName)]
        public void OpenPresetWindowGUIEvent() => ChangePresets();

        [KSPEvent(guiName = "Select Nose", guiActiveEditor = true, groupName = GroupName)]
        public void SelectNoseModelGUIEvent() => SelectModelWindow(noseModule, noseDefs, "Nose");

        [KSPEvent(guiName = "Select Mount", guiActiveEditor = true, groupName = GroupName)]
        public void SelectMountModelGUIEvent() => SelectModelWindow(mountModule, mountDefs, "Nozzle");

        [KSPField(isPersistant = true, guiName = "V.ScaleAdj", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0.1f, maxValue = 20f, incrementLarge = 0.25f, incrementSmall = 0.05f, incrementSlide = 0.001f)]
        public float currentVScale = 1f;

        [KSPField(isPersistant = true, guiName = "Nose V.Scale", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0.25f, maxValue = 4f, incrementLarge = 0.25f, incrementSmall = 0.05f, incrementSlide = 0.001f)]
        public float currentNoseVScale = 1f;

        [KSPField(isPersistant = true, guiName = "Nozzle V.Scale", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0.25f, maxValue = 4f, incrementLarge = 0.25f, incrementSmall = 0.05f, incrementSlide = 0.001f)]
        public float currentMountVScale = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Total Length", guiFormat = "F4", guiUnits = "m", groupName = GroupName)]
        public float totalTankLength = 0.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Largest Diameter", guiFormat = "F4", guiUnits = "m", groupName = GroupName)]
        public float largestDiameter = 0.0f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Nose Rot.", groupName = GroupName),
         UI_FloatEdit(sigFigs = 0, suppressEditorShipModified = true, minValue = -180f, maxValue = 180f, incrementLarge = 45f, incrementSmall = 15f, incrementSlide = 1f)]
        public float currentNoseRotation = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Nozzle Rot.", groupName = GroupName),
         UI_FloatEdit(sigFigs = 0, suppressEditorShipModified = true, minValue = -180f, maxValue = 180f, incrementLarge = 45f, incrementSmall = 15f, incrementSlide = 1f)]
        public float currentMountRotation = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Case Thickness", guiFormat = "F4", guiUnits = "m", groupName = GroupName)]
        public float caseThickness = 0.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Case Material", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCase = "Default";

        [KSPEvent(guiActiveEditor = true, guiName = "Enable Gimbal", groupName = GroupName)]
        public void ToggleGimbal()
        {
            hasGimbal = !hasGimbal;
            UpdateGimbal();
            UpdateMass();
            UpdateCost();
            this.Events["ToggleGimbal"].guiName = hasGimbal ? "Remove Gimbal" : "Add Gimbal";
        }

        //------------------------------------------MODEL SELECTION SET PERSISTENCE-----------------------------------------------//

        [KSPField(isPersistant = true, guiName = "Variant", guiActiveEditor = true, groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentVariant = "Default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentNose = "Mount-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Core", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCore = "Mount-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nozzle", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentMount = "Mount-None";

        //------------------------------------------TEXTURE SET PERSISTENCE-----------------------------------------------//

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentNoseTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Core Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCoreTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nozzle Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentMountTexture = "default";

        //------------------------------------------RECOLORING PERSISTENCE-----------------------------------------------//

        //persistent data for modules; stores colors
        [KSPField(isPersistant = true)] public string noseModulePersistentData = string.Empty;

        [KSPField(isPersistant = true)] public string coreModulePersistentData = string.Empty;

        [KSPField(isPersistant = true)] public string mountModulePersistentData = string.Empty;

        #endregion KSPFields

        #region Private Variables

        [Persistent]
        public string configNodeData = string.Empty;

        private bool initialized = false;

        private float modifiedMass = -1;

        private float modifiedCost = -1;

        private float prevDiameter = 0;
        private float prevLength = 0;
        private float prevNose = 0;
        private float prevCore = 0;
        private float prevMount = 0;

        private string[] noseNodeNames;
        private string[] coreNodeNames;
        private string[] mountNodeNames;

        internal ROLModelModule<ModuleROSolids> noseModule;
        internal ROLModelModule<ModuleROSolids> coreModule;
        internal ROLModelModule<ModuleROSolids> mountModule;

        private readonly Dictionary<string, ModelDefinitionVariantSet> variantSets = new Dictionary<string, ModelDefinitionVariantSet>();

        private ModelDefinitionVariantSet GetVariantSet(string name)
        {
            if (!variantSets.TryGetValue(name, out ModelDefinitionVariantSet set))
            {
                set = new ModelDefinitionVariantSet(name);
                variantSets.Add(name, set);
            }
            return set;
        }

        public const KeyCode hoverRecolorKeyCode = KeyCode.J;
        public const KeyCode hoverPresetKeyCode = KeyCode.N;

        private ModelDefinitionVariantSet GetVariantSet(ModelDefinitionLayoutOptions def) =>
            variantSets.Values.FirstOrDefault(a => a.definitions.Contains(def));

        internal ModelDefinitionLayoutOptions[] coreDefs;
        internal ModelDefinitionLayoutOptions[] noseDefs;
        internal ModelDefinitionLayoutOptions[] mountDefs;

        private DimensionWindow dimWindow;
        private ModelWindow modWindow;
        private PresetWindow presetWindow;

        #endregion Private Variables

        internal void ModelChangedHandler(bool pushNodes)
        {
            if (validateNose || validateMount)
                ValidateModules();
            ValidateRotation();
            ValidateVScale();
            UpdateModulePositions();
            UpdateTankVolume(lengthWidth);
            UpdateDimensions();
            UpdateModelMeshes();
            UpdateAttachNodes(pushNodes);
            UpdateAvailableVariants();
            SetPreviousModuleLength();
            UpdateDragCubes();
            UpdateEngineModule();
            if (scaleMass)
                UpdateMass();
            if (scaleCost)
                UpdateCost();
            ROLStockInterop.UpdatePartHighlighting(part);
        }

        internal void ModelChangedHandlerWithSymmetry(bool pushNodes, bool symmetry)
        {
            ModelChangedHandler(pushNodes);
            if (symmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.FindModuleImplementing<ModuleROSolids>().ModelChangedHandler(pushNodes);
                }
            }
        }

        #region Standard KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            Initialize();
            UpdateModulePositions();
            UpdateDimensions();
            UpdateModelMeshes();
        }

        public override void OnStart(StartState state)
        {
            Initialize();
            ModelChangedHandler(false);
            InitializeUI();
        }

        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onPartActionUIDismiss.Remove(OnPawClose);
            }
        }

        #endregion Standard KSP Overrides


        #region Interface Overrides

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => Mathf.Max(0, modifiedMass);
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => Mathf.Max(0, modifiedCost);

        public string[] getSectionNames() => new string[] { "Nose", "Core", "Nozzle" };

        public RecoloringData[] getSectionColors(string section)
        {
            return section switch
            {
                "Nose" => noseModule.recoloringData,
                "Core" => coreModule.recoloringData,
                "Nozzle" => mountModule.recoloringData,
                _ => coreModule.recoloringData,
            };
        }

        public void setSectionColors(string section, RecoloringData[] colors)
        {
            if (section == "Nose") noseModule.setSectionColors(colors);
            else if (section == "Core") coreModule.setSectionColors(colors);
            else if (section == "Nozzle") mountModule.setSectionColors(colors);
        }

        public TextureSet getSectionTexture(string section)
        {
            return section switch
            {
                "Nose" => noseModule.textureSet,
                "Core" => coreModule.textureSet,
                "Nozzle" => mountModule.textureSet,
                _ => coreModule.textureSet,
            };
        }

        #endregion Interface Overrides

        #region Custom Update Methods

        private void Initialize()
        {
            if (initialized) { return; }
            initialized = true;

            noseNodeNames = ROLUtils.parseCSV(noseManagedNodes);
            coreNodeNames = ROLUtils.parseCSV(coreManagedNodes);
            mountNodeNames = ROLUtils.parseCSV(mountManagedNodes);

            ConfigNode node = ROLConfigNodeUtils.ParseConfigNode(configNodeData);

            List<ModelDefinitionLayoutOptions> coreDefList = new List<ModelDefinitionLayoutOptions>();

            foreach (ConfigNode n in node.GetNodes("CORE"))
            {
                string variantName = n.ROLGetStringValue("variant", "Default");
                coreDefs = ROLModelData.getModelDefinitionLayouts(n.ROLGetStringValues("model"));
                coreDefList.AddUniqueRange(coreDefs);
                ModelDefinitionVariantSet mdvs = GetVariantSet(variantName);
                mdvs.AddModels(coreDefs);
            }

            coreDefs = coreDefList.ToArray();
            noseDefs = ROLModelData.getModelDefinitions(node.GetNodes("NOSE"));
            mountDefs = ROLModelData.getModelDefinitions(node.GetNodes("NOZZLE"));

            coreModule = new ROLModelModule<ModuleROSolids>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-CORE"), ModelOrientation.CENTRAL, nameof(currentCore), null, nameof(currentCoreTexture), nameof(coreModulePersistentData));
            coreModule.name = "ModuleROSolids-Core";
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.getValidOptions = () => GetVariantSet(currentVariant).definitions;


            noseModule = new ROLModelModule<ModuleROSolids>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-NOSE"), ModelOrientation.TOP, nameof(currentNose), null, nameof(currentNoseTexture), nameof(noseModulePersistentData));
            noseModule.name = "ModuleROSolids-Nose";
            noseModule.getSymmetryModule = m => m.noseModule;
            if (validateNose)
            {
                noseModule.getValidOptions = () => noseModule.getValidModels(noseDefs, coreModule.definition.style);
            }
            else
            {
                noseModule.getValidOptions = () => noseDefs;
            }

            mountModule = new ROLModelModule<ModuleROSolids>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-NOZZLE"), ModelOrientation.BOTTOM, nameof(currentMount), null, nameof(currentMountTexture), nameof(mountModulePersistentData));
            mountModule.name = "ModuleROSolids-Nozzle";
            mountModule.getSymmetryModule = m => m.mountModule;
            if (validateMount)
            {
                mountModule.getValidOptions = () => mountModule.getValidModels(mountDefs, coreModule.definition.style);
            }
            else
            {
                mountModule.getValidOptions = () => mountDefs;
            }

            noseModule.volumeScalar = volumeScalingPower;
            coreModule.volumeScalar = volumeScalingPower;
            mountModule.volumeScalar = volumeScalingPower;

            prevDiameter = currentDiameter;
            prevLength = 1;
            prevCore = 1;
            prevNose = 0;
            prevMount = 0;

            //set up the model lists and load the currently selected model
            noseModule.setupModelList(noseDefs);
            coreModule.setupModelList(coreDefs);
            mountModule.setupModelList(mountDefs);
            coreModule.setupModel();
            noseModule.setupModel();
            mountModule.setupModel();
            if (validateNose || validateMount)
                ValidateModules();

            GetModuleByName(engineTransformSource).renameEngineThrustTransforms(engineThrustTransform);
            GetModuleByName(engineTransformSource).renameGimbalTransforms(gimbalTransform);
        }

        public void InitializeUI()
        {
            string[] variantNames = ROLUtils.getNames(variantSets.Values, m => m.variantName);
            this.ROLupdateUIChooseOptionControl(nameof(currentVariant), variantNames, variantNames);
            Fields[nameof(currentVariant)].guiActiveEditor = variantSets.Count > 1;

            Fields[nameof(currentVariant)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                ModelDefinitionVariantSet prevMdvs = GetVariantSet(coreModule.definition.name);
                int previousIndex = prevMdvs.IndexOf(coreModule.layoutOptions);
                ModelDefinitionVariantSet mdvs = GetVariantSet(currentVariant);
                ModelDefinitionLayoutOptions newCoreDef = mdvs[previousIndex];
                this.ROLactionWithSymmetry(m =>
                {
                    if (lengthWidth)
                        m.SetModelFromDimensions();
                    else
                        m.coreModule.modelSelected(newCoreDef.definition.name);
                    ModelChangedHandler(true);
                });
                MonoUtilities.RefreshPartContextWindow(part);
            };

            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentDiameter)].uiControlEditor.onSymmetryFieldChanged = OnDiameterChanged;

            Fields[nameof(currentLength)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentLength)].uiControlEditor.onSymmetryFieldChanged = OnLengthChanged;

            Fields[nameof(currentVScale)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentVScale)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentNoseVScale)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentNoseVScale)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentMountVScale)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentMountVScale)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentNoseRotation)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentNoseRotation)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentMountRotation)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentMountRotation)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentNose)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentNose)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentCore)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentCore)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentMount)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentMount)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;

            //------------------MODEL DIAMETER / LENGTH SWITCH UI INIT---------------------//
            this.ROLupdateUIFloatEditControl(nameof(currentDiameter), minDiameter, maxDiameter, diameterLargeStep, diameterSmallStep, diameterSlideStep);
            this.ROLupdateUIFloatEditControl(nameof(currentLength), minLength, maxLength, diameterLargeStep, diameterSmallStep, diameterSlideStep);
            this.ROLupdateUIFloatEditControl(nameof(currentVScale),
                Mathf.Min(coreModule.definition.minVerticalScale, 1), Mathf.Max(coreModule.definition.maxVerticalScale, 1), 0.25f, 0.05f, 0.001f);
            this.ROLupdateUIFloatEditControl(nameof(currentNoseVScale),
                Mathf.Min(noseModule.definition.minVerticalScale, 1), Mathf.Max(noseModule.definition.maxVerticalScale, 1), 0.25f, 0.05f, 0.001f);
            this.ROLupdateUIFloatEditControl(nameof(currentMountVScale),
                Mathf.Min(mountModule.definition.minVerticalScale, 1), Mathf.Max(mountModule.definition.maxVerticalScale, 1), 0.25f, 0.05f, 0.001f);

            Fields[nameof(currentDiameter)].guiActiveEditor = maxDiameter != minDiameter;
            Fields[nameof(currentLength)].guiActiveEditor = lengthWidth && maxLength != minLength;
            Fields[nameof(currentVScale)].guiActiveEditor = enableVScale && !lengthWidth;
            Fields[nameof(currentNoseVScale)].guiActiveEditor = enableNoseVScale;
            Fields[nameof(currentMountVScale)].guiActiveEditor = enableMountVScale;
            Fields[nameof(currentNoseRotation)].guiActiveEditor = noseModule.moduleCanRotate;
            Fields[nameof(currentMountRotation)].guiActiveEditor = mountModule.moduleCanRotate;

            //------------------MODULE TEXTURE SWITCH UI INIT---------------------//
            Fields[nameof(currentNoseTexture)].uiControlEditor.onFieldChanged = noseModule.textureSetSelected;
            Fields[nameof(currentCoreTexture)].uiControlEditor.onFieldChanged = coreModule.textureSetSelected;
            Fields[nameof(currentMountTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onPartActionUIDismiss.Add(OnPawClose);
            }
        }

        private void ValidateModules()
        {
            if (validateNose && !coreModule.isValidModel(noseModule, coreModule.definition.style))
            {
                string coreStyle = coreModule.definition.style;
                ROLModelDefinition def = coreModule.findFirstValidModel(noseModule, coreStyle);
                if (def == null) { ROLLog.error("Could not locate valid definition for NOSE"); }
                noseModule.modelSelected(def.name);
            }
            if (validateMount && !coreModule.isValidModel(mountModule, coreModule.definition.style))
            {
                string coreStyle = coreModule.definition.style;
                ROLModelDefinition def = coreModule.findFirstValidModel(mountModule, coreStyle);
                if (def == null) { ROLLog.error("Could not locate valid definition for NOZZLE"); }
                mountModule.modelSelected(def.name);
            }
        }

        private void SetPreviousModuleLength()
        {
            prevDiameter = currentDiameter;
            prevLength = currentLength;
            prevNose = noseModule.moduleHeight;
            prevCore = coreModule.moduleHeight;
            prevMount = mountModule.moduleHeight;
        }

        public void OnModelSelectionChanged(BaseField f, object o)
        {
            if (f.name == Fields[nameof(currentMount)].name) mountModule.modelSelected(currentMount);
            else if (f.name == Fields[nameof(currentCore)].name) coreModule.modelSelected(currentCore);
            else if (f.name == Fields[nameof(currentNose)].name) noseModule.modelSelected(currentNose);

            ModelChangedHandler(true);
            MonoUtilities.RefreshPartContextWindow(part);
        }

        private void OnDiameterChanged(BaseField f, object o)
        {
            // KSP 1.7.3 bug, symmetry invocations will have o=newValue instead of previousValue
            if ((float)f.GetValue(this) == prevDiameter) return;
            if (lengthWidth)
            {
                ValidateLength();
                SetModelFromDimensions();
            }
            if (presetWindow != null && presetWindow.Enabled)
            {
                presetWindow.UpdateDiameter();
            }
            ModelChangedHandler(true);
        }

        private void OnLengthChanged(BaseField f, object o)
        {
            if ((float)f.GetValue(this) == prevLength) return;
            ValidateLength();
            SetModelFromDimensions();
            if (presetWindow != null && presetWindow.Enabled)
            {
                presetWindow.UpdateLength();
            }
            ModelChangedHandler(true);
        }

        public void UpdateModulePositions()
        {
            if (lengthWidth)
                coreModule.setScaleForHeightAndDiameter(currentLength, currentDiameter);
            else
                coreModule.RescaleToDiameter(currentDiameter, coreModule.definition.diameter, currentVScale);

            noseModule.RescaleToDiameter(coreModule.moduleUpperDiameter, noseModule.moduleLowerDiameter / noseModule.moduleHorizontalScale, currentNoseVScale);
            mountModule.RescaleToDiameter(coreModule.moduleLowerDiameter, mountModule.moduleUpperDiameter / mountModule.moduleHorizontalScale, currentMountVScale);

            float totalHeight = noseModule.moduleHeight + coreModule.moduleHeight + mountModule.moduleHeight;

            float pos = totalHeight * 0.5f; // abs top of model
            Vector3 rot = new Vector3(0f, 0f, 0f);

            pos -= noseModule.moduleHeight; // bottom of nose model
            rot.y = currentNoseRotation;
            noseModule.SetPosition(pos);
            noseModule.SetRotation(rot);

            pos -= coreModule.moduleHeight * 0.5f; // center of 'core' model
            coreModule.SetPosition(pos);

            pos -= coreModule.moduleHeight * 0.5f; // bottom of 'core' model
            rot.y = currentMountRotation;
            mountModule.SetPosition(pos);
            mountModule.SetRotation(rot);
        }

        public void UpdateModelMeshes()
        {
            noseModule.UpdateModelScalesAndLayoutPositions();
            coreModule.UpdateModelScalesAndLayoutPositions();
            mountModule.UpdateModelScalesAndLayoutPositions();
        }

        public void UpdateMass()
        {
            modifiedMass = coreModule.moduleMass + noseModule.moduleMass + mountModule.moduleMass;
        }

        public void UpdateCost()
        {
            modifiedCost = coreModule.moduleCost + noseModule.moduleCost + mountModule.moduleCost;
        }

        public void UpdateDimensions()
        {
            // If mount module is originally designed as a bottom (mount) module, refer to upper diameter.  If it is anything repurposed, use its max diameter.
            float mountMaxDiam = mountModule.definition.shouldInvert(ModelOrientation.BOTTOM) ? Math.Max(mountModule.moduleLowerDiameter, mountModule.moduleUpperDiameter) : mountModule.moduleUpperDiameter;
            float noseMaxDiam = Math.Max(noseModule.moduleLowerDiameter, noseModule.moduleUpperDiameter);
            totalTankLength = GetTotalHeight();
            largestDiameter = Math.Max(currentDiameter, Math.Max(noseMaxDiam, mountMaxDiam));
        }

        public void UpdateAttachNodes(bool userInput)
        {
            // Update the standard top and bottom attach nodes, using the node position(s) defined in the nose and mount modules
            noseModule.UpdateAttachNode("top", ModelOrientation.TOP, userInput);
            mountModule.UpdateAttachNode("bottom", ModelOrientation.BOTTOM, userInput);

            // Update the model-module specific attach nodes, using the per-module node definitions from the part
            noseModule.updateAttachNodeBody(noseNodeNames, userInput);
            coreModule.updateAttachNodeBody(coreNodeNames, userInput);
            mountModule.updateAttachNodeBody(mountNodeNames, userInput);

            // Update the Nose Interstage Node
            int nodeSize = Mathf.RoundToInt(coreModule.moduleDiameter) + 1;
            Vector3 pos = new Vector3(0, coreModule.ModuleTop, 0);
            ROLSelectableNodes.updateNodePosition(part, noseInterstageNode, pos);
            if (part.FindAttachNode(noseInterstageNode) is AttachNode noseInterstage)
                ROLAttachNodeUtils.UpdateAttachNodePosition(part, noseInterstage, pos, Vector3.up, userInput, nodeSize);

            // Update the Mount Interstage Node
            nodeSize = Mathf.RoundToInt(coreModule.moduleDiameter) + 1;
            pos = new Vector3(0, coreModule.ModuleBottom, 0);
            ROLSelectableNodes.updateNodePosition(part, mountInterstageNode, pos);
            if (part.FindAttachNode(mountInterstageNode) is AttachNode mountInterstage)
                ROLAttachNodeUtils.UpdateAttachNodePosition(part, mountInterstage, pos, Vector3.down, userInput, nodeSize);

            // Update surface attach node position, part position, and any surface attached children
            if (part.srfAttachNode is AttachNode surfaceNode)
                coreModule.UpdateSurfaceAttachNode(surfaceNode, prevDiameter, prevNose, prevCore, prevMount, noseModule.moduleHeight, coreModule.moduleHeight, mountModule.moduleHeight, userInput);
        }

        private float CaseThickness()
        {
            float thickness = 0f;

            return thickness;
        }

        private void UpdateEngineModule()
        {
            ROLModelModule<ModuleROSolids> engineTransformSource = GetModuleByName(this.engineTransformSource);
            engineTransformSource.RenameEngineThrustTransforms(engineThrustTransform);
            engineTransformSource.RenameGimbalTransforms(gimbalTransform);

            /*
            ModuleEngines engine = part.GetComponent<ModuleEngines>();
            if (engine != null)
            {
                ModelModule<SSTUModularPart> engineThrustSource = getModuleByName(this.engineThrustSource);
                engineThrustSource.updateEngineModuleThrust(engine, thrustScalingPower);
            }

            //re-init gimbal module
            ModuleGimbal gimbal = part.GetComponent<ModuleGimbal>();
            if (gimbal != null)
            {
                //check to see that gimbal was already initialized
                if ((HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight) && gimbal.gimbalTransforms!=null)
                {
                    float range = 0;
                    gimbal.OnStart(StartState.Flying);
                    if (engineTransformSource.definition.engineTransformData != null) { range = engineTransformSource.definition.engineTransformData.gimbalFlightRange; }
                    gimbal.gimbalRange = range;

                    //re-init gimbal offset module if it exists
                    SSTUGimbalOffset gOffset = part.GetComponent<SSTUGimbalOffset>();
                    if (gOffset != null)
                    {
                        range = 0;
                        if (engineTransformSource.definition.engineTransformData != null)
                        {
                            range = engineTransformSource.definition.engineTransformData.gimbalAdjustmentRange;
                        }
                        gOffset.gimbalXRange = range;
                        gOffset.gimbalZRange = range;
                        gOffset.reInitialize();
                    }
                }
            }

            ROLModelConstraint constraints = part.GetComponent<ROLModelConstraint>();
            if (constraints!=null)
            {
                ConfigNode constraintNode;
                if (engineTransformSource.definition.constraintData != null)
                {
                    constraintNode = engineTransformSource.definition.constraintData.constraintNode;
                }
                else
                {
                    constraintNode = new ConfigNode("CONSTRAINT");
                }
                constraints.loadExternalData(constraintNode);
            }
            UpdateEffectsScale();
            */
        }

        private float GetTotalHeight()
        {
            float totalHeight = coreModule.moduleHeight + noseModule.moduleHeight + mountModule.moduleHeight;
            return totalHeight;
        }

        public void UpdateAvailableVariants()
        {
            noseModule.updateSelections();
            coreModule.updateSelections();
            mountModule.updateSelections();
        }

        private void UpdateDragCubes() => ROLModInterop.OnPartGeometryUpdate(part, true);

        private void ValidateRotation()
        {
            Fields[nameof(currentNoseRotation)].guiActiveEditor = noseModule.moduleCanRotate;
            Fields[nameof(currentMountRotation)].guiActiveEditor = mountModule.moduleCanRotate;
            if (!noseModule.moduleCanRotate) currentNoseRotation = 0f;
            if (!mountModule.moduleCanRotate) currentMountRotation = 0f;
        }

        private void ValidateVScale()
        {
            enableNoseVScale = noseModule.moduleCanVScale;
            enableMountVScale = mountModule.moduleCanVScale;
            Fields[nameof(currentNoseVScale)].guiActiveEditor = enableNoseVScale;
            Fields[nameof(currentMountVScale)].guiActiveEditor = enableMountVScale;

            this.ROLupdateUIFloatEditControl(nameof(currentVScale),
                Mathf.Min(coreModule.definition.minVerticalScale, 1), Mathf.Max(coreModule.definition.maxVerticalScale, 1), 0.25f, 0.05f, 0.001f);

            this.ROLupdateUIFloatEditControl(nameof(currentNoseVScale),
                Mathf.Min(noseModule.definition.minVerticalScale, 1), Mathf.Max(noseModule.definition.maxVerticalScale, 1), 0.25f, 0.05f, 0.001f);

            this.ROLupdateUIFloatEditControl(nameof(currentMountVScale),
                Mathf.Min(mountModule.definition.minVerticalScale, 1), Mathf.Max(mountModule.definition.maxVerticalScale, 1), 0.25f, 0.05f, 0.001f);

            if (!enableNoseVScale) currentNoseVScale = 1f;
            if (!enableMountVScale) currentMountVScale = 1f;
        }

        private void UpdateTankVolume(bool lw)
        {
            if (!lw)
            {
                float totalVol = noseModule.moduleVolume + coreModule.moduleVolume + mountModule.moduleVolume;
                SendVolumeChangedEvent(totalVol);
                return;
            }

            // Get the additional volume from the nose and mounts *beyond what is provided by the tank length extension*
            // Nose & Mount Diameters are the diameter of the model at the point of attachment.
            // Inversion handles a particular model that mounts in either orientation, and its values are for the orientation specified.
            float noseDiameter = noseModule.definition.shouldInvert(ModelOrientation.TOP) ? noseModule.definition.upperDiameter : noseModule.definition.lowerDiameter;
            float noseScale = Mathf.Pow(currentDiameter / noseDiameter, 3);

            float mountDiameter = mountModule.definition.shouldInvert(ModelOrientation.BOTTOM) ? mountModule.definition.lowerDiameter : mountModule.definition.upperDiameter;
            float mountScale = Mathf.Pow(currentDiameter / mountDiameter, 3);

            float noseAdditionalVol = noseScale * noseModule.definition.additionalVolume * 1000f;
            float mountAdditionalVol = mountScale * mountModule.definition.additionalVolume * 1000f;

            // Calculate the volume of the main tank
            float r = currentDiameter / 2;
            float effectiveVolume = (ROLUtils.EllipsoidVolume(r, r, r / 2) + ROLUtils.CylinderVolume(r, EffectiveCylinderLength())) * 1000f;
            effectiveVolume += noseAdditionalVol + mountAdditionalVol;

            ROLModInterop.RealFuelsVolumeUpdate(part, effectiveVolume);
        }

        public void SendVolumeChangedEvent(float newVol)
        {
            var data = new BaseEventDetails(BaseEventDetails.Sender.USER);
            data.Set<string>("volName", "Tankage");
            data.Set<double>("newTotalVolume", newVol);
            part.SendEvent("OnPartVolumeChanged", data, 0);
        }

        #endregion ENDREGION - Custom Update Methods


        #region GUI

        private void OnGUI()
        {
            foreach (var window in AbstractWindow.Windows.Values)
                window.Draw();
        }

        private void HideAllWindows()
        {
            if (dimWindow != null)
            {
                dimWindow.Hide();
                dimWindow = null;
            }
            if (modWindow != null)
            {
                modWindow.Hide();
                modWindow = null;
            }
            if (presetWindow != null)
            {
                presetWindow.Hide();
                presetWindow = null;
            }
        }

        private void OnSceneChange(GameScenes _) => HideAllWindows();
        private void OnPawClose(Part p) { if (p == part) HideAllWindows(); }

        public void EditDimensions()
        {
            if (dimWindow != null && dimWindow.Enabled)
            {
                dimWindow.Hide();
                dimWindow = null;
            }
            else
            {
                dimWindow = new DimensionWindow(this);
                dimWindow.Show();
            }
        }

        public void ChangePresets()
        {
            if (presetWindow != null && presetWindow.Enabled)
            {
                presetWindow.Hide();
                presetWindow = null;
            }
            else
            {
                presetWindow = new PresetWindow(this);
                presetWindow.Show();
            }
        }

        public void SelectModelWindow(ROLModelModule<ModuleROSolids> m, ModelDefinitionLayoutOptions[] defs, string name)
        {
            if (modWindow != null && modWindow.Enabled)
            {
                bool openedDifferentWindow = modWindow.module != m;
                modWindow.Hide();
                modWindow = null;
                if (!openedDifferentWindow) return;
            }
            modWindow = new ModelWindow(this, m, defs, name);
            modWindow.Show();
        }

        private void OnMouseOver()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;
            if (!ROLGameSettings.ROTanksEditorShortcuts()) return;
            if (Input.GetKeyDown(hoverRecolorKeyCode))
                part.Modules.GetModule<SSTURecolorGUI>().recolorGUIEvent();
            else if (Input.GetKeyDown(hoverPresetKeyCode))
                EditDimensions();
        }

        #endregion GUI
    }
}
