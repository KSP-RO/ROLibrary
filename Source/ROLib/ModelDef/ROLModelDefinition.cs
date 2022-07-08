using System;
using System.Collections.Generic;
using System.Linq;
using KSPShaderTools;
using UnityEngine;

namespace ROLib
{

    /// <summary>
    /// Immutable data storage for persistent data about a single 'model'.<para/>
    /// A 'model' is defined by any number of meshes from models in the GameDatabase, along with any function-specific data for the functions of those meshes -- animations, gimbals, rcs, engine, texture sets<para/>
    /// Includes height, volume, mass, cost, tech-limitations, attach-node positions,
    /// texture-set data, and whether the model is intended for top, center, or bottom stack mounting.
    /// </summary>
    public class ROLModelDefinition
    {

        /// <summary>
        /// The config node that this ModelDefinition is loaded from.
        /// </summary>
        public readonly ConfigNode configNode;

        /// <summary>
        /// The name of the model in the model database to use, if any.  May be blank if model is a 'dummy' model (no meshes).<para/>
        /// </summary>
        public readonly string modelName;

        /// <summary>
        /// The unique registered name for this model definition.  MUST be unique among all model definitions.
        /// </summary>
        public readonly string name;

        /// <summary>
        /// The display name to use for this model definition.  This will be the value displayed on any GUIs for this specific model def.
        /// </summary>
        public readonly string title;

        /// <summary>
        /// A description of this model definition.  Optional.  Currently unused.
        /// </summary>
        public readonly string description;

        /// <summary>
        /// The name of the PartUpgrade that 'unlocks' this model definition.  If blank, denotes always unlocked.<para/>
        /// If populated, MUST correspond to one of the part-upgrades in the PartModule that this model-definition is used in.
        /// </summary>
        public readonly string upgradeUnlock;

        /// <summary>
        /// The height of this model, as denoted by colliders and/or attach nodes.<para/>
        /// This value is used to determine positioning of other models relative to this model when used in multi-model setups.
        /// </summary>
        public readonly float height = 1.0f;

        /// <summary>
        /// The actual height of this model.<para/>
        /// This value is used for the Booster tanks that use a different declared height than their true height.
        /// </summary>
        public readonly float actualHeight = 1.0f;

        /// <summary>
        /// The core diameter of this model.  If upper/lower diameter are unspecified, assume that the model is cylindrical with this diameter across its entire length.
        /// </summary>
        public readonly float diameter = 5.0f;

        /// <summary>
        /// Solar panel information.
        /// </summary>
        public readonly float panelLength = 1.0f;
        public readonly float panelWidth = 1.0f;
        public readonly float panelArea = 1.0f;
        public readonly float panelScale = 1.0f;
        public readonly string secondaryTransformName = "suncatcher";
        public readonly string pivotName = "sunPivot";
        public readonly bool lengthWidth = false;
        public readonly string animationName = string.Empty;
        public readonly bool isTracking = false;

        /// <summary>
        /// The diameter of the upper attachment point on this model.  Defaults to 'diameter' if unspecified.  Used during model-scale-chaining to determine the model scale to use for adapter models.
        /// </summary>
        public readonly float upperDiameter = 5;

        /// <summary>
        /// The diameter of the lower attachment point on this model.  Defaults to 'diameter' if unspecified.  Used during model-scale-chaining to determine the model scale to use for adapter models.
        /// </summary>
        public readonly float lowerDiameter = 5;

        /// <summary>
        /// Minimum scalar offset that can be applied to vertical scale as compared to horizontal scale.
        /// </summary>
        public readonly float minVerticalScale = 0.25f;

        /// <summary>
        /// Maximum scalar offset that can be applied to vertical scale as compared to horizontal scale.
        /// </summary>
        public readonly float maxVerticalScale = 4f;

        /// <summary>
        /// The vertical offset applied to the meshes in the model to make the model conform with its specified orientation setup.<para/>
        /// Applied internally during model setup, should not be needed beyond when the model is first created.<para/>
        /// Should not be needed when COMPOUND_MODEL setups are used (transforms should be positioned properly in their defs).
        /// Only used when SUBMODEL is not defined, otherwise submodel data overrides.
        /// </summary>
        public readonly Vector3 positionOffset = Vector3.zero;

        /// <summary>
        /// The Euler XYZ rotation that should be applied to this model to make it conform to Y+=UP / Z+=FWD conventions.
        /// Only used when SUBMODEL is not defined, otherwise submodel data overrides.
        /// </summary>
        public readonly Vector3 rotationOffset = Vector3.zero;

        /// <summary>
        /// The XYZ scale that should be applied to the model.
        /// Only used when SUBMODEL is not defined, otherwise submodel data overrides.
        /// </summary>
        public readonly Vector3 scaleOffset = Vector3.one;

        /// <summary>
        /// The resource volume that this model definition contains at default scale.  Used to determine the total volume available for resource containers.
        /// </summary>
        public readonly float volume = 0;

        /// <summary>
        /// The 'mass' of this model-definition.  Modular part modules may use this value to adjust the config-specified mass of the part based on what models are selected.
        /// </summary>
        public readonly float mass = 0;

        /// <summary>
        /// The 'cost' of this model-definition.  Modular part modules may use this value to adjust the config-specified cost of the part based on what models are currently selected.
        /// </summary>
        public readonly float cost = 0;

        /// <summary>
        /// The effective size of the nose or mount. This is the amount of space that you can use for additional dome length.
        /// </summary>
        public readonly float effectiveLength = 0;

