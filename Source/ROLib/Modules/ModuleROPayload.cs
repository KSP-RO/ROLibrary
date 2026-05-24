using System.Linq;
using UnityEngine;

namespace ROLib
{
    public class ModuleROPayload : ModuleDeployableSolarPanel
    {
        protected ModuleROTank pmROTank;

        public override void OnLoad(ConfigNode node)
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING)
                isBreakable = false;
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            pmROTank = part.Modules.GetModule<ModuleROTank>();
            if (state == StartState.Editor && pmROTank != null)
            {
                if (pmROTank?.Fields["currentCore"] is BaseField cCbf)
                {
                    cCbf.uiControlEditor.onFieldChanged += OnCoreChanged;
                }
                pmROTank.enableVScale = false;
                pmROTank.Fields[nameof(pmROTank.currentVScale)].guiActiveEditor = false;
            }

            var fld = Fields[nameof(sunAOA)];
            fld.guiActive = fld.guiActiveEditor = false;
            fld = Fields[nameof(flowRate)];
            fld.guiActive = fld.guiActiveEditor = false;
            fld = Fields[nameof(brokenStatusWarning)];
            fld.guiActive = fld.guiActiveEditor = isBreakable;

            if (pmROTank != null)
            {
                UpdateAnimationAndTracking();
            }

            base.OnStart(state);

            // OnStart will clobber the state to EXTENDED which can result in exception spam
            if (!useAnimation && deployState == DeployState.EXTENDED && panelRotationTransform == null)
                deployState = DeployState.RETRACTED;
        }

        internal void OnCoreChanged(BaseField bf, object obj)
        {
            UpdateAnimationAndTracking();
            startFSM();
        }

        private void UpdateAnimationAndTracking()
        {
            ROLModelDefinition modelDef = pmROTank.coreModule.definition;
            isTracking = modelDef.isTracking;
            animationName = modelDef.animationName;
            pivotName = modelDef.pivotName;
            secondaryTransformName = raycastTransformName = modelDef.secondaryTransformName;

            if (string.IsNullOrEmpty(pivotName))
            {
                alignType = PanelAlignType.X;   // Anything but Pivot
            }
            else
            {
                alignType = PanelAlignType.PIVOT;
            }

            FindAnimations();
            panelRotationTransform = string.IsNullOrEmpty(pivotName) ? null : part.FindModelTransform(pivotName);
            hasPivot = panelRotationTransform is Transform;
            originalRotation = currentRotation = panelRotationTransform?.localRotation ?? Quaternion.identity;
        }

        private void FindAnimations()
        {
            anim = null;
            if (!string.IsNullOrEmpty(animationName))
            {
                Animation[] animations = part.transform.ROLFindRecursive("model").GetComponentsInChildren<Animation>();
                anim = animations.FirstOrDefault(x => x.GetClip(animationName) is AnimationClip);
                anim ??= animations.FirstOrDefault();
            }
            useAnimation = anim != null;
        }

        public override string GetInfo()
        {
            return base.GetInfo();
        }
    }
}