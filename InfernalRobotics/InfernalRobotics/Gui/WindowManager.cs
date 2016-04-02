using InfernalRobotics.Command;
using InfernalRobotics.Control;
using InfernalRobotics.Utility;
using KSP.IO;
using KSP.UI.Screens;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace InfernalRobotics.Gui
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class IRFlightWindowManager : WindowManager
    {
        public override string AddonName { get { return this.name; } }
    }

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class IREditorWindowManager : WindowManager
    {
        public override string AddonName { get { return this.name; } }
    }

    public class WindowManager : MonoBehaviour
    {
        public virtual String AddonName { get; set; }

        private static GameObject _controlWindow;
        private static GameObject _editorWindow;

        private static Dictionary<ServoController.ControlGroup,GameObject> _servoGroupUIControls;
        private static Dictionary<IServo, GameObject> _servoUIControls;

        private static bool useElectricCharge;
        private static bool allowServoFlip;

        private static WindowManager _instance;
        private static bool guiSetupDone;
        private ApplicationLauncherButton appLauncherButton;
        private static Texture2D appLauncherButtonTexture;

        private bool guiGroupEditorEnabled;
        private bool guiPresetsEnabled;
        private IServo associatedServo;
        private bool guiPresetMode;
        private bool guiHidden;

        private static int editorWindowWidth = 400;
        private static int controlWindowWidth = 360;

        public bool GUIEnabled { get; set; }

        public static WindowManager Instance
        {
            get { return _instance; }
        }

        static WindowManager()
        {
            useElectricCharge = true;
            guiSetupDone = false;
            appLauncherButtonTexture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
            TextureLoader.LoadImageFromFile (appLauncherButtonTexture, "icon_button.png");

            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

        }

        private void Awake()
        {
            
            LoadConfigXml();

            Logger.Log("[NewGUI] awake, Mode: " + AddonName);
            GUIEnabled = true; //for testing
            guiGroupEditorEnabled = false;

            GameScenes scene = HighLogic.LoadedScene;

            if (scene == GameScenes.FLIGHT || scene == GameScenes.EDITOR)
            {
                _instance = this;

                _servoGroupUIControls = new Dictionary<ServoController.ControlGroup, GameObject>();
                _servoUIControls = new Dictionary<IServo, GameObject>();
            }
            else
            {
                _instance = null;
                //actually we don't need to go further if it's not flight or editor
                return;
            }
            //GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);

            //GameEvents.onGUIApplicationLauncherReady.Add (AddAppLauncherButton);

            Logger.Log("[GUI] Added Toolbar GameEvents Handlers", Logger.Level.Debug);

            GameEvents.onShowUI.Add(OnShowUI);
            GameEvents.onHideUI.Add(OnHideUI);

            Logger.Log("[GUI] awake finished successfully", Logger.Level.Debug);
        }

        private void OnShowUI()
        {
            guiHidden = false;
        }

        private void OnHideUI()
        {
            guiHidden = true;
        }


        public void Start()
        {
            
        }

        private void InitGroupControls(GameObject newServoGroupLine, ServoController.ControlGroup g)
        {
            var hlg = newServoGroupLine.GetChild("ServoGroupControlsHLG");
            var servosVLG = newServoGroupLine.GetChild("ServoGroupServosVLG");

            hlg.GetChild("ServoGroupNameText").GetComponent<Text>().text = g.Name;

            var groupToggle = hlg.GetChild("ServoGroupExpandedStatusToggle").GetComponent<Toggle>();
            groupToggle.isOn = g.Expanded;
            servosVLG.SetActive(g.Expanded);
            groupToggle.onValueChanged.AddListener(b => { g.Expanded = b; servosVLG.SetActive(b); });

            var groupSpeed = hlg.GetChild("ServoGroupSpeedMultiplier").GetComponent<InputField>();
            groupSpeed.text = g.Speed;
            groupSpeed.onEndEdit.AddListener(v => { g.Speed = v; });

            var groupMoveLeftToggle = hlg.GetChild("ServoGroupMoveLeftToggleButton").GetComponent<Button>();
            groupMoveLeftToggle.onClick.AddListener(() =>
            {
                if (g.MovingNegative)
                {
                    g.Stop();
                    g.MovingNegative = false;
                    g.MovingPositive = false;
                }
                else
                {
                    g.MoveLeft();
                    g.MovingNegative = true;
                    g.MovingPositive = false;
                };
            });

            var groupMoveLeftButton = hlg.GetChild("ServoGroupMoveLeftButton");
            var groupMoveLeftHoldButton = groupMoveLeftButton.AddComponent<HoldButton>();
            groupMoveLeftHoldButton.callbackOnDown = g.MoveLeft;
            groupMoveLeftHoldButton.callbackOnUp = g.Stop;
            
            var groupMoveCenterButton = hlg.GetChild("ServoGroupMoveCenterButton");
            var groupMoveCenterHoldButton = groupMoveCenterButton.AddComponent<HoldButton>();
            groupMoveCenterHoldButton.callbackOnDown = g.MoveCenter;
            groupMoveCenterHoldButton.callbackOnUp = g.Stop;

            var groupMoveRightButton = hlg.GetChild("ServoGroupMoveRightButton");
            var groupMoveRightHoldButton = groupMoveRightButton.AddComponent<HoldButton>();
            groupMoveRightHoldButton.callbackOnDown = g.MoveRight;
            groupMoveRightHoldButton.callbackOnUp = g.Stop;

            var groupMoveRightToggle = hlg.GetChild("ServoGroupMoveRightToggleButton").GetComponent<Button>();
            groupMoveLeftToggle.onClick.AddListener(() =>
            {
                if (g.MovingPositive)
                {
                    g.Stop();
                    g.MovingNegative = false;
                    g.MovingPositive = false;
                }
                else
                {
                    g.MoveRight();
                    g.MovingNegative = false;
                    g.MovingPositive = true;
                };
            });

            //now list servos
            for (int j = 0; j < g.Servos.Count; j++)
            {
                var s = g.Servos[j];

                Logger.Log("[NEW UI] Trying to draw servo via prefab, servo name" + s.Name);

                var newServoLine = GameObject.Instantiate(UIAssetsLoader.controlWindowServoLinePrefab);
                newServoLine.transform.SetParent(servosVLG.transform, false);

                InitServoControls(newServoLine, s);

                _servoUIControls.Add(s, newServoLine);
            }
        }
    
        private void InitServoControls(GameObject newServoLine, IServo s)
        {
            var servoStatusLight = newServoLine.GetChild("ServoStatusRawImage").GetComponent<RawImage>();
            if (s.Mechanism.IsLocked)
            {
                var redDot = UIAssetsLoader.iconAssets.Find(i => i.name == "dotRed");
                if (redDot != null)
                    servoStatusLight.texture = redDot;
            }

            var servoName = newServoLine.GetChild("ServoNameText").GetComponent<Text>();
            servoName.text = s.Name;

            var servoPosition = newServoLine.GetChild("ServoPositionText").GetComponent<Text>();
            servoPosition.text = string.Format("{0:#0.##}", s.Mechanism.Position);

            var servoLockToggle = newServoLine.GetChild("ServoLockToggleButton").GetComponent<Button>();
            var servoLockToggleIcon = newServoLine.GetChild("ServoLockToggleButton").GetChild("Icon").GetComponent<RawImage>();
            if (s.Mechanism.IsLocked)
            {
                servoLockToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "locked");
            }
            else
            {
                servoLockToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "unlocked");
            }
            servoLockToggle.onClick.AddListener(() =>
            {
                if(s.Mechanism.IsLocked)
                {
                    s.Mechanism.IsLocked = false;
                    servoLockToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "unlocked");
                }
                else
                {
                    s.Mechanism.IsLocked = true;
                    servoLockToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "locked");
                }
            });

            var servoMoveLeftButton = newServoLine.GetChild("ServoMoveLeftButton");
            var servoMoveLeftHoldButton = servoMoveLeftButton.AddComponent<HoldButton>();
            servoMoveLeftHoldButton.callbackOnDown = s.Motor.MoveLeft;
            servoMoveLeftHoldButton.callbackOnUp = s.Motor.Stop;

            var servoMoveCenterButton = newServoLine.GetChild("ServoMoveCenterButton");
            var servoMoveCenterHoldButton = servoMoveCenterButton.AddComponent<HoldButton>();
            servoMoveCenterHoldButton.callbackOnDown = s.Motor.MoveCenter;
            servoMoveCenterHoldButton.callbackOnUp = s.Motor.Stop;

            var servoMoveRightButton = newServoLine.GetChild("ServoMoveRightButton");
            var servoMoveRightHoldButton = servoMoveRightButton.AddComponent<HoldButton>();
            servoMoveRightHoldButton.callbackOnDown = s.Motor.MoveRight;
            servoMoveRightHoldButton.callbackOnUp = s.Motor.Stop;

            var servoInvertAxisToggle = newServoLine.GetChild("ServoInvertAxisToggleButton").GetComponent<Button>();
            var servoInverAxisToggleIcon = newServoLine.GetChild("ServoInvertAxisToggleButton").GetChild("Icon").GetComponent<RawImage>();
            if (s.Motor.IsAxisInverted)
            {
                servoInverAxisToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "inverted");
            }
            else
            {
                servoInverAxisToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "noninverted");
            }
            servoInvertAxisToggle.onClick.AddListener(() =>
            {
                if (s.Motor.IsAxisInverted)
                {
                    s.Motor.IsAxisInverted = false;
                    servoInverAxisToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "noninverted");
                }
                else
                {
                    s.Motor.IsAxisInverted = true;
                    servoInverAxisToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "inverted");
                }
            });

        }

        public void RebuildUI()
        {
            //should be called by ServoController when required (Vessel changed and such).
            _servoGroupUIControls?.Clear();
            _servoUIControls?.Clear();

            if (HighLogic.LoadedSceneIsFlight)
            {
                _controlWindow?.DestroyGameObjectImmediate();

                //here we need to wait until prefabs become available and then Instatiate the window
                if (UIAssetsLoader.controlWindowPrefabReady && _controlWindow == null)
                {
                    _controlWindow = GameObject.Instantiate(UIAssetsLoader.controlWindowPrefab);
                    _controlWindow.transform.SetParent(MainCanvasUtil.MainCanvas.transform, false);
                    _controlWindow.GetChild("FlightServoControlWindowTitle").AddComponent<PanelDragger>();
                    _controlWindow.GetChild("FlightServoControlWindowContent").AddComponent<PanelFocuser>();
                    _controlWindow.GetChild("FlightServoControlWindowFooter").AddComponent<PanelFocuser>();
                }

                Logger.Log("[NEW UI] Are prefabs ready:  " + guiSetupDone);

                if (guiSetupDone)
                {
                    GameObject servoGroupsArea = _controlWindow.GetChild("FlightServoControlWindowContent").GetChild("ServoGroupsVLG");

                    for (int i = 0; i < ServoController.Instance.ServoGroups.Count; i++)
                    {
                        Logger.Log("[NEW UI] Trying to draw group via prefab");
                        ServoController.ControlGroup g = ServoController.Instance.ServoGroups[i];

                        if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ActiveVessel != g.Vessel)
                            continue;

                        var newServoGroupLine = GameObject.Instantiate(UIAssetsLoader.controlWindowGroupLinePrefab);
                        newServoGroupLine.transform.SetParent(servoGroupsArea.transform, false);

                        InitGroupControls(newServoGroupLine, g);
                        
                        _servoGroupUIControls.Add(g, newServoGroupLine);
                    }

                }
            }
        }
        public void UpdateServoReadouts(IServo s, GameObject servoUIControls)
        {
            var servoStatusLight = servoUIControls.GetChild("ServoStatusRawImage").GetComponent<RawImage>();
            if (s.Mechanism.IsLocked)
            {
                servoStatusLight.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "dotRed");
            }
            else if(s.Mechanism.IsMoving)
            {
                servoStatusLight.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "dotGreen");
            }
            else
            {
                servoStatusLight.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "dotYellow");
            }

            var servoName = servoUIControls.GetChild("ServoNameText").GetComponent<Text>();
            servoName.text = s.Name;

            var servoPosition = servoUIControls.GetChild("ServoPositionText").GetComponent<Text>();
            servoPosition.text = string.Format("{0:#0.##}", s.Mechanism.Position);

            var servoLockToggle = servoUIControls.GetChild("ServoLockToggleButton").GetComponent<Button>();
            var servoLockToggleIcon = servoUIControls.GetChild("ServoLockToggleButton").GetChild("Icon").GetComponent<RawImage>();
            if (s.Mechanism.IsLocked)
            {
                servoLockToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "locked");
            }
            else
            {
                servoLockToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "unlocked");
            }
            
            var servoInvertAxisToggle = servoUIControls.GetChild("ServoInvertAxisToggleButton").GetComponent<Button>();
            var servoInverAxisToggleIcon = servoUIControls.GetChild("ServoInvertAxisToggleButton").GetChild("Icon").GetComponent<RawImage>();
            if (s.Motor.IsAxisInverted)
            {
                servoInverAxisToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "inverted");
            }
            else
            {
                servoInverAxisToggleIcon.texture = UIAssetsLoader.iconAssets.Find(i => i.name == "noninverted");
            }
            
        }


        public void Update()
        {
            if (!ServoController.APIReady)
                return;

            if(!guiSetupDone && ControlsGUI.IRGUI.GUIEnabled)
            {
                guiSetupDone = UIAssetsLoader.controlWindowPrefabReady
                                && UIAssetsLoader.controlWindowGroupLinePrefabReady
                                && UIAssetsLoader.controlWindowServoLinePrefabReady;

                RebuildUI();
               
            }
            else
            {
                //at this poitn we should have window instantiated and filled with groups and servos
                //all we need to do is update the fields

                _controlWindow?.SetActive(ControlsGUI.IRGUI.GUIEnabled);


                if (!ControlsGUI.IRGUI.GUIEnabled)
                    return;

                //here we need to update servo statuses, servo positions and status of Locked and Inverted
                foreach(var pair in _servoUIControls)
                {
                    if (!pair.Value.activeInHierarchy)
                        continue;
                    UpdateServoReadouts(pair.Key, pair.Value);
                }
            }

        }

        private void AddAppLauncherButton()
        {
            Logger.Log(string.Format("[GUI] AddAppLauncherButton Called, button=null: {0}", (appLauncherButton == null)), Logger.Level.Debug);

            if (appLauncherButton != null) return;

            if (!ApplicationLauncher.Ready)
                return;

            try
            {
                appLauncherButton = ApplicationLauncher.Instance.AddModApplication(delegate { GUIEnabled = true; },
                    delegate { GUIEnabled = false; }, null, null, null, null,
                    ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.VAB |
                    ApplicationLauncher.AppScenes.SPH, appLauncherButtonTexture);

                ApplicationLauncher.Instance.AddOnHideCallback(OnHideCallback);
            }
            catch (Exception ex)
            {
                Logger.Log(string.Format("[GUI AddAppLauncherButton Exception, {0}", ex.Message), Logger.Level.Fatal);
            }

            Logger.Log(string.Format("[GUI] AddAppLauncherButton finished, button=null: {0}", (appLauncherButton == null)), Logger.Level.Debug);
        }

        private void OnHideCallback()
        {
            GUIEnabled = false;
        }

        void OnGameSceneLoadRequestedForAppLauncher(GameScenes SceneToLoad)
        {
            DestroyAppLauncherButton();
        }

        private void DestroyAppLauncherButton()
        {
            try
            {
                if (appLauncherButton != null && ApplicationLauncher.Instance != null)
                {
                    ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                    appLauncherButton = null;
                }

                if (ApplicationLauncher.Instance != null)
                    ApplicationLauncher.Instance.RemoveOnHideCallback(OnHideCallback);
            }
            catch (Exception e)
            {
                Logger.Log("[GUI] Failed unregistering AppLauncher handlers," + e.Message);
            }
        }

        private void OnDestroy()
        {
            Logger.Log("[GUI] destroy");

            //GameEvents.onGUIApplicationLauncherReady.Remove (AddAppLauncherButton);
            //GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequestedForAppLauncher);
            DestroyAppLauncherButton();

            GameEvents.onShowUI.Remove(OnShowUI);
            GameEvents.onHideUI.Remove(OnHideUI);

            _controlWindow?.DestroyGameObject();
            _editorWindow?.DestroyGameObject();

            EditorLock(false);
            SaveConfigXml();
            Logger.Log("[GUI] OnDestroy finished successfully", Logger.Level.Debug);
        }


        private void RefreshKeysFromGUI()
        {
            foreach (var g in ServoController.Instance.ServoGroups)
            {
                g.RefreshKeys();
            }
        }


        /// <summary>
        ///     Applies or removes the lock
        /// </summary>
        /// <param name="apply">Which way are we going</param>
        internal void EditorLock(Boolean apply)
        {
            //only do this lock in the editor - no point elsewhere
            if (HighLogic.LoadedSceneIsEditor && apply)
            {
                //only add a new lock if there isnt already one there
                if (InputLockManager.GetControlLock("IRGUILockOfEditor") != ControlTypes.EDITOR_LOCK)
                {
                    Logger.Log(String.Format("[GUI] AddingLock-{0}", "IRGUILockOfEditor"), Logger.Level.Debug);

                    InputLockManager.SetControlLock(ControlTypes.EDITOR_LOCK, "IRGUILockOfEditor");
                }
            }
            //Otherwise make sure the lock is removed
            else
            {
                //Only try and remove it if there was one there in the first place
                if (InputLockManager.GetControlLock("IRGUILockOfEditor") == ControlTypes.EDITOR_LOCK)
                {
                    Logger.Log(String.Format("[GUI] Removing-{0}", "IRGUILockOfEditor"), Logger.Level.Debug);
                    InputLockManager.RemoveControlLock("IRGUILockOfEditor");
                }
            }
        }

        public void LoadConfigXml()
        {
            PluginConfiguration config = PluginConfiguration.CreateForType<WindowManager>();
            config.load();
            useElectricCharge = config.GetValue<bool>("useEC");
            allowServoFlip = config.GetValue<bool>("allowFlipHack");
        }

        public void SaveConfigXml()
        {
            PluginConfiguration config = PluginConfiguration.CreateForType<WindowManager>();
            config.SetValue("useEC", useElectricCharge);
            config.SetValue("allowFlipHack", allowServoFlip);
            config.save();
        }
    }
}