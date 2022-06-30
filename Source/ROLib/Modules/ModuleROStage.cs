using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPShaderTools;
using System.Diagnostics.CodeAnalysis;

namespace ROLib
{
    public class ModuleROStage : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
    {
        private const string GroupDisplayName = "RO-Stages";
        private const string GroupName = "ModuleROStage";
        private const float MinModelRatio = 0.5f;
        private const float MaxModelRatio = 8;

        /// <summary>
        /// This module is created so the player creates a contiguous stage from Interstage to Engine Mount (usually).
        /// Component Parts:
        ///     CORE: Bulkhead (Separate or Common Bulkhead) -> Maybe instead called Tank Design??
        ///         That could allow for the player choosing much different tank layouts like
        ///         Separate, Common Bulkhead, Multiple Cylinders, Multiple Spheres (think something like Fregat),
        ///         Soyuz
        ///     TOP: Top (usually an adapter or nosecone)
        ///     BOTTOM: Bottom (usually an engine mount)
        ///     UPPER: Upper Tank
        ///     LOWER: Lower Tank
        ///     UPPER-DOME: Upper Tank Dome Style
        ///     LOWER-DOME: Lower Tank Dome Style
        ///     TOP-STRINGERS: Stringers, Smooth, None
        ///     BOTTOM-STRINGERS: Stringers, Smooth, None
        ///     RACEWAY: Option for models that will be raceways radially
        ///     RADIAL: Option for tanks that can be added radially (like Pressurant or RCS Tanks)
        /// </summary>

        /*****************************************************************************************
            FEATURES TO ADD
                * Calculate the avionics "tank" size available from the voids in the tank designs
                * Structure Length separated from tank length (can build in extra room)
                * Avionics tank that works with ProcAvionics and doesn't force the entire tank
                    to be SM
                * Radially added tanks can only be HP and used for RCS and pressurant
                * Instead of relying on RF to do tank calculations, allow the options in
                    in RO-Stages.
                    * Choice of Tank Type (Isogrid, Separate, SM, Balloon).
                    * Choice of Materials (Steel, Aluminum, AlCu, etc)
                    * Mass multipliers and cost multipliers will exist for each choice
                    * Tech unlock system similar to how RealAntennas and ROSolar work to simulate
                        materials improvements (allowing for the different materials)
        *****************************************************************************************/

        #region KSPFields

        [KSPField] public float diameterLargeStep = 0.1f;
        [KSPField] public float diameterSmallStep = 0.1f;
        [KSPField] public float diameterSlideStep = 0.001f;
        [KSPField] public float minDiameter = 0.1f;
        [KSPField] public float maxDiameter = 100.0f;
        [KSPField] public float minLength = 0.1f;
        [KSPField] public float maxLength = 100.0f;
        [KSPField] public float minStringers = 0.0f;
        [KSPField] public float maxStringers = 100.0f;
        [KSPField] public float upperTankLength = 0f;
        [KSPField] public float lowerTankLength = 0f;
        [KSPField] public float bulkheadLength = 0f;
        [KSPField] public float totalStructureLength = 0.0f;    // TO-DO: Convert to Player Option
        [KSPField] public int numberOfTanks = 0;                // TO-DO: Convert to Player Option

        [KSPField] public float avionicsContainerMinPercent = 0f;
        [KSPField] public float avionicsContainerMaxPercent = 0f;

        [KSPField] public string bodyManagedNodes = string.Empty;
        [KSPField] public string upperManagedNodes = string.Empty;  // top of the tank
        [KSPField] public string lowerManagedNodes = string.Empty;  // bottom of the tank
        [KSPField] public string topManagedNodes = string.Empty;    // top module
        [KSPField] public string bottomManagedNodes = string.Empty; // bottom module

        [KSPField] public string upperAddedNode = "upperNode"; // top of tank structure
        [KSPField] public string lowerAddedNode = "lowerNode"; // bottom of tank structure

        [KSPField] public string tankDesignTypes = "Separate-Bulkhead, Common-Bulkhead, Cylinder-Cluster, Sphere-Culster, Soyuz";
        [KSPField] public string domeTypes = "Half, Full";
        [KSPField] public string upperParentOptions = "Tank, Dome, Top";
        [KSPField] public string lowerParentOptions = "Tank, Dome, Bottom";

        [KSPField] public int topFairingIndex = -1;
        [KSPField] public int bottomFairingIndex = -1;

        // TO-DO: Add radial parent options for the different sections of the part

