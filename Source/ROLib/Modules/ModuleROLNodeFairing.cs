using System;
using UnityEngine;
using System.Text;
using KSPShaderTools;

namespace ROLib
{
    /// <summary>
    /// Procedrually created (and adjustable/configurable) replacement for engine fairings, or any other part-attached fairing.
    /// </summary>           
    public class ModuleROLNodeFairing : PartModule, IRecolorable
    {
        private const string GroupDisplayName = "Fairings";
        private const string GroupName = "ROLNodeFairing";

        #region REGION - Standard KSP Config Fields
        /// <summary>
        /// CSV List of transforms to remove from the model, to be used to override stock engine fairing configuration
        /// </summary>
        [KSPField]
        public String rendersToRemove = String.Empty;

        /// <summary>
        /// Name used for GUI actions for this fairing
        /// </summary>
        [KSPField]
        public String fairingName = "Fairing";
        
        /// <summary>
        /// If can manually jettison, this will be the action name in the GUI (combined with fairing name above)
        /// </summary>
        [KSPField]
        public String actionName = "Jettison";

        /// <summary>
        /// The node that this fairing will watch if fairing type == node
        /// </summary>
        [KSPField]
        public String nodeName = String.Empty;

        [KSPField]
        public bool snapToNode = true;

        [KSPField]
        public bool snapToSecondNode = false;

        [KSPField]
        public bool updateDragCubes = true;

        [KSPField]
        public bool canDisableInEditor = true;

        /// <summary>
        /// Can user jettison fairing manually when in flight? - should mostly be used for non-node attached fairings
        /// </summary>
        [KSPField]
        public bool canManuallyJettison = false;

        /// <summary>
        /// If the fairing will automatically jettison/reparent when its attached node is decoupled
        /// </summary>
        [KSPField]
        public bool canAutoJettison = true;

        //determines if user can adjust bottom diameter (also needs per-fairing canAdjustBottom flag)
        [KSPField]
        public bool canAdjustTop = true;

        //determines if user can adjust top diameter (also needs per-fairing canAdjustTop flag)
        [KSPField]
        public bool canAdjustBottom = true;

        /// <summary>
        /// Can user adjust how many fairing sections this fairing consists of?
        /// </summary>
        [KSPField]
        public bool canAdjustSections = true;

        /// <summary>
        /// Increment to be used when adjusting top radius
        /// </summary>
        [KSPField]
        public float topDiameterIncrement = 0.1f;

        /// <summary>
        /// Increment to be used when adjusting bottom radius
        /// </summary>
        [KSPField]
        public float bottomDiameterIncrement = 0.1f;

        /// <summary>
        /// Maximum top radius (by whole increment; adjust slider will allow this + one radius increment)
        /// </summary>
        [KSPField]
        public float maxTopDiameter = 50f;

        /// <summary>
        /// Minimum top radius
        /// </summary>
        [KSPField]
        public float minTopDiameter = 0.1f;

        /// <summary>
        /// Maximum bottom radius (by whole increment; adjust slider will allow this + one radius increment)
        /// </summary>
        [KSPField]
        public float maxBottomDiameter = 50f;

        /// <summary>
        /// Minimum bottom radius
        /// </summary>
        [KSPField]
        public float minBottomDiameter = 0.1f;
        
        [KSPField] public string noseFairingNode = "nosefairing";
        [KSPField] public string mountFairingNode = "mountfairing";

        #endregion

        #region REGION - GUI Visible Config Fields

