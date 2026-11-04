using System;
using System.Collections.Generic;

namespace Deepwave.RetroOS.Web
{
    /// <summary>One address a tab has been to, and what it was called.</summary>
    public readonly struct Visit
    {
        public readonly string Url;
        public readonly string Title;

        public Visit(string url, string title)
        {
            Url = url;
            Title = title;
        }
    }

    /// <summary>The trail one browser tab has been down, and where in it the tab is. Every tab keeps its own.</summary>
    public class BrowseHistory
    {
        readonly List<Visit> _visits = new();

        /// <summary>Where in the trail the tab is; -1 before anything loads.</summary>
        public int Index { get; private set; } = -1;

        public IReadOnlyList<Visit> Visits => _visits;
        public int Count => _visits.Count;

        public string Current => Index >= 0 && Index < _visits.Count ? _visits[Index].Url : null;
        public string CurrentTitle =>
            Index >= 0 && Index < _visits.Count ? _visits[Index].Title : null;

        public bool CanGoBack => Index > 0;
        public bool CanGoForward => Index >= 0 && Index < _visits.Count - 1;

        /// <summary>Records a move to <paramref name="url"/>; returns <c>false</c> for the page already showing (a reload). Going somewhere new drops the forward trail.</summary>
        public bool Go(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            url = url.Trim();

            if (Index >= 0 && string.Equals(Current, url, StringComparison.OrdinalIgnoreCase))
                return false;

            if (Index < _visits.Count - 1)
                _visits.RemoveRange(Index + 1, _visits.Count - Index - 1);

            _visits.Add(new Visit(url, url));
            Index = _visits.Count - 1;
            return true;
        }

        /// <summary>Steps back one, and says whether there was one to step to.</summary>
        public bool Back()
        {
            if (!CanGoBack) return false;
            Index--;
            return true;
        }

        public bool Forward()
        {
            if (!CanGoForward) return false;
            Index++;
            return true;
        }

        /// <summary>Jumps to an entry by position - what the history drop-down does.</summary>
        public bool GoTo(int index)
        {
            if (index < 0 || index >= _visits.Count || index == Index) return false;
            Index = index;
            return true;
        }

        /// <summary>Names the current entry once the page reports its title; until then it is titled with its address.</summary>
        public void Retitle(string title)
        {
            if (Index < 0 || Index >= _visits.Count) return;
            _visits[Index] = new Visit(_visits[Index].Url, title);
        }

        public void Clear()
        {
            _visits.Clear();
            Index = -1;
        }
    }
}
