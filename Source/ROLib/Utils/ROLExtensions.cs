using System;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections.Generic;

namespace ROLib
{
    public static class ROLExtensions
    {
        #region ConfigNode extension methods
        private readonly static List<string> cacheList = new List<string>();
        public static string[] ROLGetStringValues(this ConfigNode node, string name, bool reverse = false)
        {
            string[] values = node.GetValues(name);
            if (reverse)
            {
                cacheList.Clear();
                cacheList.AddRange(values);
                cacheList.Reverse();
                values = cacheList.ToArray();
            }
            return values;
        }

        public static string[] ROLGetStringValues(this ConfigNode node, string name, string[] defaults, bool reverse = false) => 
            node.HasValue(name) ? node.ROLGetStringValues(name, reverse) : defaults;

        public static string ROLGetStringValue(this ConfigNode node, string name, string defaultValue = "") => node.HasValue(name) ? node.GetValue(name) : defaultValue;

        public static bool[] ROLGetBoolValues(this ConfigNode node, string name)
        {
            string[] values = node.GetValues(name);
            int len = values.Length;
            bool[] vals = new bool[len];
            for (int i = 0; i < len; i++)
            {
                vals[i] = ROLUtils.safeParseBool(values[i]);
            }
            return vals;
        }

        public static bool ROLGetBoolValue(this ConfigNode node, string name, bool defaultValue = false)
        {
            return node.GetValue(name) is string value && bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        public static float[] ROLGetFloatValues(this ConfigNode node, string name, float[] defaults)
        {
            string baseVal = node.ROLGetStringValue(name);
            if (!string.IsNullOrEmpty(baseVal))
            {
                string[] split = baseVal.Split(new char[] { ',' });
                float[] vals = new float[split.Length];
                for (int i = 0; i < split.Length; i++) { vals[i] = ROLUtils.safeParseFloat(split[i]); }
                return vals;
            }
            return defaults;
        }

        public static float[] ROLGetFloatValues(this ConfigNode node, string name) => ROLGetFloatValues(node, name, new float[] { });
        public static float[] ROLGetFloatValuesCSV(this ConfigNode node, string name) => ROLGetFloatValuesCSV(node, name, new float[] { });

        public static float[] ROLGetFloatValuesCSV(this ConfigNode node, string name, float[] defaults)
        {
            float[] values = defaults;
            if (node.HasValue(name))
            {
                string strVal = node.ROLGetStringValue(name);
                string[] splits = strVal.Split(',');
                values = new float[splits.Length];
                for (int i = 0; i < splits.Length; i++)
                {
                    values[i] = float.Parse(splits[i]);
                }
            }
            return values;
        }

        public static float ROLGetFloatValue(this ConfigNode node, string name, float defaultValue = 0)
        {
            return node.GetValue(name) is string value && float.TryParse(value, out float result) ? result : defaultValue;
        }

        public static double ROLGetDoubleValue(this ConfigNode node, string name, double defaultValue = 0)
        {
            return node.GetValue(name) is string value && double.TryParse(value, out double result) ? result : defaultValue;
        }

        public static int ROLGetIntValue(this ConfigNode node, string name, int defaultValue = 0)
        {
            return node.GetValue(name) is string value && int.TryParse(value, out int result) ? result : defaultValue;
        }

        public static int[] ROLGetIntValues(this ConfigNode node, string name, int[] defaultValues = null)
        {
            int[] values = defaultValues;
            string[] stringValues = node.GetValues(name);
            if (stringValues == null || stringValues.Length == 0) { return values; }
            int len = stringValues.Length;
            values = new int[len];
            for (int i = 0; i < len; i++)
            {
                values[i] = ROLUtils.safeParseInt(stringValues[i]);
            }
            return values;
        }

        public static Vector3 ROLGetVector3(this ConfigNode node, string name, Vector3 defaultValue)
        {
            return node.GetValue(name) is string value && value.Split(',') is string[] vals && vals.Length >= 3
                ? new Vector3((float)ROLUtils.safeParseDouble(vals[0]), (float)ROLUtils.safeParseDouble(vals[1]), (float)ROLUtils.safeParseDouble(vals[2]))
                : defaultValue;
        }

        public static Vector3 ROLGetVector3(this ConfigNode node, string name) => ROLGetVector3(node, name, Vector3.zero);

        public static FloatCurve ROLGetFloatCurve(this ConfigNode node, string name, FloatCurve defaultValue = null)
        {
            FloatCurve curve = new FloatCurve();
            if (node.HasNode(name))
            {
                ConfigNode curveNode = node.GetNode(name);
                string[] values = curveNode.GetValues("key");
                int len = values.Length;
                string[] splitValue;
                float a, b, c, d;
                for (int i = 0; i < len; i++)
                {
                    splitValue = Regex.Replace(values[i], @"\s+", " ").Split(' ');
                    if (splitValue.Length > 2)
                    {
                        a = ROLUtils.safeParseFloat(splitValue[0]);
                        b = ROLUtils.safeParseFloat(splitValue[1]);
                        c = ROLUtils.safeParseFloat(splitValue[2]);
                        d = ROLUtils.safeParseFloat(splitValue[3]);
                        curve.Add(a, b, c, d);
                    }
                    else
                    {
                        a = ROLUtils.safeParseFloat(splitValue[0]);
                        b = ROLUtils.safeParseFloat(splitValue[1]);
                        curve.Add(a, b);
                    }
                }
            }
            else if (defaultValue != null)
            {
                foreach (Keyframe f in defaultValue.Curve.keys)
                {
                    curve.Add(f.time, f.value, f.inTangent, f.outTangent);
                }
            }
            else
            {
                curve.Add(0, 0);
                curve.Add(1, 1);
            }
            return curve;
        }

        public static ConfigNode getNode(this FloatCurve curve, string name)
        {
            ConfigNode node = new ConfigNode(name);
            foreach (Keyframe key in curve.Curve.keys)
            {
                node.AddValue("key", $"{key.time} {key.value} {key.inTangent} {key.outTangent}");
            }
            return node;
        }

        public static Color getColor(this ConfigNode node, string name)
        {
            Color color = new Color();
            float[] vals = node.ROLGetFloatValuesCSV(name);
            color.r = vals[0];
            color.g = vals[1];
            color.b = vals[2];
            color.a = vals[3];
            return color;
        }

        public static Color ROLgetColorFromByteValues(this ConfigNode node, string name)
        {
            Color color = new Color();
            float[] vals = node.ROLGetFloatValuesCSV(name);
            color.r = vals[0]/255f;
            color.g = vals[1]/255f;
            color.b = vals[2]/255f;
            color.a = vals[3]/255f;
            return color;
        }

        public static Axis getAxis(this ConfigNode node, string name, Axis def = Axis.ZPlus)
        {
            string val = node.ROLGetStringValue(name, def.ToString());
            Axis axis = (Axis)Enum.Parse(typeof(Axis), val, true);
            return axis;
        }

        #endregion

        #region Transform extensionMethods

        /// <summary>
        /// Same as transform.FindChildren() but also searches for children with the (Clone) tag on the name.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="modelName"></param>
        /// <returns></returns>
        public static Transform[] ROLFindModels(this Transform transform, string modelName)
        {
            Transform[] trs = transform.ROLFindChildren(modelName);
            Transform[] trs2 = transform.ROLFindChildren($"{modelName}(Clone)");
            Transform[] trs3 = new Transform[trs.Length + trs2.Length];
            trs3.AddUniqueRange(trs);
            trs3.AddUniqueRange(trs2);
            return trs3;
        }

        /// <summary>
        /// Same as transform.FindRecursive() but also searches for models with "(Clone)" added to the end of the transform name
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="modelName"></param>
        /// <returns></returns>
        public static Transform ROLFindModel(this Transform transform, string modelName)
        {
            if (transform.ROLFindRecursive(modelName) is Transform tr) return tr;
            return transform.ROLFindRecursive($"{modelName}(Clone)");
        }

        /// <summary>
        /// Same as transform.FindRecursive() but returns an array of all children with that name under the entire heirarchy of the model
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Transform[] ROLFindChildren(this Transform transform, string name)
        {
            List<Transform> trs = new List<Transform>();
            if (transform.name == name) trs.Add(transform);
            ROLlocateTransformsRecursive(transform, name, trs);
            return trs.ToArray();
        }

        private static void ROLlocateTransformsRecursive(Transform tr, string name, List<Transform> output)
        {
            foreach (Transform child in tr)
            {
                if (child.name == name) { output.Add(child); }
                ROLlocateTransformsRecursive(child, name, output);
            }
        }

        /// <summary>
        /// Searches entire model heirarchy from the input transform to end of branches for transforms with the input transform name and returns the first match found, or null if none.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Transform ROLFindRecursive(this Transform transform, string name)
        {
            if (transform.name == name) { return transform; }//was the original input transform
            if (transform.Find(name) is Transform tr) return tr;    //found as a direct child
            foreach(Transform child in transform)
            {
                if (child.ROLFindRecursive(name) is Transform t) return t;
            }
            return null;
        }

        /// <summary>
        /// Uses transform.FindRecursive to search for the given transform as a child of the input transform; if it does not exist, it creates a new transform and nests it to the input transform (0,0,0 local position and scale).
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Transform ROLFindOrCreate(this Transform transform, string name)
        {
            if (transform.ROLFindRecursive(name) is Transform t) return t;
            GameObject newGO = new GameObject(name);
            newGO.SetActive(true);
            newGO.name = newGO.transform.name = name;
            newGO.transform.NestToParent(transform);
            return newGO.transform;
        }

        /// <summary>
        /// Returns -ALL- children/grand-children/etc transforms of the input; everything in the heirarchy.
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static Transform[] ROLGetAllChildren(this Transform transform)
        {
            List<Transform> trs = new List<Transform>();
            ROLrecurseAddChildren(transform, trs);
            return trs.ToArray();
        }

        private static void ROLrecurseAddChildren(Transform transform, List<Transform> trs)
        {
            foreach (Transform child in transform)
            {
                trs.Add(child);
                ROLrecurseAddChildren(child, trs);
            }
        }

        /// <summary>
        /// Returns true if the input 'isParent' transform exists anywhere upwards of the input transform in the heirarchy.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="isParent"></param>
        /// <param name="checkUpwards"></param>
        /// <returns></returns>
        public static bool ROLisParent(this Transform transform, Transform isParent, bool checkUpwards = true)
        {
            if (isParent == null) { return false; }
            if (isParent == transform.parent) { return true; }
            if (checkUpwards)
            {
                Transform p = transform.parent;
                if (p == null) { return false; }
                else { p = p.parent; }
                while (p != null)
                {
                    if (p == isParent) { return true; }
                    p = p.parent;
                }
            }
            return false;
        }

        public static Vector3 ROLgetTransformAxis(this Transform transform, Axis axis)
        {
            return axis switch
            {
                Axis.XPlus => transform.right,
                Axis.XNeg => -transform.right,
                Axis.YPlus => transform.up,
                Axis.YNeg => -transform.up,
                Axis.ZPlus => transform.forward,
                Axis.ZNeg => -transform.forward,
                _ => transform.forward,
            };
        }

        public static Vector3 ROLgetLocalAxis(this Transform transform, Axis axis)
        {
            return axis switch
            {
                Axis.XPlus => Vector3.right,
                Axis.XNeg => Vector3.left,
                Axis.YPlus => Vector3.up,
                Axis.YNeg => Vector3.down,
                Axis.ZPlus => Vector3.forward,
                Axis.ZNeg => Vector3.back,
                _ => Vector3.forward,
            };
        }

        #endregion

        #region PartModule extensionMethods

        private static UI_Control GetWidget(PartModule module, string fieldName)
        {
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor) return null;
            BaseField bf = module.Fields[fieldName];
            return HighLogic.LoadedSceneIsEditor ? bf.uiControlEditor : bf.uiControlFlight;
        }