        /// <summary>
        /// Additional volume that needs to be added to the space for the additional dome length.
        /// </summary>
        public readonly float additionalVolume = 0;

        /// <summary>
        /// The orientation that this model module is defined in.  Calls to 'setPosition' take this into account to position the model properly.  The model setup/origin MUST be setup properly to match the orientation as specified in the model definition.<para/>
        /// Use the 'verticalOffset' function of the ModelDefinition to fix any model positioning to ensure model conforms to specified orientation.
        /// </summary>
        public readonly ModelOrientation orientation = ModelOrientation.CENTRAL;

        /// <summary>
        /// Axis to use when inverting a model when a 'TOP' model is used for slot marked as 'BOTTOM' or 'BOTTOM' models used for a slot marked as 'TOP'.
        /// </summary>
        public readonly Vector3 invertAxis = Vector3.forward;

        /// <summary>
        /// Container for the faring data for this model definition.  Enabled/disabled, positioning, sizing data.
        /// </summary>
        public readonly ModelFairingData fairingData;

        /// <summary>
        /// Data defining a submodel setup -- a custom model comprised of multiple sub-models, all being treated as a single model-definition.<para/>
        /// All model definitions are mapped to a SUBMODEL setup internally during model creation, even if they only use the basic singular 'modelName=' configuration setup.
        /// This single model from the database becomes a single subModelData entry, using all of the transforms from the specified database model.
        /// </summary>
        public readonly SubModelData[] subModelData;

        /// <summary>
        /// Data defining a compound model setup -- a model that has special handling for vertical-scaling where only some of its transforms scale vertically.<para/>
        /// Can be used in combination with SubModel setup if needed/desired.<para/>
        /// If undefined, the model will use standard scale handling.
        /// </summary>
        public readonly CompoundModelData compoundModelData;

        /// <summary>
        /// Data defining what meshes in the model will be merged into joined meshes.  For cases of compound models that will all share
        /// the same materials, makes for more efficient use of material/rendering and cuts down on the number of GOs in the model tree.<para/>
        /// These meshes are merged whenever the model from this definition is constructed.
        /// </summary>
        public readonly MeshMergeData[] mergeData;

        /// <summary>
        /// Attach node data for the 'top' attach node.  Will only be used if the top of this model is uncovered.
        /// </summary>
        public readonly AttachNodeBaseData topNodeData;

        /// <summary>
        /// Attach node data for the 'bottom' attach node.  Will only be used if the bottom of this model is uncovered.
        /// </summary>
        public readonly AttachNodeBaseData bottomNodeData;

        /// <summary>
        /// Attach node data for the 'body' nodes. Will be used regardless of top/bottom covered status.
        /// </summary>
        public readonly AttachNodeBaseData[] bodyNodeData;

        /// <summary>
        /// Data defining the surface attach-node to use for this model definition.  If undefined, it defaults to an attach node on X axis at 'diameter' distance from model origin, with vertical position at model origin.
        /// Only used by 'core' models in modular part setups.
        /// </summary>
        public readonly AttachNodeBaseData surfaceNode;

        public readonly string[] disableTransforms;

        /// <summary>
        /// The 'default' texture set for this model definition.  If unspecified, is set to the first available texture set if any are present in the model definition.
        /// </summary>
        public readonly string defaultTextureSet;

        /// <summary>
        /// The texture sets that are applicable to this model definition.  Will be null if no texture sets are defined in the config.
        /// </summary>
        public readonly TextureSet[] textureSets;

        /// <summary>
        /// The model animation constraint data that is applicable to this model definition.  Will be null if no constraint data is specified in the config.
        /// </summary>
        public readonly ModelConstraintData constraintData;

        /// <summary>
        /// The model engine thrust data -- engine min and max thrust for the default model scale.
        /// </summary>
        public readonly ModelEngineThrustData engineThrustData;

        /// <summary>
        /// The engine thrust transform data -- transform name(s), and thrust percentages in the case of non-uniform split thrust transforms.
        /// </summary>
        public readonly ModelEngineTransformData engineTransformData;

        /// <summary>
        /// The RCS position data for this model definition.  If RCS is attached to this model, determines where should it be located.<para/>
        /// Upper RCS module position should always be in index 0 if multiple modules are present.
        /// </summary>
        public readonly ModelAttachablePositionData[] rcsPositionData;

        /// <summary>
        /// The rcs-module data for use by the RCS thrusters in this model -- thrust, fuel type, ISP.  Will be null if this is not an RCS model.
        /// </summary>
        public readonly ModelRCSModuleData rcsModuleData;

        public readonly string[] requiredCore;
        public readonly string style;

        public readonly bool canRotate = false;
        public readonly bool canVScale = false;

        // Field Definitions for ROStations
        public readonly bool canAdjustHab = false;
        public readonly float habitat = 0f;
        public readonly float surfaceArea = 0f;
        public readonly bool canExercise = false;
        public readonly bool hasPanorama = false;
        public readonly bool hasPlants = false;
        public readonly float trussVolume = 0f;
        public readonly float totalVolume = 0f;
        public readonly StationType stationType = StationType.None;

