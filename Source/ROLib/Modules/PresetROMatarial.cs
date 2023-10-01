using UnityEngine;
using EdyCommonTools;
using System;
using System.Collections.Generic;
using System.IO;


namespace ROLib
{
    public enum PresetType
    {
        Core,
        Skin,
//        Combined,
    }
    public class PresetROMatarial
    {
        public static readonly Dictionary<string, PresetROMatarial> PresetsCore = new Dictionary<string, PresetROMatarial>();
        public static readonly Dictionary<string, PresetROMatarial> PresetsSkin = new Dictionary<string, PresetROMatarial>();
        public static bool Initialized { get; private set; } = false;

        [Persistent] public string name = "";
        [Persistent] public string description = "";
        [Persistent] public PresetType type = PresetType.Skin;
        [Persistent] public bool disableModAblator = false;

        // procedural heat shield parameters
        [Persistent] public float heatShieldAblator = 0.0f;
        [Persistent] public float heatShieldBaseCost = 0.0f;
        [Persistent] public float costPerArea = 0.0f;
        [Persistent] public float costPerAreaMax = 0.0f;
        [Persistent] public float heatShieldAreaMult = 1.0f;
        [Persistent] public float heatShieldDiameterCost = 0.0f;

        // part parameters override
        //[Persistent] public float massOverride = -1;
        [Persistent] public double maxTempOverride = -1f;
        [Persistent] public double skinMaxTempOverride = -1f;
        [Persistent] public double specificHeatCapacity = -1f;
        
        [Persistent] public double emissiveConstantOverride = -1f;
        [Persistent] public double absorptiveConstant = -1f;
        [Persistent] public double thermalConductivity = -1f;
        [Persistent] public double skinSkinConductivity = -1f;
        

        [Persistent] public double skinHeightMin = 0.0f;
        [Persistent] public double skinMassPerArea = -1f;
        [Persistent] public double skinSpecificHeatCapacity = -1f;
        [Persistent] public double skinIntTransferCoefficient = -1f;

        [Persistent] public double skinHeightMax = -1.0f;
        [Persistent] public double skinMassPerAreaMax = -1f;
        [Persistent] public double skinSpecificHeatCapacityMax = -1f;
        [Persistent] public double skinIntTransferCoefficientMax = -1f;

        // ModuleAblator overrides
        [Persistent] public string _ablativeResource;
        [Persistent] public double _lossExp;
        [Persistent] public double _lossConst;
        [Persistent] public double _pyrolysisLossFactor;
        [Persistent] public double _ablationTempThresh;
        [Persistent] public double _reentryConductivity;
        [Persistent] public bool _useNode;
        [Persistent] public string _nodeName;
        [Persistent] public float _charAlpha;
        [Persistent] public float _charMax;
        [Persistent] public float _charMin;
        [Persistent] public bool _useChar;
        [Persistent] public string _charModuleName;
        [Persistent] public string _outputResource;
        [Persistent] public double _outputMult;
        [Persistent] public double _infoTemp;
        [Persistent] public bool _usekg;
        [Persistent] public string _unitsName;
        [Persistent] public double _nominalAmountRecip;

        [Persistent] public string reentryTag;
        [SerializeField] public string[] restrictors = new string[] { };

        public string AblativeResource;
        public double? LossExp;
        public double? LossConst;
        public double? PyrolysisLossFactor;
        public double? AblationTempThresh;
        public double? ReentryConductivity;
        public bool? UseNode;
        public string NodeName;
        public float? CharAlpha;
        public float? CharMax;
        public float? CharMin;
        public bool? UseChar;
        public string CharModuleName;
        public string OutputResource;
        public double? OutputMult;
        public double? InfoTemp;
        public bool? Usekg;
        public string UnitsName;
        public double? NominalAmountRecip;
        public double[,] thermalPropMin;
        public double[,] thermalPropMax;
        public bool hasCVS = false;