        public static void ROLupdateUIFloatEditControl(this PartModule module, string fieldName, float min, float max, float incLarge, float incSmall, float incSlide)
        {
            if (GetWidget(module, fieldName) is UI_FloatEdit widget)
            {
                widget.minValue = min;
                widget.maxValue = max;
                widget.incrementLarge = incLarge;
                widget.incrementSmall = incSmall;
                widget.incrementSlide = incSlide;
            }
        }

        /// <summary>
        /// FOR EDITOR USE ONLY - will not update or activate UI fields in flight scene
        /// </summary>
        /// <param name="module"></param>
        /// <param name="fieldName"></param>
        /// <param name="options"></param>
        /// <param name="display"></param>
        public static void ROLupdateUIChooseOptionControl(this PartModule module, string fieldName, string[] options, string[] display)
        {
            if (display.Length == 0 && options.Length > 0) { display = new string[] { "NONE" }; }
            if (options.Length == 0) { options = new string[] { "NONE" }; }
            module.Fields[fieldName].guiActiveEditor = options.Length > 1;
            if (HighLogic.LoadedSceneIsEditor && GetWidget(module, fieldName) is UI_ChooseOption widget)
            {
                widget.display = display;
                widget.options = options;
            }
        }

        public static void ROLupdateUIScaleEditControl(this PartModule module, string fieldName, float[] intervals, float[] increments)
        {
            if (GetWidget(module, fieldName) is UI_ScaleEdit widget)
            {
                widget.intervals = intervals;
                widget.incrementSlide = increments;
            }
        }