        /// <summary>
        /// Construct the model definition from the data in the input ConfigNode.<para/>
        /// All data constructs MUST conform to the expected format (see documentation), or things will not load properly and the model will likely not work as expected.
        /// </summary>
        /// <param name="node"></param>
        public ROLModelDefinition(ConfigNode node)
        {
            //load basic model definition values -- data that pertains to every model definition regardless of end-use.
            configNode = node;
            name = node.ROLGetStringValue("name", string.Empty);
            if (string.IsNullOrEmpty(name))
            {
                ROLLog.error("ERROR: Cannot load ROLModelDefinition with null or empty name.  Full config:\n" + node.ToString());
            }
            title = node.ROLGetStringValue("title", name);
            description = node.ROLGetStringValue("description", title);
            modelName = node.ROLGetStringValue("modelName", string.Empty);
            upgradeUnlock = node.ROLGetStringValue("upgradeUnlock", upgradeUnlock);
            height = node.ROLGetFloatValue("height", height);
            actualHeight = node.ROLGetFloatValue("actualHeight", actualHeight);
            volume = node.ROLGetFloatValue("volume", volume);
            mass = node.ROLGetFloatValue("mass", mass);
            cost = node.ROLGetFloatValue("cost", cost);
            diameter = node.ROLGetFloatValue("diameter", diameter);
            minVerticalScale = node.ROLGetFloatValue("minVerticalScale", minVerticalScale);
            maxVerticalScale = node.ROLGetFloatValue("maxVerticalScale", maxVerticalScale);
            upperDiameter = node.ROLGetFloatValue("upperDiameter", diameter);
            lowerDiameter = node.ROLGetFloatValue("lowerDiameter", diameter);
            panelLength = node.ROLGetFloatValue("panelLength", panelLength);
            panelWidth = node.ROLGetFloatValue("panelWidth", panelWidth);
            panelArea = node.ROLGetFloatValue("panelArea", panelArea);
            panelScale = node.ROLGetFloatValue("panelScale", panelScale);
            secondaryTransformName = node.ROLGetStringValue("secondaryTransformName", secondaryTransformName);
            pivotName = node.ROLGetStringValue("pivotName", pivotName);
            animationName = node.ROLGetStringValue("animationName", animationName);
            lengthWidth = node.ROLGetBoolValue("lengthWidth", lengthWidth);
            isTracking = node.ROLGetBoolValue("isTracking", isTracking);
            effectiveLength = node.ROLGetFloatValue("effectiveLength", effectiveLength);
            additionalVolume = node.ROLGetFloatValue("additionalVolume", additionalVolume);
            canRotate = node.ROLGetBoolValue("canRotate", canRotate);
            canVScale = node.ROLGetBoolValue("canVScale", canVScale);
            canAdjustHab = node.ROLGetBoolValue("canAdjustHab", canAdjustHab);
            habitat = node.ROLGetFloatValue("habitat", habitat);
            surfaceArea = node.ROLGetFloatValue("surfaceArea", surfaceArea);
            trussVolume = node.ROLGetFloatValue("trussVolume", trussVolume);
            totalVolume = node.ROLGetFloatValue("totalVolume", totalVolume);
            Enum.TryParse(node.ROLGetStringValue("stationType", StationType.None.ToString()), out stationType);
            canExercise = node.ROLGetBoolValue("canExercise", canExercise);
            hasPanorama = node.ROLGetBoolValue("hasPanorama", hasPanorama);
            hasPlants = node.ROLGetBoolValue("hasPlants", hasPlants);
            if (node.HasValue("verticalOffset"))
            {
                positionOffset = new Vector3(0, node.ROLGetFloatValue("verticalOffset"), 0);
            }
            else
            {
                positionOffset = node.ROLGetVector3("positionOffset", Vector3.zero);
            }
            rotationOffset = node.ROLGetVector3("rotationOffset", rotationOffset);
            scaleOffset = node.ROLGetVector3("scaleOffset", Vector3.one);

            orientation = (ModelOrientation)Enum.Parse(typeof(ModelOrientation), node.ROLGetStringValue("orientation", ModelOrientation.TOP.ToString()));
            invertAxis = node.ROLGetVector3("invertAxis", invertAxis);

            List<string> compatibleCores = new List<string>();
            if (node.HasValue("requiredCore"))
            {
                foreach (string core in node.ROLGetStringValues("requiredCore"))
                {
                    compatibleCores.Add(core);
                }
                requiredCore = compatibleCores.ToArray();
            }
            style = node.HasValue("style") ? node.ROLGetStringValue("style") : "NONE";
            disableTransforms = node.ROLGetStringValues("disableTransform");
            if (disableTransforms.Length > 0)
            {
                var sb = StringBuilderCache.Acquire();
                sb.Append($"Disabled Transforms ({disableTransforms.Length}): ");
                foreach (var s in disableTransforms)
                    sb.Append($"{s}  ");
                ROLLog.log(sb.ToStringAndRelease());
            }

            //load sub-model definitions
            ConfigNode[] subModelNodes = node.GetNodes("SUBMODEL");
            int len = subModelNodes.Length;
            if (len == 0)//no defined submodel data, check for regular single model definition, if present, build a submodel definition for it.
            {
                if (!string.IsNullOrEmpty(modelName))
                {
                    SubModelData smd = new SubModelData(modelName, new string[0], disableTransforms, string.Empty, positionOffset, rotationOffset, scaleOffset);
                    subModelData = new SubModelData[] { smd };
                }
                else//is an empty proxy model with no meshes
                {
                    subModelData = new SubModelData[0];
                }
            }
            else
            {
                subModelData = new SubModelData[len];
                for (int i = 0; i < len; i++)
                {
                    subModelData[i] = new SubModelData(subModelNodes[i]);
                }
            }

            if (node.HasNode("MERGEDMODELS"))
            {
                ConfigNode[] mergeNodes = node.GetNodes("MERGEDMODELS");
                len = mergeNodes.Length;
                mergeData = new MeshMergeData[len];
                for (int i = 0; i < len; i++)
                {
                    mergeData[i] = new MeshMergeData(mergeNodes[i]);
                }
            }
            else mergeData = new MeshMergeData[0];

            //Load texture set definitions.
            List<TextureSet> textureSetList = new List<TextureSet>();
            foreach (string tsName in node.ROLGetStringValues("textureSet"))
            {
                if (TexturesUnlimitedLoader.getTextureSet(tsName) is TextureSet ts)
                    textureSetList.Add(ts);
            }
            //then load any of the model-specific sets
            foreach (ConfigNode tsNode in node.GetNodes("KSP_TEXTURE_SET"))
            {
                textureSetList.Add(new TextureSet(tsNode));
            }
            textureSets = textureSetList.ToArray();

            //Load the default texture set specification
            defaultTextureSet = node.ROLGetStringValue("defaultTextureSet");
            //if none is defined in the model def, but texture sets are present, set it to the name of the first defined texture set
            if (string.IsNullOrEmpty(defaultTextureSet) && textureSets.Length > 0)
            {
                defaultTextureSet = textureSets[0].name;
            }

            if (node.HasValue("topNode"))
            {
                topNodeData = new AttachNodeBaseData(node.ROLGetStringValue("topNode"));
            }
            else
            {
                float y = height;
                if (orientation == ModelOrientation.CENTRAL) { y *= 0.5f; }
                else if (orientation == ModelOrientation.BOTTOM) { y = 0; }
                topNodeData = new AttachNodeBaseData(0, y, 0, 0, 1, 0, diameter / 1.25f);
            }
            if (node.HasValue("bottomNode"))
            {
                bottomNodeData = new AttachNodeBaseData(node.ROLGetStringValue("bottomNode"));
            }
            else
            {
                float y = -height;
                if (orientation == ModelOrientation.CENTRAL) { y *= 0.5f; }
                else if (orientation == ModelOrientation.TOP) { y = 0; }
                bottomNodeData = new AttachNodeBaseData(0, y, 0, 0, -1, 0, diameter / 1.25f);
            }
            if (node.HasValue("bodyNode"))
            {
                string[] nodeData = node.ROLGetStringValues("bodyNode");
                len = nodeData.Length;
                bodyNodeData = new AttachNodeBaseData[len];
                for (int i = 0; i < len; i++)
                {
                    bodyNodeData[i] = new AttachNodeBaseData(nodeData[i]);
                }
            }

            //load the surface attach node specifications, or create default if none are defined.
            if (node.HasValue("surface"))
                surfaceNode = new AttachNodeBaseData(node.ROLGetStringValue("surface"));
            else
                surfaceNode = new AttachNodeBaseData($"{diameter / 2},0,0,1,0,0,2");

            if (node.HasNode("COMPOUNDMODEL"))
                compoundModelData = new CompoundModelData(node.GetNode("COMPOUNDMODEL"));

            if (node.HasNode("CONSTRAINT"))
                constraintData = new ModelConstraintData(node.GetNode("CONSTRAINT"));

            if (node.HasNode("ENGINE_THRUST"))
                engineThrustData = new ModelEngineThrustData(node.GetNode("ENGINE_THRUST"));

            if (node.HasNode("ENGINE_TRANSFORM"))
                engineTransformData = new ModelEngineTransformData(node.GetNode("ENGINE_TRANSFORM"));

            //load the fairing data, if present
            if (node.HasNode("FAIRINGDATA"))
            {
                fairingData = new ModelFairingData(node.GetNode("FAIRINGDATA"));
            }
        }

