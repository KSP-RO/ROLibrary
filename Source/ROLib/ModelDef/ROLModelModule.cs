﻿using System;
using System.Linq;
using UnityEngine;
using KSPShaderTools;
using static ROLib.ROLLog;

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
        /// Internal cached 'name' for this model-module.  Used in error-reporting to more easily tell the difference between various modules in any given part.
        /// </summary>
        private string moduleName = "ROLModelModule";

        /// <summary>
        /// Local cache to the root transforms of the models used for the layout.  If only a single position in layout, this will be a length=1 array.
        /// Will contain one transform for each position in the layout, with identical ordering to the specification in the layout.
        /// </summary>
        private Transform[] models;

        /// <summary>
        /// Local cache of the recoloring data to use for this module.  Loaded from persistence data if the recoloring persistence field is present.  Auto-saved out to persistence field on color updates.
        /// May be NULL if no coloring data has been loaded.
        /// </summary>
        private RecoloringData[] customColors;

        /// <summary>
        /// The -current- model definition.  Pulled from the array of all defs.
        /// </summary>
        private ModelDefinitionLayoutOptions currentLayoutOptions;

        /// <summary>
        /// The -current- model definition.  Cached local access to the def stored in the current layout option.
        /// </summary>
        private ROLModelDefinition currentDefinition;

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
        private BaseField dataField;

        /// <summary>
        /// Local reference to the persistent data field used to store texture set names for this module.  May be null when texture switching is not used.
        /// </summary>
        private BaseField textureField;

        /// <summary>
        /// Local reference to the persistent data field used to store the current model name for this module.  Must not be null.
        /// </summary>
        private BaseField modelField;

        /// <summary>
        /// Local referenec to the persistent data field used to store the current layout name for this module.  May be null if layouts are unsupported, in which case it will always return 'defualt'.
        /// </summary>
        private BaseField layoutField;

        /// <summary>
        /// Scaling powers used for mass, volume, and engine/rcs thrust values
        /// </summary>
        private float massScalePower = 3f;
        private float volumeScalePower = 3f;
        private float thrustScalePower = 3f;

        /// <summary>
        /// Local cached working variables for scale, sizing, mass, and cost.
        /// </summary>
        private float currentHorizontalScale = 1f;
        private float currentVerticalScale = 1f;
        private float currentDiameter;
        private float currentHeight;
        private float panelLength;
        private float panelWidth;
        private float currentVerticalPosition;
        private float currentCost;
        private float currentMass;
        private float currentVolume;
        private float actualHeight;
        private string secondaryTransformName;
        private string pivotName;
        private string animationName;

        #endregion ENDREGION - Private working data

        #region REGION - BaseField wrappers

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        private string textureSetName
        {
            get { return textureField == null ? "default" : textureField.GetValue<string>(partModule); }
            set { if (textureField != null) { textureField.SetValue(value, partModule); } }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        private string modelName
        {
            get { return modelField.GetValue<string>(partModule); }
            set { modelField.SetValue(value, partModule); }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        private string persistentData
        {
            get { return dataField == null ? string.Empty : dataField.GetValue<string>(partModule); }
            set { if (dataField != null) { dataField.SetValue(value, partModule); } }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        private string layoutName
        {
            get { return layoutField == null ? "default" : layoutField.GetValue<string>(partModule); }
            set { if (layoutField != null) { layoutField.SetValue(value, partModule); } }
        }

        #endregion ENDREGION - BaseField wrappers

        #region REGION - Convenience wrappers for accessing model definition data for external use

        /// <summary>
        /// Return the current 'name' of this model-module.  Currently only used in error reporting.<para/>
        /// Name must be set manually after module is instantiated.
        /// </summary>
        public string name
        {
            get { return moduleName; }
            set { moduleName = value; }
        }

        /// <summary>
        /// Return true/false if this module has engine transform data for its current configuration.
        /// </summary>
        public bool engineTransformEnabled { get { return definition.engineTransformData != null; } }

        /// <summary>
        /// Return true/false if this module has engine thrust data for its current configuration.
        /// </summary>
        public bool engineThrustEnabled { get { return definition.engineThrustData != null; } }

        //TODO -- create specific gimbal transform data holder class
        /// <summary>
        /// Return true/false if this module has gimbal transform data for its current configuration.
        /// </summary>
        public bool engineGimalEnabled { get { return definition.engineTransformData != null; } }

        /// <summary>
        /// Return the currently 'active' model definition.
        /// </summary>
        public ROLModelDefinition definition { get { return currentDefinition; } }

        /// <summary>
        /// Return the currently active texture set from the currently active model definition.
        /// </summary>
        public TextureSet textureSet { get { return definition.findTextureSet(textureSetName); } }

        /// <summary>
        /// Return the currently active model layout.
        /// </summary>
        public ModelLayoutData layout { get { return currentLayoutOptions.layouts.ROLFind(m=>m.name==layoutName); } }

        /// <summary>
        /// Return the currently active layout options for the current model definition.
        /// </summary>
        public ModelDefinitionLayoutOptions layoutOptions { get { return currentLayoutOptions; } }

        public float volumeScalar
        {
            get { return volumeScalePower; }
            set { volumeScalePower = value; }
        }

        public float massScalar
        {
            get { return massScalePower; }
            set { massScalePower = value; }
        }

        public float thrustScalar
        {
            get { return thrustScalePower; }
            set { thrustScalePower = value; }
        }

        /// <summary>
        /// Return the current mass for this module slot.  Includes adjustments from the definition mass based on the current scale.
        /// </summary>
        public float moduleMass { get { return currentMass; } }

        /// <summary>
        /// Return the current cost for this module slot.  Includes adjustments from the definition cost based on the current scale.
        /// </summary>
        public float moduleCost { get { return currentCost; } }

        /// <summary>
        /// Return the current usable resource volume for this module slot.  Includes adjustments from the definition volume based on the current scale.
        /// </summary>
        public float moduleVolume { get { return currentVolume; } }

        /// <summary>
        /// Return the current diameter of the model in this module slot.  This is the base diamter as specified in the model definition, modified by the currently specified scale.
        /// </summary>
        public float moduleDiameter { get { return currentDiameter; } }
        public float modulePanelLength { get { return panelLength; } }
        public float modulePanelWidth { get { return panelWidth; } }

        /// <summary>
        /// Return the current upper-mounting diamter of the model in this module slot.  This value is to be used for sizing/scaling of any module slot used for an upper-adapter/nose option for this slot.
        /// </summary>
        public float moduleUpperDiameter { get { return (definition.shouldInvert(orientation) ? definition.lowerDiameter : definition.upperDiameter) * currentHorizontalScale; } }

        /// <summary>
        /// Return the current lower-mounting diamter of the model in this module slot.  This value is to be used for sizing/scaling of any module slot used for a lower-adapter/mount option for this slot.
        /// </summary>
        public float moduleLowerDiameter { get { return (definition.shouldInvert(orientation) ? definition.upperDiameter : definition.lowerDiameter) * currentHorizontalScale; } }

        /// <summary>
        /// Return the current height of the model in this module slot.  Based on the definition specified height and the current vertical scale.
        /// </summary>
        public float moduleHeight { get { return currentHeight; } }

        /// <summary>
        /// Return the actual height of the model in this module slot used for Booster style tanks.
        /// </summary>
        public float moduleActualHeight { get { return actualHeight; } }

        /// <summary>
        /// Return the current x/z scaling used by the model in this module slot.
        /// </summary>
        public float moduleHorizontalScale { get { return currentHorizontalScale; } }

        /// <summary>
        /// Return the current y scaling used by the model in this module slot.
        /// </summary>
        public float moduleVerticalScale { get { return currentVerticalScale; } }

        /// <summary>
        /// Return the current origin position of the Y corrdinate of this module, in part-centric space.<para/>
        /// A value of 0 denotes origin is at the parts' origin/COM.
        /// </summary>
        public float modulePosition { get { return currentVerticalPosition; } }

        /// <summary>
        /// Return the Y coordinate of the top-most point in the model in part-centric space, as defined by model-height in the model definition and modified by current model scale,
        /// </summary>
        public float moduleTop
        {
            get
            {
                //the current 'origin' of the model
                float pos = currentVerticalPosition;
                //adjust the 'top' value based on the orientation
                if (orientation == ModelOrientation.TOP)
                {
                    //the current 'origin' is at the bottom of the model
                    //offset for current height
                    pos += moduleHeight;
                }
                else if (orientation == ModelOrientation.CENTRAL)
                {
                    //the current 'origin' is in the center of the model
                    //offset for half of current height
                    pos += moduleHeight * 0.5f;
                }
                else if (orientation == ModelOrientation.BOTTOM)
                {
                    //the current 'origin' is at the top of the model
                    //return unadjusted position
                }
                return pos;
            }
        }

        /// <summary>
        /// Return the Y coordinate of the physical 'center' of this model in part-centric space.
        /// </summary>
        public float moduleCenter { get { return moduleTop - 0.5f * moduleHeight; } }

        /// <summary>
        /// Returns the Y coordinate of the bottom of this model in part-centric space.
        /// </summary>
        public float moduleBottom { get { return moduleTop - moduleHeight; } }

        /// <summary>
        /// Return the currently configured custom color data for this module slot.
        /// </summary>
        public RecoloringData[] recoloringData { get { return customColors; } }

        /// <summary>
        /// Return the transforms that represent the root transforms for the models in this module slot.  Under normal circumstaces (standard single model layout), this should return an array of a single transform.
        /// </summary>
        public Transform[] moduleModelTransforms { get { return models; } }

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
            this.modelField = partModule.Fields[modelPersistenceFieldName];
            this.layoutField = partModule.Fields[layoutPersistenceFieldName];
            this.textureField = partModule.Fields[texturePersistenceFieldName];
            this.dataField = partModule.Fields[recolorPersistenceFieldName];
            getValidOptions = delegate () { return optionsCache; };
            loadColors(persistentData);
        }

        public override string ToString()
        {
            return getErrorReportModuleName();
        }

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
                error("No models found for: " + getErrorReportModuleName());
            }
            if (!Array.Exists(optionsCache, m => m.definition.name == modelName))
            {
                error("Currently configured model name: " + modelName + " was not located while setting up: "+getErrorReportModuleName());
                modelName = optionsCache[0].definition.name;
                error("Now using model: " + modelName + " for: "+getErrorReportModuleName());
            }
        }

        /// <summary>
        /// Initialization method.  Creates the model transforms, and sets their position and scale to the current config values.<para/>
        /// Initializes texture set, including 'defualts' handling.  Initializes animation module with the animation data for the current model.<para/>
        /// Only for use during part initialization.  Subsequent changes to model should call the modelSelectedXXX methods.
        /// </summary>
        public void setupModel()
        {
            ROLUtils.destroyChildrenImmediate(root);
            currentLayoutOptions = Array.Find(optionsCache, m => m.definition.name == modelName);
            if (currentLayoutOptions == null)
            {
                error("Could not locate model definition for: " + modelName + " for " + getErrorReportModuleName());
            }
            currentDefinition = currentLayoutOptions.definition;
            currentLayout = currentLayoutOptions.getLayout(layoutName);
            if (!currentLayoutOptions.isValidLayout(layoutName))
            {
                log("Existing layout: "+layoutName+" for " + getErrorReportModuleName() + " was null.  Assigning default layout: " + currentLayoutOptions.getDefaultLayout().name);
                layoutName = currentLayoutOptions.getDefaultLayout().name;
            }
            constructModels();
            updateModelScalesAndLayoutPositions();
            updateModelMeshes();
            setupTextureSet();
            updateModuleStats();
        }

        #endregion ENDREGION - Constructors and Init Methods

        #region REGION - Update Methods

        /// <summary>
        /// If the model definition contains rcs-thrust-transform data, will rename the model's rcs thrust transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleRCS when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void renameRCSThrustTransforms(string destinationName)
        {
            if (definition.rcsModuleData == null)
            {
                //not really an error -- null is a valid value for many model defs
                //error("RCS module data (transformNames,thrust) is null for model definition: " + definition.name+" for: "+getErrorReportModuleName()+"\nCould not update RCS transform names.");
                //TODO -- need to add a dummy RCS transform if one is not already present, to prevent stock modules' from spamming log with errors
                return;
            }
            definition.rcsModuleData.renameTransforms(root, destinationName);
        }

        /// <summary>
        /// If the model definition contains engine-thrust-transform data, will rename the model's engine thrust transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleEngines when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void renameEngineThrustTransforms(string destinationName)
        {
            if (definition.engineTransformData == null)
            {
                //not really an error -- null is a valid value for many model defs
                //error("Engine transform data is null for model definition: " + definition.name + " for: "+getErrorReportModuleName() + "\nCould not update engine thrust transform names.");
                return;
            }
            definition.engineTransformData.renameThrustTransforms(root, destinationName);
        }

        /// <summary>
        /// If the model definition contains gimbal-transform data, will rename the model's gimbal transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleGimbal when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void renameGimbalTransforms(string destinationName)
        {
            if (definition.engineTransformData == null)
            {
                //not really an error -- null is a valid value for many model defs
                //error("Engine transform data is null for model definition: " + definition.name+" for: "+getErrorReportModuleName());
                return;
            }
            definition.engineTransformData.renameGimbalTransforms(root, destinationName);
        }

        /// <summary>
        /// Update the input moduleEngines min, max, and split thrust values.  Any engine thrust transforms need to have been already renamed prior to this call.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="thrustScalePower"></param>
        public void updateEngineModuleThrust(ModuleEngines engine, float thrustScalePower)
        {
            if (engine == null || !engineThrustEnabled) { return; }
            float scalar = Mathf.Pow(Mathf.Sqrt(currentHorizontalScale * currentVerticalScale), thrustScalePower);
            float min = definition.engineThrustData.minThrust * layout.positions.Count() * scalar;
            float max = definition.engineThrustData.maxThrust * layout.positions.Count() * scalar;
            float[] splitThrust = definition.engineThrustData.getCombinedSplitThrust(layout.positions.Count());
            engine.thrustTransformMultipliers = splitThrust.ToList();
            ROLStockInterop.UpdateEngineThrust(engine, min, max); //calls engine.OnLoad(...);
        }

        /// <summary>
        /// Update the input moduleRCS enabled, thrust and axis enable/disable status.  Calls rcs.OnStart() to update
        /// </summary>
        /// <param name="rcs"></param>
        /// <param name="thrustScaleFactor"></param>
        public void updateRCSModule(ModuleRCS rcs, float thrustScaleFactor)
        {
            float power = 0;
            if (definition.rcsModuleData != null)
            {
                ModelRCSModuleData data = definition.rcsModuleData;
                power = data.rcsThrust;
                float scale = Mathf.Sqrt(currentHorizontalScale * currentVerticalScale);
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
        public void textureSetSelected(BaseField field, System.Object oldValue)
        {
            actionWithSymmetry(m =>
            {
                m.textureSetName = textureSetName;
                m.applyTextureSet(m.textureSetName, !ROLGameSettings.persistRecolor());
                if (m.textureField != null)
                {
                    m.partModule.ROLupdateUIChooseOptionControl(m.textureField.name, m.definition.getTextureSetNames(), m.definition.getTextureSetTitles(), true, m.textureSetName);
                }
            });
        }

        /// <summary>
        /// Symmetry-enabled method.  Should only be called when symmetry updates are desired.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="oldValue"></param>
        public void modelSelected(BaseField field, System.Object oldValue)
        {
            actionWithSymmetry(m =>
            {
                m.setupModel();
            });
        }

        /// <summary>
        /// Symmetry-enabled method.  Should only be called when symmetry updates are desired.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="oldValue"></param>
        public void layoutSelected(BaseField field, System.Object oldValue)
        {
            actionWithSymmetry(m =>
            {
                if (m != this) { m.layoutName = layoutName; }
                m.layoutSelected(m.layoutName);
            });
        }

        /// <summary>
        /// Symmetry enabled.  Updates the current persistent color data, and reapplies the textures/color data to the models materials.
        /// </summary>
        /// <param name="colors"></param>
        public void setSectionColors(RecoloringData[] colors)
        {
            actionWithSymmetry(m =>
            {
                m.textureSetName = textureSetName;
                m.customColors = colors;
                m.enableTextureSet();
                m.saveColors(m.customColors);
            });
        }

        /// <summary>
        /// NON-Symmetry enabled method.<para/>
        /// Sets the currently selected model name to the input model, and setup
        /// </summary>
        /// <param name="newModel"></param>
        public void modelSelected(string newModel)
        {
            if (Array.Exists(optionsCache, m => m.definition.name == newModel))
            {
                modelName = newModel;
                setupModel();
            }
            else
            {
                error("No model definition found for input name: " + newModel+ " for: "+getErrorReportModuleName());
            }
        }

        /// <summary>
        /// NON-Symmetry enabled method.  Sets the current layout and updates models for current layout.  Uses current vertical position/all other current position data.
        /// </summary>
        /// <param name="newLayout"></param>
        public void layoutSelected(string newLayout)
        {
            if (!currentLayoutOptions.isValidLayout(newLayout))
            {
                newLayout = currentLayoutOptions.getDefaultLayout().name;
                error("Could not find layout definition by name: " + newLayout + " using default layout for model: " + getErrorReportModuleName());
            }
            layoutName = newLayout;
            currentLayout = currentLayoutOptions.getLayout(newLayout);
            setupModel();
            updateSelections();
        }

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
                if (getValidOptions == null) { error("ModelModule delegate 'getValidOptions' is not populated for: " + getErrorReportModuleName()); }
                ModelDefinitionLayoutOptions[] availableOptions = getValidOptions();
                if (availableOptions == null || availableOptions.Length < 1) { MonoBehaviour.print("ERROR: No valid models found for: " + getErrorReportModuleName()); }
                string[] names = ROLUtils.getNames(availableOptions, s => s.definition.name);
                string[] displays = ROLUtils.getNames(availableOptions, s => s.definition.title);
                partModule.ROLupdateUIChooseOptionControl(modelField.name, names, displays, true, modelName);
                modelField.guiActiveEditor = names.Length > 1;
            }
            //updates the texture set selection for the currently configured model definition, including disabling of the texture-set selection UI when needed
            if (textureField != null)
            {
                partModule.ROLupdateUIChooseOptionControl(textureField.name, definition.getTextureSetNames(), definition.getTextureSetTitles(), true, textureSetName);
            }
            if (layoutField != null)
            {
                ModelDefinitionLayoutOptions mdlo = optionsCache.ROLFind(m => m.definition == definition);
                string[] layoutNames = mdlo.getLayoutNames();
                string[] layoutTitles = mdlo.getLayoutTitles();
                partModule.ROLupdateUIChooseOptionControl(layoutField.name, layoutNames, layoutTitles, true, layoutName);
                layoutField.guiActiveEditor = layoutField.guiActiveEditor && currentLayout.positions.Length > 1;
            }
        }

        /// <summary>
        /// NON-symmetry enabled method.
        /// Updates the current models with the current scale and position data.  Includes setup of current layout and its internal scale factors.
        /// </summary>
        public void updateModelMeshes()
        {
            updateModelScalesAndLayoutPositions();
        }

        /// <summary>
        /// NON-symmetry enabled method.
        /// Updates the current models with the current scale and position data.  Includes setup of current layout and its internal scale factors.
        /// </summary>
        public void updateModelMeshes(bool solar)
        {
            updateModelScalesAndLayoutPositions(solar);
        }


        /// <summary>
        /// Updates the diamter/scale values so that the upper-diameter of this model matches the input diamter
        /// </summary>
        /// <param name="newDiameter"></param>
        public void setDiameterFromAbove(float newDiameter, float vScalar = 0f)
        {
            float baseUpperDiameter = definition.shouldInvert(orientation) ? definition.lowerDiameter : definition.upperDiameter;
            float scale = newDiameter / baseUpperDiameter;
            setScale(scale, scale * vScaleOffset(vScalar));
        }

        /// <summary>
        /// Updates the diamter/scale values so that the lower-diameter of this model matches the input diamter
        /// </summary>
        /// <param name="newDiameter"></param>
        public void setDiameterFromBelow(float newDiameter, float vScalar = 0f)
        {
            float baseLowerDiameter = definition.shouldInvert(orientation) ? definition.upperDiameter : definition.lowerDiameter;
            float scale = newDiameter / baseLowerDiameter;
            setScale(scale, scale * vScaleOffset(vScalar));
        }

        /// <summary>
        /// Updates the diameter/scale values so that the core-diameter of this model matches the input diameter
        /// </summary>
        /// <param name="newDiameter"></param>
        public void setScaleForDiameter(float newDiameter, float vScalar = 0f)
        {
            float newScale = newDiameter / definition.diameter;
            setScale(newScale, newScale * vScaleOffset(vScalar));
        }

        /// <summary>
        /// Updates the current internal scale values for the input diameter and height values.
        /// </summary>
        /// <param name="newHeight"></param>
        /// <param name="newDiameter"></param>
        public void setScaleForHeightAndDiameter(float newHeight, float newDiameter)
        {
            float newHorizontalScale = newDiameter / definition.diameter;
            float newVerticalScale = newHeight / definition.height;
            setScale(newHorizontalScale, newVerticalScale);
        }

        /// <summary>
        /// Updates the current internal scale values for the input diameter and height values.
        /// </summary>
        /// <param name="newHeight"></param>
        /// <param name="newDiameter"></param>
        public void setScaleForHeightAndDiameter(float newHeight, float newDiameter, bool solar)
        {
            float newHorizScale, newVertScale = 0.0f;
            if (solar)
            {
                newHorizScale = newDiameter / definition.panelWidth;
                newVertScale = newHeight / definition.panelLength;
            }
            else
            {
                newHorizScale = newVertScale = newDiameter;
            }
            setScale(newHorizScale, newVertScale, true);
        }

        /// <summary>
        /// Updates the current internal scale values for the input scale.  Sets x,y,z scale to the input value specified.
        /// </summary>
        /// <param name="newScale"></param>
        public void setScale(float newScale)
        {
            setScale(newScale, newScale);
        }

        /// <summary>
        /// Updates the current internal scale values for the input scales.  Updates x,z with the 'horizontal scale' and updates 'y' with the 'vertical scale'.
        /// </summary>
        /// <param name="newHorizontalScale"></param>
        /// <param name="newVerticalScale"></param>
        public void setScale(float newHorizontalScale, float newVerticalScale)
        {
            float min = newHorizontalScale * definition.minVerticalScale;
            float max = newHorizontalScale * definition.maxVerticalScale;
            newVerticalScale = Mathf.Clamp(newVerticalScale, min, max);
            currentHorizontalScale = newHorizontalScale;
            currentVerticalScale = newVerticalScale;
            currentHeight = newVerticalScale * definition.height;
            actualHeight = newVerticalScale * definition.actualHeight;
            currentDiameter = newHorizontalScale * definition.diameter;
            updateModuleStats();
        }

        public void setScale(float newHorizontalScale, float newVerticalScale, bool solar)
        {
            float min = newHorizontalScale * definition.minVerticalScale;
            float max = newHorizontalScale * definition.maxVerticalScale;
            newVerticalScale = Mathf.Clamp(newVerticalScale, min, max);
            currentHorizontalScale = newHorizontalScale;
            currentVerticalScale = newVerticalScale;
            currentHeight = newVerticalScale * definition.height;
            actualHeight = newVerticalScale * definition.actualHeight;
            panelLength = newVerticalScale * definition.panelLength;
            currentDiameter = newHorizontalScale * definition.diameter;
            panelWidth = newHorizontalScale * definition.panelWidth;
            updateModuleStats();
        }

        public string GetSecondaryTransform()
        {
            secondaryTransformName = definition.secondaryTransformName;
            return secondaryTransformName;
        }

        public string GetPivotName()
        {
            pivotName = definition.pivotName;
            return pivotName;
        }

        public string GetAnimationName()
        {
            animationName = definition.animationName;
            return animationName;
        }

        private float vScaleOffset(float aspectInput)
        {
            float min = definition.minVerticalScale;
            float max = definition.maxVerticalScale;
            float vScale = 1f;
            if (aspectInput < 0)
            {
                aspectInput = Mathf.Abs(aspectInput);
                vScale -= aspectInput * (1 - min);
            }
            else if (aspectInput > 0)
            {
                vScale += aspectInput * (max - 1);
            }
            return vScale;
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - Public/External methods

        /// <summary>
        /// Updates the input texture-control text field with the texture-set names for this model.  Disables field if no texture sets found, enables field if more than one texture set is available.
        /// </summary>
        public void updateTextureUIControl()
        {
            if (textureField == null) { return; }
            string[] names = definition.getTextureSetNames();
            string[] titles = definition.getTextureSetTitles();
            partModule.ROLupdateUIChooseOptionControl(textureField.name, names, titles, true, textureSetName);
            textureField.guiActiveEditor = names.Length > 1;
        }

        /// <summary>
        /// Updates the position of the model.
        /// </summary>
        /// <param name="originPos"></param>
        public void setPosition(float originPos)
        {
            currentVerticalPosition = originPos;
        }

        /// <summary>
        /// Updates the attach nodes on the part for the input list of attach nodes and the current specified nodes for this model.
        /// Any 'extra' attach nodes from the part will be disabled.
        /// </summary>
        /// <param name="nodeNames"></param>
        /// <param name="userInput"></param>
        public void updateAttachNodeBody(String[] nodeNames, bool userInput)
        {
            if (nodeNames == null || nodeNames.Length < 1) { return; }
            if (nodeNames.Length == 1 && (nodeNames[0] == "NONE" || nodeNames[0] == "none")) { return; }
            float currentVerticalPosition = this.currentVerticalPosition;

            AttachNode node = null;
            AttachNodeBaseData data;

            Vector3 pos = Vector3.zero;
            Vector3 orient = Vector3.up;
            int size = 4;

            bool invert = definition.shouldInvert(orientation);

            int nodeCount = definition.bodyNodeData == null ? 0 : definition.bodyNodeData.Length;
            int len = nodeNames.Length;
            for (int i = 0; i < len; i++)
            {
                node = part.FindAttachNode(nodeNames[i]);
                if (i < nodeCount)
                {
                    data = definition.bodyNodeData[i];
                    size = Mathf.RoundToInt(data.size * currentHorizontalScale);
                    pos = data.position;
                    pos.y *= currentVerticalScale;
                    pos.x *= currentHorizontalScale;
                    pos.z *= currentHorizontalScale;
                    if (invert)
                    {
                        pos.y = -pos.y;
                        pos.x = -pos.x;
                    }
                    pos.y += currentVerticalPosition;
                    orient = data.orientation;
                    if (invert) { orient = -orient; orient.z = -orient.z; }
                    if (node == null)//create it
                    {
                        ROLAttachNodeUtils.createAttachNode(part, nodeNames[i], pos, orient, size);
                    }
                    else//update its position
                    {
                        ROLAttachNodeUtils.updateAttachNodePosition(part, node, pos, orient, userInput, size);
                    }
                }
                else//extra node, destroy
                {
                    if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
                    {
                        ROLAttachNodeUtils.destroyAttachNode(part, node);
                    }
                }
            }
        }

        /// <summary>
        /// Update the input attach node for the 'top' node specified in current model def
        /// </summary>
        /// <param name="nodeName"></param>
        /// <param name="userInput"></param>
        public void updateAttachNodeTop(string nodeName, bool userInput)
        {
            bool invert = definition.shouldInvert(orientation);
            AttachNodeBaseData nodeData = invert ? definition.bottomNodeData : definition.topNodeData;
            if (nodeData == null)
            {
                //TODO - disable attach node if unoccupied
                debug("TODO - Disable unused and empty attach node");
                return;
            }
            AttachNode node = part.FindAttachNode(nodeName);
            updateAttachNode(nodeData, node, invert, userInput);
        }

        /// <summary>
        /// Update the input attach node for the 'bottom' node specified in current model def
        /// </summary>
        /// <param name="nodeName"></param>
        /// <param name="userInput"></param>
        public void updateAttachNodeBottom(string nodeName, bool userInput)
        {
            bool invert = definition.shouldInvert(orientation);
            AttachNodeBaseData nodeData = invert ? definition.topNodeData : definition.bottomNodeData;
            if (nodeData == null)
            {
                //TODO - disable attach node if unoccupied
                debug("TODO - Disable unused and empty attach node");
                return;
            }
            AttachNode node = part.FindAttachNode(nodeName);
            updateAttachNode(nodeData, node, invert, userInput);
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
            if (node != null)
            {
                float currentDiameter = moduleDiameter;
                float hScale = currentDiameter / definition.diameter;
                int size = node.size;
                AttachNodeBaseData surfNodeData = definition.surfaceNode;
                Vector3 pos = surfNodeData.position * hScale;
                Vector3 ori = surfNodeData.orientation;
                ROLAttachNodeUtils.updateAttachNodePosition(part, node, pos, ori, userInput, size);
                if (userInput)
                    ROLAttachNodeUtils.updateSurfaceAttachedChildren(part, prevDiameter, currentDiameter);
            }
        }

        public void updateSurfaceAttachNode(AttachNode node, float length, float width, bool userInput)
        {
            if (node != null)
            {
                int size = 1;
                AttachNodeBaseData surfNodeData = definition.surfaceNode;
                float lengthScale = definition.surfaceNodeY * (length / definition.panelLength);
                float widthScale = definition.surfaceNodeX * (width / definition.panelWidth);
                Vector3 pos = new Vector3(widthScale, lengthScale, definition.surfaceNodeZ);
                Vector3 ori = surfNodeData.orientation;
                ROLAttachNodeUtils.updateAttachNodePosition(part, node, pos, ori, userInput, size);
            }
        }

        /// <summary>
        /// Internal helper method for updating of an attach node from attach-node data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="node"></param>
        /// <param name="invert"></param>
        /// <param name="userInput"></param>
        private void updateAttachNode(AttachNodeBaseData data, AttachNode node, bool invert, bool userInput)
        {
            if (node == null) { return; }
            Vector3 pos = data.position;
            pos.y *= currentVerticalScale;
            pos.x *= currentHorizontalScale;
            pos.z *= currentHorizontalScale;
            Vector3 ori = data.orientation;
            if (invert)
            {
                pos.y = -pos.y;
                pos.x = -pos.x;
                ori.y = -ori.y;
            }
            int size = Mathf.RoundToInt(data.size * currentHorizontalScale);
            pos.y += modulePosition + getPlacementOffset();
            ROLAttachNodeUtils.updateAttachNodePosition(part, node, pos, ori, userInput, size);
        }

        #endregion ENDREGION - Public/External methods

        #region ENDREGION - Private/Internal methods

        /// <summary>
        /// Update the cached volume, mass, and cost values for the currently configured model setup.  Must be called anytime that model definition or scales are changed.
        /// </summary>
        private void updateModuleStats()
        {
            int positions = layout.positions.Count();
            float averageScale = (moduleHorizontalScale + moduleHorizontalScale + moduleVerticalScale) / 3;
            float mScalar = Mathf.Pow(averageScale, massScalar);
            float vScalar = Mathf.Pow(averageScale, volumeScalar);
            float cScalar = Mathf.Pow(averageScale, massScalar);
            currentMass = definition.mass * mScalar * positions;
            currentCost = definition.cost * cScalar * positions;
            currentVolume = definition.volume * vScalar * positions;
        }

        /// <summary>
        /// Load custom colors from persistent color data.  Creates default array of colors if no data is present persistence.
        /// </summary>
        /// <param name="data"></param>
        private void loadColors(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                string[] colorSplits = data.Split(';');
                int len = colorSplits.Length;
                customColors = new RecoloringData[len];
                for (int i = 0; i < len; i++)
                {
                    customColors[i] = new RecoloringData(colorSplits[i]);
                }
            }
        }

        /// <summary>
        /// Save the current custom color data to persistent data in part-module.
        /// </summary>
        /// <param name="colors"></param>
        private void saveColors(RecoloringData[] colors)
        {
            if (colors == null || colors.Length == 0) { return; }
            int len = colors.Length;
            string data = string.Empty;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) { data = data + ";"; }
                data = data + colors[i].getPersistentData();
            }
            persistentData = data;
        }

        /// <summary>
        /// Applies the currently selected texture set.  Does not validate anything.
        /// </summary>
        /// <param name="setName"></param>
        private void enableTextureSet()
        {
            if(string.IsNullOrEmpty(textureSetName) || textureSetName == "none" )
            {
                return;
            }
            TextureSet textureSet = this.textureSet;
            if (textureSet != null)
            {
                textureSet.enable(root, customColors);
            }
        }

        /// <summary>
        /// Initialization method.  Validates the current texture set selection, assigns default set if current selection is invalid.
        /// </summary>
        private void setupTextureSet()
        {
            bool useDefaultTextureColors = false;
            if (!isValidTextureSet(textureSetName))
            {
                TextureSet def = definition.getDefaultTextureSet();
                textureSetName = def == null ? "none" : def.name;
                if (!isValidTextureSet(textureSetName))
                {
                    error("Default texture set: " + textureSetName + " set for model: " + definition.name + " is invalid.  This is a configuration level error in the model definition that needs to be corrected.  Bad things are about to happen....");
                }
                useDefaultTextureColors = true;
            }
            else if (customColors == null || customColors.Length == 0)
            {
                useDefaultTextureColors = true;
            }
            applyTextureSet(textureSetName, useDefaultTextureColors);
        }

        /// <summary>
        /// Updates recoloring data for the input texture set, applies the texture set to the model, updates UI controls for the current texture set selection.<para/>
        /// Should be called whenever a new model is selected, or when a new texture set for the current model is chosen.
        /// </summary>
        /// <param name="setName"></param>
        /// <param name="useDefaultColors"></param>
        private void applyTextureSet(string setName, bool useDefaultColors)
        {
            textureSetName = setName;
            TextureSet textureSet = this.textureSet;
            if (useDefaultColors || textureSet == null)
            {
                if (textureSet != null && textureSet.maskColors != null && textureSet.maskColors.Length > 0)
                {
                    customColors = new RecoloringData[3];
                    customColors[0] = textureSet.maskColors[0];
                    customColors[1] = textureSet.maskColors[1];
                    customColors[2] = textureSet.maskColors[2];
                }
                else//invalid colors or texture set, create default placeholder color array
                {
                    //debug("Could not use default coloring from texture set: " + textureSetName + ".  No texture set or coloring data found.  Using placeholder coloring.  Module: "+getErrorReportModuleName());
                    RecoloringData placeholder = new RecoloringData(Color.white, 1, 1);
                    customColors = new RecoloringData[] { placeholder, placeholder, placeholder };
                }
                saveColors(customColors);
            }
            enableTextureSet();
            updateTextureUIControl();
            ROLModInterop.OnPartTextureUpdated(part);
        }

        /// <summary>
        /// Applies the current module position to the root transform of the ModelModule.  Does not adjust rotation or handle multi-model positioning setup for layouts.  Does not update scales.
        /// Loops through the individual model instances and updates their position, rotation, and scale, for the currently configured ModelLayoutData.  Does not update 'root' transform for module position.
        /// </summary>
        private void updateModelScalesAndLayoutPositions()
        {
            root.transform.localPosition = new Vector3(0, currentVerticalPosition + getPlacementOffset(), 0);
            int len = layout.positions.Length;
            float posScalar = getLayoutPositionScalar();
            float scaleScalar = getLayoutScaleScalar();
            for (int i = 0; i < len; i++)
            {
                Transform model = models[i];
                ModelPositionData mpd = layout.positions[i];
                model.transform.localPosition = mpd.localPosition * posScalar;
                model.transform.localRotation = Quaternion.Euler(mpd.localRotation);
                model.transform.localScale = mult(mpd.localScale, new Vector3(currentHorizontalScale, currentVerticalScale, currentHorizontalScale)) * scaleScalar;
                if (definition.compoundModelData != null)
                {
                    //on compound model setups, only adjust for the position scalar and mpd scale
                    //the model internal scale will be setup by the compound model data
                    model.transform.localScale = mpd.localScale * scaleScalar;
                    definition.compoundModelData.setHeightFromScale(definition, model.gameObject, currentHorizontalScale, currentVerticalScale, definition.orientation);
                }
                else
                {
                    //normal models, apply all scales to the model root transform
                    model.transform.localScale = mult(mpd.localScale, new Vector3(currentHorizontalScale, currentVerticalScale, currentHorizontalScale)) * scaleScalar;
                }
            }
        }

        /// <summary>
        /// Applies the current module position to the root transform of the ModelModule.  Does not adjust rotation or handle multi-model positioning setup for layouts.  Does not update scales.
        /// Loops through the individual model instances and updates their position, rotation, and scale, for the currently configured ModelLayoutData.  Does not update 'root' transform for module position.
        /// </summary>
        private void updateModelScalesAndLayoutPositions(bool solar)
        {
            root.transform.localPosition = new Vector3(0, currentVerticalPosition + getPlacementOffset(), 0);
            int len = layout.positions.Length;
            float posScalar = getLayoutPositionScalar();
            float scaleScalar = getLayoutScaleScalar();
            for (int i = 0; i < len; i++)
            {
                Transform model = models[i];
                ModelPositionData mpd = layout.positions[i];
                model.transform.localPosition = mpd.localPosition * posScalar;
                model.transform.localRotation = Quaternion.Euler(mpd.localRotation);
                if (solar)
                {
                    model.transform.localScale = mult(mpd.localScale, new Vector3(1, currentVerticalScale, currentHorizontalScale)) * scaleScalar;
                }
                else
                {
                    model.transform.localScale = mult(mpd.localScale, new Vector3(currentHorizontalScale, currentVerticalScale, currentHorizontalScale)) * scaleScalar;
                }
            }
        }

        private Vector3 mult(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        /// <summary>
        /// Constructs all of the models for the current ModelDefinition and ModelLayoutData
        /// </summary>
        private void constructModels()
        {
            //create model array with length based on the positions defined in the ModelLayoutData
            int len = layout.positions.Length;
            models = new Transform[len];
            for (int i = 0; i < len; i++)
            {
                models[i] = new GameObject("ModelModule-" + i).transform;
                models[i].NestToParent(root);
                constructSubModels(models[i]);
            }
            bool shouldInvert = definition.shouldInvert(orientation);
            Vector3 rotation = shouldInvert ? definition.invertAxis * 180f : Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(rotation);
        }

        /// <summary>
        /// Constructs a single model instance from the model definition, parents it to the input transform.<para/>
        /// Does not position or orient the created model; positionModels() should be called to update its position for the current ModelLayoutData configuration
        /// </summary>
        /// <param name="parent"></param>
        private void constructSubModels(Transform parent)
        {
            SubModelData[] smds = definition.subModelData;
            SubModelData smd;
            GameObject clonedModel;
            Transform localParent;
            int len = smds.Length;
            //add sub-models to the input model transform
            for (int i = 0; i < len; i++)
            {
                smd = smds[i];
                clonedModel = ROLUtils.cloneModel(smd.modelURL);
                if (clonedModel == null)
                {
                    error("Could not clone model for url: " + smd.modelURL + " while constructing meshes for model definition" + definition.name+" for: "+getErrorReportModuleName());
                    continue;
                }
                clonedModel.transform.NestToParent(parent.transform);
                clonedModel.transform.localRotation = Quaternion.Euler(smd.rotation);
                clonedModel.transform.localPosition = smd.position;
                clonedModel.transform.localScale = smd.scale;
                if (!string.IsNullOrEmpty(smd.parent))
                {
                    localParent = parent.transform.ROLFindRecursive(smd.parent);
                    if (localParent != null)
                    {
                        clonedModel.transform.parent = localParent;
                    }
                }
                //de-activate any non-active sub-model transforms
                //iterate through all transforms for the model and deactivate(destroy?) any not on the active mesh list
                smd.setupSubmodel(clonedModel);
            }
            if (definition.mergeData != null)
            {
                MeshMergeData[] md = definition.mergeData;
                len = md.Length;
                for (int i = 0; i < len; i++)
                {
                    md[i].mergeMeshes(parent);
                }
            }
        }

        /// <summary>
        /// Returns an offset to 'currentPosition' that is applied based on the Module orientation vs. the model-definition orientation.
        /// </summary>
        /// <returns></returns>
        private float getPlacementOffset()
        {
            float offset = 0;
            switch (orientation)
            {
                case ModelOrientation.TOP:
                    switch (definition.orientation)
                    {
                        case ModelOrientation.TOP:
                            //noop
                            break;
                        case ModelOrientation.CENTRAL:
                            offset = currentHeight * 0.5f;
                            break;
                        case ModelOrientation.BOTTOM:
                            //noop
                            break;
                        default:
                            break;
                    }
                    break;
                case ModelOrientation.CENTRAL:
                    switch (definition.orientation)
                    {
                        case ModelOrientation.TOP:
                            offset = -currentHeight * 0.5f;
                            break;
                        case ModelOrientation.CENTRAL:
                            //noop
                            break;
                        case ModelOrientation.BOTTOM:
                            offset = currentHeight * 0.5f;
                            break;
                        default:
                            break;
                    }
                    break;
                case ModelOrientation.BOTTOM:
                    switch (definition.orientation)
                    {
                        case ModelOrientation.TOP:
                            //noop
                            break;
                        case ModelOrientation.CENTRAL:
                            offset = -currentHeight * 0.5f;
                            break;
                        case ModelOrientation.BOTTOM:
                            //noop
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
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
        private bool isValidTextureSet(String val)
        {
            if (definition.textureSets.Length == 0)
            {
                return val == "none" || val == "default";
            }
            return definition.textureSets.ROLExists(m => m.name == val);
        }

        /// <summary>
        /// Internal utility method to allow accessing of symmetry ModelModules' in symmetry parts/part-modules
        /// </summary>
        /// <param name="action"></param>
        private void actionWithSymmetry(Action<ROLModelModule<U>> action)
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
        private string getErrorReportModuleName()
        {
            return "ModelModule: [" + moduleName + "] model: [" +definition+ "] in orientation: [" + orientation + "] in module: " + partModule + " in part: " + part;
        }

        /// <summary>
        /// Return the X and Y mounting positions for an RCS model-module slot parented to -this- model-module.
        /// </summary>
        /// <param name="vPos"></param>
        /// <param name="upper"></param>
        /// <param name="radius"></param>
        /// <param name="posY"></param>
        public void getRCSMountingValues(float vPos, bool upper, out float radius, out float posY)
        {
            bool invert = currentDefinition.shouldInvert(orientation);
            //if (invert) { upper = !upper; }
            if (currentDefinition.rcsPositionData != null)
            {
                ModelAttachablePositionData mapd = null;
                if (upper)//always 0th index in config
                {
                    mapd = currentDefinition.rcsPositionData[0];
                }
                else//if both positions specified, will always be 1st index, else 0th
                {
                    if (currentDefinition.rcsPositionData.Length > 1)
                    {
                        mapd = currentDefinition.rcsPositionData[1];//lower def
                    }
                    else
                    {
                        mapd = currentDefinition.rcsPositionData[0];//default to upper def if no lower defined
                    }
                }
                mapd.getModelPosition(currentHorizontalScale, currentVerticalScale, vPos, invert, out radius, out posY);
                posY += getPlacementOffset();
                posY += modulePosition;
            }
            else
            {
                radius = currentDiameter * 0.5f;
                posY = modulePosition;
                posY += getPlacementOffset();
            }
        }

        #endregion ENDREGION - Private/Internal methods

    }
}