        public static void ROLupdateUIScaleEditControl(this PartModule module, string fieldName, float min, float max, float increment, bool flight, bool editor)
        {
            float seg = (max - min) / increment;
            int numOfIntervals = Mathf.RoundToInt(seg) + 1;
            BaseField field = module.Fields[fieldName];
            if (increment <= 0 || numOfIntervals <= 1)
            {
                field.guiActive = false;
                field.guiActiveEditor = false;
                return;
            }
            float sliderInterval = increment * 0.05f;
            field.guiActive = flight;
            field.guiActiveEditor = editor;
            float[] intervals = new float[numOfIntervals];
            float[] increments = new float[numOfIntervals];
            for (int i = 0; i < numOfIntervals; i++)
            {
                intervals[i] = min + (increment * i);
                increments[i] = sliderInterval;
            }
            module.ROLupdateUIScaleEditControl(fieldName, intervals, increments);
        }

        public static void ROLupdateUIFloatRangeControl(this PartModule module, string fieldName, float min, float max, float inc)
        {
            if (GetWidget(module, fieldName) is UI_FloatRange widget)
            {
                widget.minValue = min;
                widget.maxValue = max;
                widget.stepIncrement = inc;
            }
        }

        /// <summary>
        /// Performs the input delegate onto the input part module and any modules found in symmetry counerparts.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="module"></param>
        /// <param name="action"></param>
        public static void ROLactionWithSymmetry<T>(this T module, Action<T> action) where T : PartModule
        {
            action(module);
            ROLforEachSymmetryCounterpart(module, action);
        }

