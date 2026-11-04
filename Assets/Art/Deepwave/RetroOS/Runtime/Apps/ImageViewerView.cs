using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Single-image viewer on a sunken dark surface, with the zoom, fit and
    // rotate an imaging program of the era had.
    public class ImageViewerView : AppView
    {
        const float MinZoom = 0.1f;
        const float MaxZoom = 8f;

        Sprite _sprite;
        RectTransform _viewport;
        RectTransform _picture;
        RetroStatusStrip _status;
        float _zoom = 1f;
        int _rotation;          // quarter turns, clockwise
        bool _fit = true;

        protected override void Build()
        {
            _sprite = Context.File != null &&
                      Context.File.TryLoad(Os.CurrentUser, out var picture)
                ? picture as Sprite : null;

            RetroUi.VLayout(Root.gameObject, 2, 2);
            BuildMenu();
            BuildToolbar();
            BuildSurface();

            _status = RetroUi.StatusStrip(Root, -1f, 80f, 70f);
            Apply();
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("Close", () => Window.Close(), "Alt+F4");

            menu.AddMenu("View")
                .Command("Zoom In", () => Zoom(1.25f), "+")
                .Command("Zoom Out", () => Zoom(0.8f), "-")
                .Separator()
                .Command("Actual Size", ActualSize, "Ctrl+1")
                .Check("Fit to Window", _fit, ToggleFit);

            menu.AddMenu("Image")
                .Command("Rotate Left", () => Rotate(-1))
                .Command("Rotate Right", () => Rotate(1))
                .Separator()
                .Command("Reset", Reset);

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "Image Viewer",
                "Shows one picture. Zoom with the toolbar, drag it around when " +
                "it is larger than the window."));
        }

        void BuildToolbar()
        {
            var bar = RetroUi.ToolStrip(Root);
            bar.Add(RetroIcons.Get(BuiltinIcon.Search), "Zoom in", () => Zoom(1.25f), "+");
            bar.Add(RetroIcons.Get(BuiltinIcon.Search), "Zoom out", () => Zoom(0.8f), "-");
            bar.AddSeparator();
            bar.Add(RetroIcons.Get(BuiltinIcon.Monitor), "Fit to window", () => SetFit(true), "Fit");
            bar.Add(RetroIcons.Get(BuiltinIcon.Photos), "Actual size", ActualSize, "1:1");
            bar.AddSeparator();
            bar.Add(RetroIcons.Get(BuiltinIcon.Refresh), "Rotate right", () => Rotate(1), "Turn");
        }

        void BuildSurface()
        {
            var bg = RetroUi.Panel(Root, PaletteSlot.Screen, "Surface");
            RetroUi.Flexible(bg);
            RetroUi.Bevel(bg.rectTransform, false, 1);

            // A viewport that clips, so a zoomed picture stays inside the frame
            // instead of spilling over the window chrome.
            _viewport = RetroUi.Stretch(RetroUi.NewRect("Viewport", bg.transform), 3f);
            _viewport.gameObject.AddComponent<RectMask2D>();

            if (!_sprite)
            {
                var label = RetroUi.Label(_viewport, "Unable to display image.", 14, TextAnchor.MiddleCenter);
                RetroPalette.Paint(label, PaletteSlot.FaceLight);
                RetroUi.Stretch(label.rectTransform);
                return;
            }

            var picture = RetroUi.Picture(_viewport, _sprite);
            _picture = picture.rectTransform;
            _picture.anchorMin = _picture.anchorMax = _picture.pivot = new Vector2(0.5f, 0.5f);

            // Panning is the same grab-and-move as a window title bar, bounded
            // by the viewport rather than the desktop.
            var drag = picture.gameObject.AddComponent<Windowing.WindowDragger>();
            drag.dragObject = _picture;
            drag.dragArea = _viewport;
            drag.canDrag = Pannable;
            drag.onDoubleClick = ToggleFit;
        }

        // Only worth dragging when the picture is bigger than what shows it.
        bool Pannable() =>
            _picture && (_picture.rect.width > _viewport.rect.width ||
                         _picture.rect.height > _viewport.rect.height);

        void Zoom(float factor)
        {
            _fit = false;
            _zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
            Apply();
        }

        void ActualSize()
        {
            _fit = false;
            _zoom = 1f;
            Apply();
        }

        void ToggleFit() => SetFit(!_fit);

        void SetFit(bool on)
        {
            _fit = on;
            Apply();
        }

        void Rotate(int quarters)
        {
            _rotation = ((_rotation + quarters) % 4 + 4) % 4;
            Apply();
        }

        void Reset()
        {
            _rotation = 0;
            _fit = true;
            _zoom = 1f;
            Apply();
        }

        void Apply()
        {
            if (!_picture || !_sprite)
            {
                _status?.Set(0, Context.File?.Name ?? "No image");
                return;
            }

            var natural = _sprite.rect.size;

            // A quarter turn swaps which side has to fit which, so the fit
            // scale is measured against the rotated picture, not the raw one.
            var turned = _rotation % 2 == 1;
            var shown = turned ? new Vector2(natural.y, natural.x) : natural;

            if (_fit)
            {
                var room = _viewport.rect.size;
                _zoom = shown.x <= 0f || shown.y <= 0f
                    ? 1f
                    : Mathf.Clamp(Mathf.Min(room.x / shown.x, room.y / shown.y), MinZoom, MaxZoom);
            }

            _picture.sizeDelta = natural * _zoom;
            _picture.localEulerAngles = new Vector3(0f, 0f, -90f * _rotation);
            if (_fit) _picture.anchoredPosition = Vector2.zero;

            _status?.Set(0, Context.File?.Name ?? "Image");
            _status?.Set(1, $"{natural.x:0} x {natural.y:0}");
            _status?.Set(2, _fit ? "Fit" : Mathf.RoundToInt(_zoom * 100f) + "%");
        }

        // The window can be resized under a fitted picture, so refit whenever
        // the viewport actually changes size rather than every frame.
        void Update()
        {
            if (!_fit || !_viewport) return;
            var size = _viewport.rect.size;
            if (size == _lastViewport) return;
            _lastViewport = size;
            Apply();
        }

        Vector2 _lastViewport;
    }
}
