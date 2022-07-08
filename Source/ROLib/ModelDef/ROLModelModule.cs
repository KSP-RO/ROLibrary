using System;
using System.Linq;
using UnityEngine;
using KSPShaderTools;
using static ROLib.ROLLog;
using System.Collections.Generic;

namespace ROLib
{

    //ModelModule transform hierarchy
    //part(the GO with the Part Component on it)
    //model(the standard Part 'model' transform)
    //    NamedOuterModelTransform -- positioned and scaled by ModularPart code
    //        ModelModule-0 -- positioned relative to parent by ModuleModule, using the ModelLayoutData currently set in the ModelModule -- defaults to 0,0,0 position, 1,1,1 scale, 0,0,0 rotation
    //        ModelModule-n -- second/third/etc -- there will be one entry for every 'position' in the ModelLayoutData config

    /// <summary>
    /// ModelModule is a 'standard class' that can be used from within PartModule code to manage a single active model, as well as any potential alternate model selections.<para/>
    /// Uses ModelDefinition configs to define how the models are setup.<para/>
    /// Includes support for all features that are supported by ModelDefinition (animation, solar, rcs, engine, gimbal, constraints)<para/>
    /// Creation consists of the following setup sequence:<para/>
    /// 1.) Call constructor
    /// 2.) Setup Delegate methods
    /// 3.) Call setupModelList
    /// 4.) Call setupModel
    /// 5.) Call updateSelections
    /// </summary>
    /// <typeparam name="U"></typeparam>
    public class ROLModelModule<U> where U : PartModule
    {

        #region REGION - Delegate Signatures
        public delegate ROLModelModule<U> SymmetryModule(U m);
        public delegate ModelDefinitionLayoutOptions[] ValidOptions();
        #endregion ENDREGION - Delegate Signatures

        #region REGION - Immutable fields
        public readonly Part part;
        public readonly U partModule;
        public readonly Transform root;
        public readonly ModelOrientation orientation;
        #endregion ENDREGION - Immutable fields

        #region REGION - Public Delegate Stubs

        /// <summary>
        /// Delegate poplated by a default that returns the entire model list.  May optionally provide a delegate method to return a custom list of currently valid modules.  Called by 'updateSelections'
        /// </summary>
        public ValidOptions getValidOptions;

        /// <summary>
        /// Delegate MUST be populated.
        /// </summary>
        public SymmetryModule getSymmetryModule;

        public Func<float> getLayoutPositionScalar = delegate ()
        {
            return 1f;
        };

        public Func<float> getLayoutScaleScalar = delegate ()
        {
            return 1f;
        };

        #endregion ENDREGION - Public Delegate Stubs

        #region REGION - Private working data

        /// <summary>
        /// The -current- model layout in use.  Initialized during setupModels() call, and should always be valid after that point.
        /// </summary>
        private ModelLayoutData currentLayout;

        /// <summary>
        /// Array containing all possible model definitions for this module.
        /// </summary>
        private ModelDefinitionLayoutOptions[] optionsCache;

        /// <summary>
        /// Local reference to the persistent data field used to store custom coloring data for this module.  May be null when recoloring is not used.
        /// </summary>
        private readonly BaseField dataField;

        /// <summary>
        /// Local reference to the persistent data field used to store texture set names for this module.  May be null when texture switching is not used.
        /// </summary>
        private readonly BaseField textureField;

        /// <summary>
        /// Local reference to the persistent data field used to store the current model name for this module.  Must not be null.
        /// </summary>
        private readonly BaseField modelField;

        /// <summary>
        /// Local referenec to the persistent data field used to store the current layout name for this module.  May be null if layouts are unsupported, in which case it will always return 'defualt'.
        /// </summary>
        private readonly BaseField layoutField;

        #endregion ENDREGION - Private working data

