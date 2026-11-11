using UnityEditor;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    /// <summary>
    /// Content builders for documentation pages. Pages are data-driven — chapter
    /// classes call these helpers to append rich elements to their host. All
    /// styling comes from <c>MCPForUnityDocs.uss</c> (ab-docs__*).
    ///
    /// <para>Inline links: paragraphs support UI Toolkit rich text. Use
    /// <see cref="Link"/> to build a colored, underlined <c>&lt;link&gt;</c> tag whose
    /// click routes through <see cref="DocActions"/> (schemes: page:, menu:,
    /// asset:, url:).</para>
    /// </summary>
    internal static class DocUI
    {
        /// <summary>Rich-text inline link. Example:
        /// <c>DocUI.Para(host, $"See {DocUI.Link("the Quick Start", "page:quick-start")}.")</c>
        /// <para>The colour comes from <see cref="MCPForUnityTheme"/> rather than a constant: a
        /// rich-text tag cannot read a USS variable, so this is the one place where the accent has
        /// to be resolved in C# to stay readable on both editor skins.</para></summary>
        public static string Link(string text, string target)
            => $"<link=\"{target}\"><color={MCPForUnityTheme.AccentHex}><u>{text}</u></color></link>";

        public static Label H1(VisualElement host, string text) => AddLabel(host, text, "ab-docs__h1");
        public static Label H2(VisualElement host, string text) => AddLabel(host, text, "ab-docs__h2");
        public static Label H3(VisualElement host, string text) => AddLabel(host, text, "ab-docs__h3");

        /// <summary>Body paragraph. Rich text enabled; inline links clickable.</summary>
        public static Label Para(VisualElement host, string richText)
        {
            var label = AddLabel(host, richText, "ab-docs__para");
            WireLinks(label);
            return label;
        }

        /// <summary>Bulleted list; each entry supports rich text + links.</summary>
        public static void Bullets(VisualElement host, params string[] items)
        {
            foreach (var item in items)
            {
                var label = AddLabel(host, "•  " + item, "ab-docs__bullet");
                WireLinks(label);
            }
        }

        /// <summary>
        /// Callout box. kind: "info" | "tip" | "warn".
        ///
        /// <para>The kind is announced by a text badge, not an icon. This used to print U+1F4A1
        /// for "tip" and U+2139 for "info" — neither exists in any font the editor can reach,
        /// fallbacks and dynamic atlas population included, so twelve of the manual's seventeen
        /// callouts opened with a missing-glyph box. ASCII cannot fail that way.</para>
        /// </summary>
        public static void Callout(VisualElement host, string kind, string richText)
        {
            var box = new VisualElement();
            box.AddToClassList("ab-docs__callout");
            box.AddToClassList($"ab-docs__callout--{kind}");

            var badge = new Label(kind == "warn" ? "WARNING" : kind == "tip" ? "TIP" : "NOTE");
            badge.AddToClassList("ab-docs__callout-badge");
            badge.AddToClassList($"ab-docs__callout-badge--{kind}");
            box.Add(badge);

            var label = new Label(richText) { enableRichText = true };
            label.AddToClassList("ab-docs__callout-text");
            WireLinks(label);
            box.Add(label);
            host.Add(box);
        }

        /// <summary>Monospace code block with a Copy button.</summary>
        public static void Code(VisualElement host, string code)
        {
            var box = new VisualElement();
            box.AddToClassList("ab-docs__code");
            var text = new Label(code) { enableRichText = false };
#if UNITY_2023_1_OR_NEWER
            text.selection.isSelectable = true; // nice-to-have; API absent on 2022.3
#endif
            text.AddToClassList("ab-docs__code-text");
            box.Add(text);

            var copy = new Button { text = "Copy" };
            copy.AddToClassList("ab-docs__code-copy");
            copy.tooltip = "Copy this snippet to the clipboard.";
            // A copy that says nothing leaves the user pressing it twice to be sure it took.
            copy.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = code;
                copy.text = "Copied";
                copy.AddToClassList("ab-docs__code-copy--done");
                copy.schedule.Execute(() =>
                {
                    copy.text = "Copy";
                    copy.RemoveFromClassList("ab-docs__code-copy--done");
                }).StartingIn(1200);
            };
            box.Add(copy);
            host.Add(box);
        }

        /// <summary>Row of action buttons; each target uses the DocActions schemes.</summary>
        public static void Actions(VisualElement host, params (string label, string target)[] actions)
        {
            var row = new VisualElement();
            row.AddToClassList("ab-docs__actions");
            foreach (var (label, target) in actions)
            {
                string captured = target;
                var button = new Button(() => DocActions.Handle(captured)) { text = label };
                button.AddToClassList("ab-docs__action-btn");
                row.Add(button);
            }
            host.Add(row);
        }

        /// <summary>Horizontal separator between page sections.</summary>
        public static void Divider(VisualElement host)
        {
            var divider = new VisualElement();
            divider.AddToClassList("ab-docs__divider");
            host.Add(divider);
        }

        /// <summary>Two-column key/value table (e.g. shortcuts, field references).</summary>
        public static void Table(VisualElement host, params (string key, string value)[] rows)
        {
            var table = new VisualElement();
            table.AddToClassList("ab-docs__table");
            foreach (var (key, value) in rows)
            {
                var row = new VisualElement();
                row.AddToClassList("ab-docs__table-row");
                var keyLabel = new Label(key); keyLabel.AddToClassList("ab-docs__table-key");
                var valueLabel = new Label(value) { enableRichText = true };
                valueLabel.AddToClassList("ab-docs__table-val");
                WireLinks(valueLabel);
                row.Add(keyLabel); row.Add(valueLabel);
                table.Add(row);
            }
            host.Add(table);
        }

        /// <summary>Numbered tour step with an optional action button and a persistent
        /// "done" checkmark (EditorPrefs) — the building block of the guided tour.</summary>
        public static void TourStep(VisualElement host, int number, string title,
            string body, string actionLabel = null, string actionTarget = null)
        {
            string prefKey = $"AB.MCPForUnity.Docs.Tour.Step{number}";
            var step = new VisualElement();
            step.AddToClassList("ab-docs__tour-step");

            var head = new VisualElement();
            head.AddToClassList("ab-docs__tour-head");
            var toggle = new Toggle { value = EditorPrefs.GetBool(prefKey, false) };
            toggle.AddToClassList("ab-docs__tour-check");
            toggle.tooltip = "Mark this step done (remembered on this machine).";
            toggle.RegisterValueChangedCallback(e => EditorPrefs.SetBool(prefKey, e.newValue));
            head.Add(toggle);
            var titleLabel = new Label($"{number}.  {title}");
            titleLabel.AddToClassList("ab-docs__tour-title");
            head.Add(titleLabel);
            step.Add(head);

            var bodyLabel = new Label(body) { enableRichText = true };
            bodyLabel.AddToClassList("ab-docs__tour-body");
            WireLinks(bodyLabel);
            step.Add(bodyLabel);

            if (actionLabel != null && actionTarget != null)
                Actions(step, (actionLabel, actionTarget));

            host.Add(step);
        }

        private static Label AddLabel(VisualElement host, string text, string className)
        {
            var label = new Label(text) { enableRichText = true };
            label.AddToClassList(className);
            host.Add(label);
            return label;
        }

        private static void WireLinks(Label label)
        {
            // Link-tag events live under Experimental (Unity 2022.2+ incl. Unity 6) —
            // still the supported way to make rich-text <link> tags clickable.
            label.RegisterCallback<UnityEngine.UIElements.Experimental.PointerDownLinkTagEvent>(
                e => DocActions.Handle(e.linkID));
        }
    }
}