        /// <summary>
        /// Return true/false if this model definition is available given the input 'part upgrades' list.  Checks versus the 'upgradeUnlock' specified in the model definition config.
        /// </summary>
        /// <param name="partUpgrades"></param>
        /// <returns></returns>
        public bool IsAvailable(List<string> partUpgrades) => string.IsNullOrEmpty(upgradeUnlock) || partUpgrades.Contains(upgradeUnlock);

        public string[] GetTransformsToRemove() => disableTransforms;

        /// <summary>
        /// Return a string array containing the names of the texture sets that are available for this model definition.
        /// </summary>
        /// <returns></returns>
        public string[] GetTextureSetNames() => ROLUtils.getNames(textureSets, m => m.name);

        /// <summary>
        /// Returns a string array of the UI-label titles for the texture sets for this model definition.<para/>
        /// Returned in the same order as getTextureSetNames(), so they can be used in with basic indexing to map one value to another.
        /// </summary>
        /// <returns></returns>
        public string[] GetTextureSetTitles() => ROLUtils.getNames(textureSets, m => m.title);

        /// <summary>
        /// Return the TextureSet data for the input texture set name.<para/>
        /// Returns null if the input texture set name was not found in the currently loaded texture sets for this model definition.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public TextureSet FindTextureSet(string name) => Array.Find(textureSets, m => m.name == name);

        /// <summary>
        /// Returns the default texture set as defined in the model definition config
        /// </summary>
        /// <returns></returns>
        public TextureSet GetDefaultTextureSet() => FindTextureSet(defaultTextureSet);

        /// <summary>
        /// Return true/false if this model should be inverted/rotated based on the input use-orientation and the models config-defined orientation.<para/>
        /// If specified model orientation == CENTER, model will never invert regardless of input value.
        /// </summary>
        /// <param name="orientation"></param>
        /// <returns></returns>
        internal bool shouldInvert(ModelOrientation orientation)
        {
            return (orientation == ModelOrientation.BOTTOM && this.orientation == ModelOrientation.TOP) || (orientation == ModelOrientation.TOP && this.orientation == ModelOrientation.BOTTOM);
        }

