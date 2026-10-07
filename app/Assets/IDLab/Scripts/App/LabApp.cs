using System;
using System.Collections.Generic;
using IDLab.App.Interaction;
using IDLab.App.Micro;
using IDLab.App.Rendering;
using IDLab.App.UI;
using IDLab.App.Vfx;
using IDLab.App.Views;
using IDLab.Core;
using IDLab.Core.Interaction;
using IDLab.Core.Model;
using UnityEngine;

namespace IDLab.App
{
    /// <summary>
    /// Composition root of the Unity front-end. Owns the core <see cref="LabWorld"/> and wires it to views,
    /// input, HUD, micro view and VFX. Frame order:
    ///   DragController (-50) moves transforms → LabApp.Update pushes poses → LabWorld.Advance (fixed steps)
    ///   → views/HUD/micro view render the new state.
    /// </summary>
    [DefaultExecutionOrder(0)]
    public sealed class LabApp : MonoBehaviour
    {
        private readonly Dictionary<int, EquipmentView> _views = new Dictionary<int, EquipmentView>();
        private GameObject _selectionRing;

        public static LabApp Instance { get; private set; }

        public LabWorld World { get; private set; }
        public SnapSystem Snaps { get; private set; }
        public ExperimentCatalog Experiments { get; private set; }
        public ExperimentDefinition CurrentExperiment { get; private set; }

        public Camera MainCamera { get; private set; }
        public OrbitCameraController CameraRig { get; private set; }
        public DragController Drag { get; private set; }
        public LabHud Hud { get; private set; }
        public MicroViewController Micro { get; private set; }
        public HazardEffects Effects { get; private set; }
        public Transform EquipmentRoot { get; private set; }

        public EquipmentView Selected { get; private set; }
        public IEnumerable<EquipmentView> Views => _views.Values;

        public event Action<EquipmentView> SelectionChanged;
        public event Action<ExperimentDefinition> ExperimentLoaded;

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Physics.defaultContactOffset = 0.002f; // small-scale objects (centimetres)
            Physics.defaultSolverIterations = 12;

            World = new LabWorld();
            Snaps = new SnapSystem(World);
            Experiments = ExperimentCatalog.CreateDefault();
            World.InstanceAdded += OnInstanceAdded;
            World.InstanceRemoved += OnInstanceRemoved;
            World.Disconnected += OnDisconnected;

            EquipmentRoot = new GameObject("Equipment").transform;
            LabEnvironment.Build(transform);
            SetupCamera();

            Effects = gameObject.AddComponent<HazardEffects>();
            Effects.Initialize(this);
            Micro = new GameObject("MicroView").AddComponent<MicroViewController>();
            Micro.Initialize(this);
            Drag = gameObject.AddComponent<DragController>();
            Drag.Initialize(this);
            Hud = gameObject.AddComponent<LabHud>();
            Hud.Initialize(this);

            _selectionRing = new GameObject("SelectionRing");
            _selectionRing.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Ring(0.5f, 0.56f, 0.001f);
            var ringRenderer = _selectionRing.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = MaterialFactory.Additive(new Color(0.25f, 0.75f, 1f, 0.8f));
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selectionRing.SetActive(false);

            LoadExperiment(ExperimentCatalog.SandboxId);
        }

        private void SetupCamera()
        {
            MainCamera = Camera.main;
            if (MainCamera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                MainCamera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            MainCamera.nearClipPlane = 0.01f;
            MainCamera.farClipPlane = 40f;
            MainCamera.fieldOfView = 40f;
            MainCamera.clearFlags = CameraClearFlags.SolidColor;
            MainCamera.backgroundColor = UITheme.SceneBackground;
            MainCamera.allowHDR = true;
            MainCamera.allowMSAA = true;
            CameraRig = MainCamera.gameObject.GetComponent<OrbitCameraController>();
            if (CameraRig == null)
            {
                CameraRig = MainCamera.gameObject.AddComponent<OrbitCameraController>();
            }
        }

        // ------------------------------------------------------------------ experiments

        public void LoadExperiment(string experimentId)
        {
            Select(null);
            World.Reset();
            CurrentExperiment = Experiments.Get(experimentId);
            foreach (var (id, x, z) in CurrentExperiment.InitialLayout)
            {
                Spawn(id, new Vector3(x, 0.001f, z));
            }

            ExperimentLoaded?.Invoke(CurrentExperiment);
        }

        public IEnumerable<EquipmentDefinition> InventoryForCurrentExperiment()
        {
            if (CurrentExperiment == null || CurrentExperiment.Equipment.Count == 0)
            {
                foreach (var d in World.Catalog.All)
                {
                    yield return d;
                }

                yield break;
            }

            foreach (var id in CurrentExperiment.Equipment)
            {
                yield return World.Catalog.Get(id);
            }
        }

        // ------------------------------------------------------------------ equipment lifecycle

        public EquipmentView Spawn(string definitionId, Vector3 position)
        {
            var instance = World.Spawn(definitionId, new Pose3(position.ToCore(), System.Numerics.Quaternion.Identity));
            return _views[instance.Id];
        }

        public void Remove(EquipmentView view)
        {
            if (view != null)
            {
                World.Remove(view.Instance);
            }
        }

        public EquipmentView ViewOf(EquipmentInstance instance) =>
            instance != null && _views.TryGetValue(instance.Id, out var v) ? v : null;

        public void Snap(EquipmentView view, SnapCandidate candidate)
        {
            var host = ViewOf(candidate.Target);
            Snaps.Apply(view.Instance, candidate);
            view.TiltDegrees = 0f;
            view.transform.SetPositionAndRotation(candidate.SnappedPose.Position.ToUnity(), candidate.SnappedPose.Rotation.ToUnity());
            view.AttachTo(host);
        }

        private void OnInstanceAdded(EquipmentInstance instance)
        {
            var view = EquipmentViewFactory.Create(this, instance);
            view.transform.SetParent(EquipmentRoot, true);
            _views.Add(instance.Id, view);
        }

        private void OnInstanceRemoved(EquipmentInstance instance)
        {
            if (!_views.TryGetValue(instance.Id, out var view))
            {
                return;
            }

            _views.Remove(instance.Id);
            if (Selected == view)
            {
                Select(null);
            }

            Destroy(view.gameObject);
        }

        private void OnDisconnected(Connection connection)
        {
            var child = ViewOf(connection.Child);
            if (child != null && child.HostView == ViewOf(connection.Host))
            {
                child.DetachFromHost();
                if (!child.IsHeld)
                {
                    child.SetKinematic(false);
                }
            }
        }

        public void Select(EquipmentView view)
        {
            if (Selected == view)
            {
                return;
            }

            Selected = view;
            SelectionChanged?.Invoke(view);
        }

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            foreach (var view in _views.Values)
            {
                view.PushPoseToCore();
            }

            World.Advance(Time.deltaTime);

            foreach (var view in _views.Values)
            {
                view.SyncVisuals(Time.deltaTime);
            }

            UpdateSelectionRing();
        }

        private void UpdateSelectionRing()
        {
            if (Selected == null)
            {
                _selectionRing.SetActive(false);
                return;
            }

            Bounds b = Selected.WorldBounds();
            float size = Mathf.Max(b.extents.x, b.extents.z) * 2.3f;
            _selectionRing.SetActive(true);
            _selectionRing.transform.position = new Vector3(b.center.x, b.min.y + 0.0015f, b.center.z);
            _selectionRing.transform.localScale = new Vector3(size, 1f, size);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
