using System;
using System.Collections.Generic;
using UnityEngine;
//using KSPShaderTools;

namespace ROLib
{

    /// <summary>
    /// Static loading/management class for ModelBaseData.  This class is responsible for loading ModelBaseData from configs and returning ModelBaseData instances for an input model name.
    /// </summary>
    public static class ROLModelData
    {
        private static Dictionary<String, ROLModelDefinition> baseModelData = new Dictionary<String, ROLModelDefinition>();
        private static bool defsLoaded = false;

        private static void loadDefs()
        {
            if (defsLoaded) { return; }
            defsLoaded = true;
            ConfigNode[] modelDatas = GameDatabase.Instance.GetConfigNodes("ROL_MODEL");
            ROLModelDefinition data;
            foreach (ConfigNode node in modelDatas)
            {
                data = new ROLModelDefinition(node);
                if (ROLGameSettings.LoggingEnabled)
                {
                    ROLLog.log("Loading model definition data for name: " + data.name + " with model URL: " + data.modelName);
                }
                if (baseModelData.ContainsKey(data.name))
                {
                    ROLLog.error("Model defs already contains def for name: " + data.name + ".  Please check your configs as this is an error.  The duplicate entry was found in the config node of:\n"+node);
                    continue;
                }
                baseModelData.Add(data.name, data);
            }
        }

        public static void loadConfigData()
        {
            defsLoaded = false;
            baseModelData.Clear();
            loadDefs();
        }

        /// <summary>
        /// Find a single model definition by name.  Returns null if not found.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static ROLModelDefinition getModelDefinition(String name)
        {
            if (!defsLoaded) { loadDefs(); }
            ROLModelDefinition data = null;
            baseModelData.TryGetValue(name, out data);
            return data;
        }

        /// <summary>
        /// Return a group of model definition layout options by model definition name.
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        public static ROLModelDefinition[] getModelDefinitions(string[] names)
        {
            List<ROLModelDefinition> defs = new List<ROLModelDefinition>();
            int len = names.Length;
            for (int i = 0; i < len; i++)
            {
                ROLModelDefinition def = getModelDefinition(names[i]);
                if (def != null)
                {
                    defs.AddUnique(def);
                }
                else
                {
                    ROLLog.error("Could not locate model defintion for name: " + names[i]);
                }
            }
            return defs.ToArray();
        }

        /// <summary>
        /// Create a group of model definition layout options by model definition name, with default (single position) layouts.
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        public static ModelDefinitionLayoutOptions[] getModelDefinitionLayouts(string[] names)
        {
            List<ModelDefinitionLayoutOptions> defs = new List<ModelDefinitionLayoutOptions>();
            int len = names.Length;
            for (int i = 0; i < len; i++)
            {
                ROLModelDefinition def = getModelDefinition(names[i]);
                if (def != null)
                {
                    defs.Add(new ModelDefinitionLayoutOptions(def));
                }
                else
                {
                    ROLLog.error("Could not locate model defintion for name: " + names[i]);
                }
            }
            return defs.ToArray();
        }

        /// <summary>
        /// Create a group of model definition layout sets.  Loads the model definitions + their supported layout configurations.
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static ModelDefinitionLayoutOptions[] getModelDefinitions(ConfigNode[] nodes)
        {
            int len = nodes.Length;

            List<ModelDefinitionLayoutOptions> options = new List<ModelDefinitionLayoutOptions>();
            List<ModelLayoutData> layoutDataList = new List<ModelLayoutData>();
            ROLModelDefinition def;

            string[] groupedNames;
            string[] groupedLayouts;
            int len2;

            for (int i = 0; i < len; i++)
            {
                //because configNode.ToString() reverses the order of values, and model def layouts are always loaded from string-cached config nodes
                //we need to reverse the order of the model and layout names during parsing
                groupedNames = nodes[i].ROLGetStringValues("model");
                groupedLayouts = nodes[i].ROLGetStringValues("layout", new string[] { "default" });
                len2 = groupedNames.Length;
                for (int k = 0; k < len2; k++)
                {
                    def = ROLModelData.getModelDefinition(groupedNames[k]);
                    layoutDataList.AddRange(ROLModelLayout.findLayouts(groupedLayouts));
                    if (nodes[i].HasValue("position") || nodes[i].HasValue("rotation") || nodes[i].HasValue("scale"))
                    {
                        Vector3 pos = nodes[i].ROLGetVector3("position", Vector3.zero);
                        Vector3 scale = nodes[i].ROLGetVector3("scale", Vector3.one);
                        Vector3 rot = nodes[i].ROLGetVector3("rotation", Vector3.zero);
                        ModelPositionData mpd = new ModelPositionData(pos, scale, rot);
                        ModelLayoutData custom = new ModelLayoutData("default", new ModelPositionData[] { mpd });
                        if (layoutDataList.Exists(m => m.name == "default"))
                        {
                            ModelLayoutData del = layoutDataList.Find(m => m.name == "default");
                            layoutDataList.Remove(del);
                        }
                        layoutDataList.Add(custom);
                    }
                    if (def == null)
                    {
                        ROLLog.error("Model definition was null for name: " + groupedNames[k]+". Skipping definition during loading of part");
                    }
                    else
                    {
                        options.Add(new ModelDefinitionLayoutOptions(def, layoutDataList.ToArray()));
                    }
                    layoutDataList.Clear();
                }
            }
            return options.ToArray();
        }

    }
}