        public override string ToString() => $"ModelDef[ {name} ]";

    }

    /// <summary>
    /// Information for a single model definition that specifies engine thrust transforms -- essentially just transform name(s).
    /// </summary>
    public class ModelEngineTransformData
    {
        public readonly string thrustTransformName;
        public readonly string gimbalTransformName;
        public readonly float gimbalAdjustmentRange;//how far the gimbal can be adjusted from reference while in the editor
        public readonly float gimbalFlightRange;//how far the gimbal may be actuated while in flight from the adjusted reference angle

        public ModelEngineTransformData(ConfigNode node)
        {
            thrustTransformName = node.ROLGetStringValue("thrustTransform");
            if (string.IsNullOrEmpty(thrustTransformName)) { ROLLog.error("ERROR: THrust transform name was null for model def engine transform data"); }
            gimbalTransformName = node.ROLGetStringValue("gimbalTransform");
            gimbalAdjustmentRange = node.ROLGetFloatValue("gimbalAdjustRange", 0);
            gimbalFlightRange = node.ROLGetFloatValue("gimbalFlightRange", 0);
        }

        public void RenameThrustTransforms(Transform root, string destinationName) => RenameTransforms(root, thrustTransformName, destinationName);
        public void RenameGimbalTransforms(Transform root, string destinationName) => RenameTransforms(root, gimbalTransformName, destinationName);
        public void RenameTransforms(Transform root, string transformName, string destinationName)
        {
            foreach (Transform tr in root.FindChildren(transformName))
            {
                tr.gameObject.name = tr.name = destinationName;
            }
        }
    }

    /// <summary>
    /// Information for a single model definition that specifies engine thrust information<para/>
    /// Min, Max, and per-transform percentages.
    /// </summary>
    public class ModelEngineThrustData
    {
        public readonly float maxThrust;
        public readonly float minThrust;
        public readonly float[] thrustSplit;

        public ModelEngineThrustData(ConfigNode node)
        {
            minThrust = node.ROLGetFloatValue("minThrust", 0);
            maxThrust = node.ROLGetFloatValue("maxThrust", 1);
            thrustSplit = node.ROLGetFloatValuesCSV("thrustSplit", new float[] { 1.0f });
        }

        public float[] GetCombinedSplitThrust(int count)
        {
            if (thrustSplit == null) { return null; }
            int l1 = thrustSplit.Length;
            int l2 = l1 * count;
            float[] retCache = new float[l2];
            for (int i = 0, j = 0; i < count; i++)
            {
                for (int k = 0; k < l1; k++, j++)
                {
                    retCache[j] = thrustSplit[k] / count;
                }
            }
            return retCache;
        }
    }

    /// <summary>
    /// Information denoting the model-animation-constraint setup for the meshes a single ModelDefinition.  Contains all information for all constraints used by the model.
    /// </summary>
    public class ModelConstraintData
    {
        public ConfigNode constraintNode;
        public ModelConstraintData(ConfigNode node)
        {
            constraintNode = node;
        }
    }

    /// <summary>
    /// Information pertaining to a single ModelDefinition, defining how NodeFairings are configured for the model at its default scale.
    /// </summary>
    public class ModelFairingData
    {

        /// <summary>
        /// Are fairings supported on this model?
        /// </summary>
        public readonly bool fairingsSupported = false;

        /// <summary>
        /// Position of the 'top' of the fairing, relative to model in its defined orientation and scale.
        /// If the model is used in oposite orientation, this value is negated.
        /// </summary>
        public readonly float top = 0f;

        /// <summary>
        /// Position of the 'bottom' of the fairing, relative to model in its defined orientation and scale.
        /// If the model is used in oposite orientation, this value is negated.
        /// </summary>
        public readonly float bottom = 0f;

        public ModelFairingData(ConfigNode node)
        {
            fairingsSupported = node.GetBoolValue("enabled", false);
            top = node.GetFloatValue("top", 0f);
            bottom = node.GetFloatValue("bottom", 0f);
        }

        public float GetTop(float scale, bool invert)
        {
            if (invert) { return scale * bottom; }
            return scale * top;
        }

        public float GetBottom(float scale, bool invert)
        {
            if (invert) { return scale * top; }
            return scale * bottom;
        }

    }

    /// <summary>
    /// Container for RCS position related data for a standard structural model definition.
    /// </summary>
    public class ModelRCSModuleData
    {

        /// <summary>
        /// The name of the thrust transforms as they are in the model hierarchy.  These will be renamed at runtime to match whatever the RCS module is expecting.
        /// </summary>
        public readonly string thrustTransformName;

        /// <summary>
        /// The thrust of the RCS model at its base scale.
        /// </summary>
        public readonly float rcsThrust;

        public readonly bool enableX, enableY, enableZ, enablePitch, enableYaw, enableRoll;

        public ModelRCSModuleData(ConfigNode node)
        {
            thrustTransformName = node.GetStringValue("thrustTransformName");
            rcsThrust = node.GetFloatValue("thrust");
            enableX = node.GetBoolValue("enableX", true);
            enableY = node.GetBoolValue("enableY", true);
            enableZ = node.GetBoolValue("enableZ", true);
            enablePitch = node.GetBoolValue("enablePitch", true);
            enableYaw = node.GetBoolValue("enableYaw", true);
            enableRoll = node.GetBoolValue("enableRoll", true);
        }

        public float GetThrust(float scale) => rcsThrust * scale * scale;

