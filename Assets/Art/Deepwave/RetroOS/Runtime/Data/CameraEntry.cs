using System;
using UnityEngine;
using UnityEngine.Video;

namespace Deepwave.RetroOS.Data
{
    // A CCTV channel: live feed (deviceId), recorded tape (recording), still
    // image (staticImage), and optional DVR buffering. All empty = NO SIGNAL.
    [Serializable]
    public class CameraEntry
    {
        public string label = "CAM 01";

        [Tooltip("Id of a CctvCameraDevice component placed on a scene camera. " +
                 "Set this to wire the channel to a real camera in the scene.")]
        public string deviceId;

        [Tooltip("Recorded tape reviewed in PLAYBACK mode. Optional - drop a " +
                 "VideoClip (.mp4/.webm) here to make the channel playable back.")]
        public VideoClip recording;

        [Tooltip("Still image shown when the channel has no live signal.")]
        public Texture staticImage;

        [Tooltip("Record the live feed into an in-engine buffer while it is being " +
                 "watched, so PLAYBACK can rewind it even with no recorded tape.")]
        public bool allowDvr = true;
    }
}