        public PresetROMatarial(ConfigNode node)
        {
            double thermalInsulance = 0.0;
            double thermalInsulanceMax= 0.0;

            node.TryGetValue("name", ref name);
            node.TryGetValue("description", ref description);
            if (!node.TryGetEnum<PresetType>("type", ref type, PresetType.Skin))
                Debug.Log("[ROThermal] ThermalPreset type not defined: " + name);
            node.TryGetValue("disableModAblator", ref disableModAblator);

            node.TryGetValue("heatShieldAblator", ref heatShieldAblator);
            node.TryGetValue("heatShieldBaseCost", ref heatShieldBaseCost);
            node.TryGetValue("heatShieldDiameterCost", ref heatShieldDiameterCost);
            
            node.TryGetValue("heatShieldAreaMult", ref heatShieldAreaMult);

            node.TryGetValue("maxTemp", ref maxTempOverride);
            node.TryGetValue("skinMaxTemp", ref skinMaxTempOverride);
            node.TryGetValue("specificHeatCapacity", ref specificHeatCapacity);

            node.TryGetValue("thermalContactConductivity", ref thermalConductivity);
            node.TryGetValue("emissiveConstant", ref emissiveConstantOverride);
            node.TryGetValue("absorptiveConstant", ref absorptiveConstant);
            node.TryGetValue("skinSkinConductivity", ref skinSkinConductivity);

            node.TryGetValue("skinHeightMin", ref skinHeightMin);
            node.TryGetValue("costPerArea", ref costPerArea);
            node.TryGetValue("skinMassPerArea", ref skinMassPerArea);
            node.TryGetValue("skinSpecificHeatCapacity", ref skinSpecificHeatCapacity);
            node.TryGetValue("thermalInsulance", ref thermalInsulance);
 
            node.TryGetValue("skinHeightMax", ref skinHeightMax);
            node.TryGetValue("costPerAreaMax", ref costPerAreaMax);
            node.TryGetValue("skinMassPerAreaMax", ref skinMassPerAreaMax);
            node.TryGetValue("skinSpecificHeatCapacityMax", ref skinSpecificHeatCapacityMax);
            node.TryGetValue("thermalInsulanceMax", ref thermalInsulanceMax);
            node.TryGetValue("Reentry", ref reentryTag);

            skinIntTransferCoefficient = 1.0 / thermalInsulance;
            skinIntTransferCoefficientMax = 1.0 / thermalInsulanceMax;

            if (node.TryGetValue("ablativeResource", ref _ablativeResource))
                AblativeResource = _ablativeResource;
            if (node.TryGetValue("lossExp", ref _lossExp))
                LossExp = _lossExp;
            if (node.TryGetValue("lossConst", ref _lossConst))
                LossConst = _lossConst;
            if (node.TryGetValue("pyrolysisLossFactor", ref _pyrolysisLossFactor))
                PyrolysisLossFactor = _pyrolysisLossFactor;
            if (node.TryGetValue("ablationTempThresh", ref _ablationTempThresh))
                AblationTempThresh = _ablationTempThresh;
            if (node.TryGetValue("reentryConductivity", ref _reentryConductivity))
                ReentryConductivity = _reentryConductivity;
            if (node.TryGetValue("useNode", ref _useNode))
                UseNode = _useNode;
            if (node.TryGetValue("nodeName", ref _nodeName))
                NodeName = _nodeName;
            if (node.TryGetValue("charAlpha", ref _charAlpha))
                CharAlpha = _charAlpha;
            if (node.TryGetValue("charMax", ref _charMax))
                CharMax = _charMax;
            if (node.TryGetValue("charMin", ref _charMin))
                CharMin = _charMin;
            if (node.TryGetValue("useChar", ref _useChar))
                UseChar = _useChar;
            if (node.TryGetValue("charModuleName", ref _charModuleName))
                CharModuleName = _charModuleName;
            if (node.TryGetValue("outputResource", ref _outputResource))
                OutputResource = _outputResource;
            if (node.TryGetValue("outputMult", ref _outputMult))
                OutputMult = _outputMult;
            if (node.TryGetValue("infoTemp", ref _infoTemp))
                InfoTemp = _infoTemp;
            if (node.TryGetValue("usekg", ref _usekg))
                Usekg = _usekg;
            if (node.TryGetValue("unitsName", ref _unitsName))
                UnitsName = _unitsName;
            if (node.TryGetValue("nominalAmountRecip", ref _nominalAmountRecip))
                NominalAmountRecip = _nominalAmountRecip;

            if (node.TryGetValue("restrictors", ref restrictors))
                Debug.Log("[ROThermal] available restrictors loaded");
        }

