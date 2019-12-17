using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ROLib
{
    public class ModuleROSolar : ModuleDeployableSolarPanel, IPartCostModifier, IPartMassModifier
    {
        #region KSPFields

        [KSPField(isPersistant = true, guiName = "Variant", guiActiveEditor = true, guiActive = false, groupName = "ModuleROSolar"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentVariant = "Default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Design", groupName = "ModuleROSolar"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCore = "Mount-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Panel Length", guiFormat = "N3", guiUnits = "m", groupDisplayName = "RO-Solar", groupName = "ModuleROSolar"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float panelLength = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Panel Width", guiFormat = "N3", guiUnits = "m", groupName = "ModuleROSolar"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float panelWidth = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Panel Scale", guiFormat = "N3", guiUnits = "x", groupName = "ModuleROSolar"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float panelScale = 1.0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Tech Level", guiFormat = "N0", groupName = "ModuleROSolar"),
        UI_FloatRange(minValue = 0f, stepIncrement = 1f, scene = UI_Scene.Editor, suppressEditorShipModified = true)]
        public float TechLevel = -1f;
        public int techLevel => Convert.ToInt32(TechLevel);

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Charge Rate", guiFormat = "F4", guiUnits = " kW", groupDisplayName = "RO-Solar", groupName = "ModuleROSolar")]
        public float currentRate = 0.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Area", guiFormat = "F4", guiUnits = " m^2", groupName = "ModuleROSolar")]
        public float area = 0.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Mass", guiFormat = "F4", guiUnits = " m", groupName = "ModuleROSolar")]
        public float mass = 0.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Cost", guiFormat = "F1", groupName = "ModuleROSolar")]
        public float cost = 0.0f;

        [KSPEvent(guiActiveEditor = true, guiName = "Reset Model to Original", groupName = "ModuleROSolar")]
        public void ResetModel()
        {
            panelLength = coreModule.definition.panelLength;
            panelWidth = coreModule.definition.panelWidth;
            panelScale = 1.0f;
            this.ROLupdateUIFloatEditControl(nameof(panelLength), minLength, maxLength, largeStep, smallStep, slideStep, true, panelLength);
            this.ROLupdateUIFloatEditControl(nameof(panelWidth), minWidth, maxWidth, largeStep, smallStep, slideStep, true, panelWidth);
            this.ROLupdateUIFloatEditControl(nameof(panelScale), 0.1f, 100f, largeStep, smallStep, slideStep, true, panelScale);
            UpdateModulePositions();
            UpdateAttachNodes(true);
            UpdateAvailableVariants();
            UpdateDragCubes();
            UpdateMassAndCost();
            RecalculateStats();
        }

        [KSPField]
        public string solarPanelType = "static";

        [KSPField]
        public float kwPerM2 = 0.0f;

        [KSPField]
        public float kgPerM2 = 0.0f;

        [KSPField]
        public float costPerM2 = 0.0f;

        [KSPField]
        public int maxTechLevel = 0;

        [KSPField]
        public float largeStep = 1.0f;

        [KSPField]
        public float smallStep = 0.1f;

        [KSPField]
        public float slideStep = 0.001f;

        [KSPField]
        public string solarCore = "Mount-None";

        [KSPField]
        public float minWidth = 0.1f;

        [KSPField]
        public float maxWidth = 100.0f;

        [KSPField]
        public float minLength = 0.1f;

        [KSPField]
        public float maxLength = 100.0f;

        [KSPField]
        public float addMass = 0.0f;

        [KSPField]
        public float addCost = 0.0f;

        [KSPField]
        public string coreManagedNodes = string.Empty;

        [KSPField]
        public float surfaceNodeX = -0.1f;

        [KSPField(isPersistant = true)]
        public bool initializedDefaults = false;

        [KSPField]
        public bool fullScale = true;


        #endregion KSPFields


        #region Custom Fields

        private string modName = "ModuleROSOlar - ";

        /// <summary>
        /// Standard work-around for lack of config-node data being passed consistently and lack of support for mod-added serializable classes.
        /// </summary>
        [Persistent]
        public string configNodeData = string.Empty;

        /// <summary>
        /// Has initialization been run?  Set to true the first time init methods are run (OnLoad/OnStart), and ensures that init is only run a single time.
        /// </summary>
        private bool initialized = false;

        /// <summary>
        /// The adjusted modified mass for this part.
        /// </summary>
        private float modifiedMass = -1;

        /// <summary>
        /// The adjusted modified cost for this part.
        /// </summary>
        private float modifiedCost = -1;

        /// <summary>
        /// Previous length value
        /// </summary>
        private float prevLength = -1;

        /// <summary>
        /// Previous length value
        /// </summary>
        private float prevWidth = -1;

        /// <summary>
        /// Previous length value
        /// </summary>
        private float prevScale = -1;

        private string[] coreNodeNames;
        private ROLModelModule<ModuleROSolar> coreModule;

        /// <summary>
        /// Mapping of all of the variant sets available for this part.  When variant list length > 0, an additional 'variant' UI slider is added to allow for switching between variants.
        /// </summary>
        private Dictionary<string, ModelDefinitionVariantSet> variantSets = new Dictionary<string, ModelDefinitionVariantSet>();

        /// <summary>
        /// Helper method to get or create a variant set for the input variant name.  If no set currently exists, a new set is empty set is created and returned.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private ModelDefinitionVariantSet getVariantSet(string name)
        {
            ModelDefinitionVariantSet set = null;
            if (!variantSets.TryGetValue(name, out set))
            {
                set = new ModelDefinitionVariantSet(name);
                variantSets.Add(name, set);
            }
            return set;
        }

        ModelDefinitionLayoutOptions[] coreDefs;
        private SolarTechLimit stl;
        private bool lengthWidth = false;

        #endregion Custom Fields


        #region Standard KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData))
            {
                ROLLog.debug($"{modName}: OnLoad loading configNodeData");
                configNodeData = node.ToString();
                ROLLog.debug($"{modName}: OnLoad() configNodeData: {configNodeData}");
            }
            ROLLog.debug($"{modName}: OnLoad calling Initialize()");
            Initialize();
        }

        public override void OnStart(StartState state)
        {
            //ROLLog.debug($"{modName} OnStart Get ModuleDeployableSolarPanel");
            //SP = GetComponent<ModuleDeployableSolarPanel>();
            base.OnStart(state);
            ROLLog.debug($"{modName} OnStart calling SetMaxTechLevel()");
            SetMaxTechLevel();
            ROLLog.debug($"{modName} OnStart calling Initialize()");
            Initialize();
            ROLLog.debug($"{modName} OnStart calling InitializeUI()");
            InitializeUI();
        }

        public void Start()
        {
            initializedDefaults = true;
            UpdateDragCubes();
        }

        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        private void onEditorVesselModified(ShipConstruct ship)
        {
            //update available variants for attach node changes
            UpdateAvailableVariants();
        }

        // IPartMass/CostModifier override
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        // IPartMass/CostModifier override
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        // IPartMass/CostModifier override
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass == -1) { return 0; }
            return -defaultMass + modifiedMass;
        }

        // IPartMass/CostModifier override
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost == -1) { return 0; }
            return -defaultCost + modifiedCost;
        }


        #endregion Standard KSP Overrides


        #region Custom Update Methods

        /// <summary>
        /// Initialization method.  Sets up model modules, loads their configs from the input config node.  Does all initial linking of part-modules.<para/>
        /// Does NOT set up their UI interaction -- that is all handled during OnStart()
        /// </summary>
        private void Initialize()
        {
            if (initialized) { return; }
            ROLLog.debug($"{modName}: Initialize Starting");
            initialized = true;

            prevLength = panelLength;
            prevWidth = panelWidth;
            prevScale = panelScale;

            ROLLog.debug($"{modName}: Initialize() parseCSV");
            coreNodeNames = ROLUtils.parseCSV(coreManagedNodes);

            ROLLog.debug($"{modName}: Initialize() Model-Module Initialization");
            // Model-Module Setup / Initialization
            ConfigNode node = ROLConfigNodeUtils.parseConfigNode(configNodeData);

            ROLLog.debug($"{modName}: Initialize() Core Model Nodes");
            // List of CORE model nodes from config
            // each one may contain multiple 'model=modelDefinitionName' entries
            // but must contain no more than a single 'variant' entry.
            // If no variant is specified, they are added to the 'Default' variant.
            ConfigNode[] coreDefNodes = node.GetNodes("CORE");

            ROLLog.debug($"{modName}: Initialize() MDLO");
            List<ModelDefinitionLayoutOptions> coreDefList = new List<ModelDefinitionLayoutOptions>();
            int coreDefLen = coreDefNodes.Length;
            for (int i = 0; i < coreDefLen; i++)
            {
                string variantName = coreDefNodes[i].ROLGetStringValue("variant", "Default");
                coreDefs = ROLModelData.getModelDefinitionLayouts(coreDefNodes[i].ROLGetStringValues("model"));
                coreDefList.AddUniqueRange(coreDefs);
                ModelDefinitionVariantSet mdvs = getVariantSet(variantName);
                mdvs.addModels(coreDefs);
            }
            coreDefs = coreDefList.ToArray();

            coreModule = new ROLModelModule<ModuleROSolar>(part, this, getRootTransform("ModuleROSolar-CORE"), ModelOrientation.CENTRAL, nameof(currentCore), null, null, null);
            coreModule.name = "ModuleROSolar-Core";
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.getValidOptions = () => getVariantSet(currentVariant).definitions;

            coreModule.setupModelList(coreDefs);
            coreModule.setupModel();

            if (GameDatabase.Instance.GetConfigNode("ROSolar/TechLimits/ROSOLAR_CONFIG") is ConfigNode ROSconfig)
            {
                SolarTechLimit.Init(ROSconfig);
            }

            stl = SolarTechLimit.GetTechLevel(techLevel);

            UpdateModulePositions();
            UpdateAttachNodes(false);
            UpdateAvailableVariants();
            UpdateMassAndCost();
            RecalculateStats();
            ROLStockInterop.updatePartHighlighting(part);
        }

        /// <summary>
        /// Initialize the UI controls, including default values, and specifying delegates for their 'onClick' methods.<para/>
        /// All UI based interaction code will be defined/run through these delegates.
        /// </summary>
        public void InitializeUI()
        {
            ROLLog.debug($"{modName} - InitalizeUI() modelChangedAction");
            Action<ModuleROSolar> modelChangedAction = (m) =>
            {
                m.stl = SolarTechLimit.GetTechLevel(techLevel);
                m.UpdateModulePositions();
                m.UpdateAttachNodes(true);
                m.UpdateAvailableVariants();
                m.UpdateDragCubes();
                m.UpdateMassAndCost();
                m.RecalculateStats();
            };

            // Set up the core variant UI control
            string[] variantNames = ROLUtils.getNames(variantSets.Values, m => m.variantName);
            this.ROLupdateUIChooseOptionControl(nameof(currentVariant), variantNames, variantNames, true, currentVariant);
            Fields[nameof(currentVariant)].guiActiveEditor = variantSets.Count > 1;
            Fields[nameof(currentVariant)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                // Query the index from that variant set
                ModelDefinitionVariantSet prevMdvs = getVariantSet(coreModule.definition.name);
                // This is the index of the currently selected model within its variant set
                int previousIndex = prevMdvs.indexOf(coreModule.layoutOptions);
                // Grab ref to the current/new variant set
                ModelDefinitionVariantSet mdvs = getVariantSet(currentVariant);
                // And a reference to the model from same index out of the new set ([] call does validation internally for IAOOBE)
                ModelDefinitionLayoutOptions newCoreDef = mdvs[previousIndex];
                // Now, call model-selected on the core model to update for the changes, including symmetry counterpart updating.
                this.ROLactionWithSymmetry(m =>
                {
                    m.currentVariant = currentVariant;
                    m.coreModule.modelSelected(newCoreDef.definition.name);
                    lengthWidth = coreModule.definition.lengthWidth;
                    if (!lengthWidth)
                    {
                        Fields[nameof(panelLength)].guiActiveEditor = false;
                        Fields[nameof(panelWidth)].guiActiveEditor = false;
                        Fields[nameof(panelScale)].guiActiveEditor = true;
                        this.ROLupdateUIFloatEditControl(nameof(panelScale), 0.1f, 100f, largeStep, smallStep, slideStep, true, panelScale);
                    }
                    else
                    {
                        Fields[nameof(panelLength)].guiActiveEditor = true;
                        Fields[nameof(panelWidth)].guiActiveEditor = true;
                        Fields[nameof(panelScale)].guiActiveEditor = false;
                        this.ROLupdateUIFloatEditControl(nameof(panelLength), minLength, maxLength, largeStep, smallStep, slideStep, true, panelLength);
                        this.ROLupdateUIFloatEditControl(nameof(panelWidth), minWidth, maxWidth, largeStep, smallStep, slideStep, true, panelWidth);
                    }
                    modelChangedAction(m);
                });
            };

            Fields[nameof(currentCore)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                coreModule.modelSelected(a, b);
                lengthWidth = coreModule.definition.lengthWidth;
                if (!lengthWidth)
                {
                    Fields[nameof(panelLength)].guiActiveEditor = false;
                    Fields[nameof(panelWidth)].guiActiveEditor = false;
                    Fields[nameof(panelScale)].guiActiveEditor = true;
                    this.ROLupdateUIFloatEditControl(nameof(panelScale), 0.1f, 100f, largeStep, smallStep, slideStep, true, panelScale);
                }
                else
                {
                    Fields[nameof(panelLength)].guiActiveEditor = true;
                    Fields[nameof(panelWidth)].guiActiveEditor = true;
                    Fields[nameof(panelScale)].guiActiveEditor = false;
                    this.ROLupdateUIFloatEditControl(nameof(panelLength), minLength, maxLength, largeStep, smallStep, slideStep, true, panelLength);
                    this.ROLupdateUIFloatEditControl(nameof(panelWidth), minWidth, maxWidth, largeStep, smallStep, slideStep, true, panelWidth);
                }
                this.ROLactionWithSymmetry(modelChangedAction);
                ROLStockInterop.fireEditorUpdate();
            };

            Fields[nameof(panelLength)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.ROLactionWithSymmetry(m =>
                {
                    if (m != this) { m.panelLength = this.panelLength; }
                    modelChangedAction(m);
                    m.prevLength = m.panelLength;
                });
                ROLStockInterop.fireEditorUpdate();
            };

            Fields[nameof(panelWidth)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.ROLactionWithSymmetry(m =>
                {
                    if (m != this) { m.panelWidth = this.panelWidth; }
                    modelChangedAction(m);
                    m.prevWidth = m.panelWidth;
                });
                ROLStockInterop.fireEditorUpdate();
            };

            Fields[nameof(panelScale)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.ROLactionWithSymmetry(m =>
                {
                    if (m != this) { m.panelScale = this.panelScale; }
                    modelChangedAction(m);
                    m.prevScale = m.panelScale;
                });
                ROLStockInterop.fireEditorUpdate();
            };

            if (maxLength == minLength || !lengthWidth)
            {
                Fields[nameof(panelLength)].guiActiveEditor = false;
            }
            else
            {
                this.ROLupdateUIFloatEditControl(nameof(panelLength), minLength, maxLength, largeStep, smallStep, slideStep, true, panelLength);
            }

            if (maxWidth == minWidth || !lengthWidth)
            {
                Fields[nameof(panelWidth)].guiActiveEditor = false;
            }
            else
            {
                this.ROLupdateUIFloatEditControl(nameof(panelWidth), minWidth, maxWidth, largeStep, smallStep, slideStep, true, panelWidth);
            }

            if (lengthWidth)
            {
                Fields[nameof(panelScale)].guiActiveEditor = false;
            }
            else
            {
                Fields[nameof(panelScale)].guiActiveEditor = true;
                this.ROLupdateUIFloatEditControl(nameof(panelScale), 0.1f, 100f, largeStep, smallStep, slideStep, true, panelScale);
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }

            Fields[nameof(TechLevel)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                UpdateMassAndCost();
                RecalculateStats();
                this.ROLactionWithSymmetry(modelChangedAction);
                ROLStockInterop.fireEditorUpdate();
            };
        }

        private void SetMaxTechLevel()
        {
            ROLLog.debug("ModuleROSolar: SetMaxTechLevel() Start");
            ROLLog.debug($"SetMaxTechLevel() maxTechLevel: {maxTechLevel}");
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
            {
                ROLLog.debug("Game Mode is Not Career");
                maxTechLevel = 7;
                ROLLog.debug($"SetMaxTechLevel() maxTechLevel: {maxTechLevel}");
            }
            if (Fields[nameof(TechLevel)].uiControlEditor is UI_FloatRange tl)
            {
                ROLLog.debug("UI Control Editor Setting");
                tl.maxValue = maxTechLevel;
                ROLLog.debug($"SetMaxTechLevel() maxTechLevel: {maxTechLevel}");
            }
            if (HighLogic.LoadedSceneIsEditor && TechLevel < 0)
            {
                ROLLog.debug("Set TechLevel to MaxTechLevel");
                TechLevel = maxTechLevel;
                ROLLog.debug($"SetMaxTechLevel() maxTechLevel: {maxTechLevel}");
            }
            ROLLog.debug("ModuleROSolar: SetMaxTechLevel() Finish");
        }

        #endregion Custom Update Methods


        #region Custom Methods

        private void UpdateModulePositions()
        {
            float height, pos = 0.0f;
            if (fullScale)
            {
                ROLLog.debug("UpdateModulePositions() fullScale");
                coreModule.setScaleForDiameter(panelScale, 1);
                height = coreModule.moduleHeight;
                pos = height * 0.5f;
                coreModule.setPosition(pos);
                coreModule.updateModelMeshes();
            }
            else
            {
                ROLLog.debug("UpdateModulePositions()");
                lengthWidth = coreModule.definition.lengthWidth;
                ROLLog.debug($"lengthWidth: {lengthWidth}");
                if (lengthWidth)
                {
                    coreModule.setScaleForHeightAndDiameter(panelLength, panelWidth, lengthWidth);
                    height = coreModule.modulePanelLength;
                }
                else
                {
                    coreModule.setScaleForHeightAndDiameter(panelScale, panelScale, lengthWidth);
                    height = coreModule.moduleHeight;
                }
                pos = height * 0.5f;
                coreModule.setPosition(pos);
                coreModule.updateModelMeshes(lengthWidth);
            }

            ROLLog.debug("Setting the rotation information for the solar panel.");
            this.pivotName = coreModule.GetPivotName();
            this.panelRotationTransform = this.part.FindModelTransform(this.pivotName);
            this.originalRotation = this.currentRotation = this.panelRotationTransform.localRotation;
            this.secondaryTransformName = coreModule.GetSecondaryTransform();
        }

        private void UpdateMassAndCost()
        {
            lengthWidth = coreModule.definition.lengthWidth;
            if (!lengthWidth)
            {
                ROLLog.debug($"UpdateMassAndCost() lengthWidth false");
                area = coreModule.definition.panelArea * panelScale * panelScale;
                ROLLog.debug($"coreModule.definition.panelArea: {coreModule.definition.panelArea}");
                ROLLog.debug($"panelScale: {panelScale}");
                ROLLog.debug($"area: {area}");
            }
            else
            {
                ROLLog.debug($"UpdateMassAndCost() lengthWidth true");
                float lengthScale = panelLength / coreModule.definition.panelLength;
                float widthScale = panelWidth / coreModule.definition.panelWidth;
                area = coreModule.definition.panelArea * lengthScale * widthScale;
                ROLLog.debug($"coreModule.definition.panelArea: {coreModule.definition.panelArea}");
                ROLLog.debug($"lengthScale: {lengthScale}");
                ROLLog.debug($"widthScale: {widthScale}");
                ROLLog.debug($"area: {area}");
            }

            kgPerM2 = stl.kgPerM2;
            costPerM2 = stl.costPerM2;
            mass = area * kgPerM2;
            cost = area * costPerM2;
            switch (solarPanelType)
            {
                case "hinged":
                    mass *= stl.massMultHinged;
                    cost *= stl.costMultHinged;
                    break;
                case "folded":
                    mass *= stl.massMultFolded;
                    cost *= stl.costMultFolded;
                    break;
                case "tracking":
                    mass *= stl.massMultTrack;
                    cost *= stl.costMultTrack;
                    break;
                default:
                    break;
            }
            if (addMass > 0)
            {
                mass += addMass;
            }
            if (addCost > 0)
            {
                cost += addCost;
            }
            modifiedMass = mass = Math.Max(mass, 0.0001f);
            modifiedCost = cost = Math.Max(cost, 0.1f);
        }

        private void UpdateAttachNodes(bool userInput)
        {
            coreModule.updateAttachNodeBody(coreNodeNames, userInput);
            AttachNode surfaceNode = part.srfAttachNode;
            ROLLog.debug($"part.srfAttachNode: {part.srfAttachNode}");
            coreModule.updateSurfaceAttachNode(surfaceNode, panelLength, panelWidth, userInput);
        }

        /// <summary>
        /// Update the UI visibility for the currently available selections.<para/>
        /// Will hide/remove UI fields for slots with only a single option (models, textures, layouts).
        /// </summary>
        private void UpdateAvailableVariants()
        {
            coreModule.updateSelections();
        }

        /// <summary>
        /// Calls the generic ROT procedural drag-cube updating routines.  Will update the drag cubes for whatever the current model state is.
        /// </summary>
        private void UpdateDragCubes()
        {
            ROLModInterop.onPartGeometryUpdate(part, true);
        }

        /// <summary>
        /// Return the root transform for the specified name.  If does not exist, will create it and parent it to the parts' 'model' transform.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="recreate"></param>
        /// <returns></returns>
        private Transform getRootTransform(string name)
        {
            Transform root = part.transform.ROLFindRecursive(name);
            if (root != null)
            {
                GameObject.DestroyImmediate(root.gameObject);
                root = null;
            }
            root = new GameObject(name).transform;
            root.NestToParent(part.transform.ROLFindRecursive("model"));
            return root;
        }

        private ROLModelModule<ModuleROSolar> getModuleByName(string name)
        {
            return coreModule;
        }

        public void RecalculateStats()
        {
            kwPerM2 = stl.kwPerM2;
            this.chargeRate = currentRate = area * kwPerM2;

            var outResource = resHandler.outputResources.First(x => x.id == PartResourceLibrary.ElectricityHashcode);
            outResource.rate = (double)this.chargeRate;

            this.timeEfficCurve.ROLloadSingleLine(stl.key1);
            this.timeEfficCurve.ROLloadSingleLine(stl.key20);
            this.timeEfficCurve.ROLloadSingleLine(stl.key80);
            this.timeEfficCurve.ROLloadSingleLine(stl.key99);
        }

        #endregion Custom Methods

        #region Model Definition Variants

        /// <summary>
        /// Data storage for a group of model definitions that share the same 'variant' type.  Used by modular-part in variant-defined configurations.
        /// </summary>
        public class ModelDefinitionVariantSet
        {
            public readonly string variantName;

            public ModelDefinitionLayoutOptions[] definitions = new ModelDefinitionLayoutOptions[0];

            public ModelDefinitionLayoutOptions this[int index]
            {
                get
                {
                    if (index < 0) { index = 0; }
                    if (index >= definitions.Length) { index = definitions.Length - 1; }
                    return definitions[index];
                }
            }

            public ModelDefinitionVariantSet(string name)
            {
                this.variantName = name;
            }

            public void addModels(ModelDefinitionLayoutOptions[] defs)
            {
                List<ModelDefinitionLayoutOptions> allDefs = new List<ModelDefinitionLayoutOptions>();
                allDefs.AddRange(definitions);
                allDefs.AddUniqueRange(defs);
                definitions = allDefs.ToArray();
            }

            public int indexOf(ModelDefinitionLayoutOptions def)
            {
                return definitions.IndexOf(def);
            }
        }

        #endregion Model Definition Variants
    }
}
