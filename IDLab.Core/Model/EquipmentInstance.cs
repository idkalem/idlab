using System.Collections.Generic;
using IDLab.Core.Components;

namespace IDLab.Core.Model
{
    /// <summary>A live piece of equipment on the bench: definition + pose + simulation components.</summary>
    public sealed class EquipmentInstance
    {
        private readonly List<LabComponent> _components = new List<LabComponent>();

        public int Id { get; }
        public EquipmentDefinition Definition { get; }
        public LabWorld World { get; }

        /// <summary>World pose, mirrored from the front-end every frame (the 3D engine owns rigid-body physics).</summary>
        public Pose3 Pose { get; set; }

        public IReadOnlyList<LabComponent> Components => _components;

        internal EquipmentInstance(int id, EquipmentDefinition definition, LabWorld world, Pose3 pose)
        {
            Id = id;
            Definition = definition;
            World = world;
            Pose = pose;
        }

        internal void AddComponent(LabComponent component)
        {
            _components.Add(component);
            component.Attach(this);
        }

        public T Get<T>() where T : LabComponent
        {
            foreach (var c in _components)
            {
                if (c is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        public bool Has<T>() where T : LabComponent => Get<T>() != null;

        public override string ToString() => $"{Definition.Id}#{Id}";
    }
}