        [KSPField(isPersistant =true, guiActiveEditor = true, guiName ="Opacity", groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_Toggle(disabledText ="Opaque", enabledText = "Transparent", suppressEditorShipModified = true)]
        public bool editorTransparency = true;

        /// <summary>
        /// Number of sections for the fairing, only enabled for editing if 'canAdjustSections' == true
        /// </summary>
        [KSPField(guiActiveEditor = true, guiName = "Sections", isPersistant = true, groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_FloatRange(minValue = 1f, stepIncrement = 1f, maxValue = 6f, suppressEditorShipModified = true)]
        public float numOfSections = 1;

        [KSPField(guiName = "Top Diam", guiActiveEditor = true, isPersistant = true, groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float guiTopDiameter = -1f;

        [KSPField(guiName = "Bot. Diam", guiActiveEditor = true, isPersistant = true, groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float guiBottomDiameter = -1f;

        [KSPField(guiName = "Texture Set", guiActiveEditor = true, isPersistant = true, groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_ChooseOption(suppressEditorShipModified =true)]
        public String currentTextureSet = String.Empty;

        [KSPField(isPersistant = true, guiName ="Colliders", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled", suppressEditorShipModified = true)]
        public bool generateColliders = false;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Fairing Len.", guiUnits = "m", groupName = GroupName, groupDisplayName = GroupDisplayName),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentFairingLength = 1.0f;

        #endregion

        #region REGION - Persistent config fields

        /// <summary>
        /// Has the fairing been jettisoned?  If true, no further interaction is possible.  Only set to true by in-flight jettison actions
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingJettisoned = false;

        /// <summary>
        /// Has the fairing mesh been created?  This should be kept up-to-date along with if the mesh exists.
        /// The only desync should be on initial load, where this value determines if the fairing should be created at the 'initialization' stage
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingCreated = false;

        /// <summary>
        /// If fairing has been currently enabled/disabled by user
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingEnabled = true;

        /// <summary>
        /// If fairing has been 'force' enabled/disabled by external plugin (MEC).  This completely removes GUI interaction and forces permanent disabled status unless re-enabled by external plugin
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingForceDisabled = false;

        [KSPField(isPersistant = true)]
        public string customColorData = string.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedColors = false;

        //this one is quite hacky; storing ConfigNode data in the string, because the -fields- load fine on revert-to-vab (and everywhere), but the config-node data is not present in all situations
        /// <summary>
        /// Persistent data from fairing parts; stores their current top/bottom positions and radius data
        /// </summary>
        [KSPField(isPersistant = true)]
        public String persistentDataString = String.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

        #endregion

        #region REGION - private working vars, not user editable

        private RecoloringHandler recolorHandler;
                
        //the current fairing panels
        private ROLNodeFairingData[] fairingParts;

        /// <summary>
        /// If not null, will be applied during initialization or late update tick
        /// </summary>
        private ROLFairingUpdateData externalUpdateData = null;

        //private vars set from examining the individual fairing sections; these basically control gui enabled/disabled status
        private bool enableBottomDiameterControls;
        private bool enableTopDiameterControls;

        /// <summary>
        /// Used as part of attach-node detection.  Set to a part when a part is attached to the 'watchedNode'. 
        /// Used to monitor when the part is detached in flight, to trigger jettison/reparenting of the fairing panels.
        /// </summary>
        private Part prevAttachedPart = null;

        /// <summary>
        /// Flipped to true when the fairing should check/update attach node status, due to editor/vessel modified events and/or startup/init.
        /// </summary>
        private bool needsStatusUpdate = false;

        /// <summary>
        /// Flipped to true during internal updates -- determines if the fairing should be rebuilt on the next LateUpdate()
        /// </summary>
        private bool needsRebuilt = false;

        /// <summary>
        /// Flipped to true during internal updates -- determines if the gui data should be updated on the next LateUpdate()
        /// </summary>
        private bool needsGuiUpdate = false;
                
        #endregion

        #region REGION - Gui Interaction Methods

        [KSPAction("Jettison Fairing")]
        public void jettisonAction(KSPActionParam param)
        {
            JettisonFairing();
            UpdateGuiState();
        }
        
        [KSPEvent(guiName = "Jettison Fairing", guiActive = true, guiActiveEditor = false, groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public void jettisonEvent()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                this.actionWithSymmetry(m => 
                {
                    m.JettisonFairing();
                    m.UpdateGuiState();
                });
            }
        }

        [KSPEvent(guiName = "Toggle Fairing", guiActive = false, guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public void toggleFairing()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                fairingEnabled = !fairingEnabled;
                this.actionWithSymmetry(m => 
                {
                    m.fairingEnabled = fairingEnabled;
                    if (m.fairingEnabled)
                    {
                        m.needsStatusUpdate = true;
                    }
                    else
                    {
                        m.DestroyFairing();
                    }
                });
            }
        }

        #endregion

        #region REGION - ksp overrides

        //on load, not called properly on 'revertToVAB'
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            if (node.HasValue("customColor1"))
            {
                Color c1 = node.GetColorFromFloatCSV("customColor1");
                Color c2 = node.GetColorFromFloatCSV("customColor2");
                Color c3 = node.GetColorFromFloatCSV("customColor3");
                string colorData = c1.r + "," + c1.g + "," + c1.b + "," + c1.a + ",0;";
                colorData = colorData + c2.r + "," + c2.g + "," + c2.b + "," + c2.a + ",0;";
                colorData = colorData + c3.r + "," + c3.g + "," + c3.b + "," + c3.a + ",0";
                customColorData = colorData;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);            
            UpdatePersistentDataString();
            node.SetValue(nameof(persistentDataString), persistentDataString, true);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Initialize();

            Fields[nameof(guiTopDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m =>
                {
                    m.guiTopDiameter = guiTopDiameter;
                    float radius = m.guiTopDiameter * 0.5f;
                    int len = m.fairingParts.Length;
                    for (int i = 0; i < len; i++)
                    {
                        if (m.fairingParts[i].canAdjustTop && m.fairingParts[i].topRadius != radius)
                        {
                            m.fairingParts[i].topRadius = radius;
                            m.needsRebuilt = m.fairingCreated;
                        }
                    }
                });
            };

            Fields[nameof(guiBottomDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.guiBottomDiameter = guiBottomDiameter;
                    float radius = m.guiBottomDiameter * 0.5f;
                    int len = m.fairingParts.Length;
                    for (int i = 0; i < len; i++)
                    {
                        if (m.fairingParts[i].canAdjustBottom && m.fairingParts[i].bottomRadius != radius)
                        {
                            m.fairingParts[i].bottomRadius = radius;
                            m.needsRebuilt = m.fairingCreated;
                        }
                    }
                });
            };

            Fields[nameof(currentFairingLength)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentFairingLength = currentFairingLength;
                    foreach (ROLNodeFairingData fdata in m.fairingParts)
                    {
                        if (m.fairingName == "Top Fairing")
                        {
                            fdata.topY = currentFairingLength + fdata.bottomY;
                        }
                        if (m.fairingName == "Bottom Fairing")
                        {
                            fdata.bottomY = fdata.topY - currentFairingLength;
                        }
                    }
                    m.needsRebuilt = m.fairingCreated;
                });
            };

            Fields[nameof(numOfSections)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.numOfSections = numOfSections;
                    m.needsRebuilt = m.fairingCreated;
                });
            };

            Fields[nameof(editorTransparency)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.editorTransparency = editorTransparency;
                    m.UpdateOpacity();
                });
            };

            Fields[nameof(generateColliders)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m=> 
                {
                    m.generateColliders = generateColliders;
                    m.UpdateColliders();
                });
            };

            Fields[nameof(currentTextureSet)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.currentTextureSet = currentTextureSet;
                    m.UpdateTextureSet(!ROLGameSettings.persistRecolor());
                });
            };

