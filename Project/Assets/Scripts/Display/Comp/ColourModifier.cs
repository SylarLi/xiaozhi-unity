using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public abstract class ColourModifier : UIBehaviour
    {
        [NonSerialized] private Graphic _graphic;

        [SerializeField] private Colour _colour;

        protected override void Awake()
        {
            base.Awake();
            _graphic = GetComponent<Graphic>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _graphic.SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _graphic.SetVerticesDirty();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            _graphic.SetVerticesDirty();
            base.OnDidApplyAnimationProperties();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _graphic.SetVerticesDirty();
        }

#endif

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            if (baseMaterial == null) return null;
            baseMaterial.color = ThemeManager.FetchColor(ThemeManager.Theme, _colour);
            return baseMaterial;
        }
    }
}