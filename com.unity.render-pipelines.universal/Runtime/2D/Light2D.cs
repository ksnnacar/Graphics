using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor.Experimental.SceneManagement;
#endif

namespace UnityEngine.Experimental.Rendering.Universal
{
    /// <summary>
    /// Class <c>Light2D</c> is a 2D light which can be used with the 2D Renderer.
    /// </summary>
    ///
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Rendering/2D/Light 2D (Experimental)")]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/2DLightProperties.html")]
    public sealed partial class Light2D : MonoBehaviour
    {
        /// <summary>
        /// an enumeration of the types of light
        /// </summary>

        public enum LightType
        {
            Parametric = 0,
            Freeform = 1,
            Sprite = 2,
            Point = 3,
            Global = 4
        }

#if USING_ANIMATION_MODULE
        [UnityEngine.Animations.NotKeyable]
#endif
        [SerializeField]
        LightType m_LightType = LightType.Parametric;
        LightType m_PreviousLightType = (LightType)LightType.Parametric;

        [SerializeField, FormerlySerializedAs("m_LightOperationIndex")]
        int m_BlendStyleIndex = 0;


        [SerializeField]
        float m_FalloffIntensity = 0.5f;

        [ColorUsage(false)]
        [SerializeField]
        Color m_Color = Color.white;
        [SerializeField]
        float m_Intensity = 1;

        [SerializeField] float m_LightVolumeOpacity = 0.0f;
        [SerializeField] int[] m_ApplyToSortingLayers = new int[1];     // These are sorting layer IDs. If we need to update this at runtime make sure we add code to update global lights
        [SerializeField] Sprite m_LightCookieSprite = null;
        [SerializeField] bool m_UseNormalMap = false;

        [SerializeField] int m_LightOrder = 0;
        [SerializeField] bool m_AlphaBlendOnOverlap = false;

        // int m_PreviousLightOrder = -1;
        // int m_PreviousBlendStyleIndex;
        float       m_PreviousLightVolumeOpacity;
        bool        m_PreviousLightCookieSpriteExists = false;
        Sprite      m_PreviousLightCookieSprite     = null;
        Mesh        m_Mesh;
        Bounds      m_LocalBounds;


        [Range(0,1)]
        [SerializeField] float m_ShadowIntensity    = 0.0f;
        [Range(0,1)]
        [SerializeField] float m_ShadowVolumeIntensity = 0.0f;

        /// <summary>
        /// The lights current type
        /// </summary>
        public LightType lightType
        {
            get => m_LightType;
            set
            {
                m_LightType = value;
                ErrorIfDuplicateGlobalLight();
            }
        }

        /// <summary>
        /// The lights current operation index
        /// </summary>
        public int blendStyleIndex { get => m_BlendStyleIndex; set => m_BlendStyleIndex = value; }

        /// <summary>
        /// Specifies the darkness of the shadow
        /// </summary>
        public float shadowIntensity { get => m_ShadowIntensity; set => m_ShadowIntensity = Mathf.Clamp01(value); }

        /// <summary>
        /// Specifies the darkness of the shadow
        /// </summary>
        public float shadowVolumeIntensity { get => m_ShadowVolumeIntensity; set => m_ShadowVolumeIntensity = Mathf.Clamp01(value); }


        /// <summary>
        /// The lights current color
        /// </summary>
        public Color color
        {
            get { return m_Color; }
            set { m_Color = value; }
        }

        /// <summary>
        /// The lights current intensity
        /// </summary>
        public float intensity
        {
            get { return m_Intensity; }
            set { m_Intensity = value; }
        }

        /// <summary>
        /// The lights current intensity
        /// </summary>
        public float volumeOpacity => m_LightVolumeOpacity;
        public Sprite lightCookieSprite => m_LightCookieSprite;
        public float falloffIntensity => m_FalloffIntensity;
        public bool useNormalMap => m_UseNormalMap;
        public bool alphaBlendOnOverlap => m_AlphaBlendOnOverlap;
        public int lightOrder { get => m_LightOrder; set => m_LightOrder = value; }

        static SortingLayer[] s_SortingLayers;

        internal int GetTopMostLitLayer()
        {
            int largestIndex = -1;
            int largestLayer = 0;

            SortingLayer[] layers;
            if (Application.isPlaying)
            {
                if (s_SortingLayers == null)
                    s_SortingLayers = SortingLayer.layers;

                layers = s_SortingLayers;
            }
            else
                layers = SortingLayer.layers;


            for (int i = 0; i < m_ApplyToSortingLayers.Length; ++i)
            {
                for(int layer = layers.Length - 1; layer >= largestLayer; --layer)
                {
                    if (layers[layer].id == m_ApplyToSortingLayers[i])
                    {
                        largestIndex = i;
                        largestLayer = layer;
                    }
                }
            }

            if (largestIndex >= 0)
                return m_ApplyToSortingLayers[largestIndex];
            else
                return -1;
        }

        void UpdateMesh()
        {
            GetMesh(true);
        }

        internal bool IsLitLayer(int layer)
        {
            return m_ApplyToSortingLayers != null ? Array.IndexOf(m_ApplyToSortingLayers, layer) >= 0 : false;
        }

        internal BoundingSphere GetBoundingSphere()
        {
            return IsShapeLight() ? GetShapeLightBoundingSphere() : GetPointLightBoundingSphere();
        }

        internal Mesh GetMesh(bool forceUpdate = false)
        {
            if (m_Mesh != null && !forceUpdate)
                return m_Mesh;

            if (m_Mesh == null)
                m_Mesh = new Mesh();

            Color combinedColor = m_Intensity * m_Color;
            switch (m_LightType)
            {
                case LightType.Freeform:
                    m_LocalBounds = LightUtility.GenerateShapeMesh(ref m_Mesh, m_ShapePath, m_ShapeLightFalloffSize);
                    break;
                case LightType.Parametric:
                    m_LocalBounds = LightUtility.GenerateParametricMesh(ref m_Mesh, m_ShapeLightParametricRadius, m_ShapeLightFalloffSize, m_ShapeLightParametricAngleOffset, m_ShapeLightParametricSides);
                    break;
                case LightType.Sprite:
                    m_Mesh.Clear();
                    m_LocalBounds = LightUtility.GenerateSpriteMesh(ref m_Mesh, m_LightCookieSprite, 1);
                    break;
                case LightType.Point:
                    m_LocalBounds = LightUtility.GenerateParametricMesh(ref m_Mesh, 1.412135f, 0, 0, 4);
                    break;
            }

            return m_Mesh;
        }

        internal void ErrorIfDuplicateGlobalLight()
        {
            for (int i = 0; i < m_ApplyToSortingLayers.Length; ++i)
            {
                int sortingLayer = m_ApplyToSortingLayers[i];

                if(Light2DManager.ContainsDuplicateGlobalLight(sortingLayer, blendStyleIndex))
                    Debug.LogError("More than one global light on layer " + SortingLayer.IDToName(sortingLayer) + " for light blend style index " + m_BlendStyleIndex);
            }
        }

        private void Awake()
        {
            GetMesh();
        }

        void OnEnable()
        {
            if (m_LightType == LightType.Global)
                ErrorIfDuplicateGlobalLight();

            m_PreviousLightType = m_LightType;

            Light2DManager.RegisterLight(this);
        }

        private void OnDisable()
        {
            Light2DManager.DeregisterLight(this);
        }

        internal List<Vector2> GetFalloffShape()
        {
            List<Vector2> shape = new List<Vector2>();
            List<Vector2> extrusionDir = new List<Vector2>();
            LightUtility.GetFalloffShape(m_ShapePath, ref extrusionDir);
            for (int i = 0; i < m_ShapePath.Length; i++)
            {
                Vector2 position = new Vector2();
                position.x = m_ShapePath[i].x + this.shapeLightFalloffSize * extrusionDir[i].x;
                position.y = m_ShapePath[i].y + this.shapeLightFalloffSize * extrusionDir[i].y;
                shape.Add(position);
            }
            return shape;
        }

        private void LateUpdate()
        {
            var rebuildMesh = false;

            // Mesh Rebuilding
            rebuildMesh |= LightUtility.CheckForChange(m_ShapeLightFalloffSize, ref m_PreviousShapeLightFalloffSize);
            rebuildMesh |= LightUtility.CheckForChange(m_ShapeLightParametricRadius, ref m_PreviousShapeLightParametricRadius);
            rebuildMesh |= LightUtility.CheckForChange(m_ShapeLightParametricSides, ref m_PreviousShapeLightParametricSides);
            rebuildMesh |= LightUtility.CheckForChange(m_LightVolumeOpacity, ref m_PreviousLightVolumeOpacity);
            rebuildMesh |= LightUtility.CheckForChange(m_ShapeLightParametricAngleOffset, ref m_PreviousShapeLightParametricAngleOffset);
            rebuildMesh |= LightUtility.CheckForChange(m_LightCookieSprite != null, ref m_PreviousLightCookieSpriteExists);
            rebuildMesh |= LightUtility.CheckForChange(m_LightCookieSprite, ref m_PreviousLightCookieSprite);
            rebuildMesh |= LightUtility.CheckForChange(m_ShapeLightFalloffOffset, ref m_PreviousShapeLightFalloffOffset);

#if UNITY_EDITOR
            rebuildMesh |= LightUtility.CheckForChange(LightUtility.GetShapePathHash(m_ShapePath), ref m_PreviousShapePathHash);
#endif
            if(rebuildMesh && m_LightType != LightType.Global)
                UpdateMesh();
        }
    }
}