        public PresetROMatarial(string name)
        {
            this.name = name;
        }

        public static void LoadPresets()
        {
             Debug.Log(" this is a branch");
            if (Initialized && PresetsCore.Count > 0 && PresetsSkin.Count > 0)
                return;

            var nodes = GameDatabase.Instance.GetConfigNodes("ROThermal_PRESET");
            string s = string.Empty;
            foreach (var node in nodes)
            {
                PresetROMatarial preset = null;

                if (node.TryGetValue("name", ref s) && !string.IsNullOrEmpty(s))
                    preset = new PresetROMatarial(node);

                if (preset != null) {
                    if (preset.type == PresetType.Skin) {
                        PresetsSkin[preset.name] = preset;
                        if (File.Exists("GameData/ROLib/Data/csv/" + preset.name + "_min.csv") & File.Exists("GameData/ROLib/Data/csv/" + preset.name + "_max.csv")) 
                        {
                            preset.loadCSV("GameData/ROLib/Data/csv/" + preset.name + "_min.csv", out preset.thermalPropMin);
                            preset.loadCSV("GameData/ROLib/Data/csv/" + preset.name + "_max.csv", out preset.thermalPropMax);
                            preset.hasCVS = true;
                        } 
                        else {
                            Debug.Log("[ROThermal] CSV doesnt exit: GameData/ROLib/Data/csv/" + preset.name + ".csv");
                        }
                        
                    } else {
                        PresetsCore[preset.name] = preset;
                    }
                }

                UnityEngine.Debug.Log($"[ROThermal] Found and loaded preset {preset.name}");
            }

            // initialize default fallback preset
            if (!PresetsCore.ContainsKey("default")){
                Debug.Log("[ROThermal] Preset \"default\" not found, creating an empty one");
                PresetsCore["default"] = new PresetROMatarial("default")
                {
                    type = PresetType.Core
                };
            }
                
            if (!PresetsSkin.ContainsKey("None")){
                Debug.Log("[ROThermal] Preset \"None\" not found, creating an empty one");
                PresetsSkin["None"] = new PresetROMatarial("None")
                {
                    type = PresetType.Skin
                };
            }
            Initialized = true;
        }

        public bool loadCSV (string fileName, out double[,] array) 
        {
            CsvFileReader reader = new CsvFileReader(fileName);
            CsvRow lines = new CsvRow();

            bool skipFirst = true;
            List<string[]> list = new List<string[]>();

            while (reader.ReadRow(lines))
            {
                if (skipFirst)
                {
                    skipFirst = false;
                    continue;
                }
                string[] row = lines.LineText.Split(
                        new string[] { "," },
                        StringSplitOptions.None
                    );
                list.Add(row);
            }
            reader.Close();

            int rowCount = list.Count;
            int columnCount = list[0].Length;

            array = new double[rowCount, columnCount];

            string str = "CSV table\n";
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < columnCount; j++)
                {
                    array[i, j] = double.Parse(list[i][j]);
                    str += array[i, j] + ", ";
                }
                str +='\n';
            }
            Debug.Log("[ROThermal] Loaded csv Data for " + fileName + ": GameData/ROLib/Data/csv/" + fileName + ".csv");
            Debug.Log(str);
            return true;
        }
    }
}