            this.updateUIFloatEditControl(nameof(guiTopDiameter), minTopDiameter, maxTopDiameter, topDiameterIncrement * 2, topDiameterIncrement, topDiameterIncrement * 0.05f, true, guiTopDiameter);
            this.updateUIFloatEditControl(nameof(guiBottomDiameter), minBottomDiameter, maxBottomDiameter, bottomDiameterIncrement * 2, bottomDiameterIncrement, bottomDiameterIncrement * 0.05f, true, guiBottomDiameter);
            this.updateUIFloatEditControl(nameof(currentFairingLength), 0.1f, 50f, topDiameterIncrement * 2, topDiameterIncrement, topDiameterIncrement * 0.05f, true, currentFairingLength);

            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(OnEditorVesselModified));
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(OnVesselModified));
        }
        
        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(OnEditorVesselModified));
            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(OnVesselModified));
        }

        public void Start()
        {
            if (fairingParts != null)
            {
                UpdateOpacity();
            }
        }

        public void OnVesselModified(Vessel v)
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            //MonoBehaviour.print("Flight Vessel Modified, checking status");
            //updateFairingStatus();
            needsStatusUpdate = true;
        }

        public void OnEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            //MonoBehaviour.print("Vessel modified, checking status");
            needsStatusUpdate = true;
        }

        private void Initialize()
        {
            //MonoBehaviour.print("NodeFairingInit: "+fairingCreated+ " :: " +fairingForceDisabled+ " :: "+fairingJettisoned + " :: " +fairingEnabled);
            if (rendersToRemove != null && rendersToRemove.Length > 0)
            {
                ROLUtils.removeTransforms(part, ROLUtils.parseCSV(rendersToRemove));
            }
            LoadFairingData(ROLConfigNodeUtils.parseConfigNode(configNodeData));
            if (externalUpdateData != null)
            {
                UpdateFromExternalData(externalUpdateData);
            }
            if (fairingCreated || (fairingEnabled && !fairingJettisoned && !fairingForceDisabled && string.IsNullOrEmpty(nodeName)))//previously existed, recreate it, or should exist by default values in the config
            {
                BuildFairing();
                UpdateFairingNodes();
                if (!string.IsNullOrEmpty(nodeName))
                {
                    AttachNode n = part.FindAttachNode(nodeName);
                    if (n != null && n.attachedPart != null)
                    {
                        prevAttachedPart = n.attachedPart;
                        //MonoBehaviour.print("Setting initial attached part to: " + prevAttachedPart);
                    }
                }
            }
            else if(!fairingJettisoned && !fairingForceDisabled && !string.IsNullOrEmpty(nodeName))//else could potentially be activated by a node...check for activation
            {
                needsStatusUpdate = true;
            }
            UpdateTextureSet(false);
            needsGuiUpdate = true;
            //MonoBehaviour.print("NodeFairingInit End: " + fairingCreated + " :: " + fairingForceDisabled + " :: " + fairingJettisoned + " :: " + fairingEnabled);
        }

        private void UpdatePersistentDataString()
        {            
            if (fairingParts == null) { return; }
            StringBuilder sb = new StringBuilder();
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                if (i > 0)
                {
                    sb.Append(":");
                }
                sb.Append(fairingParts[i].GetPersistence());
            }
            persistentDataString = sb.ToString();
        }

        public void LateUpdate()
        {
            //MonoBehaviour.print("Checking late update1: " + needsStatusUpdate + " :: " + needsRebuilt);
            if (externalUpdateData != null)
            {
                UpdateFromExternalData(externalUpdateData);
            }
            //MonoBehaviour.print("Checking late update2: " + needsStatusUpdate + " :: " + needsRebuilt);
            if (needsStatusUpdate)
            {
                UpdateFairingStatus();
            }
            //MonoBehaviour.print("Checking late update3: " + needsStatusUpdate + " :: " + needsRebuilt);
            if (needsRebuilt)
            {
                RebuildFairing();
                UpdatePersistentDataString();
                ROLStockInterop.fireEditorUpdate();
                needsGuiUpdate = true;
                needsRebuilt = false;
            }
            if (needsGuiUpdate)
            {
                UpdateGuiState();
                needsGuiUpdate = false;
            }
            if (HighLogic.LoadedSceneIsEditor && fairingParts != null)
            {
                UpdateOpacity();
            }
        }

        public string[] getSectionNames()
        {
            return new string[] { fairingName };
        }

        public RecoloringData[] getSectionColors(string name)
        {
            return recolorHandler.getColorData();
        }

        public void setSectionColors(string name, RecoloringData[] colors)
        {
            recolorHandler.setColorData(colors);
            UpdateTextureSet(false);
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            return TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
        }

        #endregion

        #region REGION - external interaction methods

        public void UpdateExternal(ROLFairingUpdateData data)
        {
            externalUpdateData = data;
        }

        private void UpdateFromExternalData(ROLFairingUpdateData eData)
        {
            //MonoBehaviour.print("Updating fairing from external interaction ");
            if (fairingParts == null)
            {
                MonoBehaviour.print("ERROR: Fairing parts are null for external update");
            }
            foreach (ROLNodeFairingData data in fairingParts)
            {                
                if (eData.hasTopY)
                {
                    if (eData.topY != data.topY)
                    {
                        needsRebuilt = fairingCreated;
                    }
                    data.topY = eData.topY;
                    //MonoBehaviour.print("Set top pos: " + eData.topY);
                }
                if (eData.hasBottomY)
                {
                    if (eData.bottomY != data.bottomY)
                    {
                        needsRebuilt = fairingCreated;
                    }
                    data.bottomY = eData.bottomY;
                    //MonoBehaviour.print("Set bot pos: " + eData.bottomY);
                }
                if (eData.hasTopRad && data.canAdjustTop)
                {
                    if (eData.topRadius != data.topRadius)
                    {
                        needsRebuilt = fairingCreated;
                    }
                    data.topRadius = eData.topRadius;
                    guiTopDiameter = data.topRadius * 2f;
                    //MonoBehaviour.print("Set top rad: " + eData.topRadius);
                }
                if (eData.hasBottomRad && data.canAdjustBottom)
                {
                    if (eData.bottomRadius != data.bottomRadius)
                    {
                        needsRebuilt = fairingCreated;
                    }
                    data.bottomRadius = eData.bottomRadius;
                    guiBottomDiameter = data.bottomRadius * 2f;
                    //MonoBehaviour.print("Set bot rad: " + eData.bottomRadius);
                }
                if (eData.noseNode != data.noseNode)
                {
                    data.noseNode = eData.noseNode;
                }
                if (eData.mountNode != data.mountNode)
                {
                    data.mountNode = eData.mountNode;
                }
            }
            if (eData.hasEnable)
            {
                fairingForceDisabled = !eData.enable;
                //MonoBehaviour.print("Set enable: " + eData.enable);
            }
            else
            {
                fairingForceDisabled = false;//default to NOT force disabled
            }
            if (fairingCreated && fairingForceDisabled)
            {
                needsRebuilt = false;
                DestroyFairing();
            }
            else
            {
                needsStatusUpdate = true;
            }
            needsGuiUpdate = true;
            externalUpdateData = null;
        }

        #endregion
        
        #region REGION - Initialization methods

        //creates/recreates FairingData instances from data from config node and any persistent node (if applicable)
        private void LoadFairingData(ConfigNode node)
        {
            recolorHandler = new RecoloringHandler(Fields[nameof(customColorData)]);

            ConfigNode[] fairingNodes = node.GetNodes("FAIRING");
            fairingParts = new ROLNodeFairingData[fairingNodes.Length];

            Transform modelBase = part.transform.FindRecursive("model");
            Transform parent;
            ModuleROLNodeFairing[] cs = part.GetComponents<ModuleROLNodeFairing>();
            int l = Array.IndexOf(cs, this);
            int moduleIndex = l;
            for (int i = 0; i < fairingNodes.Length; i++)
            {
                parent = modelBase.FindOrCreate(fairingName + "-" + moduleIndex + "-"+i);
                fairingParts[i] = new ROLNodeFairingData();
                fairingParts[i].load(fairingNodes[i], parent.gameObject);
                if (fairingParts[i].canAdjustTop)
                {
                    enableTopDiameterControls = true;
                    if (guiTopDiameter < 0)
                    {
                        guiTopDiameter = fairingParts[i].topRadius * 2f;
                    }
                    else
                    {
                        fairingParts[i].topRadius = guiTopDiameter * 0.5f;
                    }
                }
                if (fairingParts[i].canAdjustBottom)
                {
                    enableBottomDiameterControls = true;
                    if (guiBottomDiameter < 0)
                    {
                        guiBottomDiameter = fairingParts[i].bottomRadius * 2f;
                    }
                    else
                    {
                        fairingParts[i].bottomRadius = guiBottomDiameter * 0.5f;
                    }
                }
            }
            //reload fairing data from persistence;
            //it -should- already match the guiTopDiameter/guiBottomDiameter (or else was already corrupted/invalid when saved out).
            if (!String.IsNullOrEmpty(persistentDataString))
            {
                String[] datas = ROLUtils.parseCSV(persistentDataString, ":");
                int length = datas.Length;
                for (int i = 0; i < length; i++)
                {
                    fairingParts[i].LoadPersistence(datas[i]);
                }
            }
            string[] names = node.ROLGetStringValues("textureSet");
            string[] titles = ROLUtils.getNames(TexturesUnlimitedLoader.getTextureSets(names), m => m.title);
            TextureSet t = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            if (t == null)
            {
                currentTextureSet = names[0];
                t = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
                initializedColors = false;
            }
            if (!initializedColors)
            {
                initializedColors = true;
                recolorHandler.setColorData(t.maskColors);
            }
            this.updateUIChooseOptionControl(nameof(currentTextureSet), names, titles, true, currentTextureSet);
        }

        /// <summary>
        /// Updates GUI labels and action availability based on current module state (jettisoned, watchedNode attached status, canAdjustRadius, etc)
        /// </summary>
        private void UpdateGuiState()
        {
            bool topAdjustEnabled = enableTopDiameterControls && canAdjustTop;
            bool bottomAdjustEnabled = enableBottomDiameterControls && canAdjustBottom;
            bool currentlyEnabled = fairingCreated;
            if (fairingForceDisabled || !currentlyEnabled || fairingJettisoned)//adjustment not possible if faring jettisoned
            {
                topAdjustEnabled = bottomAdjustEnabled = false;
            }
            Fields[nameof(guiTopDiameter)].guiActiveEditor = topAdjustEnabled;
            Fields[nameof(guiBottomDiameter)].guiActiveEditor = bottomAdjustEnabled;
            Fields[nameof(numOfSections)].guiActiveEditor = currentlyEnabled && canAdjustSections;
            Events[nameof(jettisonEvent)].guiName = actionName + " " + fairingName;
            Actions[nameof(jettisonAction)].guiName = actionName + " " + fairingName;
            Events[nameof(jettisonEvent)].active = HighLogic.LoadedSceneIsFlight && currentlyEnabled && canManuallyJettison && (numOfSections > 1 || String.IsNullOrEmpty(nodeName));
            Actions[nameof(jettisonAction)].active = currentlyEnabled && canManuallyJettison && (numOfSections > 1 || String.IsNullOrEmpty(nodeName));
            Events[nameof(toggleFairing)].guiName = "Toggle " + fairingName;
            Events[nameof(toggleFairing)].active = HighLogic.LoadedSceneIsEditor && !fairingForceDisabled && ((currentlyEnabled && canDisableInEditor) || CanSpawnFairing());
            Fields[nameof(editorTransparency)].guiActiveEditor = currentlyEnabled;
            Fields[nameof(generateColliders)].guiActiveEditor = currentlyEnabled;
            Fields[nameof(currentTextureSet)].guiActiveEditor = currentlyEnabled;
            Fields[nameof(currentFairingLength)].guiActiveEditor = currentlyEnabled;
        }

        #endregion

        #region REGION - Fairing Update Methods

        /// <summary>
        /// Blanket method to update the attached/visible status of the fairing based on its fairing type, current jettisoned status, and if a part is present on the fairings watched node (if any/applicable)
        /// </summary>
        private void UpdateFairingStatus()
        {
            //MonoBehaviour.print("Updating fairing status");
            needsStatusUpdate = false;

            if (fairingForceDisabled || fairingJettisoned || !fairingEnabled)
            {
                DestroyFairing();
            }
            else if (!String.IsNullOrEmpty(nodeName))//should watch node
            {
                UpdateStatusForNode();
            }
            else if (!fairingJettisoned && fairingEnabled && !fairingCreated)//else manual fairing that needs to be rebuilt
            {
                needsRebuilt = true;
            }
            needsGuiUpdate = true;
        }

        private void UpdateStatusForNode()
        {
            AttachNode watchedNode = null;
            Part triggerPart = null;
            float fairingPos = 0;
            if (ShouldSpawnFairingForNode(out watchedNode, out triggerPart, out fairingPos))
            {
                //MonoBehaviour.print("Triggering (re)build from node attachment");
                needsRebuilt = needsRebuilt || !fairingCreated;
                if (snapToNode)
                {
                    foreach (ROLNodeFairingData data in fairingParts)
                    {
                        if (data.canAdjustBottom && data.bottomY != fairingPos)
                        {
                            data.bottomY = fairingPos;
                            needsRebuilt = true;
                        }
                    }
                }                
                prevAttachedPart = triggerPart;
            }
            else if (prevAttachedPart != null)
            {
                //MonoBehaviour.print("Triggering jettison/disable from node detachment: "+prevAttachedPart);
                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (canAutoJettison)
                    {
                        JettisonFairing();
                    }
                    else
                    {
                        //NOOP
                    }
                }
                else
                {
                    DestroyFairing();
                }
            }
            else
            {
                //MonoBehaviour.print("Destroying fairing due to no prev part / no node attachment");
                DestroyFairing();
            }
        }
        
        /// <summary>
        /// Reparents the fairing panel parts to the input part; should only be used on jettison of the fairings when they stay attached to the part below
        /// </summary>
        /// <param name="newParent"></param>
        private void ReparentFairing(Part newParent)
        {
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                fairingParts[i].fairingBase.ReparentFairing(newParent.transform.FindRecursive("model"));
            }
        }
        
        private void JettisonFairing()
        {
            //MonoBehaviour.print("Jettisoning fairing - prev: "+prevAttachedPart);
            if (numOfSections == 1 && prevAttachedPart != null)
            {
                //MonoBehaviour.print("Reparenting fairing to: " + prevAttachedPart);
                ReparentFairing(prevAttachedPart);
                ROLModInterop.OnPartGeometryUpdate(prevAttachedPart, true);//update other parts highlight renderers, to add the new fairing bits to it.
            }
            else
            {
                //MonoBehaviour.print("Jettisoning Panels: " + fairingParts.Length);
                foreach (ROLNodeFairingData data in fairingParts)
                {
                    data.JettisonPanels(part);
                }
            }
            prevAttachedPart = null;
            fairingJettisoned = true;
            fairingEnabled = false;
            DestroyFairing();//cleanup any leftover bits in fairing containers
        }

        private void EnableFairing(bool enable)
        {
            fairingEnabled = enable;
            if (fairingEnabled)
            {
                needsRebuilt = fairingCreated;
            }
            else
            {
                DestroyFairing();
            }
        }

        private void BuildFairing()
        {
            //MonoBehaviour.print("Building Fairing "+guiTopDiameter+" : "+guiBottomDiameter);
            needsRebuilt = false;
            fairingCreated = true;
            int len = fairingParts.Length;            
            if (HighLogic.LoadedSceneIsEditor)//only enforce editor sizing while in the editor;
            {
                for (int i = 0; i < len; i++)
                {
                    //MonoBehaviour.print("pt t/b " + fairingParts[i].topRadius + " : " + fairingParts[i].bottomRadius);
                    if (fairingParts[i].canAdjustTop && canAdjustTop)
                    {
                        fairingParts[i].topRadius = guiTopDiameter * 0.5f;
                    }
                    if (fairingParts[i].canAdjustBottom && canAdjustBottom)
                    {
                        fairingParts[i].bottomRadius = guiBottomDiameter * 0.5f;
                    }
                    //MonoBehaviour.print("pt t/b 2" + fairingParts[i].topRadius + " : " + fairingParts[i].bottomRadius);
                }
            }
            for (int i = 0; i < len; i++)
            {
                fairingParts[i].generateColliders = this.generateColliders;
                fairingParts[i].facesPerCollider = 1;
                fairingParts[i].numOfSections = (int)Math.Round(numOfSections);
                fairingParts[i].CreateFairing(editorTransparency ? 0.25f : 1f);
            }
            ROLModInterop.OnPartGeometryUpdate(part, true);
        }
        
        private void RebuildFairing()
        {
            DestroyFairing();
            BuildFairing();
            UpdateFairingNodes();
            UpdateTextureSet(false);
            UpdateColliders();
        }

        public void UpdateFairingNodes()
        {
            ROLLog.log($"UpdateFairingNodes()");
            foreach (ROLNodeFairingData data in fairingParts)
            {
                ROLLog.log($"fairingName: {fairingName}");
                // Update the Nose Interstage Node
                if (fairingName == "Top Fairing")
                {
                    Vector3 pos = new Vector3(0, data.topY, 0);
                    ROLLog.log($"data.topY: {data.topY}");
                    ROLSelectableNodes.updateNodePosition(part, noseFairingNode, pos);
                    if (part.FindAttachNode(noseFairingNode) is AttachNode noseInterstage)
                        ROLAttachNodeUtils.updateAttachNodePosition(part, noseInterstage, pos, Vector3.up, true, 2);
                }
                // Update the Mount Interstage Node
                if (fairingName == "Bottom Fairing")
                {
                    Vector3 pos = new Vector3(0, data.bottomY, 0);
                    ROLSelectableNodes.updateNodePosition(part, data.mountNode, pos);
                    if (part.FindAttachNode(mountFairingNode) is AttachNode mountInterstage)
                        ROLAttachNodeUtils.updateAttachNodePosition(part, mountInterstage, pos, Vector3.down, true, 2);
                }
            }
        }

        public void DestroyFairing()
        {
            //MonoBehaviour.print("Destroying Fairing");
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                fairingParts[i].DestroyFairing();

            }
            if (fairingCreated)
            {
                ROLModInterop.OnPartGeometryUpdate(part, true);
            }
            fairingCreated = false;
        }
        
        private void UpdateTextureSet(bool useDefaults)
        {
            TextureSet s = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            RecoloringData[] colors = useDefaults ? s.maskColors : getSectionColors(string.Empty);
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                fairingParts[i].fairingBase.EnableTextureSet(currentTextureSet, colors);
            }
            if (useDefaults)
            {
                recolorHandler.setColorData(colors);
            }
            UpdateOpacity();
            ROLModInterop.OnPartTextureUpdated(part);
        }

        private bool ShouldSpawnFairingForNode(out AttachNode watchedNode, out Part triggerPart, out float fairingPos)
        {
            watchedNode = part.FindAttachNode(nodeName);
            if (watchedNode == null)
            {
                //no node found, return false
                fairingPos = 0;
                triggerPart = null;
                return false;
            }
            triggerPart = watchedNode.attachedPart;
            fairingPos = watchedNode.position.y;
            if (snapToSecondNode && triggerPart != null)
            {
                watchedNode = GetLowestNode(triggerPart, out fairingPos);
                if (watchedNode != null && watchedNode.attachedPart != part)//don't spawn fairing if there is only one node and this is the part attached
                {
                    triggerPart = watchedNode.attachedPart;
                }
                else
                {
                    triggerPart = null;
                }
            }
            return triggerPart != null;
        }

        /// <summary>
        /// Returns true for empty/null node name (whereas shouldSpawnFairing returns false)
        /// </summary>
        /// <returns></returns>
        private bool CanSpawnFairing()
        {
            if (String.IsNullOrEmpty(nodeName)) { return true; }
            AttachNode n = null;
            Part p = null;
            float pos;
            return ShouldSpawnFairingForNode(out n, out p, out pos);
        }

        private AttachNode GetLowestNode(Part p, out float fairingPos)
        {
            AttachNode node = null;
            AttachNode nodeTemp;
            float pos = float.PositiveInfinity;
            Vector3 posTemp;
            int len = p.attachNodes.Count;

            for (int i = 0; i < len; i++)
            {
                nodeTemp = p.attachNodes[i];
                posTemp = nodeTemp.position;
                posTemp = p.transform.TransformPoint(posTemp);
                posTemp = part.transform.InverseTransformPoint(posTemp);
                if (posTemp.y < pos)
                {
                    node = nodeTemp;
                    pos = posTemp.y;
                }
            }
            fairingPos = pos;
            return node;
        }

        private void UpdateShieldingStatus()
        {
            ModuleROLAirstreamShield shield = part.GetComponent<ModuleROLAirstreamShield>();
            if (shield != null)
            {
                if (fairingCreated)
                {
                    string name = fairingName + "" + part.Modules.IndexOf(this);
                    float top=float.NegativeInfinity, bottom=float.PositiveInfinity, topRad=0, botRad=0;
                    bool useTop=false, useBottom=false;
                    if (!string.IsNullOrEmpty(nodeName))
                    {
                        useTop = nodeName == "top";
                        useBottom = nodeName == "bottom";
                    }
                    int len = fairingParts.Length;
                    ROLFairingData fp;
                    for (int i = 0; i < len; i++)
                    {
                        fp = fairingParts[i];
                        if (fp.topY > top) { top = fp.topY; }
                        if (fp.bottomY < bottom) { bottom = fp.bottomY; }
                        if (fp.topRadius > topRad) { topRad = fp.topRadius; }
                        if (fp.bottomRadius > botRad) { botRad = fp.bottomRadius; }
                    }
                    shield.addShieldArea(name, topRad, botRad, top, bottom, useTop, useBottom);
                }
                else
                {
                    shield.removeShieldArea(fairingName + "" + part.Modules.IndexOf(this));
                }
            }
        }

        private void UpdateColliders()
        {
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                if (fairingParts[i].generateColliders != generateColliders)
                {
                    fairingParts[i].generateColliders = generateColliders;
                    needsRebuilt = true;
                }
            }
        }

        private void UpdateOpacity()
        {
            float opacity = editorTransparency && HighLogic.LoadedSceneIsEditor ? 0.25f : 1f;
            foreach (ROLFairingData fd in fairingParts) { fd.fairingBase.SetOpacity(opacity); }
        }

        #endregion

    }

    public class ROLNodeFairingData : ROLFairingData
    {
        public void LoadPersistence(String data)
        {
            String[] csv = ROLUtils.parseCSV(data);
            topY = ROLUtils.safeParseFloat(csv[0]);
            bottomY = ROLUtils.safeParseFloat(csv[1]);
            topRadius = ROLUtils.safeParseFloat(csv[2]);
            bottomRadius = ROLUtils.safeParseFloat(csv[3]);
            noseNode = csv[4];
            mountNode = csv[5];
        }

        public String GetPersistence()
        {
            return topY + "," + bottomY + "," + topRadius + "," + bottomRadius + "," + noseNode + "," + mountNode;
        }
    }
    
    public class ROLFairingUpdateData
    {
        public bool enable;
        public float topY;
        public float bottomY;
        public float topRadius;
        public float bottomRadius;
        public String noseNode;
        public String mountNode;
        public bool hasEnable;
        public bool hasTopY;
        public bool hasBottomY;
        public bool hasTopRad;
        public bool hasBottomRad;
        public ROLFairingUpdateData() { }
        public void SetTopY(float val) { topY = val; hasTopY = true; }
        public void SetBottomY(float val) { bottomY = val; hasBottomY = true; }
        public void SetTopRadius(float val) { topRadius = val;  hasTopRad = true; }
        public void SetBottomRadius(float val) { bottomRadius = val;  hasBottomRad = true; }
        public void SetNoseFairingNode(String val) { noseNode = val; }
        public void SetMountFairingNode(String val) { mountNode = val; }
        public void SetEnable(bool val) { enable = val; hasEnable = true; }

        public override string ToString()
        {
            return enable + ", " + topY + ", " + bottomY + ", " + topRadius + ", " + bottomRadius;
        }
    }

}

