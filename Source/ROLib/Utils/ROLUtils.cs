using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.IO;

namespace ROLib
{
    public class ROLUtils
    {
        /// <summary>
        /// Return the root transform for the specified name.  If does not exist, will create it and parent it to the parts' 'model' transform.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="recreate"></param>
        /// <returns></returns>
        public static Transform GetRootTransform(Part part, string name)
        {
            if (part.transform.ROLFindRecursive(name) is Transform t)
                return t;
            Transform root = new GameObject(name).transform;
            root.NestToParent(part.transform.ROLFindRecursive("model"));
            return root;
        }

        public static ConfigNode parseConfigNode(string input)
        {
            ConfigNode baseCfn = ConfigNode.Parse(input);
            if (baseCfn == null) { MonoBehaviour.print("ERROR: Base config node was null!!\n" + input); }
            else if (baseCfn.nodes.Count <= 0) { MonoBehaviour.print("ERROR: Base config node has no nodes!!\n" + input); }
            return baseCfn.nodes[0];
        }

        public static GameObject createJettisonedObject(GameObject toJettison, Vector3 velocity, Vector3 force, float mass)
        {
            GameObject jettisonedObject = new GameObject("JettisonedDebris");
            Transform parent = toJettison.transform.parent;
            if (parent != null)
            {
                jettisonedObject.transform.position = parent.position;
                jettisonedObject.transform.rotation = parent.rotation;
            }
            else
            {
                jettisonedObject.transform.position = toJettison.transform.position;
            }
            toJettison.transform.NestToParent(jettisonedObject.transform);
            physicalObject po = jettisonedObject.AddComponent<physicalObject>();
            Rigidbody rb = jettisonedObject.AddComponent<Rigidbody>();
            po.rb = rb;
            rb.velocity = velocity;
            rb.mass = mass;
            rb.AddForce(force);
            rb.useGravity = false;
            return jettisonedObject;
        }

        public static bool isTechUnlocked(string techName)
        {
            if (HighLogic.CurrentGame == null) { return true; }
            else if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX)
            {
                if (ResearchAndDevelopment.Instance == null) { MonoBehaviour.print("ERROR: R&D instance is null, no tech data available"); return true; }
                RDTech.State techState = ResearchAndDevelopment.GetTechnologyState(techName);
                return techState == RDTech.State.Available;
            }
            return false;
        }

