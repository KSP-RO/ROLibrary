using HarmonyLib;
using Smooth.Slinq.Context;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;


namespace ROLib.Harmony
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class HarmonyPatcher : MonoBehaviour
    {
        internal void Start()
        {
            //HarmonyLib.Harmony.DEBUG = true;
            var harmony = new HarmonyLib.Harmony("ROLib.Harmony");
            harmony.PatchAll();
        }
    }

    static class HarmonyPatches {
        
        [HarmonyPatch(typeof(FlightIntegrator), "FixedUpdate")] 
        public class FixedUpdate_patch{
            internal static void Prefix() 
            {
                HarmonyFunctions.cacheStandardSpecificHeatCapacity = PhysicsGlobals.StandardSpecificHeatCapacity;
            }
        }

        [HarmonyPatch(typeof(FlightIntegrator), "UpdateMassStats")]
        public static class UpdateMassStats_patch {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions) {
                var SetSkinThermalMass = AccessTools.Method(typeof(FlightIntegrator), nameof(FlightIntegrator.SetSkinThermalMass));
                var resourceMass = AccessTools.Field(typeof(Part), nameof(Part.resourceMass));
                var thermalMass = AccessTools.Field(typeof(Part), nameof(Part.thermalMass));

                var codes = codeInstructions.ToList();
                bool found1 = false;
                bool found2 = false;
                bool found3 = false;
                int index = 0;
                int index2 = 0; 
                int index3 = 0; 
                for (int i = 0; i < codes.Count; i++) {
                    CodeInstruction code = codes[i];

                    // find start of the first part.thermalMass calculation
                    if (!found1 & code.opcode == OpCodes.Stfld && code.OperandIs(resourceMass)) 
                    {
                        found1 = true;
                        index = i + 1;
                    }
                    else if (code.opcode == OpCodes.Callvirt & code.OperandIs(SetSkinThermalMass)) 
                    {
                        // replace SetSkinThermalMass() call with our SetSkinThermalMass()
                        codes[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HarmonyFunctions), nameof(HarmonyFunctions.SetSkinThermalMass)));
                        index2 = i;
                        found2 = true;
                    }
                    // skip secound thermalMass calculation
                    else if (found1 & found2 & !found3) 
                    {
                        if (code.opcode == OpCodes.Stfld && code.OperandIs(thermalMass)){
                            found3 = true;
                            index3 = i;
                        }
                    }
                }
                if (index2 > 0) {
                    // remove: IL_0056, which loads the instance of the class (FlightIntegrator) onto the stack before SetSkinThermalMass call
                    codes.RemoveRange(index2 - 2, 1);
                    index2 -= 1;
                    // remove code for the second part.thermalMass calculation
                    if (index3 > 0)
                        codes.RemoveRange(index2 + 1, index3 - index2 - 1);
                    // remove code for the first part.thermalMass calculation
                    if (index > 0)
                        codes.RemoveRange(index, index2 - index - 1);
                } 
                if (found2 is false) 
                {
                    throw new ArgumentException("[ROThermal] Harmony Transpiler instructions not found");
                }
                else 
                {
                    Debug.Log("[ROThermal] Harmony Transpiler found instruction 1 " + found1 + " instruction 2  " + found2 + " instruction 3 " + found3 );
                }
                return codes;
            }
        }
    }
    public static class HarmonyFunctions
    {
        public static double cacheStandardSpecificHeatCapacity;
        public static void SetSkinThermalMass(Part part)
        {
            Debug.Log("[ROThermal] Harmony SetSkinThermalMass call for " + part);
            double num = cacheStandardSpecificHeatCapacity * part.thermalMassModifier;
            double massSkin = part.skinMassPerArea * part.radiativeArea;

            part.thermalMass = ((double)part.mass - massSkin) * num + part.resourceThermalMass;

            part.skinThermalMass = Math.Max(0.1, 0.001 * part.skinMassPerArea * part.skinThermalMassModifier * part.radiativeArea * num);
            part.skinThermalMassRecip = 1.0 / part.skinThermalMass;
        }
    }
}
// /