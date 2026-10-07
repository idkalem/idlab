using IDLab.Core.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IDLab.App.UI
{
    /// <summary>
    /// One row in the equipment inventory. Dragging sideways out of the panel spawns the item and hands it to
    /// the 3D DragController; vertical drags scroll the list; a tap places it in the middle of the bench.
    /// </summary>
    public sealed class InventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private LabApp _app;
        private EquipmentDefinition _definition;
        private ScrollRect _scroll;
        private bool _forwardingToScroll;

        public void Initialize(LabApp app, EquipmentDefinition definition, ScrollRect scroll)
        {
            _app = app;
            _definition = definition;
            _scroll = scroll;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Vector2 d = eventData.delta;
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            {
                _forwardingToScroll = false;
                _app.Drag.BeginSpawnDrag(_definition.Id);
            }
            else
            {
                _forwardingToScroll = true;
                _scroll?.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_forwardingToScroll)
            {
                _scroll?.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_forwardingToScroll)
            {
                _scroll?.OnEndDrag(eventData);
            }

            _forwardingToScroll = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging)
            {
                return;
            }

            // Accessible alternative to dragging: drop it at a free-ish spot near the centre of the bench.
            float x = Random.Range(-0.15f, 0.15f);
            float z = Random.Range(-0.15f, 0.05f);
            var view = _app.Spawn(_definition.Id, new Vector3(x, 0.12f, z));
            _app.Select(view);
        }
    }
}
