using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.Web
{
    // Cycles an Image through sprite frames - the era-appropriate "GIF".
    public class FlipbookImage : MonoBehaviour
    {
        Image _image;
        IReadOnlyList<Sprite> _frames;
        float _interval = 0.125f;
        float _next;
        int _index;

        public static FlipbookImage Attach(Image image, IReadOnlyList<Sprite> frames, float fps)
        {
            var book = image.gameObject.AddComponent<FlipbookImage>();
            book._image = image;
            book._frames = frames;
            book._interval = 1f / Mathf.Max(1f, fps);
            if (frames.Count > 0) image.sprite = frames[0];
            return book;
        }

        void Update()
        {
            if (_frames == null || _frames.Count < 2) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _interval;
            _index = (_index + 1) % _frames.Count;
            _image.sprite = _frames[_index];
        }
    }
}
