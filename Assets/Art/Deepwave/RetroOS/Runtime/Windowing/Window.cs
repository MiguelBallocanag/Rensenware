using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Windowing
{
    /// <summary>Per-window setup, taken from the program's settings.</summary>
    public struct WindowOptions
    {
        public bool Resizable;
        public Vector2 MinSize;
        public bool StartMaximized;

        public static WindowOptions Default => new()
        {
            Resizable = true,
            MinSize = new Vector2(320, 220)
        };
    }

    /// <summary>A draggable frame with minimize, maximize and close. Programs fill <see cref="Content"/>.</summary>
    public class Window : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>The chrome: title bar, border and content, as one rect.</summary>
        public RectTransform Frame { get; private set; }

        /// <summary>Where the program builds its interface.</summary>
        public RectTransform Content { get; private set; }

        public string Title { get; private set; }
        public Sprite Icon { get; private set; }
        public bool Maximized { get; private set; }
        /// Whether the window is out of the way - true from the moment it starts folding, so a taskbar click brings it back in one press.
        public bool Minimized => !gameObject.activeSelf ||
                                 (_transition && _transition.Playing == WindowState.Minimize);
        /// Smallest the window may be made. A program can raise it (a minefield switching to Expert); the corner grip follows.
        public Vector2 MinSize
        {
            get => _minSize;
            set
            {
                _minSize = value;
                if (_grip) _grip.minSize = value;
            }
        }

        Vector2 _minSize = new(320, 220);

        /// <summary>Height reserved at the bottom of the desktop for the taskbar.</summary>
        public float BottomInset { get; set; }

        public event Action Closed;
        public event Action Focused;
        public event Action<bool> MaximizedChanged;
        public event Action<bool> MinimizedChanged;

        Image _bar;
        Text _titleText;
        WindowTransition _transition;
        WindowDragger _dragger;
        WindowResizeAnchor _grip;
        Bounds _saved;
        bool _closed;
        bool _focused;

        // The floating pose, kept while the window is maximized.
        struct Bounds
        {
            public Vector2 AnchorMin, AnchorMax, Pivot, Size, Position;
        }

        public void Bind(RectTransform frame, RectTransform content, Image bar, Text titleText, Sprite icon)
        {
            Frame = frame;
            Content = content;
            _bar = bar;
            _titleText = titleText;
            Icon = icon;
            _transition = GetComponent<WindowTransition>();
        }

        /// <summary>Wires the title-bar handle and the corner grip, once built.</summary>
        public void BindHandles(WindowDragger dragger, WindowResizeAnchor grip)
        {
            _dragger = dragger;
            _grip = grip;
        }

        public void SetTitle(string title)
        {
            Title = title;
            if (_titleText) _titleText.text = title;
        }

        public void SetFocused(bool focused)
        {
            _focused = focused;
            PaintTitleBar();
        }

        // The title bar carries the flat colour and the wash, so gradients-off paints the same colour twice.
        void PaintTitleBar()
        {
            if (!_bar) return;

            var start = _focused ? PaletteSlot.TitleActive : PaletteSlot.TitleIdle;
            var end = _focused ? PaletteSlot.TitleActiveEnd : PaletteSlot.TitleIdleEnd;

            RetroPalette.Paint(_bar, start);
            RetroPalette.PaintGradient(_bar, start, end);
        }

        // A minimized window is off and misses a scheme change; repaint on the way back up.
        void OnEnable()
        {
            RetroSkin.Changed += PaintTitleBar;
            PaintTitleBar();
        }

        void OnDisable() => RetroSkin.Changed -= PaintTitleBar;

        public void RequestFocus() => Focused?.Invoke();

        /// <summary>Plays the window's opening transition.</summary>
        public void PlayOpen() => _transition?.Play(WindowState.In);

        public void Minimize()
        {
            if (Minimized) return;

            // Fly toward the taskbar, where the window's button lives - straight
            // down from wherever the window is sitting.
            if (_transition && Frame && transform.parent is RectTransform parent)
                _transition.MinimizeTarget = new Vector2(
                    Frame.anchoredPosition.x, -parent.rect.height * 0.5f);

            MinimizedChanged?.Invoke(true);
            if (_transition) _transition.Play(WindowState.Minimize);
            else gameObject.SetActive(false);
        }

        public void Restore()
        {
            if (!Minimized) return;
            gameObject.SetActive(true);
            MinimizedChanged?.Invoke(false);
            _transition?.Play(WindowState.In);
        }

        // Grows/shrinks from the bottom-right, keeping the top-left corner put.
        // Never grows past the desktop work area, then re-clamps its position.
        public void ResizeBy(Vector2 delta)
        {
            if (Maximized || !Frame) return;

            var size = Frame.sizeDelta;
            var max = MaxSize();
            var next = new Vector2(Mathf.Clamp(size.x + delta.x, MinSize.x, Mathf.Max(MinSize.x, max.x)),
                                   Mathf.Clamp(size.y + delta.y, MinSize.y, Mathf.Max(MinSize.y, max.y)));
            if (next == size) return;

            Frame.sizeDelta = next;
            ClampToArea();
        }

        /// <summary>Moves and sizes the window in one go, then clamps it back into the work area. Restores the window first if it was maximized.</summary>
        /// <param name="centre">Position relative to the middle of the desktop.</param>
        /// <param name="size">Wanted size, clamped between the minimum and the work area.</param>
        public void SetBounds(Vector2 centre, Vector2 size)
        {
            if (Maximized) ToggleMaximize();
            if (!Frame) return;

            var limit = MaxSize();
            Frame.sizeDelta = new Vector2(
                Mathf.Clamp(size.x, MinSize.x, Mathf.Max(MinSize.x, limit.x)),
                Mathf.Clamp(size.y, MinSize.y, Mathf.Max(MinSize.y, limit.y)));

            // centre is about the desktop middle; localPosition places the pivot, which the resize grip may have moved to a corner.
            var applied = Frame.sizeDelta;
            var pivotOffset = new Vector2((Frame.pivot.x - 0.5f) * applied.x,
                                          (Frame.pivot.y - 0.5f) * applied.y);
            Frame.localPosition = centre + pivotOffset;
            ClampToArea();
        }

        /// <summary>Resizes the window where it stands, keeping its top-left corner put. For a program whose content changes size on its own - a minefield switching to Expert, a board that grew.</summary>
        public void SetSize(Vector2 size)
        {
            if (Maximized || !Frame) return;

            var limit = MaxSize();
            var next = new Vector2(
                Mathf.Clamp(size.x, MinSize.x, Mathf.Max(MinSize.x, limit.x)),
                Mathf.Clamp(size.y, MinSize.y, Mathf.Max(MinSize.y, limit.y)));

            var grew = next - Frame.sizeDelta;
            if (grew == Vector2.zero) return;

            Frame.sizeDelta = next;

            // localPosition places the pivot, so how far the frame has to move
            // to leave its top-left corner alone depends where the pivot is.
            Frame.localPosition += new Vector3(
                grew.x * Frame.pivot.x,
                -grew.y * (1f - Frame.pivot.y), 0f);

            ClampToArea();
        }

        /// <summary>Keeps the whole window inside the desktop work area - the parent layer minus the taskbar strip - so it can never end up off-screen.</summary>
        public void ClampToArea()
        {
            if (Maximized) return;
            if (_dragger) _dragger.ClampToArea();
        }

        // The desktop can resize under a window (game view, resolution); windows follow it rather than being left off the edge.
        void OnRectTransformDimensionsChange()
        {
            if (_closed || Maximized) return;

            // Not while something is animating - a window folding into the
            // taskbar is deliberately outside the area on its way there.
            if (_transition && _transition.Playing.HasValue) return;

            ClampToArea();
        }

        // Largest size that still fits the work area (height leaves the taskbar).
        Vector2 MaxSize()
        {
            if (transform.parent is RectTransform parent)
                return new Vector2(parent.rect.width, parent.rect.height - BottomInset);
            return new Vector2(4096, 4096);
        }

        public void ToggleMaximize()
        {
            if (!Frame) return;
            Maximized = !Maximized;

            if (Maximized)
            {
                _saved = new Bounds
                {
                    AnchorMin = Frame.anchorMin,
                    AnchorMax = Frame.anchorMax,
                    Pivot = Frame.pivot,
                    Size = Frame.sizeDelta,
                    Position = Frame.localPosition
                };

                Frame.anchorMin = Vector2.zero;
                Frame.anchorMax = Vector2.one;
                Frame.offsetMin = new Vector2(0, BottomInset);
                Frame.offsetMax = Vector2.zero;
                _transition?.Play(WindowState.Fullscreen);
            }
            else
            {
                Frame.anchorMin = _saved.AnchorMin;
                Frame.anchorMax = _saved.AnchorMax;
                Frame.pivot = _saved.Pivot;
                Frame.sizeDelta = _saved.Size;
                Frame.localPosition = _saved.Position;
                ClampToArea();
                _transition?.Play(WindowState.Normalize);
            }

            if (_grip) _grip.gameObject.SetActive(!Maximized);
            MaximizedChanged?.Invoke(Maximized);
            RequestFocus();
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            Closed?.Invoke();

            // Let the window animate out before it goes; the manager and the
            // taskbar have already been told, so nothing waits on the object.
            if (_transition && isActiveAndEnabled && !_transition.instant)
            {
                _transition.Finished += OnCloseFinished;
                _transition.Play(WindowState.Out);
                return;
            }

            Destroy(gameObject);
        }

        void OnCloseFinished(WindowState state)
        {
            if (state != WindowState.Out) return;
            _transition.Finished -= OnCloseFinished;
            Destroy(gameObject);
        }

        // Logout clears the whole window layer without Close(); report that as a close too.
        // Not while the scene is unloading - Closed runs profile rules that may build UI.
        void OnDestroy()
        {
            if (_closed || !gameObject.scene.isLoaded) return;
            _closed = true;
            Closed?.Invoke();
        }

        public void OnPointerDown(PointerEventData e) => Focused?.Invoke();
    }
}
