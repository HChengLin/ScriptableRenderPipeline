using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;

namespace UnityEditor.Rendering.LookDev
{
    internal class CameraController
    { 
        private float m_StartZoom = 0.0f;
        private float m_ZoomSpeed = 0.0f;
        private float m_TotalMotion = 0.0f;
        private Vector3 m_Motion = new Vector3();
        private float m_FlySpeed = 0;
        const float kFlyAcceleration = 1.1f;
        //private readonly CameraFlyModeContext m_CameraFlyModeContext = new CameraFlyModeContext();
        ViewTool m_BehaviorState;
        static TimeHelper s_Timer = new TimeHelper();

        //[TODO]
        private void ResetCameraControl()
        {
            m_BehaviorState = ViewTool.None;
            m_Motion = Vector3.zero;
        }

        private void HandleCameraScrollWheel(CameraState cameraState)
        {
            float zoomDelta = Event.current.delta.y;

            float relativeDelta = Mathf.Abs(cameraState.viewSize) * zoomDelta * .015f;
            const float deltaCutoff = .3f;
            if (relativeDelta > 0 && relativeDelta < deltaCutoff)
                relativeDelta = deltaCutoff;
            else if (relativeDelta < 0 && relativeDelta > -deltaCutoff)
                relativeDelta = -deltaCutoff;

            cameraState.viewSize += relativeDelta;
            Event.current.Use();
        }

        private void OrbitCameraBehavior(CameraState cameraState)
        {
            Event evt = Event.current;

            //cameraState.FixNegativeSize();
            Quaternion rotation = cameraState.rotationTarget;
            rotation = Quaternion.AngleAxis(evt.delta.y * .003f * Mathf.Rad2Deg, rotation * Vector3.right) * rotation;
            rotation = Quaternion.AngleAxis(evt.delta.x * .003f * Mathf.Rad2Deg, Vector3.up) * rotation;
            //if (cameraState.viewSize < 0)
            //{
            //    cameraState.pivot = cam.transform.position;
            //    cameraState.viewSize = 0;
            //}
            cameraState.rotation = rotation;
        }

        private void HandleCameraMouseDrag(CameraState cameraState, Rect screen)
        {
            Event evt = Event.current;
            switch (m_BehaviorState)
            {
                case ViewTool.Orbit:
                    OrbitCameraBehavior(cameraState);
                    break;
                case ViewTool.FPS:
                    Vector3 camPos = cameraState.pivot - cameraState.rotation * Vector3.forward * cameraState.distanceFromPivot;

                    // Normal FPS camera behavior
                    Quaternion rotation = cameraState.rotation;
                    rotation = Quaternion.AngleAxis(evt.delta.y * .003f * Mathf.Rad2Deg, rotation * Vector3.right) * rotation;
                    rotation = Quaternion.AngleAxis(evt.delta.x * .003f * Mathf.Rad2Deg, Vector3.up) * rotation;
                    cameraState.rotation = rotation;
                    cameraState.pivot = camPos + rotation * Vector3.forward * cameraState.distanceFromPivot;
                    break;
                case ViewTool.Pan:
                    //cameraState.FixNegativeSize();
                    var screenPos = cameraState.WorldToScreenPoint(screen, cameraState.pivot);
                    screenPos += new Vector3(-Event.current.delta.x, Event.current.delta.y, 0);
                    Vector3 worldDelta = cameraState.ScreenToWorldPoint(screen, screenPos) - cameraState.pivot;
                    if (evt.shift)
                        worldDelta *= 4;
                    cameraState.pivot += worldDelta;
                    break;
                case ViewTool.Zoom:
                    float zoomDelta = HandleUtility.niceMouseDeltaZoom * (evt.shift ? 9 : 3);
                    m_TotalMotion += zoomDelta;
                    if (m_TotalMotion < 0)
                        cameraState.viewSize = m_StartZoom * (1 + m_TotalMotion * .001f);
                    else
                        cameraState.viewSize = cameraState.viewSize + zoomDelta * m_ZoomSpeed * .003f;
                    break;

                default:
                    break;
            }
            evt.Use();
        }

