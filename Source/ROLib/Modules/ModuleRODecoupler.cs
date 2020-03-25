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
        [KSPField]
        public float maxImpulse = 600.0f;

        /// <summary>
        /// Diameter exponent to multiply the force by.
        /// </summary>
        [KSPField]
        public float diamExponent = 0.525f;

        /// <summary>
        /// Displays the current Ejection Force
        /// </summary>
        [KSPField(guiActiveEditor = true, guiName = "Ejection Force:", guiUnits="N", guiFormat="F0", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public float currentEjectionForce = 0.0f;

        /// <summary>
        /// Operate as a decoupler or a separator.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Type:", groupName = GroupName),
         UI_Toggle(disabledText = "Decoupler", enabledText = "Separator")]
        public bool isOmniDecoupler = false;

        #endregion KSPFields

        #region Private Variables

        private ModuleDecouple decouple;
        private ModuleROTank modularPart;

        #endregion Private Variables

        #region Standard KSP Overrides

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            decouple = part.FindModuleImplementing<ModuleDecouple>();
            modularPart = part.FindModuleImplementing<ModuleROTank>();
            if (!(decouple is ModuleDecouple && modularPart is ModuleROTank))
            {
                ROLLog.error($"{part} Unable to find ModuleDecouple or ModuleROTank modules");
                isEnabled = enabled = false;
                return;
            }
            else
            {
                decouple.isOmniDecoupler = isOmniDecoupler;
                if (modularPart is ModuleROTank)
                    modularPart.Fields[nameof(modularPart.currentDiameter)].uiControlEditor.onFieldChanged += OnDiameterChange;
                UpdateImpulseValues();
            }
        }

        #endregion Standard KSP Overrides

        #region Custom Methods

        private void OnDiameterChange(BaseField bf, object obj) => UpdateImpulseValues();
        public float EjectionForce => Mathf.Round(Mathf.Clamp(Mathf.Pow(modularPart.currentDiameter, diamExponent) * 100, minImpulse, maxImpulse));
        public void UpdateImpulseValues()
        {
            currentEjectionForce = decouple.ejectionForce = EjectionForce;
        }

        #endregion Custom Methods

    }
}
