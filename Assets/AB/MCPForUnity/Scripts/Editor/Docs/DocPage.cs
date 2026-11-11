using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    /// <summary>One page of the Documentation window. Content is built lazily into
    /// the host element via <see cref="Build"/> using the <see cref="DocUI"/> helpers.</summary>
    internal sealed class DocPage
    {
        /// <summary>Stable id used by <c>page:</c> links (kebab-case, e.g. "quick-start").</summary>
        public string Id;
        /// <summary>Tab this page lives under (one of <see cref="DocRegistry.TABS"/>).</summary>
        public string Tab;
        /// <summary>Sidebar + header title.</summary>
        public string Title;
        /// <summary>Sidebar ordering within the tab (ascending).</summary>
        public int Order;
        /// <summary>Builds the page content into the given host element.</summary>
        public Action<VisualElement> Build;
        /// <summary>Lower-case searchable text (titles + body extracts). Filled by
        /// pages so the search box can match content, not just titles.</summary>
        public string SearchText = string.Empty;
    }

    /// <summary>Central ordered registry of every documentation page. Chapter
    /// classes under <c>Docs/Pages/</c> contribute via <see cref="Collect"/>.</summary>
    internal static class DocRegistry
    {
        public const string TAB_START = "Get Started";
        public const string TAB_MANUAL = "Manual";
        public const string TAB_SCRIPTING = "Scripting";
        public const string TAB_REFERENCE = "Reference";
        public static readonly string[] TABS = { TAB_START, TAB_MANUAL, TAB_SCRIPTING, TAB_REFERENCE };

        private static List<DocPage> _pages;

        public static IReadOnlyList<DocPage> Pages
        {
            get { if (_pages == null) Collect(); return _pages; }
        }

        public static DocPage Find(string id)
        {
            foreach (var page in Pages) if (page.Id == id) return page;
            return null;
        }

        /// <summary>Rebuild the registry (used after domain reload; cheap).</summary>
        private static void Collect()
        {
            _pages = new List<DocPage>(32);
            Pages_GetStarted.Register(_pages);
            Pages_Manual.Register(_pages);
            Pages_Scripting.Register(_pages);
            Pages_Reference.Register(_pages);
            // The search matches against SearchText assuming it is lower case. Enforcing that here
            // rather than trusting every page author is the difference between "someone typed a
            // capital" and "this page silently stopped being findable". The title is folded in for
            // the same reason: a word in the title is always searchable, listed or not.
            foreach (var page in _pages)
                page.SearchText = ((page.SearchText ?? string.Empty) + " " + (page.Title ?? string.Empty))
                    .ToLowerInvariant();

            _pages.Sort((a, b) =>
            {
                int tab = Array.IndexOf(TABS, a.Tab).CompareTo(Array.IndexOf(TABS, b.Tab));
                return tab != 0 ? tab : a.Order.CompareTo(b.Order);
            });
        }
    }
}