        private void HandleCameraKeyDown()
        {
            if (Event.current.keyCode == KeyCode.Escape)
            {
                ResetCameraControl();
            }
        }

        private void HandleCameraMouseUp()
        {
            ResetCameraControl();
            Event.current.Use();
        }

        private Vector3 GetMovementDirection()
        {
            if (m_Motion.sqrMagnitude == 0)
            {
                s_Timer.Begin();
                m_FlySpeed = 0;
                return Vector3.zero;
            }
            else
            {
                var deltaTime = s_Timer.Update();
                float speed = Event.current.shift ? 5 : 1;
                if (m_FlySpeed == 0)
                    m_FlySpeed = 9;
                else
                    m_FlySpeed = m_FlySpeed * Mathf.Pow(kFlyAcceleration, deltaTime);
                return m_Motion.normalized * m_FlySpeed * speed * deltaTime;
            }
        }

        public void Update(CameraState cameraState)
        {
            Event evt = Event.current;

            if (evt.type == EventType.MouseUp)
            {
                m_BehaviorState = ViewTool.None;
            }

            if (evt.type == EventType.MouseDown)
            {
                int button = evt.button;

                bool controlKeyOnMac = (evt.control && Application.platform == RuntimePlatform.OSXEditor);

                var rightMouseButton = false;
                if (button == 2)
                {
                    m_BehaviorState = ViewTool.Pan;
                }
                else if ((button <= 0 && controlKeyOnMac)
                         || (button == 1 && evt.alt))
                {
                    m_BehaviorState = ViewTool.Zoom;

                    m_StartZoom = cameraState.viewSize;
                    m_ZoomSpeed = Mathf.Max(Mathf.Abs(m_StartZoom), .3f);
                    m_TotalMotion = 0;
                    rightMouseButton = button == 1;
                }
                else if (button <= 0)
                {
                    m_BehaviorState = ViewTool.Orbit;
                }
                else if (button == 1 && !evt.alt)
                {
                    m_BehaviorState = ViewTool.FPS;
                    rightMouseButton = true;
                }

                // see also SceneView.HandleClickAndDragToFocus()
                //if (rightMouseButton && Application.platform == RuntimePlatform.OSXEditor)
                //    window.Focus();
            }

            //var id = GUIUtility.GetControlID(FocusType.Passive);
            //using (var inputSamplingScope = new CameraFlyModeContext.InputSamplingScope(m_CameraFlyModeContext, m_CurrentViewTool, id, window))
            //{
            //    if (inputSamplingScope.inputVectorChanged)
            //        m_FlySpeed = 0;
            //    m_Motion = inputSamplingScope.currentInputVector;
            //}

            switch (evt.type)
            {
                case EventType.ScrollWheel: HandleCameraScrollWheel(cameraState); break;
                case EventType.MouseUp: HandleCameraMouseUp(); break;
                case EventType.MouseDrag: HandleCameraMouseDrag(cameraState); break;
                case EventType.KeyDown: HandleCameraKeyDown(); break;
                case EventType.Layout:
                    {
                        Vector3 motion = GetMovementDirection();
                        if (motion.sqrMagnitude != 0)
                        {
                            cameraState.pivot = cameraState.pivot + cameraState.rotation * motion;
                        }
                    }
                    break;
            }
        }
        
    }

    //[TODO: check to reuse legacy internal one]
    struct TimeHelper
    {
        public float deltaTime;
        long lastTime;

        public void Begin()
        {
            lastTime = System.DateTime.Now.Ticks;
        }

        public float Update()
        {
            deltaTime = (System.DateTime.Now.Ticks - lastTime) / 10000000.0f;
            lastTime = System.DateTime.Now.Ticks;
            return deltaTime;
        }
    }
}
