using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Apps;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Samples
{
    // Example custom app: subclass RetroAppBehaviour, add it to a child of the bootstrap,
    // set icon/window/placement in the inspector. BuildContent is your app - RetroUi widgets,
    // a prefab, sounds, events; anything it raises appears on this component's UnityEvents.
    [AddComponentMenu("")]
    public class ClockApp : RetroAppBehaviour
    {
        [Header("Clock")]
        [Tooltip("Play a chime automatically on the hour.")]
        [SerializeField] bool chimeOnTheHour;

        public override void BuildContent(AppContext ctx)
        {
            RetroUi.VLayout(ctx.Content.gameObject, 6, 8);

            var lcdPanel = RetroUi.Panel(ctx.Content, RetroSkin.Screen, "Lcd");
            RetroUi.Flexible(lcdPanel);
            RetroUi.Bevel(lcdPanel.rectTransform, false, 1);

            var time = RetroUi.Label(lcdPanel.transform, "", 42, TextAnchor.MiddleCenter, RetroSkin.Mono);
            time.color = RetroSkin.ScreenText;
            RetroUi.Stretch(time.rectTransform);
            time.rectTransform.offsetMax = new Vector2(0, -8);

            var date = RetroUi.Label(lcdPanel.transform, "", 14, TextAnchor.LowerCenter, RetroSkin.Mono);
            date.color = RetroSkin.ScreenText;
            RetroUi.Stretch(date.rectTransform, 8);

            var ticker = lcdPanel.gameObject.AddComponent<ClockTicker>();
            ticker.Init(ctx, time, date, chimeOnTheHour);

            var row = RetroUi.NewRect("Controls", ctx.Content);
            RetroUi.Size(row, h: 28);
            RetroUi.HLayout(row.gameObject, 6, 0, true);
            RetroUi.PushButton(row, "Chime", () => Chime(ctx));
        }

        // One call raises the app's own event, fires this component's
        // UnityEvents, and puts "clock:chime" on the generic app.action id.
        internal static void Chime(AppContext ctx)
        {
            ctx.Os.Audio.PlayUi(UiSfx.Notify);
            ctx.Raise("chime", ctx.Os.Clock.Now.ToString("HH:mm"));
        }

        public override void ApplyDefaults()
        {
            app.appId = "clock";
            app.title = "Clock";
            app.builtinIcon = BuiltinIcon.Clock;
            app.windowSize = new Vector2(320, 210);
            app.minWindowSize = new Vector2(260, 180);

            // Groups it under Start > Programs > Accessories; empty lists it directly under Programs.
            app.category = "Accessories";
        }
    }

    // Views that need per-frame logic are plain MonoBehaviours on the content.
    public class ClockTicker : MonoBehaviour
    {
        AppContext _ctx;
        Text _time, _date;
        bool _chimeOnTheHour;
        int _lastHour = -1;
        int _shownSecond = -1;
        int _shownDay = -1;

        public void Init(AppContext ctx, Text time, Text date, bool chimeOnTheHour)
        {
            _ctx = ctx;
            _time = time;
            _date = date;
            _chimeOnTheHour = chimeOnTheHour;
        }

        void Update()
        {
            if (_ctx?.Os == null) return;

            var now = _ctx.Os.Clock.Now;

            // Build a string only when the visible value changes - formatting every frame allocates.
            var second = now.Hour * 3600 + now.Minute * 60 + now.Second;
            if (second != _shownSecond)
            {
                _shownSecond = second;
                _time.text = now.ToString("HH:mm:ss");
            }

            if (now.DayOfYear != _shownDay)
            {
                _shownDay = now.DayOfYear;
                _date.text = now.ToString("ddd, yyyy-MM-dd");
            }

            if (!_chimeOnTheHour) { _lastHour = now.Hour; return; }
            if (_lastHour < 0) { _lastHour = now.Hour; return; }
            if (now.Hour == _lastHour) return;

            _lastHour = now.Hour;
            ClockApp.Chime(_ctx);
        }
    }
}