        public void RenameTransforms(Transform root, string destinationName)
        {
            foreach (Transform tr in root.FindChildren(thrustTransformName))
            {
                tr.gameObject.name = tr.name = destinationName;
            }
            //TODO -- if transform array is null, add a single dummy transform of the given name to stop stock modules' logspam
        }

    }

    /// <summary>
    /// Container for RCS positional data for a model definition. <para/>
    /// Specifies if the model supports RCS block addition, where on the model the blocks are positioned, and if they may have their position adjusted.
    /// </summary>
    public class ModelAttachablePositionData
    {

        /// <summary>
        /// The horizontal offset to apply to each RCS port.  Defaults to the model 'diameter' if unspecified.
        /// </summary>
        public readonly float posX;

        /// <summary>
        /// The vertical neutral position of the RCS ports, relative to the models origin.  If rcs vertical positionining is supported, this MUST be specified as the center of the offset range.
        /// </summary>
        public readonly float posY;

        /// <summary>
        /// The vertical +/- range that the RCS port may be moved through in the model at default model scale.
        /// </summary>
        public readonly float range;

        /// <summary>
        /// Angle to use when offsetting the RCS block along its specified vertical range.  To be used in the case of non cylindrical models that still want to support RCS position adjustment.
        /// </summary>
        public readonly float angle;

        public ModelAttachablePositionData(ConfigNode node)
        {
            posY = node.GetFloatValue("posY", 0);
            posX = node.GetFloatValue("posX", 0);
            range = node.GetFloatValue("range", 0);
            angle = node.GetFloatValue("angle", 0);
        }

        public void GetModelPosition(float hScale, float vScale, float vRange, bool invert, out float oRadius, out float oPosY)
        {
            float rads = Mathf.Deg2Rad * angle;
            //position of the center of the offset
            float outX = posX * hScale;
            float outY = posY * vScale;
            if (invert) { outY = -outY; }
            float sRange = vScale * range * vRange;//scaled value to move along vector denoted by 'angle' from
            float xoff = Mathf.Sin(rads);//scale along x axis
            float yoff = Mathf.Cos(rads);//scale along y axis
            outX += xoff * sRange;
            outY += yoff * sRange;
            oRadius = outX;
            oPosY = outY;
        }
    }

    /// <summary>
    /// Simple enum defining the cardinal axis' of a transform.<para/>
    /// Used with Transform extension methods to return a vector for the specified axis (local or world-space).
    /// </summary>
    public enum Axis
    {
        XPlus,
        XNeg,
        YPlus,
        YNeg,
        ZPlus,
        ZNeg
    }

    public enum StationType
    {
        None,
        Hab,
        Truss,
        CrewTube
    }

    /// <summary>
    /// Simple enum defining how a the meshes of a model are oriented relative to their root transform.<para/>
    /// ModelModule uses this information to position the model and attach nodes properly.
    /// </summary>
    public enum ModelOrientation
    {

        /// <summary>
        /// Denotes that a model is setup for use as a 'nose' or 'top' part, with the origin at the bottom of the model.<para/>
        /// Will be rotated 180 degrees around origin when used in a slot denoted for 'bottom' style models.<para/>
        /// Will be offset vertically downwards by half of its height when used in a slot denoted for 'central' models.<para/>
        /// </summary>
        TOP,

        /// <summary>
        /// Denotes that a model is setup for use as a 'central' part, with the origin in the center of the model.<para/>
        /// Will be offset upwards by half of its height when used in a slot denoted for 'top' style models.<para/>
        /// Will be offset downwards by half of its height when used in a slot denoted for 'bottom' style models.<para/>
        /// </summary>
        CENTRAL,

        /// <summary>
        /// Denotes that a model is setup for use as a 'bottom' part, with the origin located at the top of the model.<para/>
        /// Will be rotated 180 degrees around origin when used in a slot denoted for 'top' style models.<para/>
        /// Will be offset vertically upwards by half of its height when used in a slot denoted for 'central' models.<para/>
        /// </summary>
        BOTTOM
    }

    /// <summary>
    /// Class denoting a the transforms to use from a single database model.  Allows for combining multiple entire models, and/or transforms from models, all into a single active/usable Model
    /// </summary>
    public class SubModelData
    {

        public readonly string modelURL;
        public readonly string[] modelMeshes;
        public readonly string[] renameMeshes;
        public readonly string[] deleteMeshes;
        public readonly string parent;
        public readonly Vector3 rotation;
        public readonly Vector3 position;
        public readonly Vector3 scale;

        public SubModelData(ConfigNode node)
        {
            modelURL = node.ROLGetStringValue("modelName");
            modelMeshes = node.ROLGetStringValues("transform");
            renameMeshes = node.ROLGetStringValues("rename");
            deleteMeshes = node.ROLGetStringValues("disableTransform");
            parent = node.ROLGetStringValue("parent", string.Empty);
            position = node.ROLGetVector3("position", Vector3.zero);
            rotation = node.ROLGetVector3("rotation", Vector3.zero);
            scale = node.ROLGetVector3("scale", Vector3.one);
        }

        public SubModelData(string modelURL, string[] meshNames, string[] deleteNames, string parent, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            this.modelURL = modelURL;
            this.modelMeshes = meshNames;
            this.renameMeshes = new string[0];
            this.deleteMeshes = deleteNames;
            this.parent = parent;
            this.position = pos;
            this.rotation = rot;
            this.scale = scale;
        }

