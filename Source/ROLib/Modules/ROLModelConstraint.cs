using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROLib
{
    public class ROLModelConstraint : PartModule
    {
        public List<ROLConstraint> constraints = new List<ROLConstraint>();

        [KSPField]
        public int numOfPasses = 1;

        [Persistent]
        public string configNodeData = string.Empty;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public void reInitialize()
        {
            initialize();
        }

        public void loadExternalData(ConfigNode node)
        {
            configNodeData = node.ToString();
            initialize();
        }

        public void Update()
        {
            updateConstraints();
        }

        private void initialize()
        {
            constraints.Clear();
            ConfigNode node = ROLConfigNodeUtils.ParseConfigNode(configNodeData);

            ConfigNode[] lookConstraintNodes = node.GetNodes("LOOK_CONST");
            foreach (ConfigNode lcn in lookConstraintNodes)
            {
                loadLookConstraint(lcn);
            }

            ConfigNode[] lockedConstraintNodes = node.GetNodes("LOCKED_CONST");
            foreach (ConfigNode lcn in lockedConstraintNodes)
            {
                loadLockedConstraint(lcn);
            }
            updateConstraints();
        }

        private void updateConstraints()
        {
            for (int i = 0; i < numOfPasses; i++)
            {
                int len = constraints.Count;
                for (int k = 0; k < len; k++)
                {
                    constraints[k].updateConstraint(i);
                }
            }
        }

        private void loadLookConstraint(ConfigNode node)
        {
            string transformName = node.ROLGetStringValue("transformName");
            string targetName = node.ROLGetStringValue("targetName");
            bool singleTarget = node.ROLGetBoolValue("singleTarget", false);
            Transform[] movers = part.FindModelTransforms(transformName);
            Transform[] targets = part.FindModelTransforms(targetName);
            int len = movers.Length;
            ROLLookConstraint lookConst;
            for (int i = 0; i < len; i++)
            {
                lookConst = new ROLLookConstraint(node, movers[i], singleTarget ? targets[0] : targets[i], part);
                constraints.Add(lookConst);
            }
        }

        private void loadLockedConstraint(ConfigNode node)
        {
            string transformName = node.ROLGetStringValue("transformName");
            string targetName = node.ROLGetStringValue("targetName");
            bool singleTarget = node.ROLGetBoolValue("singleTarget", false);
            Transform[] movers = part.FindModelTransforms(transformName);
            Transform[] targets = part.FindModelTransforms(targetName);
            int len = movers.Length;
            ROLLockedConstraint lookConst;
            for (int i = 0; i < len; i++)
            {
                lookConst = new ROLLockedConstraint(node, movers[i], singleTarget ? targets[0] : targets[i], part);
                constraints.Add(lookConst);
            }
        }
    }

    public class ROLConstraint
    {
        public Transform mover;
        public Transform target;
        public int pass = 0;

        public ROLConstraint(ConfigNode node, Transform mover, Transform target, Part part)
        {
            this.mover = mover;
            this.target = target;
            pass = node.ROLGetIntValue("pass", pass);
        }

        public void updateConstraint(int pass)
        {
            if (pass == this.pass) { updateConstraintInternal(); }
        }

        protected virtual void updateConstraintInternal()
        {

        }
    }

    public class ROLLookConstraint : ROLConstraint
    {
        public ROLLookConstraint(ConfigNode node, Transform mover, Transform target, Part part) : base(node, mover, target, part)
        {

        }

        protected override void updateConstraintInternal()
        {
            mover.LookAt(target, mover.up);
        }
    }

    public class ROLLockedConstraint : ROLConstraint
    {
        public Vector3 lookAxis = Vector3.forward;//z+ in unity
        public Vector3 lockedAxis = Vector3.up;//y+ in unity
        private Quaternion defaultLocalRotation;
        public ROLLockedConstraint(ConfigNode node, Transform mover, Transform target, Part part) : base(node, mover, target, part)
        {
            defaultLocalRotation = mover.localRotation;
            lookAxis = node.ROLGetVector3("lookAxis", lookAxis);
            lockedAxis = node.ROLGetVector3("lockedAxis", lockedAxis);
        }

        protected override void updateConstraintInternal()
        {
            mover.localRotation = defaultLocalRotation;
            Vector3 localTargetPos = mover.InverseTransformPoint(target.position);//and in local space, to easier use tracked stuff
            //Vector3 targetPos = target.position - mover.position;//global target pos
            //MonoBehaviour.print("gtp: " + targetPos + " ltp: " + localTargetPos);
                        
            float xRot = 90f + Mathf.Atan2(localTargetPos.z, localTargetPos.x) * Mathf.Rad2Deg;
            float yRot = 90f + Mathf.Atan2(localTargetPos.z, localTargetPos.y) * Mathf.Rad2Deg;
            //float zRot = Mathf.Atan2(localTargetPos.y, localTargetPos.x) * Mathf.Rad2Deg;
            //MonoBehaviour.print("xr: " + xRot + " : yr: " + yRot + " : zr: " + zRot);

            if (lookAxis.z == -1)
            {
                if (lockedAxis.x != 0)
                {
                    //locked on x, rotate around Y
                    mover.Rotate(lockedAxis, yRot);
                }
                else if (lockedAxis.y != 0)
                {
                    mover.Rotate(lockedAxis, -xRot);
                }
            }
            else if (lookAxis.z == 1)
            {
                xRot -= 180;
                yRot -= 180;
                if (lockedAxis.x != 0)
                {
                    //MonoBehaviour.print("rotating for z==1, x!=0 : "+xRot+" : "+yRot);
                    //locked on x, rotate around Y
                    mover.Rotate(lockedAxis, yRot);
                }
                else if (lockedAxis.y != 0)
                {
                    //MonoBehaviour.print("rotating for z==1, y!=0 : " + xRot + " : " + yRot);
                    mover.Rotate(lockedAxis, -xRot);
                }
            }
            else if (lookAxis.y != 0)
            {
                //TODO
            }
            else if (lookAxis.x != 0)
            {
                //TODO
            }
        }
    }

}

