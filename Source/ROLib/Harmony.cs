using FerramAerospaceResearch.FARGUI.FAREditorGUI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;
using Debug = UnityEngine.Debug;


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

    static class HarmonyPatches 
    {
        
        [HarmonyPatch(typeof(FlightIntegrator), "FixedUpdate")] 
        public class FlightIntegrator_FixedUpdate_patch
        {
            internal static void Prefix() 
            {
                HarmonyFunctions.cacheStandardSpecificHeatCapacity = PhysicsGlobals.StandardSpecificHeatCapacity;
            }
        }

        [HarmonyPatch(typeof(FlightIntegrator), "UpdateMassStats")]
        public static class FlightIntegrator_UpdateMassStats_patch 
        {
            /// <summary>
            /// Replaceing ThermalMass calculation of parts
            /// </summary>
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator il) {
                var resourceMass = AccessTools.Field(typeof(Part), nameof(Part.resourceMass));
                var thermalMassReciprocal = AccessTools.Field(typeof(Part), nameof(Part.thermalMassReciprocal));
                var mass = AccessTools.Field(typeof(Part), nameof(Part.mass));
                var kerbalMass = AccessTools.Field(typeof(Part), "kerbalMass");
                var kerbalResourceMass = AccessTools.Field(typeof(Part), "kerbalResourceMass");
                var inventoryMass = AccessTools.Field(typeof(Part), "inventoryMass");

                var codes = codeInstructions.ToList();
                bool found1 = false;
                bool found2 = false;
                int index = 0;
                int index2 = 0; 
                for (int i = 0; i < codes.Count; i++) {
                    CodeInstruction code = codes[i];

                    /// find staring & ending index of the lines of code, in order to remove them later 
                    //    part.thermalMass = (double)part.mass * this.cacheStandardSpecificHeatCapacity * part.thermalMassModifier + part.resourceThermalMass;
                    //    this.SetSkinThermalMass(part);
                    //    part.thermalMass = Math.Max(part.thermalMass - part.skinThermalMass, 0.1);
                    //    part.thermalMassReciprocal = 1.0 / part.thermalMass;
                    if (!found1 & code.opcode == OpCodes.Stfld && code.OperandIs(resourceMass)) 
                    {
                        found1 = true;
                        index = i + 1;
                    }
                    else if (found1 && code.opcode == OpCodes.Stfld & code.OperandIs(thermalMassReciprocal)) 
                    {
                        found2 = true;
                        index2 = i;
                    }
                }
                
                
                Label goToLabel566 = il.DefineLabel();
                Label goToLabel567;
                
                if (codes[index2 + 1].labels.Any())
                {
                    goToLabel567 = codes[index2 + 1].labels.First();
                }
                else
                {
                    goToLabel567 = il.DefineLabel();
                    codes[index2 + 1].labels.Add(goToLabel567);
                }
                

                var instructionsToInsert = new List<CodeInstruction>
                {
                    //    if (part.kerbalMass != 0f)
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldfld, kerbalMass),
                    new CodeInstruction(OpCodes.Ldc_R4, 0.0f),
                    new CodeInstruction(OpCodes.Beq_S, goToLabel566),

                    //    float mass = part.mass - part.mass - part.mass - part.mass;
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldfld, mass),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldfld, kerbalMass),
                    new CodeInstruction(OpCodes.Sub),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldfld, kerbalResourceMass),
                    new CodeInstruction(OpCodes.Sub),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldfld, inventoryMass),
                    new CodeInstruction(OpCodes.Sub),

                    //    HarmonyFunctions.SetThermalMass(part, mass);
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HarmonyFunctions), nameof(HarmonyFunctions.SetThermalMass))),
                    new CodeInstruction(OpCodes.Br_S, goToLabel567),
                };

                //    HarmonyFunctions.SetThermalMass2(part);
                CodeInstruction instruction = new CodeInstruction(OpCodes.Ldloc_0);
                instruction.labels.Add(goToLabel566);
                instructionsToInsert.Add(instruction);
                instructionsToInsert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HarmonyFunctions), nameof(HarmonyFunctions.SetThermalMass2))));

                /// Remove indexed instruction & insert our own in their place
                if (index2 > 0) {
                    if (index > 0)
                    {
                        codes.RemoveRange(index, index2 - index + 1);
                        codes.InsertRange(index, instructionsToInsert);
                    }
                }
                if (found2 is false) 
                {
                    throw new ArgumentException("[ROThermal] Harmony Transpiler instructions not found");
                }
                else 
                {
                    Debug.Log("[ROThermal] Harmony Transpiler for UpdateMassStats successful");
                }
                return codes;
            }
        }
        [HarmonyPatch(typeof(EditorGUI), "FixedUpdate")]
        public class EditorGUI_FixedUpdate_patch 
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
            {
                var stopWatchReset = AccessTools.Method(typeof(Stopwatch), nameof(Stopwatch.Reset));
                var codes = codeInstructions.ToList();
                bool found = false;

                for (int i = 0; i < codes.Count; i++)
                {
                    CodeInstruction code = codes[i];
                    yield return code;
                    if (code.opcode == OpCodes.Callvirt & code.OperandIs(stopWatchReset) && found == false) 
                    {   
                        found = true;
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EditorLogic), "get_" + nameof(EditorLogic.RootPart)));
                        yield return new CodeInstruction(OpCodes.Ldstr, "OnVoxelizationComplete");
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Part), nameof(Part.SendEvent), new[] {typeof(string)}));
                    }
                }
                if (found is false) 
                {
                    throw new ArgumentException("[ROThermal] Harmony Transpiler EditorGUI_FixedUpdate_patch instructions not found");
                }
                else 
                {
                    Debug.Log("[ROThermal] Harmony Transpiler EditorGUI_FixedUpdate_patch instructions found");
                }
            }
        }
    }
    public static class HarmonyFunctions
    {
        public static double cacheStandardSpecificHeatCapacity;
        private const double MinValue = 0.001;
        // excessMass = part.kerbalMass - part.kerbalResourceMass - part.inventoryMass;
        public static void SetThermalMass(float mass, Part part)
        {
            double num = cacheStandardSpecificHeatCapacity * part.thermalMassModifier;
            double massSkin = part.skinMassPerArea * part.radiativeArea * MinValue;
            double coreMass = (double)mass - massSkin;

            if (coreMass > MinValue)
            {
                part.thermalMass = coreMass * num + part.resourceThermalMass;
            }
            else
            {
                part.thermalMass = MinValue * num + part.resourceThermalMass;
            }
            part.thermalMassReciprocal = 1.0 / part.thermalMass;
            
            part.skinThermalMass = massSkin * part.skinThermalMassModifier * num;
            if (part.skinThermalMass < MinValue)
            {
                part.skinThermalMass = MinValue;
            }
            part.skinThermalMassRecip = 1.0 / part.skinThermalMass;
        }
        public static void SetThermalMass2(Part part)
        {
            double num = cacheStandardSpecificHeatCapacity * part.thermalMassModifier;
            double massSkin = part.skinMassPerArea * part.radiativeArea * MinValue;
            double coreMass = (double)part.mass - massSkin;

            if (coreMass > MinValue)
            {
                part.thermalMass = coreMass * num + part.resourceThermalMass;
            }
            else
            {
                part.thermalMass = MinValue * num + part.resourceThermalMass;
            }
            part.thermalMassReciprocal = 1.0 / part.thermalMass;
            
            part.skinThermalMass = massSkin * part.skinThermalMassModifier * num;
            if (part.skinThermalMass < MinValue)
            {
                part.skinThermalMass = MinValue;
            }
            part.skinThermalMassRecip = 1.0 / part.skinThermalMass;
        }
        public static void Test(Part part)
        {   
            double massSkin = part.skinMassPerArea * part.radiativeArea * MinValue;
            if (part.mass != 0f)
            {
                float massCrew = part.mass - part.mass - part.mass - part.mass;
                SetThermalMass(massCrew, part);
            }
            else
            {
                SetThermalMass2(part);
            }
            double tramble = part.skinMassPerArea * part.radiativeArea * MinValue;
        }
    }
}
//  Original 
//  double num = cacheStandardSpecificHeatCapacity * part.thermalMassModifier;
//  part.thermalMass = (double)part.mass * cacheStandardSpecificHeatCapacity * part.thermalMassModifier + part.resourceThermalMass;
//  part.skinThermalMass = Math.Max(0.1, Math.Min(0.001 * part.skinMassPerArea * part.skinThermalMassModifier * part.radiativeArea * num, (double)part.mass * num * 0.5));;
//  part.thermalMass = Math.Max(part.thermalMass - part.skinThermalMass, 0.1);