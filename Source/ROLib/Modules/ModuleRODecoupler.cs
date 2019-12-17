using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ROLib
{
    public class ModuleRODecoupler : PartModule
    {

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
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Ejection Force:", guiUnits="N", guiFormat="F0", groupName = "ModuleRODecoupler", groupDisplayName = "RO-Decoupler")]
        public float currentEjectionForce = 0.0f;

        /// <summary>
        /// Operate as a decoupler or a separator.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Type:", groupName = "ModuleRODecoupler"),
         UI_Toggle(disabledText = "Decoupler", enabledText = "Separator")]
        public bool isOmniDecoupler = false;

        #endregion KSPFields


        #region Private Variables

        private ModuleDecouple decouple;
        private ModuleROTank modularPart;

        #endregion Private Variables


        #region Standard KSP Overrides

        public override void OnStart(StartState state)
        {

            base.OnStart(state);

            if (!FindDecoupler())
            {
                ROLLog.error("Unable to find any Decoupler modules");
                isEnabled = enabled = false;
                return;
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                decouple.isOmniDecoupler = isOmniDecoupler;
            }
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);

            ROLLog.debug("OnStartFinished()...");
            if (!FindModularPart())
            {
                ROLLog.error("Unable to find any Modular Part modules");
                isEnabled = enabled = false;
                return;
            }

            SetupUICallbacks();

            if (HighLogic.LoadedSceneIsEditor)
            {
                UpdateImpulseValues();
            }

        }

        #endregion Standard KSP Overrides


        #region Custom Methods

        private bool FindDecoupler()
        {
            ROLLog.debug("Finding Decoupler...");
            if (decouple == null)
                decouple = part.Modules["ModuleDecouple"] as ModuleDecouple;
            return decouple != null;
        }

        private ModuleROTank FindModularPart()
        {
            if (modularPart is null)
            {
                modularPart = part.FindModuleImplementing<ModuleROTank>();
            }
            return modularPart;
        }

        private void SetupUICallbacks()
        {
            ROLLog.debug("Setting up UICallbacks...");
            if (FindModularPart() is ModuleROTank p)
            {
                ROLLog.debug("p: " + p);
                ROLLog.debug("p.Fields[nameof(p.currentDiameter): " + p.Fields[nameof(p.currentDiameter)]);
                ROLLog.debug("p.Fields: " + p.Fields);

                UI_FloatEdit mp = p.Fields[nameof(p.currentDiameter)].uiControlEditor as UI_FloatEdit;
                mp.onFieldChanged += new Callback<BaseField, object>(OnDiameterChange);
            }
        }

        private void OnDiameterChange(BaseField bf, object obj) => UpdateImpulseValues();

        public void UpdateImpulseValues()
        {
            ROLLog.log("UpdateImpulseValues() called");
            float nf = (float) Math.Min(100f * Math.Pow(modularPart.currentDiameter, diamExponent), maxImpulse);
            nf = (float) Math.Round(nf, 0);
            currentEjectionForce = decouple.ejectionForce = nf;
        }

        #endregion Custom Methods

    }
}
