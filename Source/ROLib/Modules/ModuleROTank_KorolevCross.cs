using System.Diagnostics;
using UnityEngine;

namespace ROLib
{
    public partial class ModuleROTank
    {
        /// <summary>
        /// Whether the Korolev cross feature can be enabled on this part.
        /// </summary>
        [KSPField]
        public bool supportsKorolevCross = false;

        /// <summary>
        /// Whether to try to replicate a Korolev cross on decouple.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Korolev cross", groupName = GroupName),
         UI_Toggle(suppressEditorShipModified = true)]
        public bool korolevCross = false;

        [KSPField(isPersistant = true)]
        public bool isFinished = false;

        /// <summary>
        /// Determines the force of booster tank getting pushed away from core. Dry mass multiplied by this number will give kN of force.
        /// </summary>
        [KSPField]
        public float force1Scale = 20;

        /// <summary>
        /// Determines the force at the tip of the booster tank that makes it spin. Dry mass multiplied by this number will give kN of force.
        /// </summary>
        [KSPField]
        public float force2Scale = 8;

        /// <summary>
        /// Between -1 and 1, 0 is no offset. Positive number moves the tip of the tank closer to core.
        /// </summary>
        [KSPField]
        public float topHorizOffsetFraction = 0.82f;

        [KSPField]
        public float force1Duration = 0.5f;
        [KSPField]
        public float force2Start = 1f;
        [KSPField]
        public float force2Duration = 0.5f;

        private HingeJoint joint;
        private ModuleDecouple decoupler;
        private Vector3 tipOffsetToRoot;
        private float fixedTimeSinceStart = 0;

        private void ToggleKorolevCross()
        {
            CleanupJoint();

            if (korolevCross)
            {
                CreateJoint();
            }
        }

        private void SetupKorolevCross()
        {
            if (!korolevCross || isFinished) return;

            if (decoupler == null)
            {
                ModuleDecouple[] decouplers = part.GetComponents<ModuleDecouple>();
                decoupler = decouplers[0];
                decoupler.ejectionForcePercent = 0;    // Disable builtin decoupler and only add force through code
            }

            float tankTopPos = coreModule.moduleHeight - coreModule.ModuleCenter;
            float tankRadius = coreModule.moduleDiameter / 2;
            tipOffsetToRoot = Vector3.up * tankTopPos + Vector3.back * topHorizOffsetFraction * tankRadius * topHorizOffsetFraction;

            if (HighLogic.LoadedSceneIsFlight)
            {
                CleanupJoint();
                CreateJoint();
            }
        }

        private void BindKorolevCrossUI()
        {
            if (!supportsKorolevCross)
            {
                Fields[nameof(korolevCross)].guiActiveEditor = false;
            }
            else
            {
                Fields[nameof(korolevCross)].uiControlEditor.onFieldChanged =
                Fields[nameof(korolevCross)].uiControlEditor.onSymmetryFieldChanged = (a, b) =>
                {
                    ToggleKorolevCross();
                };
            }
        }

        private void OnVesselOnRails(Vessel data)
        {
            if (data != vessel) return;

            CleanupJoint();
        }

        private void OnVesselOffRails(Vessel data)
        {
            if (data != vessel) return;

            SetupKorolevCross();
        }

        private void CleanupJoint()
        {
            if (joint != null)
            {
                Destroy(joint);
                joint = null;
            }
        }

        private void CreateJoint()
        {
            if (part.parent == null || isFinished) return;

            joint = part.gameObject.AddComponent<HingeJoint>();
            joint.connectedBody = part.parent.Rigidbody;
            joint.anchor = tipOffsetToRoot;
            joint.axis = Vector3.right;
            joint.breakForce = float.PositiveInfinity;
            joint.breakTorque = float.PositiveInfinity;
        }

        public override void OnFixedUpdate()
        {
            if (!isFinished && decoupler.isDecoupled)
            {
                float mass = GetPartDryMassRecursive(part);

                if (fixedTimeSinceStart < force1Duration)
                {
                    float remainingTime = force1Duration - fixedTimeSinceStart;
                    float dtScale = remainingTime < Time.fixedDeltaTime ? remainingTime / Time.fixedDeltaTime : 1;
                    float forceMagnitude = dtScale * force1Scale * mass;
                    Vector3 forceDir = part.transform.rotation * Vector3.forward;
                    part.AddForce(forceMagnitude * forceDir);
                }

                if (fixedTimeSinceStart >= force2Start)
                {
                    if (joint != null)
                        Destroy(joint);

                    float remainingTime = force2Start + force2Duration - fixedTimeSinceStart;
                    float dtScale = remainingTime < Time.fixedDeltaTime ? remainingTime / Time.fixedDeltaTime : 1;
                    float forceMagnitude = dtScale * force2Scale * mass;
                    Vector3 forceDir = part.transform.rotation * Vector3.forward;
                    Vector3 pos = part.transform.position + part.transform.rotation * tipOffsetToRoot;
                    part.AddForceAtPosition(forceMagnitude * forceDir, pos);
                }

                fixedTimeSinceStart += Time.fixedDeltaTime;
                if (fixedTimeSinceStart >= force2Start + force2Duration)
                {
                    isFinished = true;
                }
            }
        }

        [Conditional("DEBUG")]
        public void Update()
        {
            if (!korolevCross || isFinished) return;

            Transform partTransform = part.gameObject.transform;
            DebugDrawer.DebugTransforms(partTransform);

            var forceDir = Vector3.forward;
            var forceVector = part.transform.rotation * forceDir;
            DebugDrawer.DebugLine(part.transform.position, part.transform.position + forceVector * 2, Color.yellow);

            forceDir = Vector3.forward;
            forceVector = part.transform.rotation * forceDir;

            var tankTipPos = part.transform.position + part.transform.rotation * tipOffsetToRoot;
            DebugDrawer.DebugLine(tankTipPos, tankTipPos + forceVector, Color.red);

            if (joint == null || part.parent == null) return;

            Transform parentTransform = part.parent.transform;
            DebugDrawer.DebugLine(joint.transform.position + joint.transform.rotation * joint.anchor, parentTransform.position, Color.magenta);
        }

        private static float GetPartDryMassRecursive(Part part)
        {
            float mass = part.mass;
            foreach (Part cPart in part.children)
            {
                mass += GetPartDryMassRecursive(cPart);
            }

            return mass;
        }
    }
}