        public static bool isResearchGame()
        {
            if (HighLogic.CurrentGame == null) { return false; }
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX) { return true; }
            return false;
        }

        public static string[] getNames<T>(IEnumerable<T> input, Func<T, string> alg)
        {
            return input.Select(alg).ToArray();
        }

        public static T findNext<T>(T[] array, System.Predicate<T> alg, bool iterateBackwards=false)
        {
            int index = Array.FindIndex(array, alg);
            int len = array.Length;
            if (index < 0 || index >= len)
            {
                return default;//invalid
            }
            int iter = iterateBackwards ? -1 : 1;
            index += iter;
            if (index < 0) { index = len - 1; }
            if (index >= len) { index = 0; }
            return array[index];
        }

        public static T findNext<T>(List<T> list, System.Predicate<T> alg, bool iterateBackwards=false)
        {
            int index = list.FindIndex(alg);
            int len = list.Count;
            if (index < 0 || index >= len)
            {
                return default;//invalid
            }
            int iter = iterateBackwards ? -1 : 1;
            index += iter;
            if (index < 0) { index = len - 1; }
            if (index >= len) { index = 0; }
            return list[index];
        }

        public static T findNextEligible<T>(List<T> list, System.Predicate<T> matchCurrent, System.Predicate<T> matchEligible, bool iterateBackwards)
        {
            int iter = iterateBackwards ? -1 : 1;
            int startIndex = list.FindIndex(matchCurrent);
            int length = list.Count;
            int index;
            for (int i = 1; i <= length; i++)//will always loop around to catch the start index...
            {
                index = startIndex + iter * i;
                while (index >= length) { index -= length; }
                while (index < 0) { index += length; }

                if (matchEligible.Invoke(list[index]))
                {
                    return list[index];
                }
            }
            return default;
        }

        public static T findNextEligible<T>(T[] list, System.Predicate<T> matchCurrent, System.Predicate<T> matchEligible, bool iterateBackwards)
        {
            int iter = iterateBackwards ? -1 : 1;
            int startIndex = Array.FindIndex(list, matchCurrent);
            int length = list.Length;
            int index;
            for (int i = 1; i <= length; i++)//will always loop around to catch the start index...
            {
                index = startIndex + iter * i;
                while (index >= length) { index -= length; }
                while (index < 0) { index += length; }

                if (matchEligible.Invoke(list[index]))
                {
                    return list[index];
                }
            }
            return default;
        }

        public static double safeParseDouble(string val)
        {
            double.TryParse(val, out double res);
            return res;
        }

        internal static bool safeParseBool(string v)
        {
            if (v == null) { return false; }
            else if (v.Equals("true") || v.Equals("yes") || v.Equals("1")) { return true; }
            return false;
        }

        public static float safeParseFloat(string val)
        {
            float.TryParse(val, out float res);
            return res;
        }

        public static int safeParseInt(string val)
        {
            int.TryParse(val, out int res);
            return res;
        }

        public static string[] parseCSV(string input, string split=",")
        {
            string[] vals = input.Split(new string[] { split }, StringSplitOptions.None);
            int len = vals.Length;
            for (int i = 0; i < len; i++)
            {
                vals[i] = vals[i].Trim();
            }
            return vals;
        }

        public static int[] parseIntArray(string input)
        {
            string[] vals = parseCSV(input, ",");
            int len = vals.Length;
            int[] iVals = new int[len];
            for (int i = 0; i < len; i++)
            {
                iVals[i] = safeParseInt(vals[i]);
            }
            return iVals;
        }

        public static float[] parseFloatArray(string input)
        {
            string[] strs = parseCSV(input, ",");
            int len = strs.Length;
            float[] flts = new float[len];
            for (int i = 0; i < len; i++)
            {
                flts[i] = safeParseFloat(strs[i]);
            }
            return flts;
        }

        public static Color parseColorFromBytes(string input)
        {
            Color color = new Color();
            float[] vals = parseFloatArray(input);
            color.r = vals[0] / 255f;
            color.g = vals[1] / 255f;
            color.b = vals[2] / 255f;
            color.a = vals[3] / 255f;
            return color;
        }

        public static string concatArray(float[] array)
        {
            string val = "";
            if (array != null)
            {
                foreach (float f in array) { val = $"{val}{f},"; }
            }
            return val;
        }

        public static string concatArray(string[] array)
        {
            string val = "";
            if (array != null)
            {
                foreach (string f in array) { val = $"{val}{f},"; }
            }
            return val;
        }

        public static string printList<T>(List<T> list, string separator)
        {
            string str = "";
            int len = list.Count;
            for (int i = 0; i < len; i++)
            {
                str += list[i].ToString();
                if (i < len - 1) { str += separator; }
            }
            return str;
        }

        public static string printArray<T>(T[] array, string separator)
        {
            string str = "";
            if (array != null)
            {
                int len = array.Length;
                for (int i = 0; i < len; i++)
                {
                    str += array[i].ToString();
                    if (i < len - 1) { str += separator; }
                }
            }
            return str;
        }

        public static string PrintFloatCurve(FloatCurve curve)
        {
            string str = "";
            if (curve != null)
            {
                int len = curve.Curve.length;
                Keyframe key;
                for (int i = 0; i < len; i++)
                {
                    if (i > 0) { str += "\n"; }
                    key = curve.Curve.keys[i];
                    str = $"{str}{key.time},{key.value},{key.inTangent},{key.outTangent}";
                }
            }
            return str;
        }

        public static void destroyChildren(Transform tr)
        {
            if (tr == null || tr.childCount<=0) { return; }

            foreach (Transform child in tr)
            {
                if (child == null) { continue; }
                child.parent = null;
                GameObject.Destroy(child.gameObject);
            }
        }

        public static void destroyChildrenImmediate(Transform tr)
        {
            if (tr == null || tr.childCount <= 0) { return; }
            int len = tr.childCount;
            for (int i = len-1; i >=0; i--)
            {
                Transform child = tr.GetChild(i);
                if (child == null)
                {
                    continue;
                }
                child.parent = null;
                GameObject.DestroyImmediate(child.gameObject);
            }
        }

        public static void recursePrintChildTransforms(Transform tr, string prefix)
        {
            MonoBehaviour.print("Transform found: " + prefix + tr.name);
            for (int i = 0; i < tr.childCount; i++)
            {
                recursePrintChildTransforms(tr.GetChild(i), $"{prefix}  ");
            }
        }

        public static void recursePrintComponents(GameObject go, string prefix)
        {
            int childCount = go.transform.childCount;
            Component[] comps = go.GetComponents<Component>();
            MonoBehaviour.print($"Found gameObject: {prefix}{go.name} enabled: {go.activeSelf} inHierarchy: {go.activeInHierarchy} layer: {go.layer} children: {childCount} components: {comps.Length} position: {go.transform.position} scale: {go.transform.localScale}");
            foreach (Component comp in comps)
            {
                if (comp is MeshRenderer r)
                {
                    Material m = r.material;
                    Shader s = m == null ? null : m.shader;
                    MonoBehaviour.print($"Found Mesh Rend : {prefix}* Mat: {m} :: Shader: {s}");
                }
                else
                {
                    MonoBehaviour.print($"Found Component : {prefix}* {comp}");
                }
            }
            Transform t = go.transform;
            foreach (Transform child in t)
            {
                recursePrintComponents(child.gameObject, $"{prefix}  ");
            }
        }

        public static void enableMeshColliderRecursive(Transform tr, bool enabled, bool convex)
        {
            MeshCollider mc = tr.GetComponent<MeshCollider>();
            if (mc != null)
            {
                mc.enabled = enabled;
                mc.convex = convex;
            }
            int len = tr.childCount;
            for (int i = 0; i < len; i++)
            {
                enableMeshColliderRecursive(tr.GetChild(i), enabled, convex);
            }
        }

        public static void addMeshCollidersRecursive(Transform tr, bool enabled, bool convex)
        {
            MeshCollider mc = tr.GetComponent<MeshCollider>();
            if (mc == null)
            {
                MeshFilter mf = tr.GetComponent<MeshFilter>();
                if (mf != null && mf.mesh != null)
                {
                    mc = tr.gameObject.AddComponent<MeshCollider>();
                }
            }
            if (mc != null)
            {
                mc.enabled = enabled;
                mc.convex = convex;
            }
            int len = tr.childCount;
            for (int i = 0; i < len; i++)
            {
                addMeshCollidersRecursive(tr.GetChild(i), enabled, convex);
            }
        }

        public static void enableRenderRecursive(Transform tr, bool val)
        {
            foreach (Renderer rend in tr.GetComponentsInChildren<Renderer>())
            {
                rend.enabled = val;
            }
        }

        public static void enableColliderRecursive(Transform tr, bool val)
        {
            foreach (Collider collider in tr.gameObject.GetComponentsInChildren<Collider>(false))
            {
                collider.enabled = val;
            }
        }

        public static Texture findTexture(string textureName, bool normal)
        {
            return GameDatabase.Instance.GetTexture(textureName, normal);
        }

        public static float distanceFromLine(Ray ray, Vector3 point)
        {
            return Vector3.Cross(ray.direction, point - ray.origin).magnitude;
        }

        public static Material loadMaterial(string diffuse, string normal)
        {
            return loadMaterial(diffuse, normal, string.Empty, "KSP/Bumped Specular");
        }

        public static Material loadMaterial(string diffuse, string normal, string shader)
        {
            return loadMaterial(diffuse, normal, string.Empty, shader);
        }

        public static Material loadMaterial(string diffuse, string normal, string emissive, string shader)
        {
            Material material;
            Texture diffuseTexture = findTexture(diffuse, false);
            Texture normalTexture = string.IsNullOrEmpty(normal) ? null : findTexture(normal, true);
            Texture emissiveTexture = string.IsNullOrEmpty(emissive) ? null : findTexture(emissive, false);
            material = new Material(Shader.Find(shader));
            material.SetTexture("_MainTex", diffuseTexture);
            if (normalTexture != null)
            {
                material.SetTexture("_BumpMap", normalTexture);
            }
            if (emissiveTexture != null)
            {
                material.SetTexture("_Emissive", emissiveTexture);
            }
            return material;
        }

        public static void setMainTextureRecursive(Transform tr, Texture tex)
        {
            setTextureRecursive(tr, tex, "_MainTex");
        }

        public static void setTextureRecursive(Transform tr, Texture tex, string texID)
        {
            int id = Shader.PropertyToID(texID);
            setTextureRecursive(tr, tex, id);
        }

        public static void setTextureRecursive(Transform tr, Texture tex, int id)
        {
            if (tr == null) { return; }
            Renderer renderer = tr.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                renderer.material.SetTexture(id, tex);
            }
            foreach (Transform tr1 in tr)
            {
                setTextureRecursive(tr1, tex, id);
            }
        }

        public static void setMaterialRecursive(Transform tr, Material mat)
        {
            Renderer renderer = tr.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = mat;
            }
            int len = tr.childCount;
            for (int i = 0; i < len; i++)
            {
                setMaterialRecursive(tr.GetChild(i), mat);
            }
        }

        public static MaterialPropertyBlock sharedBlock = new MaterialPropertyBlock();

        public static void setOpacityRecursive(Transform tr, float opacity)
        {
            Renderer renderer = tr.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                sharedBlock.Clear();
                renderer.GetPropertyBlock(sharedBlock);
                sharedBlock.SetFloat(PropertyIDs._Opacity, opacity);
                renderer.SetPropertyBlock(sharedBlock);
                renderer.sharedMaterial.renderQueue = opacity >= 1f ? -1 : 6000;
            }
            foreach (Transform child in tr) { setOpacityRecursive(child, opacity); }
        }

        public static Bounds getRendererBoundsRecursive(GameObject gameObject)
        {
            Renderer[] childRenders = gameObject.GetComponentsInChildren<Renderer>(false);
            Renderer parentRender = gameObject.GetComponent<Renderer>();

            Bounds combinedBounds = default;

            bool initializedBounds = false;

            if (parentRender != null && parentRender.enabled)
            {
                combinedBounds = parentRender.bounds;
                initializedBounds = true;
            }
            int len = childRenders.Length;
            for (int i = 0; i < len; i++)
            {
                if (initializedBounds)
                {
                    combinedBounds.Encapsulate(childRenders[i].bounds);
                }
                else
                {
                    combinedBounds = childRenders[i].bounds;
                    initializedBounds = true;
                }
            }
            return combinedBounds;
        }

        public static void findShieldedPartsCylinder(Part basePart, Bounds fairingRenderBounds, List<Part> shieldedParts, float topY, float bottomY, float topRadius, float bottomRadius)
        {
            float height = topY - bottomY;
            float largestRadius = topRadius > bottomRadius ? topRadius : bottomRadius;

            Vector3 lookupCenterLocal = new Vector3(0, bottomY + (height * 0.5f), 0);
            Vector3 lookupTopLocal = new Vector3(0, topY, 0);
            Vector3 lookupBottomLocal = new Vector3(0, bottomY, 0);
            Vector3 lookupCenterGlobal = basePart.transform.TransformPoint(lookupCenterLocal);

            Ray lookupRay = new Ray(lookupBottomLocal, new Vector3(0, 1, 0));

            List<Part> partsFound = new List<Part>();
            Collider[] foundColliders = Physics.OverlapSphere(lookupCenterGlobal, height * 1.5f, 1);
            foreach (Collider col in foundColliders)
            {
                Part pt = col.gameObject.GetComponentUpwards<Part>();
                if (pt != null && pt != basePart && pt.vessel == basePart.vessel && !partsFound.Contains(pt))
                {
                    partsFound.Add(pt);
                }
            }

            Bounds[] otherPartBounds;
            Vector3 otherPartCenterLocal;

            float partYPos;
            float partYPercent;
            float partYRadius;
            float radiusOffset = topRadius - bottomRadius;

            foreach (Part pt in partsFound)
            {
                //check basic render bounds for containment

                //TODO this check misses the case where the fairing is long/tall, containing a wide part; it will report that the wide part can fit inside
                //of the fairing, due to the relative size of their colliders
                otherPartBounds = pt.GetRendererBounds();
                if (PartGeometryUtil.MergeBounds(otherPartBounds, pt.transform).size.sqrMagnitude > fairingRenderBounds.size.sqrMagnitude)
                {
                    continue;
                }

                Vector3 otherPartCenter = pt.partTransform.TransformPoint(PartGeometryUtil.FindBoundsCentroid(otherPartBounds, pt.transform));
                if (!fairingRenderBounds.Contains(otherPartCenter))
                {
                    continue;
                }

                //check part bounds center point against conic projection of the fairing
                otherPartCenterLocal = basePart.transform.InverseTransformPoint(otherPartCenter);

                //check vs top and bottom of the shielded area
                if (otherPartCenterLocal.y > lookupTopLocal.y || otherPartCenterLocal.y < lookupBottomLocal.y)
                {
                    continue;
                }

                //quick check vs cylinder radius
                float distFromLine = distanceFromLine(lookupRay, otherPartCenterLocal);
                if (distFromLine > largestRadius)
                {
                    continue;
                }

                //more precise check vs radius of the cone at that Y position
                partYPos = otherPartCenterLocal.y - lookupBottomLocal.y;
                partYPercent = partYPos / height;
                partYRadius = partYPercent * radiusOffset;
                if (distFromLine > (partYRadius + bottomRadius))
                {
                    continue;
                }
                shieldedParts.Add(pt);
            }
        }

        public static void removeTransforms(Part part, string[] transformNames)
        {
            Transform[] trs;
            foreach (string name in transformNames)
            {
                trs = part.transform.ROLFindChildren(name.Trim());
                foreach (Transform tr in trs)
                {
                    GameObject.Destroy(tr.gameObject);
                }
            }
        }

        public static GameObject cloneModel(string modelURL)
        {
            GameObject clonedModel = null;
            GameObject prefabModel = GameDatabase.Instance.GetModelPrefab(modelURL);
            if (prefabModel != null)
            {
                clonedModel = (GameObject)GameObject.Instantiate(prefabModel);
                clonedModel.name = modelURL;
                clonedModel.transform.name = modelURL;
                clonedModel.SetActive(true);
            }
            else
            {
                MonoBehaviour.print($"ERROR: Could not clone model by name: {modelURL} no model exists for this URL.");
            }
            return clonedModel;
        }

        public static float calcTerminalVelocity(float kilograms, float rho, float cD, float area)
        {
           return Mathf.Sqrt((2f * kilograms * 9.81f) / (rho * area * cD));
        }

        public static float calcDragKN(float rho, float cD, float velocity, float area)
        {
            return calcDynamicPressure(rho, velocity) * area * cD * 0.001f;
        }

        public static float calcDynamicPressure(float rho, float velocity)
        {
            return 0.5f * rho * velocity * velocity;
        }

        public static double toRadians(double val)
        {
            return (Math.PI / 180d) * val;
        }

        public static double toDegrees(double val)
        {
            return val * (180d / Math.PI);
        }

        public static float roundTo(float value, float roundTo)
        {
            int wholeBits = (int)Math.Round((value / roundTo), 0);
            return (float)wholeBits * roundTo;
        }

        public static float SphereVolume(float r)
        {
            return 4f / 3f * (float)Math.PI * (r * r * r);
        }

        public static float CylinderVolume(float r, float h)
        {
            return (float)Math.PI * r * r * h;
        }

        public static float EllipsoidVolume(float a, float b, float c)
        {
            return 4f / 3f * (float)Math.PI * (a * b * c);
        }
    }

    public class StringLogicalComparer
    {
        public static int Compare(string s1, string s2)
        {
            //get rid of special cases
            if ((s1 == null) && (s2 == null)) return 0;
            else if (s1 == null) return -1;
            else if (s2 == null) return 1;

            if ((s1.Equals(string.Empty) && (s2.Equals(string.Empty)))) return 0;
            else if (s1.Equals(string.Empty)) return -1;
            else if (s2.Equals(string.Empty)) return -1;

            //WE style, special case
            bool sp1 = char.IsLetterOrDigit(s1, 0);
            bool sp2 = char.IsLetterOrDigit(s2, 0);
            if (sp1 && !sp2) return 1;
            if (!sp1 && sp2) return -1;

            int i1 = 0, i2 = 0; //current index
            int r; // temp result
            while (true)
            {
                bool c1 = char.IsDigit(s1, i1);
                bool c2 = char.IsDigit(s2, i2);
                if (!c1 && !c2)
                {
                    bool letter1 = char.IsLetter(s1, i1);
                    bool letter2 = char.IsLetter(s2, i2);
                    if ((letter1 && letter2) || (!letter1 && !letter2))
                    {
                        if (letter1 && letter2)
                        {
                            r = char.ToLower(s1[i1]).CompareTo(char.ToLower(s2[i2]));
                        }
                        else
                        {
                            r = s1[i1].CompareTo(s2[i2]);
                        }
                        if (r != 0) return r;
                    }
                    else if (!letter1 && letter2) return -1;
                    else if (letter1 && !letter2) return 1;
                }
                else if (c1 && c2)
                {
                    r = CompareNum(s1, ref i1, s2, ref i2);
                    if (r != 0) return r;
                }
                else if (c1)
                {
                    return -1;
                }
                else if (c2)
                {
                    return 1;
                }
                i1++;
                i2++;
                if ((i1 >= s1.Length) && (i2 >= s2.Length))
                {
                    return 0;
                }
                else if (i1 >= s1.Length)
                {
                    return -1;
                }
                else if (i2 >= s2.Length)
                {
                    return -1;
                }
            }
        }

        private static int CompareNum(string s1, ref int i1, string s2, ref int i2)
        {
            int nzStart1 = i1, nzStart2 = i2; // nz = non zero
            int end1 = i1, end2 = i2;

            ScanNumEnd(s1, i1, ref end1, ref nzStart1);
            ScanNumEnd(s2, i2, ref end2, ref nzStart2);
            int start1 = i1; i1 = end1 - 1;
            int start2 = i2; i2 = end2 - 1;

            int nzLength1 = end1 - nzStart1;
            int nzLength2 = end2 - nzStart2;

            if (nzLength1 < nzLength2) return -1;
            else if (nzLength1 > nzLength2) return 1;

            for (int j1 = nzStart1, j2 = nzStart2; j1 <= i1; j1++, j2++)
            {
                int r = s1[j1].CompareTo(s2[j2]);
                if (r != 0) return r;
            }
            // the nz parts are equal
            int length1 = end1 - start1;
            int length2 = end2 - start2;
            if (length1 == length2) return 0;
            if (length1 > length2) return -1;
            return 1;
        }

        // Lookahead
        private static void ScanNumEnd(string s, int start, ref int end, ref int nzStart)
        {
            nzStart = start;
            end = start;
            bool countZeros = true;
            while (char.IsDigit(s, end))
            {
                if (countZeros && s[end].Equals('0'))
                {
                    nzStart++;
                }
                else countZeros = false;
                end++;
                if (end >= s.Length) break;
            }
        }

    }

    public class NumericComparer : IComparer
    {
        public NumericComparer()
        { }

        public int Compare(object x, object y)
        {
            if ((x is string sX) && (y is string sY))
            {
                return StringLogicalComparer.Compare(sX, sY);
            }
            return -1;
        }
    }
}
