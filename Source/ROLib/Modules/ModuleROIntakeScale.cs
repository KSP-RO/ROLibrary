using System;
using System.Reflection;
using UnityEngine;

namespace ROLib
{
    /// <summary>
    /// Adds independent width / height / length scaling to a fixed-mesh intake part (e.g. the stock
    /// intakes), so a single modeled part covers a range of sizes. Scales the visual model and its
    /// colliders per axis, repositions attach nodes (and the parts on them), re-renders drag cubes (and FAR
    /// voxels), scales mass/cost, and drives the intake's areas from the frontal area (width x height): AJEInlet.Area
    /// for AJE, and the stock area FAR sizes the intake by.
    /// Applied to every intake via a ModuleManager patch; nothing here is intake-specific except the area hooks.
    ///
    /// Axis convention: the part's model-local LENGTH (airflow) axis is <see cref="lengthAxis"/> (x|y|z);
    /// the other two local axes are the width and height that set the frontal capture area. Meshes differ
    /// in orientation, so lengthAxis is per-part config.
    /// </summary>
    public class ModuleROIntakeScale : PartModule, IPartMassModifier, IPartCostModifier
    {
        private const string GroupName = "ModuleROIntakeScale";
        private const string GroupDisplayName = "RO-Intake Scale";

        #region KSPFields

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Width", guiFormat = "F2", groupName = GroupName, groupDisplayName = GroupDisplayName),
        UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0.25f, maxValue = 6f, incrementLarge = 0.5f, incrementSmall = 0.1f, incrementSlide = 0.01f, sigFigs = 2)]
        public float scaleWidth = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Height", guiFormat = "F2", groupName = GroupName),
        UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0.25f, maxValue = 6f, incrementLarge = 0.5f, incrementSmall = 0.1f, incrementSlide = 0.01f, sigFigs = 2)]
        public float scaleHeight = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Length", guiFormat = "F2", groupName = GroupName),
        UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0.25f, maxValue = 6f, incrementLarge = 0.5f, incrementSmall = 0.1f, incrementSlide = 0.01f, sigFigs = 2)]
        public float scaleLength = 1f;

        /// <summary>
        /// Slider limits, shared by all three axes.
        /// </summary>
        [KSPField] public float minScale = 0.25f;
        [KSPField] public float maxScale = 6f;

        /// <summary>
        /// Which model-local axis is length/airflow (x|y|z). The other two are width and height (frontal area).
        /// </summary>
        [KSPField] public string lengthAxis = "y";

        /// <summary>
        /// mass ~ (w*h*l)^(massExponent/3); default 2 => uniform scaling grows mass with surface area, as a hollow
        /// duct does (RO's own shock cones scale about d^1.9). 3 would be proportional to volume. Cost likewise, with costExponent.
        /// </summary>
        [KSPField] public float massExponent = 2f;
        [KSPField] public float costExponent = 2f;

        #endregion KSPFields

        #region Private Variables

        private Vector3[] baseNodePos = new Vector3[0];
        private Vector3[] baseNodeOrient = new Vector3[0];
        private Vector3 baseSrfNodePos;
        private bool hasSrfNode;
        private float baseAjeArea = -1f;
        private double baseIntakeArea = -1d;
        // the scale last applied (width, height, length): surface-attached children move by the change from it
        private Vector3 appliedScale = Vector3.one;
        private Transform scaleRoot;
        private ModuleResourceIntake intake;
        private FieldInfo ajeAreaField;
        private bool ajeResolved;

        // FAR's GeometryPartModule internals (RefreshFarIntake), looked up once
        private static FieldInfo farAdjustersField;
        private static MethodInfo farSetupAdjusters;
        private static bool farResolved;

        // width/height/length -> model-local axis indices (length from lengthAxis; the remaining two are w,h).
        private int lenIdx, wIdx, hIdx;

        #endregion Private Variables

        #region Standard KSP Overrides

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            ResolveAxes();
            CaptureBase();
            CreateScaleRoot();
            scaleWidth = Mathf.Clamp(scaleWidth, minScale, maxScale);
            scaleHeight = Mathf.Clamp(scaleHeight, minScale, maxScale);
            scaleLength = Mathf.Clamp(scaleLength, minScale, maxScale);

            if (HighLogic.LoadedSceneIsEditor)
            {
                foreach (string name in new[] { nameof(scaleWidth), nameof(scaleHeight), nameof(scaleLength) })
                {
                    this.ROLupdateUIFloatEditControl(name, minScale, maxScale, 0.5f, 0.1f, 0.01f);
                    Fields[name].uiControlEditor.onFieldChanged = OnScaleChanged;
                }
            }
            // a loaded part is already where its scaled nodes put it: nothing attached is moved
            ApplyScale(false);
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            if (!IsUnscaled) UpdateDragCubes();   // unscaled parts keep their stock drag cubes
        }

        // From the prefab, not values captured in OnStart: a craft's cost and mass are also asked of the prefab's modules
        // with only the craft's fields loaded (stock part costs, KCT/RP-1 build costs).
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) =>
            BaseMass * (Mathf.Pow(Mathf.Max(1e-3f, VolumeFactor), massExponent / 3f) - 1f);
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) =>
            BaseCost * (Mathf.Pow(Mathf.Max(1e-3f, VolumeFactor), costExponent / 3f) - 1f);
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        #endregion Standard KSP Overrides

        #region Custom Methods

        private float VolumeFactor => scaleWidth * scaleHeight * scaleLength;
        private bool IsUnscaled => scaleWidth == 1f && scaleHeight == 1f && scaleLength == 1f;
        private Part Prefab => part.partInfo != null ? part.partInfo.partPrefab : null;
        private float BaseMass => Prefab is Part prefab ? prefab.mass : part.mass;
        private float BaseCost => part.partInfo != null ? part.partInfo.cost : 0f;
        private ModuleResourceIntake Intake => intake != null ? intake : (intake = part.FindModuleImplementing<ModuleResourceIntake>());

        private void ResolveAxes()
        {
            lenIdx = AxisIndex(lengthAxis);
            // width = first non-length axis, height = second (arbitrary but consistent; area = w*h either way).
            wIdx = -1; hIdx = -1;
            for (int i = 0; i < 3; i++)
            {
                if (i == lenIdx) continue;
                if (wIdx < 0) wIdx = i; else hIdx = i;
            }
        }

        private static int AxisIndex(string a)
        {
            switch (a?.Trim().ToLower())
            {
                case "x": return 0;
                case "z": return 2;
                default: return 1;
            }
        }

        /// <summary>
        /// The unscaled geometry and areas, from the prefab: a persisted or already-scaled value on the instance would
        /// otherwise be taken as the base and compound on every load.
        /// </summary>
        private void CaptureBase()
        {
            Part prefab = Prefab;
            baseNodePos = new Vector3[part.attachNodes.Count];
            baseNodeOrient = new Vector3[part.attachNodes.Count];
            for (int i = 0; i < part.attachNodes.Count; i++)
            {
                AttachNode pn = prefab != null ? prefab.attachNodes.Find(n => n.id == part.attachNodes[i].id) : null;
                AttachNode n0 = pn ?? part.attachNodes[i];
                baseNodePos[i] = n0.originalPosition;
                baseNodeOrient[i] = n0.originalOrientation;
            }
            AttachNode srf = prefab != null && prefab.srfAttachNode != null ? prefab.srfAttachNode : part.srfAttachNode;
            hasSrfNode = srf != null && part.srfAttachNode != null;
            if (hasSrfNode) baseSrfNodePos = srf.originalPosition;

            ModuleResourceIntake pmri = prefab != null ? prefab.FindModuleImplementing<ModuleResourceIntake>() : Intake;
            if (pmri != null)
            {
                baseAjeArea = ReadAjeArea(pmri);
                // the stock area (AJEInlet's too, its subclass): what FAR sizes the intake's duct through the vessel's
                // cross-section and its ram drag by, and stock IntakeAir
                baseIntakeArea = pmri.area;
            }
        }

        private void OnScaleChanged(BaseField f, object obj)
        {
            this.ROLactionWithSymmetry(m =>
            {
                m.scaleWidth = scaleWidth;
                m.scaleHeight = scaleHeight;
                m.scaleLength = scaleLength;
                m.ApplyScale(true);
                m.UpdateDragCubes();
            });
            ROLStockInterop.FireEditorUpdate();
        }

        /// <summary>
        /// Model, nodes and areas to the current scale. On a user's change the parts on its nodes move with them (or this
        /// part, when the node holds its parent) and surface-attached children keep their place on the scaled mesh.
        /// </summary>
        private void ApplyScale(bool userInput)
        {
            ApplyModelScale();

            // Part.SetMirror sets position = originalPosition x mirrorVector (and orientation likewise), so original* stays
            // unmirrored and the live values carry the mirror
            Vector3 mirror = part.mirrorVector;
            for (int i = 0; i < part.attachNodes.Count && i < baseNodePos.Length; i++)
            {
                AttachNode node = part.attachNodes[i];
                Vector3 pos = Scaled(baseNodePos[i]);
                ROLAttachNodeUtils.UpdateAttachNodePosition(part, node, Vector3.Scale(pos, mirror), Vector3.Scale(baseNodeOrient[i], mirror), userInput, node.size);
                node.originalPosition = pos;
                node.originalOrientation = baseNodeOrient[i];
            }
            if (hasSrfNode && part.srfAttachNode is AttachNode srf)
            {
                ROLAttachNodeUtils.UpdateAttachNodePosition(part, srf, Scaled(baseSrfNodePos), srf.orientation, userInput, srf.size);
                if (scaleRoot != null && scaleRoot.parent.Find(SrfColliderName) is Transform collider)
                    collider.position = part.transform.TransformPoint(srf.position);
            }

            var scale = new Vector3(scaleWidth, scaleHeight, scaleLength);
            if (userInput)
                MoveSurfaceChildren(scale);
            appliedScale = scale;

            // Frontal areas scale with width x height: AJE's Area and the stock area (FAR's and stock IntakeAir's).
            if (Intake is ModuleResourceIntake mri)
            {
                if (baseAjeArea > 0f)
                    WriteAjeArea(mri, baseAjeArea * scaleWidth * scaleHeight);
                if (baseIntakeArea > 0d)
                {
                    double area = baseIntakeArea * scaleWidth * scaleHeight;
                    if (area != mri.area)
                    {
                        mri.area = area;
                        RefreshFarIntake();
                    }
                }
            }
        }

        /// <summary>
        /// The meshes and colliders go under a transform of their own below "model", which takes the scale. "model" is left
        /// to KSP: Part.SetMirror sets its scale to rescaleFactor x mirrorVector (on a mirror flip, and on the copy a drag
        /// cube is rendered from), and that applies on top of this one. A copied part already has it, with its children.
        /// Looked up directly under "model", not with ROLUtils.GetRootTransform: that searches the part's whole hierarchy,
        /// child parts included, and would find a scaled child intake's.
        /// </summary>
        private void CreateScaleRoot()
        {
            Transform model = part.transform.Find("model");
            if (model == null) return;
            scaleRoot = model.Find(GroupName);
            if (scaleRoot != null) return;
            scaleRoot = new GameObject(GroupName).transform;
            scaleRoot.NestToParent(model);
            // the stock surface attach collider sits at the surface node, which is moved separately
            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in model)
                if (child != scaleRoot && child.name != SrfColliderName)
                    children.Add(child);
            foreach (Transform child in children)
                child.SetParent(scaleRoot, false);
        }

        private const string SrfColliderName = "Surface Attach Collider";

        private void ApplyModelScale()
        {
            if (scaleRoot != null)
                scaleRoot.localScale = Scaled(Vector3.one);
        }

        /// <summary>Parts surface-attached to this one, moved by the change of scale along each axis from appliedScale.</summary>
        private void MoveSurfaceChildren(Vector3 scale)
        {
            if (scale == appliedScale) return;
            var ratio = Vector3.one;
            ratio[wIdx] = scale.x / appliedScale.x;
            ratio[hIdx] = scale.y / appliedScale.y;
            ratio[lenIdx] = scale.z / appliedScale.z;
            foreach (Part child in part.children)
            {
                if (child.srfAttachNode == null || child.srfAttachNode.attachedPart != part) continue;
                Vector3 local = part.transform.InverseTransformPoint(child.transform.position);
                local.Scale(ratio);
                child.transform.position = part.transform.TransformPoint(local);
                child.attPos0 = child.transform.localPosition;   // the offset tool's zero, and what is saved
            }
        }

        private Vector3 Scaled(Vector3 v)
        {
            v[wIdx] *= scaleWidth;
            v[hIdx] *= scaleHeight;
            v[lenIdx] *= scaleLength;
            return v;
        }

        private void UpdateDragCubes() => ROLModInterop.OnPartGeometryUpdate(part, ShapeKey);

        private string ShapeKey => $"ROIS|{part.partInfo?.name}|{scaleWidth:F3}|{scaleHeight:F3}|{scaleLength:F3}";

        #endregion Custom Methods

        #region AJE Area reflection (AJEInlet.Area)

        private FieldInfo AjeAreaField(ModuleResourceIntake mri)
        {
            if (!ajeResolved)
            {
                ajeResolved = true;
                ajeAreaField = mri.GetType().GetField("Area", BindingFlags.Public | BindingFlags.Instance);
                if (ajeAreaField != null && ajeAreaField.FieldType != typeof(float)) ajeAreaField = null;
            }
            return ajeAreaField;
        }

        private float ReadAjeArea(ModuleResourceIntake mri)
        {
            FieldInfo fi = AjeAreaField(mri);
            try { return fi != null ? (float)fi.GetValue(mri) : -1f; }
            catch { return -1f; }
        }

        private void WriteAjeArea(ModuleResourceIntake mri, float area)
        {
            FieldInfo fi = AjeAreaField(mri);
            if (fi == null) return;
            try { fi.SetValue(mri, area); }
            catch (Exception e) { ROLLog.error($"{part} set AJEInlet.Area failed: {e.Message}"); }
        }

        #endregion AJE Area reflection (AJEInlet.Area)

        #region FAR intake duct (GeometryPartModule)

        /// <summary>
        /// FAR reads an intake's area once, when its GeometryPartModule starts (the IntakeCrossSectionAdjuster that ducts the
        /// vessel's cross-section). One that has started before the area changed is set up again; one that has not will read
        /// the scaled area when it does. Its setup needs the editor's root part or the vessel, so it waits for those too.
        /// </summary>
        private void RefreshFarIntake()
        {
            if (HighLogic.LoadedSceneIsEditor ? EditorLogic.RootPart == null : part.vessel == null) return;
            if (!(part.Modules["GeometryPartModule"] is PartModule far)) return;   // no FAR
            if (!farResolved)
            {
                // FAR is not referenced at compile time: its type comes from the module itself
                farResolved = true;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                farAdjustersField = far.GetType().GetField("crossSectionAdjusters", flags);
                farSetupAdjusters = far.GetType().GetMethod("SetupICrossSectionAdjusters", flags, null, Type.EmptyTypes, null);
            }
            if (farAdjustersField == null || farSetupAdjusters == null || farAdjustersField.GetValue(far) == null) return;
            try { farSetupAdjusters.Invoke(far, null); }
            catch (Exception e) { ROLLog.error($"{part} FAR intake refresh failed: {(e.InnerException ?? e).Message}"); }
        }

        #endregion FAR intake duct (GeometryPartModule)
    }
}
