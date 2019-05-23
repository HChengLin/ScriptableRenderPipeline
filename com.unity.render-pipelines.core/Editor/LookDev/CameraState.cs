using UnityEditor.AnimatedValues;
using UnityEngine;

namespace UnityEditor.Rendering.LookDev
{
    public interface ICameraUpdater
    {
        void UpdateCamera(Camera camera);
    }

    [System.Serializable]
    public class CameraState : ICameraUpdater
    {
        private static readonly Quaternion kDefaultRotation = Quaternion.LookRotation(new Vector3(0.0f, 0.0f, 1.0f));
        private const float kDefaultViewSize = 10f;
        private static readonly Vector3 kDefaultPivot = Vector3.zero;
        private const float kDefaultFoV = 90f;
        private static readonly float distanceCoef = 1f / Mathf.Tan(kDefaultFoV * 0.5f * Mathf.Deg2Rad);
        private const float kNearFactor = 0.000005f;
        private const float kMaxFar = 1000;

        //update camera on first frame after deserialization
        private bool m_HasUnpushedChange = true;

        //Note: we need animation to do the same focus as in SceneView
        private AnimVector3 m_Pivot = new AnimVector3(kDefaultPivot);
        private AnimQuaternion m_Rotation = new AnimQuaternion(kDefaultRotation);
        private AnimFloat m_ViewSize = new AnimFloat(kDefaultViewSize);

        [field: SerializeField]
        public Vector3 pivot
        {
            get => m_Pivot.value;
            set
            {
                m_Pivot.value = value;
                m_HasUnpushedChange = true;
            }
        }
        public Vector3 pivotTarget => m_Pivot.target;
        [field: SerializeField]
        public Quaternion rotation
        {
            get => m_Rotation.value;
            set
            {
                m_Rotation.value = value;
                m_HasUnpushedChange = true;
            }
        }
        public Quaternion rotationTarget => m_Rotation.target;
        [field: SerializeField]
        public float viewSize
        {
            get => m_ViewSize.value;
            set
            {
                m_ViewSize.value = Mathf.Max(value, 0f);
                m_HasUnpushedChange = true;
            }
        }
        public float viewSizeTarget => m_ViewSize.target;

        public float distanceFromPivot => viewSize * distanceCoef;
        public Vector3 position
            => pivot + rotation * new Vector3(0, 0, -distanceFromPivot);
        public float fieldOfView => kDefaultFoV;
        public float farClip => Mathf.Max(kMaxFar, 2 * kMaxFar * viewSize);
        public float nearClip => farClip * kNearFactor;
        public Vector3 forward => rotation * Vector3.forward;

        internal Matrix4x4 GetProjectionMatrix(float aspect)
            => Matrix4x4.Perspective(fieldOfView, aspect, nearClip, farClip);

        internal Matrix4x4 worldToCameraMatrix
            => Matrix4x4.TRS(position, rotation, Vector3.one);

        internal Matrix4x4 GetWorldToClipMatrix(float aspect)
            => GetProjectionMatrix(aspect) * worldToCameraMatrix;

        internal Vector3 ScreenToWorldPoint(Rect screen, Vector3 screenPoint)
        {
            //check right/left handed camera
            Matrix4x4 worldToCamera = worldToCameraMatrix;
            Matrix4x4 cameraToWorld = worldToCamera.inverse;
            float aspect = screen.height == 0 ? 0f : screen.width / screen.height;
            Matrix4x4 clipToWorld = (GetProjectionMatrix(aspect) * worldToCamera).inverse;
            Vector3 normalizedScreenPoint = new Vector3(
                (screenPoint.x - screen.x) * 2f / Screen.width - 1f,
                (screenPoint.y - screen.y) * 2f / Screen.height - 1f,
                0.95f);
            Vector3 pointOnPlane = clipToWorld * normalizedScreenPoint;
            Vector3 camPosition = position;
            Vector3 dir = pointOnPlane - camPosition;
            float distanceToPlane = Vector3.Dot(dir, forward);

            //distanceToPlane should ony be null if we projected at 90deg (ie near infinit)
            dir *= screenPoint.z / distanceToPlane;
            return camPosition + dir;
        }

        internal Vector3 WorldToScreenPoint(Rect screen, Vector3 worldPoint)
        {
            float aspect = screen.height == 0 ? 0f : screen.width / screen.height;
            Matrix4x4 worldToClip = GetProjectionMatrix(aspect) * worldToCameraMatrix;
            Vector3 clipPoint = worldToClip.MultiplyPoint(worldPoint);
            Vector3 dir = worldPoint - position;
            float dist = Vector3.Dot(dir, forward);

            return new Vector3(
                screen.x + (1f + clipPoint.x) * screen.width * 0.5f,
                screen.y + (1f + clipPoint.y) * screen.height * 0.5f,
                dist);
        }

        public void UpdateCamera(Camera camera)
        {
            if (!m_HasUnpushedChange)
                return;

            camera.transform.rotation = rotation;
            camera.transform.position = position;
            camera.nearClipPlane = nearClip;
            camera.farClipPlane = farClip;
            camera.fieldOfView = fieldOfView;

            m_HasUnpushedChange = false;
        }

        public void Reset()
        {
            m_Pivot = new AnimVector3(kDefaultPivot);
            m_Rotation = new AnimQuaternion(kDefaultRotation);
            m_ViewSize = new AnimFloat(kDefaultViewSize);
            m_HasUnpushedChange = true;
        }

        //[TODO: check why needed]
        //public void FixNegativeSize()
        //{
        //    if (viewSize < 0)
        //    {
        //        float distance = viewSize / Mathf.Tan(kDefaultFoV * 0.5f * Mathf.Deg2Rad);
        //        Vector3 p = pivot + rotation * new Vector3(0, 0, -distance);
        //        viewSize = -viewSize;
        //        distance = viewSize / Mathf.Tan(kDefaultFoV * 0.5f * Mathf.Deg2Rad);
        //        pivot = p + rotation * new Vector3(0, 0, distance);
        //    }
        //}

        internal void SynchronizeFrom(CameraState other)
        {
            m_HasUnpushedChange = other.m_HasUnpushedChange;
            m_Pivot.value = other.m_Pivot.value;
            m_Pivot.target = other.m_Pivot.target;
            m_Rotation.value = other.m_Rotation.value;
            m_Rotation.target = other.m_Rotation.target;
            m_ViewSize.value = other.m_ViewSize.value;
            m_ViewSize.target = other.m_ViewSize.target;
        }
    }
}
