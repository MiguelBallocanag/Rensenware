using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS.Cctv
{
    // Signal source for the CCTV app: put this on a scene Camera and reference its deviceId from a channel. Renders only while someone is watching.
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Deepwave/RetroOS/CCTV Camera Device")]
    public class CctvCameraDevice : MonoBehaviour
    {
        [Tooltip("Id a channel on the CCTV program points at with its Device Id.")]
        public string deviceId = "cam-01";

        [Tooltip("On-screen name when no channel on the CCTV program names it.")]
        public string label = "CAM 01";

        [Header("Pan (degrees around local Y)")]
        public float panRange = 60f;
        public float panSpeed = 30f;

        [Header("Zoom (field of view)")]
        public float fovMin = 25f;
        public float fovMax = 60f;
        public float zoomSpeed = 30f;

        static readonly List<CctvCameraDevice> Registry = new();
        public static IReadOnlyList<CctvCameraDevice> All => Registry;

        public static CctvCameraDevice Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            foreach (var device in Registry)
                if (string.Equals(device.deviceId, id.Trim(), StringComparison.OrdinalIgnoreCase))
                    return device;
            return null;
        }

        Camera _camera;
        Quaternion _baseRotation;
        float _pan;

        // Who is watching and what they see. The device owns both - a wall camera is one thing however many monitors point at it.
        readonly List<Component> _viewers = new();
        RenderTexture _feed;

        public Camera Camera => _camera ? _camera : _camera = GetComponent<Camera>();

        /// <summary>How many monitors are watching this camera.</summary>
        public int ViewerCount => _viewers.Count;

        /// <summary>Whoever is steering the mount, or <c>null</c>. The first monitor to tune in gets it; the rest watch what it does.</summary>
        public Component Controller => _viewers.Count > 0 ? _viewers[0] : null;

        // How far zoomed in, 1 = wide (used for the on-screen ZOOM x readout).
        public float ZoomFactor => fovMax / Mathf.Max(1f, Camera.fieldOfView);

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _baseRotation = transform.localRotation;
            _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView, fovMin, fovMax);
            _camera.enabled = false;
        }

        void OnEnable() => Registry.Add(this);

        void OnDisable()
        {
            Registry.Remove(this);
            _viewers.Clear();
            Stop();
        }

        /// <summary>Starts watching, and hands back the picture to show.</summary>
        /// <param name="viewer">The monitor tuning in.</param>
        /// <returns>The shared picture. Do not destroy it.</returns>
        public RenderTexture Watch(Component viewer)
        {
            if (!viewer) return null;
            if (!_viewers.Contains(viewer)) _viewers.Add(viewer);

            if (!_feed)
            {
                _feed = new RenderTexture(640, 480, 16) { name = deviceId + " feed" };
                _feed.Create();
            }

            Camera.targetTexture = _feed;
            Camera.enabled = true;
            return _feed;
        }

        /// <summary>Stops watching. The camera runs until the last one leaves.</summary>
        public void StopWatching(Component viewer)
        {
            _viewers.Remove(viewer);
            _viewers.RemoveAll(v => !v);
            if (_viewers.Count == 0) Stop();
        }

        void Stop()
        {
            if (_camera)
            {
                _camera.targetTexture = null;
                _camera.enabled = false;
            }

            if (!_feed) return;
            _feed.Release();
            Destroy(_feed);
            _feed = null;
        }

        void OnDestroy() => Stop();

        /// <summary>Whether this monitor is the one allowed to move the mount.</summary>
        public bool HasControl(Component viewer) => viewer && Controller == viewer;

        // direction -1 = left, +1 = right; clamped to the mount's range.
        public bool Pan(Component viewer, float direction, float deltaTime)
        {
            if (!HasControl(viewer)) return false;

            _pan = Mathf.Clamp(_pan + direction * panSpeed * deltaTime, -panRange, panRange);
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, _pan, 0f);
            return true;
        }

        // direction +1 = zoom in (narrower FOV), -1 = out.
        public bool Zoom(Component viewer, float direction, float deltaTime)
        {
            if (!HasControl(viewer)) return false;

            Camera.fieldOfView = Mathf.Clamp(
                Camera.fieldOfView - direction * zoomSpeed * deltaTime, fovMin, fovMax);
            return true;
        }
    }
}
