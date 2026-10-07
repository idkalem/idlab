using IDLab.Core.Model;
using UnityEngine;

namespace IDLab.App.Views
{
    /// <summary>
    /// Maps an equipment definition's VisualKey to a view. New equipment = new definition in the core catalog
    /// + (optionally) a new view class registered here. Production builds would swap these procedural
    /// builders for authored prefabs addressed by the same key (Addressables).
    /// </summary>
    public static class EquipmentViewFactory
    {
        public static EquipmentView Create(LabApp app, EquipmentInstance instance)
        {
            var go = new GameObject(instance.Definition.Id);
            EquipmentView view;
            switch (instance.Definition.VisualKey)
            {
                case "beaker":
                    view = WithShape(go, ContainerView.Shape.Beaker);
                    break;
                case "bottle":
                    view = WithShape(go, ContainerView.Shape.Bottle);
                    break;
                case "jar":
                    view = WithShape(go, ContainerView.Shape.Jar);
                    break;
                case "burner":
                    view = go.AddComponent<BurnerView>();
                    break;
                case "thermometer":
                case "ph_probe":
                    view = go.AddComponent<ProbeView>();
                    break;
                default:
                    Debug.LogWarning($"[IDLab] No view for visual key '{instance.Definition.VisualKey}', using a placeholder.");
                    view = go.AddComponent<PlaceholderView>();
                    break;
            }

            view.Initialize(app, instance);
            return view;
        }

        private static EquipmentView WithShape(GameObject go, ContainerView.Shape shape)
        {
            var view = go.AddComponent<ContainerView>();
            view.Configure(shape);
            return view;
        }
    }

    /// <summary>Grey box sized from the definition's bounds; lets content authors test new items before art exists.</summary>
    public sealed class PlaceholderView : EquipmentView
    {
        protected override void BuildVisuals()
        {
            Vector3 size = Instance.Definition.BoundsSize.ToUnity();
            if (size == Vector3.zero)
            {
                size = Vector3.one * 0.05f;
            }

            Box("Placeholder", size, Plastic(new Color(0.5f, 0.5f, 0.55f)), new Vector3(0, size.y * 0.5f, 0));
        }
    }
}
