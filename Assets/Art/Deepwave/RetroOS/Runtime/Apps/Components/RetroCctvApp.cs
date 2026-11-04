using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    [AddComponentMenu("")]
    public class RetroCctvApp : RetroAppBehaviour, ICameraSource
    {
        [Header("Cameras")]
        [Tooltip("Channels shown by this monitor. Each can be a live scene camera " +
                 "(deviceId), a recorded tape or a still.")]
        [SerializeField] List<CameraEntry> cameras = new();

        public List<CameraEntry> Cameras => cameras;

        public override void BuildContent(AppContext ctx) => AppView.Attach<CctvView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "cctv";
            app.title = "CCTV";
            app.builtinIcon = BuiltinIcon.Camera;
            app.windowSize = new Vector2(640, 520);
            app.minWindowSize = new Vector2(420, 340);
        }
    }
}