        public void SetupSubmodel(GameObject modelRoot)
        {
            if (modelMeshes.Length > 0)
            {
                List<Transform> toKeep = new List<Transform>();
                List<Transform> toCheck = new List<Transform>();
                foreach (Transform tr in modelRoot.transform.ROLGetAllChildren())
                {
                    if (tr is Transform)
                    {
                        if (IsActiveMesh(tr.name)) toKeep.Add(tr); else toCheck.Add(tr);
                    }
                }
                foreach (Transform tr in toCheck.Where(x => !IsParent(x, toKeep)))
                {
                    GameObject.DestroyImmediate(tr.gameObject);
                }
            }
            foreach (string renameMesh in renameMeshes)
            {
                string[] split = renameMesh.Split(',');
                if (split.Length < 2)
                {
                    ROLLog.error("ERROR: Mesh rename format invalid, must specify <oldName>,<newName>");
                    continue;
                }
                string oldName = split[0].Trim();
                string newName = split[1].Trim();
                foreach (Transform tr in modelRoot.transform.ROLFindChildren(oldName))
                {
                    tr.name = newName;
                }
            }
            foreach (Transform tr in modelRoot.transform.ROLGetAllChildren())
            {
                List<Transform> toKeep = new List<Transform>();
                List<Transform> toCheck = new List<Transform>();
                if (tr is Transform)
                {
                    toCheck.Add(tr);
                }
                foreach (string delTrans in deleteMeshes)
                {
                    foreach (Transform trans in toCheck)
                    {
                        ROLLog.log($"trans: {trans.name} -> {delTrans}");
                        if (trans.name == delTrans)
                        {
                            tr.gameObject.SetActive(false);
                            ROLLog.log($"Transform {tr} removed.");
                        }                        
                    }
                }
            }
        }

        private bool IsActiveMesh(string transformName) => modelMeshes.Contains(transformName);

        private bool IsParent(Transform toCheck, List<Transform> children)
        {
            foreach (Transform child in children)
            {
                if (child.ROLisParent(toCheck)) { return true; }
            }
            return false;
        }
    }

    /// <summary>
    /// Data class for specifying which meshes should be merged into singular mesh instances.
    /// For use in game-object reduction for models composited from many sub-meshes.
    /// </summary>
    public class MeshMergeData
    {

        /// <summary>
        /// The name of the transform to parent the merged meshes into.
        /// </summary>
        public readonly string parentTransform;

        /// <summary>
        /// The name of the transform to merge the specified meshes into.  If this transform is not present, it will be created.
        /// Will be parented to 'parentTransform' if that field is populated, else it will become the 'root' transform in the model.
        /// </summary>
        public readonly string targetTransform;

        /// <summary>
        /// The names of the meshes to merge into the target transform.
        /// </summary>
        public readonly string[] meshNames;

        public MeshMergeData(ConfigNode node)
        {
            parentTransform = node.ROLGetStringValue("parent", string.Empty);
            targetTransform = node.ROLGetStringValue("target", "MergedMesh");
            meshNames = node.ROLGetStringValues("mesh");
        }

        /// <summary>
        /// Given the input root transform for a fully assembled model (e.g. from sub-model-data),
        /// locate any transforms that should be merged, merge them into the specified target transform,
        /// and parent them to the specified parent transform (or root if NA).
        /// </summary>
        /// <param name="root"></param>
        public void MergeMeshes(Transform root)
        {
            //find target transform
            //create if it doesn't exist
            Transform target = root.ROLFindRecursive(targetTransform);
            if (target == null)
            {
                target = new GameObject(targetTransform).transform;
                target.NestToParent(root);
            }

            //locate mesh filter on target transform
            //add a new one if not already present
            MeshFilter mf = target.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = target.gameObject.AddComponent<MeshFilter>();
                mf.mesh = new Mesh();
            }

            Material material = null;

            //merge meshes into singular mesh object
            //copy material/rendering settings from one of the original meshes
            List<CombineInstance> cis = new List<CombineInstance>();
            foreach (string meshName in meshNames)
            {
                foreach (Transform tr in root.ROLFindChildren(meshName))
                {
                    //locate mesh filter from specified mesh(es)
                    if (tr.GetComponent<MeshFilter>() is MeshFilter mm)
                    {
                        CombineInstance ci = new CombineInstance
                        {
                            mesh = mm.sharedMesh,
                            transform = tr.localToWorldMatrix
                        };
                        cis.Add(ci);
                        //if we don't currently have a reference to a material, grab a ref to/copy of the shared material
                        //for the current mesh(es).  These must all use the same materials
                        material ??= tr.GetComponent<Renderer>().material;
                    }
                }
            }
            mf.mesh.CombineMeshes(cis.ToArray());