        [KSPField] public bool validateTop = false;
        [KSPField] public bool validateBottom = false;
        [KSPField] public bool topCanRotate = false;
        [KSPField] public bool bottomCanRotate = false;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Diameter", guiUnits = "m", groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentDiameter = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Structure Length", guiUnits = "m", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentStructureLength = 1.0f;

        [KSPField(isPersistant = true, guiName = "Top V.Scale", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = -1, maxValue = 1, incrementLarge = 0.25f, incrementSmall = 0.05f, incrementSlide = 0.001f)]
        public float currentTopVScale = 0f;

        [KSPField(isPersistant = true, guiName = "Bottom V.Scale", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = -1, maxValue = 1, incrementLarge = 0.25f, incrementSmall = 0.05f, incrementSlide = 0.001f)]
        public float currentBottomVScale = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Top Rot.", groupName = GroupName),
         UI_FloatEdit(sigFigs = 0, suppressEditorShipModified = true, minValue = -180f, maxValue = 180f, incrementLarge = 45f, incrementSmall = 15f, incrementSlide = 1f)]
        public float currentTopRotation = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Bottom Rot.", groupName = GroupName),
         UI_FloatEdit(sigFigs = 0, suppressEditorShipModified = true, minValue = -180f, maxValue = 180f, incrementLarge = 45f, incrementSmall = 15f, incrementSlide = 1f)]
        public float currentBottomRotation = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Avionics %", guiUnits = "%", groupName = GroupName),
         UI_FloatEdit(sigFigs = 0, suppressEditorShipModified = true, minValue = 0f, maxValue = 90f, incrementLarge = 15f, incrementSmall = 5f, incrementSlide = 1f)]
        public float currentAvionics = 0f;

        //------------------------------------------MODEL SELECTION SET PERSISTENCE-----------------------------------------------//

        [KSPField(isPersistant = true, guiName = "Variant", guiActiveEditor = true, groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentVariant = "Default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Tank Design", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBody = "Separate-Bulkhead";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Top", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTop = "None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Bottom", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBottom = "None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Dome Type", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentDome = "Half";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Stringers", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperStringers = "None";

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Up.Stringer Length", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentUpperStringersLength = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Stringers", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerStringers = "None";

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Low.Stringers Length", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentLowerStringersLength = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Body Radial", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBodyRadial = "None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Body V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentBodyRadialVOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Body H.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentBodyRadialHOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Radial", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRadial = "None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Parent", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRadialParent = "Tank";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Top V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentUpperRadialVOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Top H.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentUpperRadialHOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Radial", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRadial = "None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Parent", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRadialParent = "Tank";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Bottom V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentLowerRadialVOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Bottom H.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentLowerRadialHOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Raceway", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentRaceway = "None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Raceway V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentRacewayVOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Raceway H.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentRacewayHOffset = 0f;

        [KSPField(isPersistant = true, guiName = "Raceway V.Scale", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = -1, maxValue = 1, incrementLarge = 0.25f, incrementSmall = 0.05f, incrementSlide = 0.001f)]
        public float currentRacewayVScale = 0f;

        [KSPField(isPersistant = true, guiName = "Raceway H.Scale", groupName = GroupName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = -1, maxValue = 1, incrementLarge = 0.25f, incrementSmall = 0.05f, incrementSlide = 0.001f)]
        public float currentRacewayHScale = 0f;

        //------------------------------------------TEXTURE SET PERSISTENCE-----------------------------------------------//

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Tank Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTankTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Top Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTopTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Dome Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperDomeTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Stringers Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperStringersTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Dome Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerDomeTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Stringers Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerStringersTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Bottom Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBottomTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Body Radial Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBodyRadialTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Radial Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRadialTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Radial Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRadialTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Raceway Tex", groupName = GroupName),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentRacewayTexture = "default";

        //------------------------------------------RECOLORING PERSISTENCE-----------------------------------------------//

        [KSPField(isPersistant = true)] public string bodyModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string topModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string bottomModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string upperDomeModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string upperStringersModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string lowerDomeModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string lowerStringersModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string bodyRadialModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string upperRadialModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string lowerRadialModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)] public string racewayModulePersistentData = string.Empty;

        #endregion KSPFields


        #region Private Variables

        [Persistent] public string configNodeData = string.Empty;
        private bool initialized = false;
        private float modifiedMass = -1;
        private float modifiedCost = -1;
        private float prevDiameter = -1;
        private float prevUpperTankLength = -1;
        private float prevLowerTankLength = -1;
        private float prevTopLength = -1;
        private float prevUpperDomeLength = -1;
        private float prevUpperStringersLength = -1;
        private float prevLowerDomeLength = -1;
        private float prevLowerStringersLength = -1;
        private float prevBottomLength = -1;

        private float bodyRadialRadius = -1;
        private float upperRadialRadius = -1;
        private float lowerRadialRadius = -1;
        private float racewayRadius = -1;

        private bool enableTopVScale = false;
        private bool enableBottomVScale = false;
        private bool hasUpperStringers = false;
        private bool hasLowerStringers = false;
        private bool hasBodyRadial = false;
        private bool hasUpperRadial = false;
        private bool hasLowerRadial = false;
        private bool hasRaceway = false;

        private string[] topNodeNames;
        private string[] bodyNodeNames;
        private string[] bottomNodeNames;

        internal ROLModelModule<ModuleROStage> bodyModule;
        internal ROLModelModule<ModuleROStage> topModule;
        internal ROLModelModule<ModuleROStage> upperStringersModule;
        internal ROLModelModule<ModuleROStage> upperDomeModule;
        internal ROLModelModule<ModuleROStage> upperTankModule;
        internal ROLModelModule<ModuleROStage> lowerTankModule;
        internal ROLModelModule<ModuleROStage> lowerDomeModule;
        internal ROLModelModule<ModuleROStage> lowerStringersModule;
        internal ROLModelModule<ModuleROStage> bottomModule;
        internal ROLModelModule<ModuleROStage> bodyRadialModule;
        internal ROLModelModule<ModuleROStage> upperRadialModule;
        internal ROLModelModule<ModuleROStage> lowerRadialModule;
        internal ROLModelModule<ModuleROStage> racewayModule;

        private List<ROLModelModule<ModuleROStage>> moduleList = new List<ROLModelModule<ModuleROStage>>();
        private ROLModelModule<ModuleROStage>[] allModules;

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
        // TO-DO: public const KeyCode hoverPresetKeyCode = KeyCode.N;

        private ModelDefinitionVariantSet GetVariantSet(ModelDefinitionLayoutOptions def) =>
            variantSets.Values.FirstOrDefault(a => a.definitions.Contains(def));

        internal ModelDefinitionLayoutOptions[] bodyDefs;
        internal ModelDefinitionLayoutOptions[] topDefs;
        internal ModelDefinitionLayoutOptions[] bottomDefs;
        internal ModelDefinitionLayoutOptions[] domeDefs;
        internal ModelDefinitionLayoutOptions[] stringersDefs;
        internal ModelDefinitionLayoutOptions[] radialDefs;
        internal ModelDefinitionLayoutOptions[] racewayDefs;

        #endregion Private Variables


        #region Model Handling

        internal void ModelChangedHandler(bool pushNodes)
        {
            if (validateTop || validateBottom) ValidateModules();
            ValidateLength();
            ValidateRotation();
            UpdateModulePositions();
            UpdateTankVolume();
            UpdateDimensions();
            UpdateModelMeshes();
            ScaleRadialModels();
            UpdateAttachNodes(pushNodes);
            UpdateAvailableVariants();
            SetPreviousModuleLength();
            UpdateDragCubes();
            UpdateMass();
            UpdateCost();
            ROLStockInterop.UpdatePartHighlighting(part);
            if (HighLogic.LoadedSceneIsEditor)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        internal void ModelChangedHandlerWithSymmetry(bool pushNodes, bool symmetry)
        {
            ModelChangedHandler(pushNodes);
            if (symmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.FindModuleImplementing<ModuleROStage>().ModelChangedHandler(pushNodes);
                }
            }
        }

        #endregion Model Handling


        #region KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            Initialize();
            UpdateModulePositions();
            UpdateDimensions();
            UpdateModelMeshes();
            ScaleRadialModels();
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
                GameEvents.onEditorShipModified.Remove(OnEditorVesselModified);
                // TO-DO: GameEvents.onPartActionUIDismiss.Remove(OnPawClose);
            }
        }

        private void OnEditorVesselModified(ShipConstruct ship) => UpdateAvailableVariants();

        // IPartMass / CostModifier overrides
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => Mathf.Max(0, modifiedMass);
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => Mathf.Max(0, modifiedCost);

        #endregion KSP Overrides


        #region IRecolorable Overrides

        public string[] getSectionNames() => new string[]
            { "Tanks", "Top", "Upper Dome", "Upper Stringers", "Lower Dome", "Lower Stringers", "Bottom", "Body Radial", "Upper Radial", "Lower Radial", "Raceway"};

        public RecoloringData[] getSectionColors(string section)
        {
            return section switch
            {
                "Tanks" => bodyModule.recoloringData,
                "Top" => topModule.recoloringData,
                "Upper Dome" => upperDomeModule.recoloringData,
                "Upper Stringers" => upperStringersModule.recoloringData,
                "Lower Dome" => lowerDomeModule.recoloringData,
                "Lower Stringers" => lowerStringersModule.recoloringData,
                "Bottom" => bottomModule.recoloringData,
                "Body Radial" => bodyRadialModule.recoloringData,
                "Upper Radial" => upperRadialModule.recoloringData,
                "Bottom Radial" => lowerRadialModule.recoloringData,
                "Raceway" => racewayModule.recoloringData,
                _ => bodyModule.recoloringData,
            };
        }

        public void setSectionColors(string section, RecoloringData[] colors)
        {
            if (section == "Tanks")
            {
                bodyModule.setSectionColors(colors);
                upperTankModule.setSectionColors(colors);
                lowerTankModule.setSectionColors(colors);
            }
            else if (section == "Top") topModule.setSectionColors(colors);
            else if (section == "Upper Dome") upperDomeModule.setSectionColors(colors);
            else if (section == "Upper Stringers") upperStringersModule.setSectionColors(colors);
            else if (section == "Lower Dome") lowerDomeModule.setSectionColors(colors);
            else if (section == "Lower Stringers") lowerStringersModule.setSectionColors(colors);
            else if (section == "Bottom") bottomModule.setSectionColors(colors);
            else if (section == "Body Radial") bodyRadialModule.setSectionColors(colors);
            else if (section == "Upper Radial") upperRadialModule.setSectionColors(colors);
            else if (section == "Lower Radial") lowerRadialModule.setSectionColors(colors);
            else if (section == "Raceway") racewayModule.setSectionColors(colors);
        }

        public TextureSet getSectionTexture(string section)
        {
            return section switch
            {
                "Tanks" => bodyModule.textureSet,
                "Top" => topModule.textureSet,
                "Upper Dome" => upperDomeModule.textureSet,
                "Upper Stringers" => upperStringersModule.textureSet,
                "Lower Dome" => lowerDomeModule.textureSet,
                "Lower Stringers" => lowerStringersModule.textureSet,
                "Bottom" => bottomModule.textureSet,
                "Body Radial" => bodyRadialModule.textureSet,
                "Upper Radial" => upperRadialModule.textureSet,
                "Bottom Radial" => lowerRadialModule.textureSet,
                "Raceway" => racewayModule.textureSet,
                _ => bodyModule.textureSet,
            };
        }

        #endregion IRecolorable Overrides


        #region Initialization

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            topNodeNames = ROLUtils.parseCSV(topManagedNodes);
            bodyNodeNames = ROLUtils.parseCSV(bodyManagedNodes);
            bottomNodeNames = ROLUtils.parseCSV(bottomManagedNodes);

            //model-module setup/initialization
            ConfigNode node = ROLConfigNodeUtils.ParseConfigNode(configNodeData);

            //list of CORE model nodes from config
            //each one may contain multiple 'model=modelDefinitionName' entries
            //but must contain no more than a single 'variant' entry.
            //if no variant is specified, they are added to the 'Default' variant.
            List<ModelDefinitionLayoutOptions> bodyDefList = new List<ModelDefinitionLayoutOptions>();
            foreach (ConfigNode n in node.GetNodes("BODY"))
            {
                string variantName = n.ROLGetStringValue("variant", "Default");
                bodyDefs = ROLModelData.getModelDefinitionLayouts(n.ROLGetStringValues("model"));
                bodyDefList.AddUniqueRange(bodyDefs);
                ModelDefinitionVariantSet mdvs = GetVariantSet(variantName);
                mdvs.AddModels(bodyDefs);
            }
            bodyDefs = bodyDefList.ToArray();

            topDefs = ROLModelData.getModelDefinitions(node.GetNodes("TOP"));
            bottomDefs = ROLModelData.getModelDefinitions(node.GetNodes("BOTTOM"));
            domeDefs = ROLModelData.getModelDefinitions(node.GetNodes("DOME"));
            stringersDefs = ROLModelData.getModelDefinitions(node.GetNodes("STRINGERS"));
            radialDefs = ROLModelData.getModelDefinitions(node.GetNodes("RADIAL"));
            racewayDefs = ROLModelData.getModelDefinitions(node.GetNodes("RACEWAY"));

            bodyModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-BODY"), ModelOrientation.CENTRAL, nameof(currentBody), null, nameof(currentTankTexture), nameof(bodyModulePersistentData));
            bodyModule.name = "ModuleROStage-Body";
            bodyModule.getSymmetryModule = m => m.bodyModule;
            bodyModule.getValidOptions = () => GetVariantSet(currentVariant).definitions;

            upperTankModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-UPPER-TANK"), ModelOrientation.CENTRAL, nameof(currentBody), null, nameof(currentTankTexture), nameof(bodyModulePersistentData));
            upperTankModule.name = "ModuleROStage-UpperTank";
            upperTankModule.getSymmetryModule = m => m.upperTankModule;
            upperTankModule.getValidOptions = () => bodyDefs;

            lowerTankModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-LOWER-TANK"), ModelOrientation.CENTRAL, nameof(currentBody), null, nameof(currentTankTexture), nameof(bodyModulePersistentData));
            lowerTankModule.name = "ModuleROStage-LowerTank";
            lowerTankModule.getSymmetryModule = m => m.lowerTankModule;
            lowerTankModule.getValidOptions = () => bodyDefs;

            upperDomeModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-UPPER-DOME"), ModelOrientation.TOP, nameof(currentDome), null, nameof(currentUpperDomeTexture), nameof(upperDomeModulePersistentData));
            upperDomeModule.name = "ModuleROStage-UpperDome";
            upperDomeModule.getSymmetryModule = m => m.upperDomeModule;
            upperDomeModule.getValidOptions = () => domeDefs;

            lowerDomeModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-LOWER-DOME"), ModelOrientation.BOTTOM, nameof(currentDome), null, nameof(currentLowerDomeTexture), nameof(lowerDomeModulePersistentData));
            lowerDomeModule.name = "ModuleROStage-LowerDome";
            lowerDomeModule.getSymmetryModule = m => m.lowerDomeModule;
            lowerDomeModule.getValidOptions = () => domeDefs;

            upperStringersModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-UPPER-STRINGERS"), ModelOrientation.TOP, nameof(currentUpperStringers), null, nameof(currentUpperStringersTexture), nameof(upperStringersModulePersistentData));
            upperStringersModule.name = "ModuleROStage-UpperStringers";
            upperStringersModule.getSymmetryModule = m => m.upperStringersModule;
            upperStringersModule.getValidOptions = () => stringersDefs;

            lowerStringersModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-UPPER-STRINGERS"), ModelOrientation.BOTTOM, nameof(currentLowerStringers), null, nameof(currentLowerStringersTexture), nameof(lowerStringersModulePersistentData));
            lowerStringersModule.name = "ModuleROStage-LowerStringers";
            lowerStringersModule.getSymmetryModule = m => m.lowerStringersModule;
            lowerStringersModule.getValidOptions = () => stringersDefs;

            topModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-TOP"), ModelOrientation.TOP, nameof(currentTop), null, nameof(currentTopTexture), nameof(topModulePersistentData));
            topModule.name = "ModuleROStage-Top";
            topModule.getSymmetryModule = m => m.topModule;
            if (validateTop)
            {
                topModule.getValidOptions = () => topModule.getValidModels(topDefs, bodyModule.definition.style);
            }
            else
            {
                topModule.getValidOptions = () => topDefs;
            }

            bottomModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-BOTTOM"), ModelOrientation.BOTTOM, nameof(currentBottom), null, nameof(currentBottomTexture), nameof(bottomModulePersistentData));
            bottomModule.name = "ModuleROTank-Mount";
            bottomModule.getSymmetryModule = m => m.bottomModule;
            if (validateBottom)
            {
                bottomModule.getValidOptions = () => bottomModule.getValidModels(bottomDefs, bodyModule.definition.style);
            }
            else
            {
                bottomModule.getValidOptions = () => bottomDefs;
            }

            bodyRadialModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-RADIAL-BODY"), ModelOrientation.CENTRAL, nameof(currentBodyRadial), null, nameof(currentBodyRadialTexture), nameof(bodyRadialModulePersistentData));
            bodyRadialModule.name = "ModuleROStage-Radial-Body";
            bodyRadialModule.getSymmetryModule = m => m.bodyRadialModule;
            bodyRadialModule.getValidOptions = () => radialDefs;

            upperRadialModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-RADIAL-UPPER"), ModelOrientation.CENTRAL, nameof(currentUpperRadial), null, nameof(currentUpperRadialTexture), nameof(upperRadialModulePersistentData));
            upperRadialModule.name = "ModuleROStage-Radial-Upper";
            upperRadialModule.getSymmetryModule = m => m.upperRadialModule;
            upperRadialModule.getValidOptions = () => radialDefs;

            lowerRadialModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-RADIAL-LOWER"), ModelOrientation.CENTRAL, nameof(currentLowerRadial), null, nameof(currentLowerRadialTexture), nameof(lowerRadialModulePersistentData));
            lowerRadialModule.name = "ModuleROStage-Radial-Lower";
            lowerRadialModule.getSymmetryModule = m => m.lowerRadialModule;
            lowerRadialModule.getValidOptions = () => radialDefs;

            racewayModule = new ROLModelModule<ModuleROStage>(part, this, ROLUtils.GetRootTransform(part, "ModularPart-RACEWAY"), ModelOrientation.CENTRAL, nameof(currentRaceway), null, nameof(currentRacewayTexture), nameof(racewayModulePersistentData));
            racewayModule.name = "ModuleROStage-Raceway";
            racewayModule.getSymmetryModule = m => m.racewayModule;
            racewayModule.getValidOptions = () => racewayDefs;

            moduleList.Add(bodyModule);
            moduleList.Add(topModule);
            moduleList.Add(upperStringersModule);
            moduleList.Add(upperDomeModule);
            moduleList.Add(upperTankModule);
            moduleList.Add(lowerTankModule);
            moduleList.Add(lowerDomeModule);
            moduleList.Add(lowerStringersModule);
            moduleList.Add(bottomModule);
            moduleList.Add(bodyRadialModule);
            moduleList.Add(upperRadialModule);
            moduleList.Add(lowerRadialModule);
            moduleList.Add(racewayModule);

            bodyModule.setupModelList(bodyDefs);
            topModule.setupModelList(topDefs);
            bottomModule.setupModelList(bottomDefs);
            upperDomeModule.setupModelList(domeDefs);
            lowerDomeModule.setupModelList(domeDefs);
            upperStringersModule.setupModelList(stringersDefs);
            lowerStringersModule.setupModelList(stringersDefs);
            upperTankModule.setupModelList(bodyDefs);
            lowerTankModule.setupModelList(bodyDefs);
            bodyRadialModule.setupModelList(radialDefs);
            upperRadialModule.setupModelList(radialDefs);
            lowerRadialModule.setupModelList(radialDefs);
            racewayModule.setupModelList(racewayDefs);

            foreach (var module in moduleList)
            {
                module.setupModel();
                module.volumeScalar = 3f;
            }

            if (validateTop || validateBottom) ValidateModules();

            prevDiameter = currentDiameter;
            prevUpperTankLength = upperTankModule.moduleHeight;
            prevLowerTankLength = lowerTankModule.moduleHeight;
            prevTopLength = topModule.moduleHeight;
            prevUpperDomeLength = upperDomeModule.moduleHeight;
            prevUpperStringersLength = currentUpperStringersLength;
            prevLowerDomeLength = lowerDomeModule.moduleHeight;
            prevLowerStringersLength = currentLowerStringersLength;
            prevBottomLength = bottomModule.moduleHeight;
        }

        public void InitializeUI()
        {
            //set up the core variant UI control
            string[] variantNames = ROLUtils.getNames(variantSets.Values, m => m.variantName);
            this.ROLupdateUIChooseOptionControl(nameof(currentVariant), variantNames, variantNames);
            Fields[nameof(currentVariant)].guiActiveEditor = variantSets.Count > 1;

            Fields[nameof(currentVariant)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                ModelDefinitionVariantSet prevMdvs = GetVariantSet(bodyModule.definition.name);
                int previousIndex = prevMdvs.IndexOf(bodyModule.layoutOptions);
                ModelDefinitionVariantSet mdvs = GetVariantSet(currentVariant);
                ModelDefinitionLayoutOptions newCoreDef = mdvs[previousIndex];
                this.ROLactionWithSymmetry(m =>
                {
                    m.bodyModule.modelSelected(newCoreDef.definition.name);
                    ModelChangedHandler(true);
                });
                MonoUtilities.RefreshPartContextWindow(part);
            };

            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentDiameter)].uiControlEditor.onSymmetryFieldChanged = OnDiameterChanged;

            Fields[nameof(currentStructureLength)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentStructureLength)].uiControlEditor.onSymmetryFieldChanged = OnLengthChanged;

            Fields[nameof(currentAvionics)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentAvionics)].uiControlEditor.onSymmetryFieldChanged = OnAvionicsChanged;

            Fields[nameof(currentBodyRadialVOffset)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentBodyRadialVOffset)].uiControlEditor.onSymmetryFieldChanged = OnRadialOffsetChanged;

            Fields[nameof(currentBodyRadialHOffset)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentBodyRadialHOffset)].uiControlEditor.onSymmetryFieldChanged = OnRadialOffsetChanged;

            Fields[nameof(currentUpperRadialVOffset)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentUpperRadialVOffset)].uiControlEditor.onSymmetryFieldChanged = OnRadialOffsetChanged;

            Fields[nameof(currentUpperRadialHOffset)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentUpperRadialHOffset)].uiControlEditor.onSymmetryFieldChanged = OnRadialOffsetChanged;

            Fields[nameof(currentLowerRadialVOffset)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentLowerRadialVOffset)].uiControlEditor.onSymmetryFieldChanged = OnRadialOffsetChanged;

            Fields[nameof(currentLowerRadialHOffset)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentLowerRadialHOffset)].uiControlEditor.onSymmetryFieldChanged = OnRadialOffsetChanged;

            Fields[nameof(currentUpperRadialParent)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentUpperRadialParent)].uiControlEditor.onSymmetryFieldChanged = OnRadialParentChanged;
            
            Fields[nameof(currentLowerRadialParent)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentLowerRadialParent)].uiControlEditor.onSymmetryFieldChanged = OnRadialParentChanged;

            Fields[nameof(currentRacewayVOffset)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentRacewayVOffset)].uiControlEditor.onSymmetryFieldChanged = OnRadialOffsetChanged;

            Fields[nameof(currentRacewayHOffset)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentRacewayHOffset)].uiControlEditor.onSymmetryFieldChanged = OnRadialOffsetChanged;

            Fields[nameof(currentTopVScale)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentTopVScale)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentBottomVScale)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentBottomVScale)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentTopRotation)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentTopRotation)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentBottomRotation)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentBottomRotation)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentUpperStringersLength)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentUpperStringersLength)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentLowerStringersLength)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentLowerStringersLength)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentRacewayVScale)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentRacewayVScale)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentRacewayHScale)].uiControlEditor.onFieldChanged =
            Fields[nameof(currentRacewayHScale)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
            {
                ModelChangedHandler(true);
            };

            Fields[nameof(currentBody)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentBody)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentTop)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentTop)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentBottom)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentBottom)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentDome)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentDome)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentUpperStringers)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentUpperStringers)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentLowerStringers)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentLowerStringers)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentBodyRadial)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentBodyRadial)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentUpperRadial)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentUpperRadial)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentLowerRadial)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentLowerRadial)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;
            Fields[nameof(currentRaceway)].uiControlEditor.onFieldChanged =
                Fields[nameof(currentRaceway)].uiControlEditor.onSymmetryFieldChanged = OnModelSelectionChanged;

            //------------------MODEL DIAMETER / LENGTH SWITCH UI INIT---------------------//
            this.ROLupdateUIFloatEditControl(nameof(currentDiameter), minDiameter, maxDiameter, diameterLargeStep, diameterSmallStep, diameterSlideStep);
            this.ROLupdateUIFloatEditControl(nameof(currentStructureLength), minLength, maxLength, diameterLargeStep, diameterSmallStep, diameterSlideStep);
            this.ROLupdateUIFloatEditControl(nameof(currentAvionics), avionicsContainerMinPercent, avionicsContainerMaxPercent, 10f, 1f, 1f);
            this.ROLupdateUIFloatEditControl(nameof(currentUpperStringersLength), minStringers, maxStringers, diameterLargeStep, diameterSmallStep, diameterSlideStep);
            this.ROLupdateUIFloatEditControl(nameof(currentLowerStringersLength), minStringers, maxStringers, diameterLargeStep, diameterSmallStep, diameterSlideStep);

            //-----------------------RADIAL PARENT SWITCH UI INIT--------------------------//
            string[] upperRadialOptions = ROLUtils.parseCSV(upperParentOptions);
            string[] lowerRadialOptions = ROLUtils.parseCSV(lowerParentOptions);
            this.updateUIChooseOptionControl(nameof(currentUpperRadialParent), upperRadialOptions, upperRadialOptions, true, currentUpperRadialParent);
            this.updateUIChooseOptionControl(nameof(currentLowerRadialParent), lowerRadialOptions, lowerRadialOptions, true, currentLowerRadialParent);

            //--------------------------SHOW/HIDE GUI OPTIONS-----------------------------//
            Fields[nameof(currentDiameter)].guiActiveEditor = maxDiameter != minDiameter;
            Fields[nameof(currentStructureLength)].guiActiveEditor = maxLength != minLength;
            Fields[nameof(currentTopVScale)].guiActiveEditor = enableTopVScale;
            Fields[nameof(currentBottomVScale)].guiActiveEditor = enableBottomVScale;
            Fields[nameof(currentTopRotation)].guiActiveEditor = topModule.moduleCanRotate;
            Fields[nameof(currentBottomRotation)].guiActiveEditor = bottomModule.moduleCanRotate;

            Fields[nameof(currentUpperStringersLength)].guiActiveEditor = hasUpperStringers;
            Fields[nameof(currentLowerStringersLength)].guiActiveEditor = hasLowerStringers;

            Fields[nameof(currentBodyRadialVOffset)].guiActiveEditor = hasBodyRadial;
            Fields[nameof(currentBodyRadialHOffset)].guiActiveEditor = hasBodyRadial;
            Fields[nameof(currentUpperRadialVOffset)].guiActiveEditor = hasUpperRadial;
            Fields[nameof(currentUpperRadialHOffset)].guiActiveEditor = hasUpperRadial;
            Fields[nameof(currentLowerRadialVOffset)].guiActiveEditor = hasLowerRadial;
            Fields[nameof(currentLowerRadialHOffset)].guiActiveEditor = hasLowerRadial;

            Fields[nameof(currentRacewayVOffset)].guiActiveEditor = hasRaceway;
            Fields[nameof(currentRacewayHOffset)].guiActiveEditor = hasRaceway;
            Fields[nameof(currentRacewayVScale)].guiActiveEditor = hasRaceway;
            Fields[nameof(currentRacewayHScale)].guiActiveEditor = hasRaceway;

            //------------------MODULE TEXTURE SWITCH UI INIT---------------------//
            Fields[nameof(currentTankTexture)].uiControlEditor.onFieldChanged = OnTankTextureSelected;
            Fields[nameof(currentTopTexture)].uiControlEditor.onFieldChanged = topModule.textureSetSelected;
            Fields[nameof(currentBottomTexture)].uiControlEditor.onFieldChanged = bottomModule.textureSetSelected;
            Fields[nameof(currentUpperDomeTexture)].uiControlEditor.onFieldChanged = upperDomeModule.textureSetSelected;
            Fields[nameof(currentLowerDomeTexture)].uiControlEditor.onFieldChanged = lowerDomeModule.textureSetSelected;
            Fields[nameof(currentUpperStringersTexture)].uiControlEditor.onFieldChanged = upperStringersModule.textureSetSelected;
            Fields[nameof(currentLowerStringersTexture)].uiControlEditor.onFieldChanged = lowerStringersModule.textureSetSelected;
            Fields[nameof(currentBodyRadialTexture)].uiControlEditor.onFieldChanged = bodyRadialModule.textureSetSelected;
            Fields[nameof(currentUpperRadialTexture)].uiControlEditor.onFieldChanged = upperRadialModule.textureSetSelected;
            Fields[nameof(currentLowerRadialTexture)].uiControlEditor.onFieldChanged = lowerRadialModule.textureSetSelected;
            Fields[nameof(currentRacewayTexture)].uiControlEditor.onFieldChanged = racewayModule.textureSetSelected;
        }

        #endregion Initialization


        #region Custom Update Methods

        private void OnModelSelectionChanged(BaseField f, object o)
        {

        }

        private void ValidateModules()
        {

        }

        private void ValidateLength()
        {

        }

        private void ValidateRotation()
        {

        }

        private void UpdateModulePositions()
        {

        }

        private void UpdateTankVolume()
        {

        }

        private void UpdateDimensions()
        {

        }

        private void UpdateModelMeshes()
        {

        }

        private void ScaleRadialModels()
        {

        }

        private void UpdateAttachNodes(bool userInput)
        {

        }

        private void UpdateAvailableVariants()
        {

        }

        private void SetPreviousModuleLength()
        {

        }

        private void UpdateDragCubes()
        {

        }

        private void UpdateMass()
        {

        }

        private void UpdateCost()
        {

        }

        private void OnDiameterChanged(BaseField f, object o)
        {
        
        }

        private void OnLengthChanged(BaseField f, object o)
        {

        }

        private void OnAvionicsChanged(BaseField f, object o)
        {

        }

        private void OnRadialOffsetChanged(BaseField f, object o)
        {

        }

        private void OnRadialParentChanged(BaseField f, object o)
        {

        }

        private void OnTankTextureSelected(BaseField f, object o)
        {

        }



        #endregion Custom Update Methods





    }
}
