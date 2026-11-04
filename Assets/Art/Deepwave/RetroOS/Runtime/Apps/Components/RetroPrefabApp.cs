using UnityEngine;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Implemented by components on a RetroPrefabApp prefab that want the OS
    // context when the app opens.
    public interface IRetroAppView
    {
        void Init(AppContext ctx);
    }

    // A program with no code: a uGUI prefab for the window contents, icon and size in
    // the inspector, game events on this component. A prefab component implementing
    // IRetroAppView gets the AppContext on open.
    [AddComponentMenu("")]
    public class RetroPrefabApp : RetroAppBehaviour
    {
        [Header("Content")]
        [Tooltip("uGUI prefab instantiated inside the window. Components " +
                 "implementing IRetroAppView receive the context.")]
        [SerializeField] GameObject contentPrefab;

        public GameObject ContentPrefab
        {
            get => contentPrefab;
            set => contentPrefab = value;
        }

        public override void BuildContent(AppContext ctx)
        {
            if (!contentPrefab)
            {
                var label = RetroUi.Label(ctx.Content, "No content prefab assigned.", 14,
                    TextAnchor.MiddleCenter);
                RetroUi.Stretch(label.rectTransform);
                return;
            }

            var instance = Instantiate(contentPrefab, ctx.Content);
            if (instance.transform is RectTransform rt) RetroUi.Stretch(rt);

            foreach (var view in instance.GetComponentsInChildren<IRetroAppView>(true))
                view.Init(ctx);
        }

        public override void ApplyDefaults()
        {
            app.builtinIcon = BuiltinIcon.Computer;
        }
    }
}