            //update the material for the newly combined mesh
            //add mesh-renderer component if necessary
            Renderer renderer = target.GetComponent<Renderer>();
            renderer ??= target.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            //parent the new output GO to the specified parent
            //or parent target transform to the input root if no parent is specified
            target.parent = (!string.IsNullOrEmpty(parentTransform) && root.ROLFindRecursive(parentTransform) is Transform parent)
                            ? parent : root;
        }
    }

    /// <summary>
    /// Data that defines how a compound model scales and updates its height with scale changes.
    /// </summary>
    public class CompoundModelData
    {
        /*
            Compound Model Definition and Manipulation

            Compound Model defines the following information for all transforms in the model that need position/scale updated:
            * total model height - combined height of the model at its default diameter.
            * height - of the meshes of the transform at default scale
            * canScaleHeight - if this particular transform is allowed to scale its height
            * index - index of the transform in the model, working from origin outward.
            * v-scale axis -- in case it differs from Y axis

            Updating the height on a Compound Model will do the following:
            * Inputs - vertical scale, horizontal scale
            * Calculate the desired height from the total model height and input vertical scale factor
            * Apply horizontal scaling directly to all transforms.
            * Apply horizontal scale factor to the vertical scale for non-v-scale enabled meshes (keep aspect ratio of those meshes).
            * From total desired height, subtract the height of non-scaleable meshes.
            * The 'remainderTotalHeight' is then divided proportionately between all remaining scale-able meshes.
            * Calculate needed v-scale for the portion of height needed for each v-scale-able mesh.
         */
        private readonly CompoundModelTransformData[] compoundTransformData;

        public CompoundModelData(ConfigNode node)
        {
            ConfigNode[] trNodes = node.GetNodes("TRANSFORM");
            int len = trNodes.Length;
            compoundTransformData = new CompoundModelTransformData[len];
            for (int i = 0; i < len; i++)
            {
                compoundTransformData[i] = new CompoundModelTransformData(trNodes[i]);
            }
        }

        public void SetHeightExplicit(ROLModelDefinition def, GameObject root, float dScale, float height, ModelOrientation orientation)
        {
            float vScale = height / def.height;
            SetHeightFromScale(def, root, dScale, vScale, orientation);
        }

        public void SetHeightFromScale(ROLModelDefinition def, GameObject root, float dScale, float vScale, ModelOrientation orientation)
        {
            float desiredHeight = def.height * vScale;
            float staticHeight = GetStaticHeight() * dScale;
            float neededScaleHeight = desiredHeight - staticHeight;

            //iterate through scaleable transforms, calculate total height of scaleable transforms; use this height to determine 'percent share' of needed scale height for each transform
            float totalScaleableHeight = 0f;
            foreach (CompoundModelTransformData data in compoundTransformData)
            {
                totalScaleableHeight += data.canScaleHeight ? data.height : 0;
            }

            float pos = 0f;//pos starts at origin, is incremented according to transform height along 'dir'
            float dir = orientation == ModelOrientation.BOTTOM ? -1f : 1f;//set from model orientation, either +1 or -1 depending on if origin is at botom or top of model (ModelOrientation.TOP vs ModelOrientation.BOTTOM)
            float percent, scale, height;

            foreach (CompoundModelTransformData data in compoundTransformData)
            {
                percent = data.canScaleHeight ? data.height / totalScaleableHeight : 0f;
                height = percent * neededScaleHeight;
                scale = height / data.height;

                foreach (Transform tr in root.transform.ROLFindChildren(data.name))
                {
                    tr.localPosition = data.vScaleAxis * (pos + data.offset * dScale);
                    float localVerticalScale = data.canScaleHeight ? scale : dScale;
                    pos += dir * (data.canScaleHeight ? height : dScale * data.height);
                    tr.localScale = GetScaleVector(dScale, localVerticalScale, data.vScaleAxis);
                }
            }
        }

        /// <summary>
        /// Returns a vector representing the 'localScale' of a transform, using the input 'axis' as the vertical-scale axis.
        /// Essentially returns axis*vScale + ~axis*hScale
        /// </summary>
        /// <param name="sHoriz"></param>
        /// <param name="sVert"></param>
        /// <param name="axis"></param>
        /// <returns></returns>
        private Vector3 GetScaleVector(float sHoriz, float sVert, Vector3 axis)
        {
            if (axis.x < 0) { axis.x = 1; }
            if (axis.y < 0) { axis.y = 1; }
            if (axis.z < 0) { axis.z = 1; }
            return (axis * sVert) + (GetInverseVector(axis) * sHoriz);
        }

        /// <summary>
        /// Kind of like a bitwise inversion for a vector.
        /// If the input has any value for x/y/z, the output will have zero for that variable.
        /// If the input has zero for x/y/z, the output will have a one for that variable.
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        private Vector3 GetInverseVector(Vector3 axis)
        {
            Vector3 val = Vector3.one;
            if (axis.x != 0) { val.x = 0; }
            if (axis.y != 0) { val.y = 0; }
            if (axis.z != 0) { val.z = 0; }
            return val;
        }

        /// <summary>
        /// Returns the sum of non-scaleable transform heights from the compound model data.
        /// </summary>
        /// <returns></returns>
        private float GetStaticHeight()
        {
            float val = 0f;
            foreach (CompoundModelTransformData data in compoundTransformData)
            {
                if (!data.canScaleHeight) { val += data.height; }
            }
            return val;
        }
    }

    /// <summary>
    /// Data class for a single transform in a compound-transform-enabled model.
    /// </summary>
    public class CompoundModelTransformData
    {
        public readonly string name;
        public readonly bool canScaleHeight = false;//can this transform scale its height
        public readonly float height;//the height of the meshes attached to this transform, at scale = 1
        public readonly float actualHeight;//the height of the meshes attached to this transform, at scale = 1
        public readonly float offset;//the vertical offset of the meshes attached to this transform, when translated this amount the top/botom of the meshes will be at transform origin.
        public readonly int order;//the linear index of this transform in a vertical model setup stack
        public readonly Vector3 vScaleAxis = Vector3.up;

        public CompoundModelTransformData(ConfigNode node)
        {
            name = node.ROLGetStringValue("name");
            canScaleHeight = node.ROLGetBoolValue("canScale");
            height = node.ROLGetFloatValue("height");
            actualHeight = node.ROLGetFloatValue("actualHeight");
            offset = node.ROLGetFloatValue("offset");
            order = node.ROLGetIntValue("order");
            vScaleAxis = node.ROLGetVector3("axis", Vector3.up);
        }
    }
}