        #region REGION - BaseField wrappers

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        public string textureSetName
        {
            get { return textureField?.GetValue<string>(partModule) ?? "default"; }
            private set { textureField?.SetValue(value, partModule); }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        public string modelName
        {
            get { return modelField?.GetValue<string>(partModule) ?? string.Empty; }
            private set { modelField?.SetValue(value, partModule); }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        public string persistentData
        {
            get { return dataField?.GetValue<string>(partModule) ?? string.Empty; }
            private set { dataField?.SetValue(value, partModule); }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        public string layoutName
        {
            get { return layoutField?.GetValue<string>(partModule) ?? "default"; }
            private set { layoutField?.SetValue(value, partModule); }
        }

        #endregion ENDREGION - BaseField wrappers

        #region REGION - Model definition data

        /// <summary>
        /// Return the current 'name' of this model-module.  Currently only used in error reporting.<para/>
        /// Name must be set manually after module is instantiated.
        /// </summary>
        public string name = "ROLModelModule";

        /// <summary>
        /// Return the currently 'active' model definition.
        /// </summary>
        public ROLModelDefinition definition { get; private set; }

        /// <summary>
        /// Return the currently active texture set from the currently active model definition.
        /// </summary>
        public TextureSet textureSet => definition.FindTextureSet(textureSetName);

        /// <summary>
        /// Return the currently active model layout.
        /// </summary>
        public ModelLayoutData layout => Array.Find(layoutOptions.layouts, m => m.name == layoutName);

        /// <summary>
        /// Return the currently active layout options for the current model definition.
        /// </summary>
        public ModelDefinitionLayoutOptions layoutOptions { get; private set; }

        public float volumeScalar = 3f;
        public float MassScalar = 3f;

        /// <summary>
        /// Return the current mass for this module slot.  Includes adjustments from the definition mass based on the current scale.
        /// </summary>
        public float moduleMass { get; private set; }

        /// <summary>
        /// Return the current cost for this module slot.  Includes adjustments from the definition cost based on the current scale.
        /// </summary>
        public float moduleCost { get; private set; }

        /// <summary>
        /// Return the current usable resource volume for this module slot.  Includes adjustments from the definition volume based on the current scale.
        /// </summary>
        public float moduleVolume { get; private set; }

        public bool moduleCanRotate => definition.canRotate;
        public bool moduleCanVScale => definition.canVScale;

        // ROStations Fields
        public bool moduleCanAdjustHab => definition.canAdjustHab;
        public float moduleHabitat { get; private set; }
        public float moduleSurfaceArea { get; private set; }
        public float moduleTrussVolume { get; private set; }
        public float moduleTotalVolume { get; private set; }
        public StationType moduleStationType => definition.stationType;
        public bool moduleCanExercise => definition.canExercise;
        public bool moduleHasPanorama => definition.hasPanorama;
        public bool moduleHasPlants => definition.hasPlants;

        /// <summary>
        /// Return the current diameter of the model in this module slot.  This is the base diamter as specified in the model definition, modified by the currently specified scale.
        /// </summary>
        public float moduleDiameter { get; private set; }
        public float modulePanelLength { get; private set; }
        public float modulePanelWidth { get; private set; }

        /// <summary>
        /// Return true/false if fairings are enabled for this module in its current configuration.
        /// </summary>
        public bool fairingEnabled => definition.fairingData != null && definition.fairingData.fairingsSupported;

        /// <summary>
        /// Return the current upper-mounting diamter of the model in this module slot.  This value is to be used for sizing/scaling of any module slot used for an upper-adapter/nose option for this slot.
        /// </summary>
        public float moduleUpperDiameter => (definition.shouldInvert(orientation) ? definition.lowerDiameter : definition.upperDiameter) * moduleHorizontalScale;

        /// <summary>
        /// Return the current lower-mounting diamter of the model in this module slot.  This value is to be used for sizing/scaling of any module slot used for a lower-adapter/mount option for this slot.
        /// </summary>
        public float moduleLowerDiameter => (definition.shouldInvert(orientation) ? definition.upperDiameter : definition.lowerDiameter) * moduleHorizontalScale;

        /// <summary>
        /// Return the current height of the model in this module slot.  Based on the definition specified height and the current vertical scale.
        /// </summary>
        public float moduleHeight { get; private set; }

        /// <summary>
        /// Return the actual height of the model in this module slot used for Booster style tanks.
        /// </summary>
        public float moduleActualHeight { get; private set; }

        /// <summary>
        /// Return the current x/z scaling used by the model in this module slot.
        /// </summary>
        public float moduleHorizontalScale { get; private set; } = 1f;

        /// <summary>
        /// Return the current y scaling used by the model in this module slot.
        /// </summary>
        public float moduleVerticalScale { get; private set; } = 1f;

        /// <summary>
        /// Return the current origin position of the Y corrdinate of this module, in part-centric space.<para/>
        /// A value of 0 denotes origin is at the parts' origin/COM.
        /// </summary>
        public float modulePosition { get; private set; }

        public Vector3 moduleRotation { get; private set; }

        public string[] moduleTransformsToRemove => definition.disableTransforms;
        /// <summary>
        /// Return the Y coordinate of the top-most point in the model in part-centric space, as defined by model-height in the model definition and modified by current model scale,
        /// </summary>
        public float ModuleTop =>
            orientation switch
            {
                ModelOrientation.TOP => modulePosition + moduleHeight,
                ModelOrientation.CENTRAL => modulePosition + (moduleHeight / 2),
                ModelOrientation.BOTTOM => modulePosition,
                _ => modulePosition
            };

        /// <summary>
        /// Return the Y coordinate of the physical 'center' of this model in part-centric space.
        /// </summary>
        public float ModuleCenter => ModuleTop - (moduleHeight / 2);

        /// <summary>
        /// Returns the Y coordinate of the bottom of this model in part-centric space.
        /// </summary>
        public float ModuleBottom => ModuleTop - moduleHeight;

        /// <summary>
        /// Return the upper fairing attachment point for this module.  The returned position is relative to 'modulePosition'.
        /// </summary>
        public float fairingTop
        {
            get
            {
                float pos = modulePosition;
                if (definition.fairingData == null) { return pos; }
                pos += definition.fairingData.GetTop(moduleVerticalScale, definition.shouldInvert(orientation));
                pos += GetPlacementOffset();
                return pos;
            }
        }

        /// <summary>
        /// Return the lower fairing attachment point for this module.  The returned position is relative to 'modulePosition'.
        /// </summary>
        public float fairingBottom
        {
            get
            {
                float pos = modulePosition;
                if (definition.fairingData == null) { return pos; }
                pos += definition.fairingData.GetBottom(moduleVerticalScale, definition.shouldInvert(orientation));
                pos += GetPlacementOffset();
                return pos;
            }
        }

        /// <summary>
        /// Return the currently configured custom color data for this module slot.
        /// Loaded from persistence data if the recoloring persistence field is present.  Auto-saved out to persistence field on color updates.
        /// May be NULL if no coloring data has been loaded.
        /// </summary>
        public RecoloringData[] recoloringData { get; private set; }

        /// <summary>
        /// Return the transforms that represent the root transforms for the models in this module slot.  Under normal circumstaces (standard single model layout), this should return an array of a single transform.
        /// Will contain one transform for each position in the layout, with identical ordering to the specification in the layout.
        /// </summary>
        public Transform[] models { get; private set; }

        #endregion ENDREGION - Convenience wrappers for model definition data for external use

        #region REGION - Constructors and Init Methods

        /// <summary>
        /// Only a partial constructor.  Need to also call at least 'setupModelList' and/or 'setupModel' before the module will actually be usable.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="partModule"></param>
        /// <param name="root"></param>
        /// <param name="orientation"></param>
        /// <param name="dataFieldName"></param>
        /// <param name="modelFieldName"></param>
        /// <param name="textureFieldName"></param>
        public ROLModelModule(Part part, U partModule, Transform root, ModelOrientation orientation,
            string modelPersistenceFieldName, string layoutPersistenceFieldName, string texturePersistenceFieldName, string recolorPersistenceFieldName)
        {
            this.part = part;
            this.partModule = partModule;
            this.root = root;
            this.orientation = orientation;
            modelField = partModule.Fields[modelPersistenceFieldName];
            layoutField = partModule.Fields[layoutPersistenceFieldName];
            textureField = partModule.Fields[texturePersistenceFieldName];
            dataField = partModule.Fields[recolorPersistenceFieldName];
            getValidOptions = delegate () { return optionsCache; };
            LoadColors(persistentData);
        }

        public override string ToString() => GetErrorReportModuleName();

        /// <summary>
        /// Initialization method.  May be called to update the available model list later; if the currently selected model is invalid, it will be set to the first model in the list.<para/>
        /// Does not update current model, but does update model name.  setupModel() should be subsequently called after setupModelList().
        /// </summary>
        /// <param name="models"></param>
        public void setupModelList(ModelDefinitionLayoutOptions[] modelDefs)
        {
            optionsCache = modelDefs;
            if (modelDefs.Length <= 0)
            {
                error($"No models found for: {GetErrorReportModuleName()}");
            }
            else if (!Array.Exists(optionsCache, m => m.definition.name == modelName))
            {
                error($"Currently configured model name: {modelName} was not located while setting up: {GetErrorReportModuleName()}");
                modelName = optionsCache[0].definition.name;
                error($"Now using model: {modelName} for: {GetErrorReportModuleName()}");
            }
        }
        /// <summary>
        /// Initialization method.  Creates the model transforms, and sets their position and scale to the current config values.<para/>
        /// Initializes texture set, including 'defualts' handling.  Initializes animation module with the animation data for the current model.<para/>
        /// Only for use during part initialization.  Subsequent changes to model should call the modelSelectedXXX methods.
        /// </summary>
        public void setupModel(bool doNotRescaleX = false)
        {
            ROLUtils.destroyChildrenImmediate(root);
            layoutOptions = Array.Find(optionsCache, m => m.definition.name == modelName);
            if (layoutOptions == null)
            {
                error($"Could not locate model definition for: {modelName} for {GetErrorReportModuleName()}");
            }
            definition = layoutOptions.definition;
            currentLayout = layoutOptions.getLayout(layoutName);
            if (!layoutOptions.isValidLayout(layoutName))
            {
                log($"Existing layout: {layoutName} for {GetErrorReportModuleName()} was null.  Assigning default layout: {layoutOptions.getDefaultLayout().name}");
                layoutName = layoutOptions.getDefaultLayout().name;
            }
            ConstructModels();
            UpdateModelScalesAndLayoutPositions(doNotRescaleX);    // This calls updateModelScalesAndLayoutPositions();
            SetupTextureSet();
            UpdateModuleStats();
        }

        #endregion ENDREGION - Constructors and Init Methods

        #region REGION - Update Methods

        /// <summary>
        /// If the model definition contains rcs-thrust-transform data, will rename the model's rcs thrust transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleRCS when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void RenameRCSThrustTransforms(string destinationName) => definition?.rcsModuleData?.RenameTransforms(root, destinationName);

        /// <summary>
        /// If the model definition contains engine-thrust-transform data, will rename the model's engine thrust transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleEngines when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void RenameEngineThrustTransforms(string destinationName) => definition?.engineTransformData?.RenameThrustTransforms(root, destinationName);

        /// <summary>
        /// If the model definition contains gimbal-transform data, will rename the model's gimbal transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleGimbal when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void RenameGimbalTransforms(string destinationName) => definition.engineTransformData.RenameGimbalTransforms(root, destinationName);

        /// <summary>
        /// Update the input moduleEngines min, max, and split thrust values.  Any engine thrust transforms need to have been already renamed prior to this call.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="thrustScalePower"></param>
        public void UpdateEngineModuleThrust(ModuleEngines engine, float thrustScalePower)
        {
            if (engine && definition?.engineThrustData != null)
            {
                float scalar = Mathf.Pow(Mathf.Sqrt(moduleHorizontalScale * moduleVerticalScale), thrustScalePower);
                float min = definition.engineThrustData.minThrust * layout.positions.Count() * scalar;
                float max = definition.engineThrustData.maxThrust * layout.positions.Count() * scalar;
                float[] splitThrust = definition.engineThrustData.GetCombinedSplitThrust(layout.positions.Count());
                engine.thrustTransformMultipliers = splitThrust.ToList();
                ROLStockInterop.UpdateEngineThrust(engine, min, max); //calls engine.OnLoad(...);
            }
        }

        /// <summary>
        /// Update the input moduleRCS enabled, thrust and axis enable/disable status.  Calls rcs.OnStart() to update
        /// </summary>
        /// <param name="rcs"></param>
        /// <param name="thrustScaleFactor"></param>
        public void UpdateRCSModule(ModuleRCS rcs, float thrustScaleFactor)
        {
            float power = 0;
            if (definition.rcsModuleData is ModelRCSModuleData data)
            {
                power = data.rcsThrust;
                float scale = Mathf.Sqrt(moduleHorizontalScale * moduleVerticalScale);
                scale *= layout.modelScalarAverage();
                power *= Mathf.Pow(scale, thrustScaleFactor);
                rcs.enableX = data.enableX;
                rcs.enableY = data.enableY;
                rcs.enableZ = data.enableZ;
                rcs.enablePitch = data.enablePitch;
                rcs.enableYaw = data.enableYaw;
                rcs.enableRoll = data.enableRoll;
            }
            rcs.thrusterPower = power;
            rcs.moduleIsEnabled = power > 0;
            rcs.OnStart(PartModule.StartState.Flying);
        }

        #endregion ENDREGION - Update Methods

        #region REGION - GUI Interaction Methods - With symmetry updating

        /// <summary>
        /// Symmetry-enabled method.  Should only be called when symmetry updates are desired.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="oldValue"></param>
        public void textureSetSelected(BaseField field, object oldValue)
        {
            ActionWithSymmetry(m =>
            {
                m.textureSetName = textureSetName;
                m.ApplyTextureSet(m.textureSetName, !ROLGameSettings.PersistRecolor());
                if (m.textureField != null)
                {
                    m.partModule.ROLupdateUIChooseOptionControl(m.textureField.name, m.definition.GetTextureSetNames(), m.definition.GetTextureSetTitles());
                }
            });
        }

        /// <summary>
        /// Sets the currently selected model name to the input model, and setup
        /// </summary>
        /// <param name="newModel"></param>
        public void modelSelected(string newModel, bool doNotRescaleX = false)
        {
            if (Array.Exists(optionsCache, m => m.definition.name == newModel))
            {
                modelName = newModel;
                setupModel(doNotRescaleX);
            }
            else
            {
                error($"No model definition found for input name: {newModel}  for: {GetErrorReportModuleName()}");
            }
        }

        /// <summary>
        /// Symmetry enabled.  Updates the current persistent color data, and reapplies the textures/color data to the models materials.
        /// </summary>
        /// <param name="colors"></param>
        public void setSectionColors(RecoloringData[] colors)
        {
            ActionWithSymmetry(m =>
            {
                m.textureSetName = textureSetName;
                m.recoloringData = colors;
                m.EnableTextureSet();
                m.SaveColors(m.recoloringData);
            });
        }

        /*
        /// <summary>
        /// NON-Symmetry enabled method.  Sets the current layout and updates models for current layout.  Uses current vertical position/all other current position data.
        /// </summary>
        /// <param name="newLayout"></param>
        public void layoutSelected(string newLayout)
        {
            if (!layoutOptions.isValidLayout(newLayout))
            {
                newLayout = layoutOptions.getDefaultLayout().name;
                error("Could not find layout definition by name: " + newLayout + " using default layout for model: " + getErrorReportModuleName());
            }
            layoutName = newLayout;
            currentLayout = layoutOptions.getLayout(newLayout);
            setupModel();
            updateSelections();
        }
        */

        /// <summary>
        /// NON-Symmetry enabled method.<para/>
        /// Updates the UI controls for the currently available models specified through setupModelList.<para/>
        /// Also updates the texture-set selection widget options and visibility (if backing field is not null).
        /// Also updates the layout selection widget (if backing field is not null)
        /// </summary>
        public void updateSelections()
        {
            if (modelField != null)
            {
                if (getValidOptions == null) { error("ModelModule delegate 'getValidOptions' is not populated for: " + GetErrorReportModuleName()); }
                ModelDefinitionLayoutOptions[] availableOptions = getValidOptions();
                if (availableOptions == null || availableOptions.Length < 1) { MonoBehaviour.print("ERROR: No valid models found for: " + GetErrorReportModuleName()); }
                string[] names = ROLUtils.getNames(availableOptions, s => s.definition.name);
                string[] displays = ROLUtils.getNames(availableOptions, s => s.definition.title);
                partModule.ROLupdateUIChooseOptionControl(modelField.name, names, displays);
                modelField.guiActiveEditor = names.Length > 1;
            }
            //updates the texture set selection for the currently configured model definition, including disabling of the texture-set selection UI when needed
            if (textureField != null)
            {
                partModule.ROLupdateUIChooseOptionControl(textureField.name, definition.GetTextureSetNames(), definition.GetTextureSetTitles());
            }
            if (layoutField != null)
            {
                ModelDefinitionLayoutOptions mdlo = optionsCache.ROLFind(m => m.definition == definition);
                partModule.ROLupdateUIChooseOptionControl(layoutField.name, mdlo.getLayoutNames(), mdlo.getLayoutTitles());
                layoutField.guiActiveEditor = layoutField.guiActiveEditor && currentLayout.positions.Length > 1;
            }
        }

        /// <summary>
        /// Updates the diameter/scale values so that the diameter of this model matches the input diameter
        /// </summary>
        /// <param name="newDiameter"></param>
        /// <param name="baseDiameter"></param>
        public void RescaleToDiameter(float newDiameter, float baseDiameter, float vScalar = 0)
        {
            float scale = newDiameter / baseDiameter;
            SetScale(scale, scale * VScaleOffset(vScalar));
        }

        /// <summary>
        /// Updates the current internal scale values for the input diameter and height values.
        /// Callers are implicitly in length-width mode, instead of overall scale.
        /// </summary>
        /// <param name="newHeight"></param>
        /// <param name="newDiameter"></param>
        public void setScaleForHeightAndDiameter(float newHeight, float newDiameter, bool solar = false)
        {
            float hScale = newDiameter / (solar ? definition.panelWidth : definition.diameter);
            float vScale = newHeight / (solar ? definition.panelLength : definition.height);
            SetScale(hScale, vScale);
        }

        /// <summary>
        /// Updates the current internal scale values for the input scales.  Updates x,z with the 'horizontal scale' and updates 'y' with the 'vertical scale'.
        /// </summary>
        /// <param name="newHorizontalScale"></param>
        /// <param name="newVerticalScale"></param>
        public void SetScale(float newHorizontalScale, float newVerticalScale)
        {
            float min = newHorizontalScale * definition.minVerticalScale;
            float max = newHorizontalScale * definition.maxVerticalScale;
            newVerticalScale = Mathf.Clamp(newVerticalScale, min, max);
            moduleHorizontalScale = newHorizontalScale;
            moduleVerticalScale = newVerticalScale;
            moduleHeight = newVerticalScale * definition.height;
            moduleActualHeight = newVerticalScale * definition.actualHeight;
            moduleDiameter = newHorizontalScale * definition.diameter;
            modulePanelLength = newVerticalScale * definition.panelLength;
            modulePanelWidth = newHorizontalScale * definition.panelWidth;
            UpdateModuleStats();
        }

        private float VScaleOffset(float aspectInput)
        {
            // aspectInput is the percentage towards min[/max] scale to use.
            float vScale = 1f;
            if (aspectInput < 0)
            {
                aspectInput = Mathf.Abs(aspectInput);
                vScale -= aspectInput * (1 - definition.minVerticalScale);
            }
            else if (aspectInput > 0)
            {
                vScale += aspectInput * (definition.maxVerticalScale - 1);
            }
            return vScale;
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - Public/External methods

        /// <summary>
        /// Updates the input texture-control text field with the texture-set names for this model.  Disables field if no texture sets found, enables field if more than one texture set is available.
        /// </summary>
        public void UpdateTextureUIControl()
        {
            if (textureField is BaseField)
            {
                string[] names = definition.GetTextureSetNames();
                partModule.ROLupdateUIChooseOptionControl(textureField.name, names, definition.GetTextureSetTitles());
                textureField.guiActiveEditor = names.Length > 1;
            }
        }

        /// <summary>
        /// Updates the position of the model.
        /// </summary>
        /// <param name="originPos"></param>
        public void SetPosition(float originPos) => modulePosition = originPos;

        public void SetRotation(Vector3 newRotation) => moduleRotation = newRotation;

        /// <summary>
        /// Updates the attach nodes on the part for the input list of attach nodes and the current specified nodes for this model.
        /// Any 'extra' attach nodes from the part will be disabled.
        /// </summary>
        /// <param name="nodeNames"></param>
        /// <param name="userInput"></param>
        public void updateAttachNodeBody(string[] nodeNames, bool userInput)
        {
            if (nodeNames == null || nodeNames.Length < 1) { return; }
            if (nodeNames.Length == 1 && (string.IsNullOrWhiteSpace(nodeNames[0]) || nodeNames[0].Equals("none", StringComparison.CurrentCultureIgnoreCase))) return;

            bool invert = definition.shouldInvert(orientation);

            int nodeCount = definition?.bodyNodeData?.Length ?? 0;
            for (int i = 0; i < nodeNames.Length; i++)
            {
                AttachNode node = part.FindAttachNode(nodeNames[i]);
                if (i < nodeCount)
                {
                    UpdateAttachNode(definition.bodyNodeData[i], node, invert, userInput, true, nodeNames[i]);
                }
                else//extra node, destroy
                {
                    if (node != null && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
                        ROLAttachNodeUtils.destroyAttachNode(part, node);
                }
            }
        }

        public void UpdateAttachNode(string nodeName, ModelOrientation side, bool userInput)
        {
            bool invert = definition.shouldInvert(orientation);
            AttachNodeBaseData topData = invert ? definition.bottomNodeData : definition.topNodeData;
            AttachNodeBaseData bottomData = invert ? definition.topNodeData : definition.bottomNodeData;
            AttachNodeBaseData nodeData = side == ModelOrientation.TOP ? topData : bottomData;
            if (nodeData is AttachNodeBaseData && part.FindAttachNode(nodeName) is AttachNode node)
                UpdateAttachNode(nodeData, node, invert, userInput);
        }


        /// <summary>
        /// Update the input surface attach node for current model diameter, adjusted for model-def specified positioning.<para/>
        /// Also updates any surface-attached children on the part, by the delta of (oldDiam - newDiam)
        /// </summary>
        /// <param name="node"></param>
        /// <param name="prevDiameter"></param>
        /// <param name="userInput"></param>
        public void updateSurfaceAttachNode(AttachNode node, float prevDiameter, bool userInput)
        {
            if (node is AttachNode && definition.surfaceNode is AttachNodeBaseData surfNodeData)
            {
                Vector3 pos = surfNodeData.position * moduleHorizontalScale;
                ROLAttachNodeUtils.UpdateAttachNodePosition(part, node, pos, surfNodeData.orientation, userInput, node.size);
                if (userInput)
                    ROLAttachNodeUtils.updateSurfaceAttachedChildren(part, prevDiameter, moduleDiameter);
            }
        }

        public void UpdateSurfaceAttachNode(AttachNode node, float prevDiam, float prevNose, float prevCore, float prevMount, float newNose, float newCore, float newMount, bool userInput)
        {
            if (node is AttachNode && definition.surfaceNode is AttachNodeBaseData surfNodeData)
            {
                Vector3 pos = surfNodeData.position * moduleHorizontalScale;
                ROLAttachNodeUtils.UpdateAttachNodePosition(part, node, pos, surfNodeData.orientation, true, node.size);
                if (userInput)
                    ROLAttachNodeUtils.UpdateSurfaceAttachedChildren(part, prevDiam, moduleDiameter, prevNose, prevCore, prevMount, newNose, newCore, newMount);
            }
        }

        public void UpdateSurfaceAttachNode(AttachNode node, float prevDiam, float prevTop, float prevUpper, float prevCore, float prevLower, float prevBottom, float newTop, float newUpper, float newCore, float newLower, float newBottom, bool userInput)
        {
            if (node is AttachNode && definition.surfaceNode is AttachNodeBaseData surfNodeData)
            {
                Vector3 pos = surfNodeData.position * moduleHorizontalScale;
                ROLAttachNodeUtils.UpdateAttachNodePosition(part, node, pos, surfNodeData.orientation, true, node.size);
                if (userInput)
                    ROLAttachNodeUtils.UpdateSurfaceAttachedChildren(part, prevDiam, moduleDiameter, prevTop, prevUpper, prevCore, prevLower, prevBottom, newTop, newUpper, newCore, newLower, newBottom);
            }
        }

        /// <summary>
        /// Internal helper method for updating of an attach node from attach-node data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="node"></param>
        /// <param name="invert"></param>
        /// <param name="userInput"></param>
        private void UpdateAttachNode(AttachNodeBaseData data, AttachNode node, bool invert, bool userInput, bool create = false, string nodeName = "")
        {
            if (node == null && !create) return;
            if (data is AttachNodeBaseData)
            {
                Vector3 pos = data.position;
                pos.y *= moduleVerticalScale;
                pos.x *= moduleHorizontalScale;
                pos.z *= moduleHorizontalScale;
                Vector3 ori = data.orientation;
                if (invert)
                {
                    pos.y = -pos.y;
                    pos.x = -pos.x;
                    ori.x = -ori.x; // Maybe?
                    ori.y = -ori.y;
                }
                int size = Mathf.RoundToInt(data.size * moduleHorizontalScale);
                pos.y += modulePosition + GetPlacementOffset();

                if (node == null && create)//create it
                    ROLAttachNodeUtils.createAttachNode(part, nodeName, pos, ori, size);
                else
                    ROLAttachNodeUtils.UpdateAttachNodePosition(part, node, pos, ori, userInput, size);
            }
        }

        #endregion ENDREGION - Public/External methods

        #region ENDREGION - Private/Internal methods

        /// <summary>
        /// Update the cached volume, mass, and cost values for the currently configured model setup.  Must be called anytime that model definition or scales are changed.
        /// </summary>
        private void UpdateModuleStats()
        {
            int positions = layout.positions.Count();
            float scale = moduleHorizontalScale * moduleHorizontalScale * moduleVerticalScale;
            float mScalar = Mathf.Pow(scale, MassScalar / 3);
            float vScalar = Mathf.Pow(scale, volumeScalar / 3);
            float cScalar = Mathf.Pow(scale, MassScalar / 3);
            moduleMass = definition.mass * mScalar * positions;
            moduleCost = definition.cost * cScalar * positions;
            moduleVolume = definition.volume * vScalar * positions;
            moduleHabitat = definition.habitat * vScalar;
            moduleSurfaceArea = definition.surfaceArea * vScalar;
            moduleTrussVolume = definition.trussVolume * vScalar;
            moduleTotalVolume = definition.totalVolume * vScalar;
        }

        /// <summary>
        /// Load custom colors from persistent color data.  Creates default array of colors if no data is present persistence.
        /// </summary>
        /// <param name="data"></param>
        private void LoadColors(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                string[] colorSplits = data.Split(';');
                int len = colorSplits.Length;
                recoloringData = new RecoloringData[len];
                for (int i = 0; i < len; i++)
                {
                    recoloringData[i] = new RecoloringData(colorSplits[i]);
                }
            }
        }

        /// <summary>
        /// Save the current custom color data to persistent data in part-module.
        /// </summary>
        /// <param name="colors"></param>
        private void SaveColors(RecoloringData[] colors)
        {
            if (colors == null || colors.Length == 0) { return; }
            int len = colors.Length;
            string data = string.Empty;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) data += ";";
                data += colors[i].getPersistentData();
            }
            persistentData = data;
        }

        /// <summary>
        /// Applies the currently selected texture set.  Does not validate anything.
        /// </summary>
        /// <param name="setName"></param>
        private void EnableTextureSet()
        {
            if (!string.IsNullOrEmpty(textureSetName) && textureSetName != "none" && textureSet != null)
                textureSet.enable(root, recoloringData);
        }

        /// <summary>
        /// Initialization method.  Validates the current texture set selection, assigns default set if current selection is invalid.
        /// </summary>
        private void SetupTextureSet()
        {
            bool useDefaultTextureColors = false;
            if (!IsValidTextureSet(textureSetName))
            {
                textureSetName = definition.GetDefaultTextureSet() is TextureSet def ? def.name : "none";
                if (!IsValidTextureSet(textureSetName))
                {
                    error($"Default texture set: {textureSetName} set for model: {definition.name} is invalid.  This is a configuration level error in the model definition that needs to be corrected.  Bad things are about to happen....");
                }
                useDefaultTextureColors = true;
            }
            else if (recoloringData == null || recoloringData.Length == 0)
            {
                useDefaultTextureColors = true;
            }
            ApplyTextureSet(textureSetName, useDefaultTextureColors);
        }

        /// <summary>
        /// Updates recoloring data for the input texture set, applies the texture set to the model, updates UI controls for the current texture set selection.<para/>
        /// Should be called whenever a new model is selected, or when a new texture set for the current model is chosen.
        /// </summary>
        /// <param name="setName"></param>
        /// <param name="useDefaultColors"></param>
        private void ApplyTextureSet(string setName, bool useDefaultColors)
        {
            textureSetName = setName;
            if (useDefaultColors || textureSet == null)
            {
                if (textureSet?.maskColors?.Length > 0)
                {
                    recoloringData = new RecoloringData[3];
                    recoloringData[0] = textureSet.maskColors[0];
                    recoloringData[1] = textureSet.maskColors[1];
                    recoloringData[2] = textureSet.maskColors[2];
                }
                else//invalid colors or texture set, create default placeholder color array
                {
                    //debug("Could not use default coloring from texture set: " + textureSetName + ".  No texture set or coloring data found.  Using placeholder coloring.  Module: "+getErrorReportModuleName());
                    RecoloringData placeholder = new RecoloringData(Color.white, 1, 1);
                    recoloringData = new RecoloringData[] { placeholder, placeholder, placeholder };
                }
                SaveColors(recoloringData);
            }
            EnableTextureSet();
            UpdateTextureUIControl();
            ROLModInterop.OnPartTextureUpdated(part);
        }

        /// <summary>
        /// Applies the current module position to the root transform of the ModelModule.  Does not adjust rotation or handle multi-model positioning setup for layouts.  Does not update scales.
        /// Loops through the individual model instances and updates their position, rotation, and scale, for the currently configured ModelLayoutData.  Does not update 'root' transform for module position.
        /// </summary>
        public void UpdateModelScalesAndLayoutPositions(bool doNotRescaleX = false)
        {
            root.transform.localPosition = new Vector3(0, modulePosition + GetPlacementOffset(), 0);
            int len = layout.positions.Length;
            float posScalar = getLayoutPositionScalar();
            float scaleScalar = getLayoutScaleScalar();
            for (int i = 0; i < len; i++)
            {
                Transform model = models[i];
                ModelPositionData mpd = layout.positions[i];
                model.transform.localPosition = mpd.localPosition * posScalar;
                model.transform.localRotation = Quaternion.Euler(mpd.localRotation) * Quaternion.Euler(moduleRotation);
                float xScale = doNotRescaleX ? 1 : moduleHorizontalScale;

                if (definition.compoundModelData != null)
                {
                    //on compound model setups, only adjust for the position scalar and mpd scale
                    //the model internal scale will be setup by the compound model data
                    model.transform.localScale = mpd.localScale * scaleScalar;
                    definition.compoundModelData.SetHeightFromScale(definition, model.gameObject, moduleHorizontalScale, moduleVerticalScale, definition.orientation);
                }
                else
                {
                    //normal models, apply all scales to the model root transform
                    model.transform.localScale = Mult(mpd.localScale, new Vector3(xScale, moduleVerticalScale, moduleHorizontalScale)) * scaleScalar;
                }
            }
        }

        private Vector3 Mult(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        /// <summary>
        /// Constructs all of the models for the current ModelDefinition and ModelLayoutData
        /// </summary>
        private void ConstructModels()
        {
            //create model array with length based on the positions defined in the ModelLayoutData
            int len = layout.positions.Length;
            models = new Transform[len];
            for (int i = 0; i < len; i++)
            {
                models[i] = new GameObject("ModelModule-" + i).transform;
                models[i].NestToParent(root);
                ConstructSubModels(models[i]);
            }
            Vector3 rotation = definition.shouldInvert(orientation) ? definition.invertAxis * 180f : Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(rotation);
        }

        /// <summary>
        /// Constructs a single model instance from the model definition, parents it to the input transform.<para/>
        /// Does not position or orient the created model; positionModels() should be called to update its position for the current ModelLayoutData configuration
        /// </summary>
        /// <param name="parent"></param>
        private void ConstructSubModels(Transform parent)
        {
            //add sub-models to the input model transform
            foreach (SubModelData smd in definition.subModelData)
            {
                if (ROLUtils.cloneModel(smd.modelURL) is GameObject clonedModel)
                {
                    clonedModel.transform.NestToParent(parent.transform);
                    clonedModel.transform.localRotation = Quaternion.Euler(smd.rotation);
                    clonedModel.transform.localPosition = smd.position;
                    clonedModel.transform.localScale = smd.scale;
                    if (!string.IsNullOrEmpty(smd.parent) && parent.transform.ROLFindRecursive(smd.parent) is Transform localParent)
                    {
                        clonedModel.transform.parent = localParent;
                    }
                    //de-activate any non-active sub-model transforms
                    //iterate through all transforms for the model and deactivate(destroy?) any not on the active mesh list
                    smd.SetupSubmodel(clonedModel);
                }
                else
                {
                    error($"Could not clone model for url: {smd.modelURL} while constructing meshes for model definition{definition.name} for: {GetErrorReportModuleName()}");
                }
            }
            if (definition?.mergeData is MeshMergeData[])
            {
                foreach (MeshMergeData mmd in definition.mergeData)
                {
                    mmd.MergeMeshes(parent);
                }
            }
        }

        /// <summary>
        /// Returns an offset to 'currentPosition' that is applied based on the Module orientation vs. the model-definition orientation.
        /// </summary>
        /// <returns></returns>
        private float GetPlacementOffset()
        {
            float offset = 0;
            if (orientation == ModelOrientation.TOP && definition.orientation == ModelOrientation.CENTRAL)
                offset = moduleHeight / 2;
            else if (orientation == ModelOrientation.CENTRAL && definition.orientation == ModelOrientation.TOP)
                offset = -moduleHeight / 2;
            else if (orientation == ModelOrientation.CENTRAL && definition.orientation == ModelOrientation.BOTTOM)
                offset = moduleHeight / 2;
            else if (orientation == ModelOrientation.BOTTOM && definition.orientation == ModelOrientation.CENTRAL)
                offset = -moduleHeight / 2;
            return offset;
        }

        /// <summary>
        /// Return true/false if the input texture set name is a valid texture set for this model definition.
        /// <para/>
        /// If the model does not contain any defined texture sets, return true if the input name is 'default' or 'none'
        /// otherwise, examine the array of texture sets and return true/false depending on if the input name was found
        /// in the defined sets.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private bool IsValidTextureSet(string val) =>
            definition.textureSets.Length == 0 ? val == "none" || val == "default" : definition.textureSets.ROLExists(m => m.name == val);

        /// <summary>
        /// Internal utility method to allow accessing of symmetry ModelModules' in symmetry parts/part-modules
        /// </summary>
        /// <param name="action"></param>
        private void ActionWithSymmetry(Action<ROLModelModule<U>> action)
        {
            action(this);
            int index = part.Modules.IndexOf(partModule);
            foreach (Part p in part.symmetryCounterparts)
            {
                action(getSymmetryModule((U)p.Modules[index]));
            }
        }

        /// <summary>
        /// Return a string representing the module name and other debug related information.  Used in error logging.
        /// </summary>
        /// <returns></returns>
        private string GetErrorReportModuleName() =>
            $"ModelModule: [{name}] model: [{definition}] in orientation: [{orientation}] in module: {partModule} in part: {part}";

        #endregion ENDREGION - Private/Internal methods

        public ModelDefinitionLayoutOptions[] getValidModels(ModelDefinitionLayoutOptions[] inputOptions, string coreName)
        {
            List<ModelDefinitionLayoutOptions> validDefs = new List<ModelDefinitionLayoutOptions>();
            ModelDefinitionLayoutOptions def;
            int len = inputOptions.Length;
            for (int i = 0; i < len; i++)
            {
                def = inputOptions[i];
                //String reqCore = def.definition.requiredCore;
                foreach (string reqCore in def.definition.requiredCore)
                {
                    if (reqCore == "ALL" || reqCore == coreName)
                    {
                        validDefs.Add(def);
                    }
                }
            }
            return validDefs.ToArray();
        }

        public ROLModelDefinition findFirstValidModel(ROLModelModule<U> module, string coreName)
        {
            //return module.optionsCache.FirstOrDefault(x => x.definition.requiredCore == coreName || x.definition.requiredCore == "ALL")?.definition;
            int len = module.optionsCache.Length;
            for (int i = 0; i < len; i++)
            {
                if (module.optionsCache[i].definition.requiredCore.Contains(coreName) || module.optionsCache[i].definition.requiredCore.Contains("ALL"))
                {
                    return module.optionsCache[i].definition;
                }
            }
            //error("Could not locate any valid upper modules matching def: " + definition);
            return null;
        }

        public bool isValidModel(ROLModelModule<U> module, string coreName)
        {
            return isValidModel(module.definition, coreName);
        }

        public bool isValidModel(ROLModelDefinition def, string coreName)
        {
            if (def.requiredCore.Contains(coreName) || def.requiredCore.Contains("ALL"))
            {
                return true;
            }
            //error("Could not locate any valid upper modules matching def: " + def);
            return false;
        }

    }
}
