using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ROLib
{
    public class ModuleROPayload : ModuleDeployableSolarPanel
    {
        protected PartModule pmROTank;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            pmROTank = part.Modules.GetModule<ModuleROTank>();
            if (state == StartState.Editor && pmROTank is ModuleROTank moduleROTank)
            {
                if (moduleROTank?.Fields["currentCore"] is BaseField cCbf)
                {
                    cCbf.uiControlEditor.onFieldChanged += OnCoreChanged;
                }
                moduleROTank.enableVScale = false;
                moduleROTank.Fields[nameof(moduleROTank.currentVScale)].guiActiveEditor = false;
            }
        }

        internal void OnCoreChanged(BaseField bf, object obj)
        {
            UpdateAnimationAndTracking(bf);
            startFSM();
        }

        private void UpdateAnimationAndTracking(BaseField bf)
        {
            FindAnimations();
            panelRotationTransform = part.FindModelTransform(pivotName);
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
    }
}