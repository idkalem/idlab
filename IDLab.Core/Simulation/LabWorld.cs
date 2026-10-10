using System;
using System.Collections.Generic;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Data;
using IDLab.Core.Events;
using IDLab.Core.Model;

namespace IDLab.Core
{
    /// <summary>
    /// Root of the simulation: owns every equipment instance, the snap connections between them,
    /// the clock, the chemistry database, the event bus and the data logger.
    /// Pure C#, no engine types: the 3D front-end mirrors poses in and renders state out.
    /// </summary>
    public sealed class LabWorld
    {
        private readonly Dictionary<int, EquipmentInstance> _instances = new Dictionary<int, EquipmentInstance>();
        private readonly List<Connection> _connections = new List<Connection>();
        private readonly List<LabComponent> _stepList = new List<LabComponent>();
        private bool _stepListDirty;
        private int _nextInstanceId = 1;
        private int _nextConnectionId = 1;

        public SimClock Clock { get; } = new SimClock();
        public SubstanceDatabase Substances { get; }
        public EquipmentCatalog Catalog { get; }
        public ReactionEngine Reactions { get; }
        public LabEventBus Events { get; } = new LabEventBus();
        public DataLogger Logger { get; } = new DataLogger();

        public double AmbientTemperatureC { get; set; } = 20.0;

        /// <summary>Simulated seconds since the session started (or was last reset).</summary>
        public double Time { get; private set; }

        public IEnumerable<EquipmentInstance> Instances => _instances.Values;
        public IReadOnlyList<Connection> Connections => _connections;

        public event Action<EquipmentInstance> InstanceAdded;
        public event Action<EquipmentInstance> InstanceRemoved;
        public event Action<Connection> Connected;
        public event Action<Connection> Disconnected;

        public LabWorld()
            : this(SubstanceDatabase.Shared, EquipmentCatalog.CreateDefault())
        {
        }

        public LabWorld(SubstanceDatabase substances, EquipmentCatalog catalog)
        {
            Substances = substances;
            Catalog = catalog;
            Reactions = ReactionEngine.CreateDefault(substances);
        }

        // ------------------------------------------------------------------ lifecycle

        public EquipmentInstance Spawn(string definitionId, Pose3 pose)
        {
            var definition = Catalog.Get(definitionId);
            var instance = new EquipmentInstance(_nextInstanceId++, definition, this, pose);
            foreach (var spec in definition.Components)
            {
                instance.AddComponent(spec.CreateComponent());
            }

            _instances.Add(instance.Id, instance);
            _stepListDirty = true;
            RegisterDataChannels(instance);
            InstanceAdded?.Invoke(instance);
            return instance;
        }

        public void Remove(EquipmentInstance instance)
        {
            if (instance == null || !_instances.Remove(instance.Id))
            {
                return;
            }

            DisconnectAll(instance);
            Logger.RemoveChannelsOwnedBy(instance.Id);
            _stepListDirty = true;
            InstanceRemoved?.Invoke(instance);
        }

        public EquipmentInstance Find(int id) => _instances.TryGetValue(id, out var instance) ? instance : null;

        /// <summary>Removes all equipment, clears data and rewinds time.</summary>
        public void Reset()
        {
            foreach (var instance in new List<EquipmentInstance>(_instances.Values))
            {
                Remove(instance);
            }

            Clock.Reset();
            Logger.Clear();
            Time = 0;
        }

        // ------------------------------------------------------------------ connections

        public Connection Connect(EquipmentInstance child, string childSnapId, EquipmentInstance host, string hostSnapId)
        {
            if (child == host)
            {
                throw new ArgumentException("An instance cannot snap to itself.");
            }

            if (child.Definition.FindSnap(childSnapId) == null || host.Definition.FindSnap(hostSnapId) == null)
            {
                throw new ArgumentException($"Unknown snap {childSnapId} / {hostSnapId}.");
            }

            if (IsSnapOccupied(child, childSnapId) || IsSnapOccupied(host, hostSnapId))
            {
                throw new InvalidOperationException("Snap point already occupied.");
            }

            var connection = new Connection(_nextConnectionId++, child, childSnapId, host, hostSnapId);
            _connections.Add(connection);
            Connected?.Invoke(connection);
            return connection;
        }

        /// <summary>Detaches <paramref name="instance"/> from whatever it rests on (keeps things resting on it).</summary>
        public void DetachFromHost(EquipmentInstance instance)
        {
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                if (_connections[i].Child == instance)
                {
                    var removed = _connections[i];
                    _connections.RemoveAt(i);
                    Disconnected?.Invoke(removed);
                }
            }
        }

        public void DisconnectAll(EquipmentInstance instance)
        {
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                if (_connections[i].Involves(instance))
                {
                    var removed = _connections[i];
                    _connections.RemoveAt(i);
                    Disconnected?.Invoke(removed);
                }
            }
        }

        public bool IsSnapOccupied(EquipmentInstance instance, string snapId) => FindConnection(instance, snapId) != null;

        public Connection FindConnection(EquipmentInstance instance, string snapId)
        {
            foreach (var c in _connections)
            {
                if (c.Uses(instance, snapId))
                {
                    return c;
                }
            }

            return null;
        }

        /// <summary>The instance snapped to <paramref name="snapId"/> of <paramref name="instance"/>, or null.</summary>
        public EquipmentInstance GetAttached(EquipmentInstance instance, string snapId) =>
            FindConnection(instance, snapId)?.Other(instance);

        /// <summary>What <paramref name="instance"/> currently rests on/in, or null.</summary>
        public EquipmentInstance GetHost(EquipmentInstance instance)
        {
            foreach (var c in _connections)
            {
                if (c.Child == instance)
                {
                    return c.Host;
                }
            }

            return null;
        }

        public IEnumerable<EquipmentInstance> GetChildren(EquipmentInstance host)
        {
            foreach (var c in _connections)
            {
                if (c.Host == host)
                {
                    yield return c.Child;
                }
            }
        }

        // ------------------------------------------------------------------ simulation

        /// <summary>Call once per rendered frame with the real frame delta. Returns fixed steps executed.</summary>
        public int Advance(double realDeltaSeconds) => Clock.Advance(realDeltaSeconds, Step);

        /// <summary>Runs exactly one fixed step of <paramref name="dt"/> simulated seconds.</summary>
        public void Step(double dt)
        {
            if (_stepListDirty)
            {
                RebuildStepList();
            }

            foreach (var component in _stepList)
            {
                component.Step(dt);
            }

            Time += dt;
            Logger.Tick(Time);
        }

        /// <summary>Convenience for tests / headless runs: simulate <paramref name="seconds"/> with the fixed step.</summary>
        public void Simulate(double seconds)
        {
            int steps = (int)Math.Round(seconds / Clock.FixedStep);
            for (int i = 0; i < steps; i++)
            {
                Step(Clock.FixedStep);
            }
        }

        private void RebuildStepList()
        {
            _stepList.Clear();
            foreach (var instance in _instances.Values)
            {
                _stepList.AddRange(instance.Components);
            }

            // Stable sort: by order, then by instance id for determinism.
            _stepList.Sort((a, b) =>
            {
                int byOrder = a.StepOrder.CompareTo(b.StepOrder);
                return byOrder != 0 ? byOrder : a.Owner.Id.CompareTo(b.Owner.Id);
            });
            _stepListDirty = false;
        }

        private void RegisterDataChannels(EquipmentInstance instance)
        {
            foreach (var component in instance.Components)
            {
                if (component is IDataSource source)
                {
                    source.RegisterChannels(Logger);
                }
            }
        }
    }
}
