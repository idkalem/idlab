using IDLab.Core.Model;

namespace IDLab.Core.Components
{
    /// <summary>
    /// Base class for simulation behaviour attached to an <see cref="EquipmentInstance"/>.
    /// Components step on the fixed simulation tick, ordered by <see cref="StepOrder"/>
    /// (sources → containers → sensors) so that heat added this tick is consumed this tick.
    /// </summary>
    public abstract class LabComponent
    {
        public const int OrderSources = 0;
        public const int OrderContainers = 100;
        public const int OrderSensors = 200;

        public EquipmentInstance Owner { get; private set; }

        protected LabWorld World => Owner.World;

        public virtual int StepOrder => OrderContainers;

        internal void Attach(EquipmentInstance owner)
        {
            Owner = owner;
            OnAttached();
        }

        protected virtual void OnAttached()
        {
        }

        public abstract void Step(double dt);
    }
}
