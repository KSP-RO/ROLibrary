using System.Text.RegularExpressions;
using UnityEngine;

namespace ROLib
{
    public class ModuleRODecoupler : PartModule
    {
        private const string GroupName = "ModuleRODecoupler";
        private const string GroupDisplayName = "RO-Decoupler";
        public const float minImpulse = 0.1f;

        #region KSPFields

        /// <summary>
        /// Maximum ejection impulse available.
        /// </summary>
        [KSPField] public float maxImpulse = 600.0f;

        /// <summary>
        /// Diameter exponent to multiply the force by.
        /// </summary>
        [KSPField] public float diamExponent = 0.525f;
        
        /// <summary>
        /// Diameter exponent to multiply the force by.
        /// </summary>
        [KSPField] public double triggerTime = 0;

        /// <summary>
        /// Displays the current Ejection Force
        /// </summary>
        [KSPField(guiActiveEditor = true, guiName = "Ejection Force:", guiUnits="N", guiFormat="F0", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public float currentEjectionForce = 0.0f;
        
        /// <summary>
        /// Displays the current Ejection Force
        /// </summary>
        [KSPField(isPersistant = true, guiActive = true, guiName = "Remaining Time", guiFormat="F2", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public double remainingTime = 0f;
        
        /// <summary>
        /// Does the part automatically decouple?
        /// </summary>
        [KSPField(isPersistant = true, guiActive =true, guiActiveEditor =true, guiName = "Auto Decouple", groupName = GroupName)]
        public bool isAutoDecouple = false;

        /// <summary>
        /// Set the part to automatically decouple
        /// </summary>
        [KSPEvent(guiName = "Toggle Auto Decouple", guiActiveEditor = true)]
        public void ToggleAutoDecouple()
        {
            isAutoDecouple = !isAutoDecouple;
            this.ROLforEachSymmetryCounterpart(module => module.isAutoDecouple = this.isAutoDecouple);
            ShowAutoDecoupleDelay();
        }
        
        /// <summary>
        /// How many seconds until the other node automatically decouples
        /// </summary>
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Decoupler Delay", guiUnits = "s", groupName = GroupName),
        UI_FloatRange(suppressEditorShipModified = true)]
        public float autoDecoupleDelay = 5f;

        #endregion KSPFields

        #region Private Variables

        private ModuleDecouple[] decouplers;
        private ModuleDecouple topDecoupler;
        private ModuleDecouple bottomDecoupler;
        private ModuleROTank modularPart;
        private bool isCountingDown = false;

        #endregion Private Variables

        #region Standard KSP Overrides

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            part.ActivatesEvenIfDisconnected = true;
            this.ROLupdateUIFloatRangeControl(nameof(autoDecoupleDelay), 0, 10, 1);
            Fields[nameof(autoDecoupleDelay)].guiActiveEditor = isAutoDecouple;
            
            decouplers = part.GetComponents<ModuleDecouple>();
            topDecoupler = decouplers[0];
            bottomDecoupler = decouplers[1];
            topDecoupler.explosiveDir = Vector3.up;
            ROLLog.log($"decouplers: {decouplers}");
            ROLLog.log($"topDecoupler: {topDecoupler.explosiveNodeID}, {topDecoupler.explosiveDir}");
            ROLLog.log($"bottomDecoupler: {bottomDecoupler.explosiveNodeID}, {bottomDecoupler.explosiveDir}");

            modularPart = part.FindModuleImplementing<ModuleROTank>();
            if (!(topDecoupler is ModuleDecouple && bottomDecoupler is ModuleDecouple && modularPart is ModuleROTank))
            {
                ROLLog.error($"{part} Unable to find ModuleDecouple or ModuleROTank modules");
                isEnabled = enabled = false;
                return;
            }
            else
            {
                topDecoupler.Events[nameof(ToggleStaging)].advancedTweakable = false;
                bottomDecoupler.Events[nameof(ToggleStaging)].advancedTweakable = false;
                if (modularPart is ModuleROTank)
                    modularPart.Fields[nameof(modularPart.currentDiameter)].uiControlEditor.onFieldChanged += OnDiameterChange;
                UpdateImpulseValues();
            }
        }

        public override void OnUpdate()
        {
            if (!isAutoDecouple) return;
            if (topDecoupler.isDecoupled || bottomDecoupler.isDecoupled && triggerTime == 0)
            {
                isCountingDown = true;
                triggerTime = Planetarium.GetUniversalTime();
                ROLLog.log($"triggerTime: {triggerTime}");
            }

            if (isCountingDown && triggerTime > 0)
            {
                remainingTime = triggerTime + autoDecoupleDelay - Planetarium.GetUniversalTime();
                ROLLog.log($"remainingTime: {remainingTime}");

                if (remainingTime <= 0)
                {
                    triggerTime = 0;
                    remainingTime = 0;
                    isCountingDown = false;
                    isAutoDecouple = false;
                    if (!topDecoupler.isDecoupled) topDecoupler.Decouple();
                    if (!bottomDecoupler.isDecoupled) bottomDecoupler.Decouple();
                }
            }
        }

        #endregion Standard KSP Overrides

        #region Custom Methods

        private void OnDiameterChange(BaseField bf, object obj) => UpdateImpulseValues();
        
        public float EjectionForce => Mathf.Round(Mathf.Clamp(Mathf.Pow(modularPart.currentDiameter, diamExponent) * 100, minImpulse, maxImpulse));
        
        public void UpdateImpulseValues()
        {
            currentEjectionForce = topDecoupler.ejectionForce = bottomDecoupler.ejectionForce = EjectionForce;
        }

        private void ShowAutoDecoupleDelay()
        {
            Fields[nameof(autoDecoupleDelay)].guiActive = isAutoDecouple;
            Fields[nameof(autoDecoupleDelay)].guiActiveEditor = isAutoDecouple;
            Fields[nameof(remainingTime)].guiActive = isAutoDecouple;
        }
        
        #endregion Custom Methods

    }
}
