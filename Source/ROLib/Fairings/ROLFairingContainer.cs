﻿using System;
using System.Collections.Generic;
using UnityEngine;
using KSPShaderTools;

namespace ROLib
{
    /// <summary>
    /// Generic procedural fairing container
    /// </summary>
    public class ROLFairingContainer
    {
        public readonly int faces;

        private int panels;
        private float startAngle;
        private float endAngle;
        private float thickness;

        private float opacity;
        private float rotation;

        private List<ArcRing> profile = new List<ArcRing>();
        public readonly GameObject rootObject;
        private GameObject[] panelPivots;
        private Quaternion[] defaultPivotLocalRotations;
        public UVArea outsideUV;
        public UVArea insideUV;
        public UVArea edgesUV;
        public bool generateColliders = false;
        public int facesPerCollider = 1;

        public ROLFairingContainer(GameObject root, int cylinderFaces, int numberOfPanels, float thickness)
        {
            this.rootObject = root;
            this.faces = cylinderFaces;
            this.panels = numberOfPanels;
            this.thickness = thickness;
            SetNumberOfPanels(panels, false);
            panelPivots = new GameObject[0];
        }

        public void SetNumberOfPanels(int panels, bool recreate)
        {
            this.panels = panels;
            float anglePerPanel = 360f / (float)panels;
            float halfAngle = anglePerPanel * 0.5f;
            startAngle = halfAngle;
            endAngle = startAngle + 360f;
            if (recreate)
            {
                RecreateModels();
            }
        }

        public virtual void GenerateFairing()
        {
            ArcMeshGenerator gen = new ArcMeshGenerator(Vector3.zero, faces, panels, startAngle, endAngle, thickness, generateColliders, facesPerCollider);
            gen.outsideUV = outsideUV;
            gen.insideUV = insideUV;
            gen.edgesUV = edgesUV;
            foreach (ArcRing ring in profile)
            {
                gen.addArc(ring.height, ring.radius);
            }
            Vector3 pivot = new Vector3(0, 0, -profile[0].radius);
            panelPivots = gen.generatePanels(rootObject.transform, pivot);

            int len = panelPivots.Length;
            defaultPivotLocalRotations = new Quaternion[len];
            for (int i = 0; i < len; i++)
            {
                defaultPivotLocalRotations[i] = panelPivots[i].transform.localRotation;
            }
        }

        public void ReparentFairing(Transform newParent)
        {
            int len = panelPivots.Length;
            for (int i = 0; i < len; i++)
            {
                panelPivots[i].transform.parent = newParent;
            }
            panelPivots = new GameObject[0];
        }

        public void RecreateModels()
        {
            GenerateFairing();
            SetOpacity(opacity);
            SetPanelRotations(rotation);
        }

        public void SetOpacity(float value)
        {
            opacity = value;
            if (rootObject != null)
            {
                ROLUtils.setOpacityRecursive(rootObject.transform, value);
            }
        }

        public void JettisonPanels(Part part, float force, Vector3 jettisonDirection, float perPanelMass)
        {
            GameObject panelGO;
            Rigidbody rb;
            Vector3 globalForceDirection;
            for (int i = 0; i < panelPivots.Length; i++)
            {
                panelGO = panelPivots[i];
                panelGO.transform.parent = null;
                panelGO.AddComponent<physicalObject>();//auto-destroy when more than 1km away
                rb = panelGO.AddComponent<Rigidbody>();
                rb.velocity = part.rb.velocity;
                rb.mass = perPanelMass;
                globalForceDirection = panelGO.transform.TransformDirection(jettisonDirection);
                rb.AddForce(globalForceDirection * force);
                rb.useGravity = false;
            }
            panelPivots = new GameObject[0];//de-reference old panels for garbage collection
        }

        public void SetPanelRotations(float angle)
        {
            int len = panelPivots.Length;
            for (int i = 0; i < len; i++)
            {
                panelPivots[i].transform.localRotation = defaultPivotLocalRotations[i];
                panelPivots[i].transform.Rotate(new Vector3(1, 0, 0), angle, Space.Self);
            }
            rotation = angle;
        }

        public void ClearProfile()
        {
            profile.Clear();
        }

        public void AddRing(float height, float radius)
        {
            profile.Add(new ArcRing(height, radius));
        }

        public float GetHeight()
        {
            if (profile.Count <= 0) { return 0f; }
            float bottomY = profile[0].height;
            float topY = profile[profile.Count - 1].height;
            return topY - bottomY;
        }

        public float GetTopRadius()
        {
            if (profile.Count <= 0) { return 0f; }
            return profile[profile.Count - 1].radius;
        }

        public float GetBottomRadius()
        {
            if (profile.Count <= 0) { return 0f; }
            return profile[0].radius;
        }

        public void DestroyFairing()
        {
            if (panelPivots != null)
            {
                int len = panelPivots.Length;
                for (int i = 0; i < len; i++)
                {
                    if (panelPivots[i] != null)
                    {
                        panelPivots[i].transform.parent = null;
                        GameObject.Destroy(panelPivots[i]);
                        panelPivots[i] = null;
                    }
                }
            }
            panelPivots = new GameObject[0];
        }

        public void EnableColliders(bool val)
        {
            ROLUtils.enableColliderRecursive(rootObject.transform, val);
        }

        public void EnableTextureSet(string name, RecoloringData[] userColors)
        {
            TextureSet set = TexturesUnlimitedLoader.getTextureSet(name);
            if (set != null)
            {
                set.enable(rootObject.transform, userColors);
            }
            else
            {
                MonoBehaviour.print("ERROR: " + name + " is not a valid texture set for fairing.");
            }
        }
    }
}
