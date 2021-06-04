using System;
using System.Collections.Generic;

namespace ROLib
{
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
                index = Math.Max(0, index);
                index = Math.Min(definitions.Length - 1, index);
                return definitions[index];
            }
        }

        public ModelDefinitionVariantSet(string name) => variantName = name;

        public void AddModels(ModelDefinitionLayoutOptions[] defs)
        {
            List<ModelDefinitionLayoutOptions> allDefs = new List<ModelDefinitionLayoutOptions>();
            allDefs.AddRange(definitions);
            allDefs.AddUniqueRange(defs);
            definitions = allDefs.ToArray();
        }

        public int IndexOf(ModelDefinitionLayoutOptions def) => definitions.IndexOf(def);
    }
}
