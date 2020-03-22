using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ROLib
{
    public class ModuleROSolar : ModuleDeployableSolarPanel, IPartCostModifier, IPartMassModifier
    {
        public const string GroupName = "ROSolarGroup";
        public const string GroupDisplayName = "RO-Solar";

        #region KSPFields

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Variant", groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentVariant = "Default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Design", groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCore = "Mount-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Panel Length", guiFormat = "N3", guiUnits = "m", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float panelLength = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Panel Width", guiFormat = "N3", guiUnits = "m", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float panelWidth = 1.0f;

        [KSPField(isPersistant = true, guiName = "Panel Scale", guiFormat = "N3", guiUnits = "x", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float panelScale = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Tech Level", guiFormat = "N0", groupName = GroupName),
        UI_FloatRange(minValue = 0f, stepIncrement = 1f, scene = UI_Scene.Editor, suppressEditorShipModified = true)]
        public float TechLevel = -1f;
        public int techLevel => Convert.ToInt32(TechLevel);

        [KSPField(guiName = "Tech Level", guiFormat = "N0", groupName = GroupName)]
        public int tlText = 0;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Charge Rate (1AU)", guiFormat = "F4", guiUnits = " kW", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public float currentRate = 0.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Area", guiFormat = "F4", guiUnits = " m^2", groupName = GroupName)]
        public float area = 0.0f;

        [KSPField(guiActiveEditor = true, guiName = "Mass", guiFormat = "F4", guiUnits = " m", groupName = GroupName)]
        public float mass = 0.0f;

        [KSPField(guiActiveEditor = true, guiName = "Cost", guiFormat = "F1", groupName = GroupName)]
        public float cost = 0.0f;

        [KSPEvent(guiActiveEditor = true, guiName = "Reset Model to Original", groupName = GroupName)]
        public void ResetModel()
        {
            _ResetModel();
            foreach (Part p in part.symmetryCounterparts)
            {
                if (p.FindModuleImplementing<ModuleROSolar>() is ModuleROSolar m)
                    m._ResetModel();
            }
        }

        private void _ResetModel()
        {
            panelLength = coreModule.definition.panelLength;
            panelWidth = coreModule.definition.panelWidth;
            panelScale = 1.0f;
            this.ROLupdateUIFloatEditControl(nameof(panelLength), minLength, maxLength, largeStep, smallStep, slideStep, true, panelLength);
            this.ROLupdateUIFloatEditControl(nameof(panelWidth), minWidth, maxWidth, largeStep, smallStep, slideStep, true, panelWidth);
            this.ROLupdateUIFloatEditControl(nameof(panelScale), 0.1f, 100f, largeStep, smallStep, slideStep, true, panelScale);
            ModelChangedHandler(true);
            prevLength = panelLength;
            prevWidth = panelWidth;
            prevScale = panelScale;
        }

        [KSPField] public string solarPanelType = "static";
        [KSPField] public float largeStep = 1.0f;
        [KSPField] public float smallStep = 0.1f;
        [KSPField] public float slideStep = 0.001f;
        [KSPField] public float minWidth = 0.1f;
        [KSPField] public float maxWidth = 100.0f;
        [KSPField] public float minLength = 0.1f;
        [KSPField] public float maxLength = 100.0f;

        [KSPField] public string solarCore = "Mount-None";
        [KSPField] public float addMass = 0.0f;
        [KSPField] public float addCost = 0.0f;
        [KSPField] public string coreManagedNodes = string.Empty;
        [KSPField] public bool fullScale = true;

        #endregion KSPFields

        #region Custom Fields

        private const string modName = "[ROSOlar]";

        public int maxTechLevel = 0;


        // Previous length/width/scale values for change detection
        private float prevLength = -1;
        private float prevWidth = -1;
        private float prevScale = -1;

        /// <summary>
        /// Standard work-around for lack of config-node data being passed consistently and lack of support for mod-added serializable classes.
        /// </summary>
        [Persistent]
        public string configNodeData = string.Empty;

        /// <summary>
        /// Has initialization been run?  Set to true the first time init methods are run (OnLoad/OnStart), and ensures that init is only run a single time.
        /// </summary>
        private bool initialized = false;

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
                configNodeData = node.ToString();
            }
            Initialize();
            // OnStart() appears to be too late for setting the TimeEfficCurve for Kerbalism.
            // SolarPanelFixer is possibly getting this field too soon.
            stl = SolarTechLimit.GetTechLevel(techLevel);
            ReloadTimeCurve();
        }

        public override void OnStart(StartState state)
        {
            Debug.Log($"{modName}: {part} OnStart({state})");
            SetMaxTechLevel();
            Initialize();
            ModelChangedHandler(false);
            base.OnStart(state);
            InitializeUI();
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(OnEditorVesselModified);
        }

        private void OnEditorVesselModified(ShipConstruct ship)
        {
            //update available variants for attach node changes
            UpdateAvailableVariants();
        }

        // IPartMass/CostModifier overrides
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => Mathf.Max(mass, 0.0001f);
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => Mathf.Max(cost, 0);

        #endregion Standard KSP Overrides

        #region Custom Update Methods

        /// <summary>
        /// Initialization method.  Sets up model modules, loads their configs from the input config node.  Does all initial linking of part-modules.<para/>
        /// Does NOT set up their UI interaction -- that is all handled during OnStart()
        /// </summary>
        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            prevLength = panelLength;
            prevWidth = panelWidth;
            prevScale = panelScale;

            coreNodeNames = ROLUtils.parseCSV(coreManagedNodes);

            // Model-Module Setup / Initialization
            ConfigNode node = ROLConfigNodeUtils.parseConfigNode(configNodeData);

            // List of CORE model nodes from config
            // each one may contain multiple 'model=modelDefinitionName' entries
            // but must contain no more than a single 'variant' entry.
            // If no variant is specified, they are added to the 'Default' variant.
            ConfigNode[] coreDefNodes = node.GetNodes("CORE");

            List<ModelDefinitionLayoutOptions> coreDefList = new List<ModelDefinitionLayoutOptions>();
            foreach (ConfigNode cn in coreDefNodes)
            {
                string variantName = cn.ROLGetStringValue("variant", "Default");
                coreDefs = ROLModelData.getModelDefinitionLayouts(cn.ROLGetStringValues("model"));
                coreDefList.AddUniqueRange(coreDefs);
                ModelDefinitionVariantSet mdvs = getVariantSet(variantName);
                mdvs.addModels(coreDefs);
            }
            coreDefs = coreDefList.ToArray();

            coreModule = new ROLModelModule<ModuleROSolar>(part, this, GetRootTransform("ModuleROSolar-CORE"), ModelOrientation.CENTRAL, nameof(currentCore), null, null, null);
            coreModule.name = "ModuleROSolar-Core";
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.getValidOptions = () => getVariantSet(currentVariant).definitions;

            coreModule.setupModelList(coreDefs);
            coreModule.setupModel();

            UpdateModulePositions();
        }

        internal void ModelChangedHandler(bool pushNodes)
        {
            stl = SolarTechLimit.GetTechLevel(techLevel);
            retractable = stl.retractable && solarPanelType != "static";
            UpdateModulePositions();
            startFSM();
            UpdateAttachNodes(pushNodes);
            UpdateAvailableVariants();
            UpdateDragCubes();
            UpdateMassAndCost();
            RecalculateStats();
            ROLStockInterop.updatePartHighlighting(part);
            if (HighLogic.LoadedSceneIsEditor)
                ROLStockInterop.fireEditorUpdate();
        }

        internal void ModelChangedHandlerWithSymmetry(bool pushNodes, bool symmetry)
        {
            ModelChangedHandler(pushNodes);
            if (symmetry)
            {
                foreach (Part p in part.symmetryCounterparts.Where(x => x != part))
                {
                    p.FindModuleImplementing<ModuleROSolar>().ModelChangedHandler(pushNodes);
                }
            }
        }

        /// <summary>
        /// Initialize the UI controls, including default values, and specifying delegates for their 'onClick' methods.<para/>
        /// All UI based interaction code will be defined/run through these delegates.
        /// </summary>
        public void InitializeUI()
        {
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
                    m.coreModule.modelSelected(newCoreDef.definition.name);
                    lengthWidth = coreModule.definition.lengthWidth;
                    Fields[nameof(panelLength)].guiActiveEditor = lengthWidth;
                    Fields[nameof(panelWidth)].guiActiveEditor = lengthWidth;
                    Fields[nameof(panelScale)].guiActiveEditor = !lengthWidth;
                    if (!lengthWidth)
                    {
                        this.ROLupdateUIFloatEditControl(nameof(panelScale), 0.1f, 100f, largeStep, smallStep, slideStep, true, panelScale);
                    }
                    else
                    {
                        this.ROLupdateUIFloatEditControl(nameof(panelLength), minLength, maxLength, largeStep, smallStep, slideStep, true, panelLength);
                        this.ROLupdateUIFloatEditControl(nameof(panelWidth), minWidth, maxWidth, largeStep, smallStep, slideStep, true, panelWidth);
                    }
                    m.ResetModel();
                });
                ModelChangedHandlerWithSymmetry(true, true);
            };

            Fields[nameof(currentCore)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                coreModule.modelSelected(a, b);
                lengthWidth = coreModule.definition.lengthWidth;
                Fields[nameof(panelLength)].guiActiveEditor = lengthWidth;
                Fields[nameof(panelWidth)].guiActiveEditor = lengthWidth;
                Fields[nameof(panelScale)].guiActiveEditor = !lengthWidth;
                if (!lengthWidth)
                {
                    this.ROLupdateUIFloatEditControl(nameof(panelScale), 0.1f, 100f, largeStep, smallStep, slideStep, true, panelScale);
                }
                else
                {
                    this.ROLupdateUIFloatEditControl(nameof(panelLength), minLength, maxLength, largeStep, smallStep, slideStep, true, panelLength);
                    this.ROLupdateUIFloatEditControl(nameof(panelWidth), minWidth, maxWidth, largeStep, smallStep, slideStep, true, panelWidth);
                }
                ModelChangedHandlerWithSymmetry(true, true);
            };

            Fields[nameof(panelLength)].uiControlEditor.onFieldChanged =
            Fields[nameof(panelLength)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                if ((float) a.GetValue(this) != prevLength)
                {
                    ModelChangedHandler(true);
                    prevLength = panelLength;
                }
            };

            Fields[nameof(panelWidth)].uiControlEditor.onFieldChanged =
            Fields[nameof(panelWidth)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                if ((float) a.GetValue(this) != prevWidth)
                {
                    ModelChangedHandler(true);
                    prevWidth = panelWidth;
                }
            };

            Fields[nameof(panelScale)].uiControlEditor.onFieldChanged =
            Fields[nameof(panelScale)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                if ((float)a.GetValue(this) != prevScale)
                {
                    ModelChangedHandler(true);
                    prevScale = panelScale;
                }
            };

            Fields[nameof(panelScale)].guiActiveEditor = !lengthWidth;
            if (!lengthWidth)
                this.ROLupdateUIFloatEditControl(nameof(panelScale), 0.1f, 100f, largeStep, smallStep, slideStep, true, panelScale);

            Fields[nameof(TechLevel)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                ModelChangedHandlerWithSymmetry(true, true);
            };

            if (maxLength == minLength || !lengthWidth)
                Fields[nameof(panelLength)].guiActiveEditor = false;
            else
                this.ROLupdateUIFloatEditControl(nameof(panelLength), minLength, maxLength, largeStep, smallStep, slideStep, true, panelLength);

            if (maxWidth == minWidth || !lengthWidth)
                Fields[nameof(panelWidth)].guiActiveEditor = false;
            else
                this.ROLupdateUIFloatEditControl(nameof(panelWidth), minWidth, maxWidth, largeStep, smallStep, slideStep, true, panelWidth);

            if (HighLogic.LoadedSceneIsEditor)
                GameEvents.onEditorShipModified.Add(OnEditorVesselModified);
        }

        private void SetMaxTechLevel()
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
                maxTechLevel = 7;

            (Fields[nameof(TechLevel)].uiControlEditor as UI_FloatRange).maxValue = maxTechLevel;
            if (HighLogic.LoadedSceneIsEditor && TechLevel < 0)
            {
                TechLevel = maxTechLevel;
            }
            if (maxTechLevel == 0)
            {
                Fields[nameof(TechLevel)].guiActiveEditor = false;
                Fields[nameof(tlText)].guiActiveEditor = true;
            }
        }

        private void UpdateModulePositions()
        {
            lengthWidth = coreModule.definition.lengthWidth;
            float height = lengthWidth ? panelLength : coreModule.moduleHeight;
            if (lengthWidth)
                coreModule.setScaleForHeightAndDiameter(panelLength, panelWidth, lengthWidth);
            else
                coreModule.setScaleForHeightAndDiameter(panelScale, panelScale, lengthWidth);

            coreModule.setPosition(height / 2);
            coreModule.updateModelMeshes(lengthWidth);

            /*
            if (fullScale)
            {
                ROLLog.debug("UpdateModulePositions() fullScale");
                float currentDiameter = coreModule.definition.diameter * panelScale;
                coreModule.setScaleForDiameter(currentDiameter, 1);
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
            */

            animationName = coreModule.definition.animationName;
            FindAnimations();

            // Allow StartFSM() to configure the animation state (time/speed/weight)
            // Change handler will need to re-call StartFSM() to properly reset a model.

            if (pivotName.Equals("sunPivot"))
                hasPivot = false;

            pivotName = coreModule.GetPivotName();
            panelRotationTransform = part.FindModelTransform(pivotName);
            originalRotation = currentRotation = panelRotationTransform.localRotation;
            secondaryTransformName = raycastTransformName = coreModule.GetSecondaryTransform();
        }

        private void UpdateMassAndCost()
        {
            lengthWidth = coreModule.definition.lengthWidth;
            string s;
            if (!lengthWidth)
            {
                area = coreModule.definition.panelArea * panelScale * panelScale;
                s = $"panelScale: {panelScale:F2}";
            }
            else
            {
                float lengthScale = panelLength / coreModule.definition.panelLength;
                float widthScale = panelWidth / coreModule.definition.panelWidth;
                area = coreModule.definition.panelArea * lengthScale * widthScale;
                s = $"lengthScale: {lengthScale:F2} widthScale: {widthScale:F2}";
            }
            //Debug.Log($"{modName}: {part} UpdateMassAndCost() Area: {area:F2} from panelArea: {coreModule.definition.panelArea:F2} {s}");

            mass = area * stl.kgPerM2;
            cost = area * stl.costPerM2;
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
            mass += addMass;
            cost += addCost;
            mass = Math.Max(mass, 0.0001f);
            cost = Math.Max(cost, 0.1f);
        }

        private void UpdateAttachNodes(bool userInput)
        {
            coreModule.updateAttachNodeBody(coreNodeNames, userInput);
            coreModule.updateSurfaceAttachNode(part.srfAttachNode, panelLength, panelWidth, userInput);
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

        #endregion Custom Update Methods

        #region Custom Methods

        /// <summary>
        /// Return the root transform for the specified name.  If does not exist, will create it and parent it to the parts' 'model' transform.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="recreate"></param>
        /// <returns></returns>
        private Transform GetRootTransform(string name)
        {
            // This code used to always destroy the root transform no matter what it was, and then make a new.
            // Doing what the description says instead...
            if (part.transform.ROLFindRecursive(name) is Transform t)
                return t;
            Transform root = new GameObject(name).transform;
            root.NestToParent(part.transform.ROLFindRecursive("model"));
            return root;
        }

        private ROLModelModule<ModuleROSolar> getModuleByName(string name) => coreModule;

        public void RecalculateStats()
        {
            chargeRate = currentRate = area * stl.kwPerM2;
            var outResource = resHandler.outputResources.First(x => x.id == PartResourceLibrary.ElectricityHashcode);
            outResource.rate = chargeRate;

            ReloadTimeCurve();
        }

        public void ReloadTimeCurve()
        {
            timeEfficCurve = new FloatCurve();
            timeEfficCurve.ROLloadSingleLine(stl.key1);
            timeEfficCurve.ROLloadSingleLine(stl.key20);
            timeEfficCurve.ROLloadSingleLine(stl.key80);
            timeEfficCurve.ROLloadSingleLine(stl.key99);
        }

        private void FindAnimations()
        {
            if (animationName.Equals("fakeAnimation"))
            {
                anim = null;
                useAnimation = false;
                return;
            }

            Animation[] componentsInChildren = part.transform.ROLFindRecursive("model").GetComponentsInChildren<Animation>();
            foreach (Animation a in componentsInChildren)
            {
                if (a.GetClip(animationName) != null)
                    anim = a;
            }
            if (componentsInChildren.Length > 0 && anim == null)
                anim = componentsInChildren[0];
            useAnimation = anim != null;
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