        /// <summary>
        /// Performs the input delegate onto any modules found in symmetry counerparts. (does not effect this.module)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="module"></param>
        /// <param name="action"></param>
        public static void ROLforEachSymmetryCounterpart<T>(this T module, Action<T> action) where T : PartModule
        {
            int index = module.part.Modules.IndexOf(module);
            foreach (Part p in module.part.symmetryCounterparts)
            {
                action((T)p.Modules[index]);
            }
        }

        #endregion

        #region Generic extension and Utiltiy methods

        /// <summary>
        /// Return true/false if the input array contains at least one element that satsifies the input predicate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static bool ROLExists<T>(this T[] array, Func<T,bool> predicate)
        {
            int len = array.Length;
            for (int i = 0; i < len; i++)
            {
                if (predicate(array[i])) { return true; }
            }
            return false;
        }

        public static T ROLFind<T>(this T[] array, Func<T, bool> predicate)
        {
            int len = array.Length;
            for (int i = 0; i < len; i++)
            {
                if (array[i] == null)
                {
                    ROLLog.error("ERROR: Null value in array in Find method, at index: " + i);
                }
                if (predicate(array[i]))
                {
                    return array[i];
                }
            }
            //return default in order to properly handle value types (structs)
            //should return either null for reference types or default value for structs
            return default;
        }


        #endregion

        #region FloatCurve extensions

        public static string ROLPrint(this FloatCurve curve)
        {
            string output = "";
            foreach (Keyframe f in curve.Curve.keys)
            {
                output += $"\n{f.time} {f.value} {f.inTangent} {f.outTangent}";
            }
            return output;
        }

        public static string ROLToStringSingleLine(this FloatCurve curve)
        {
            string data = "";
            for (int i = 0; i < curve.Curve.length; i++)
            {
                Keyframe key = curve.Curve.keys[i];
                if (i > 0) data += ":";
                data += $"{key.time},{key.value},{key.inTangent},{key.outTangent}";
            }
            return data;
        }

        public static void ROLloadSingleLine(this FloatCurve curve, string input)
        {
            foreach (string keySplit in input.Split(':'))
            {
                string[] valSplits = keySplit.Split(',');
                float key = float.Parse(valSplits[0]);
                float value = float.Parse(valSplits[1]);
                float inTan = float.Parse(valSplits[2]);
                float outTan = float.Parse(valSplits[3]);
                curve.Add(key, value, inTan, outTan);
            }
        }

        #endregion

    }
}

