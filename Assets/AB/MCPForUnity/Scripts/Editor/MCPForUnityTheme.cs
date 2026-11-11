using UnityEditor;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor
{
    /// <summary>
    /// The one place that decides whether this package's windows draw light or dark.
    ///
    /// <para>Both stylesheets define their whole palette as variables on their root class and
    /// redefine it under <see cref="LightClass"/>. Adding or removing that one class is therefore
    /// the entire theme switch — no C# knows a colour except the two below, which exist only
    /// because rich-text <c>&lt;color&gt;</c> tags cannot read a USS variable.</para>
    /// </summary>
    internal static class MCPForUnityTheme
    {
        public const string LightClass = "ab--light";

        /// <summary>Accent for inline rich-text links, matching <c>--ab-accent</c> in each palette.
        /// Both values are measured at or above 4.5:1 against their editor background.</summary>
        public static string AccentHex => EditorGUIUtility.isProSkin ? "#E8A33D" : "#703E00";

        /// <summary>
        /// Applies the current editor skin to a window root.
        /// </summary>
        /// <param name="root">The window's <c>rootVisualElement</c>.</param>
        /// <param name="paletteClass">Class carrying the palette — <c>ab-docs</c> or <c>ab-welcome</c>.
        /// The variables live on that element, so the override class has to land on it too; the
        /// root itself is the fallback for when the UXML could not be loaded.</param>
        public static void Apply(VisualElement root, string paletteClass)
        {
            if (root == null) return;
            var host = root.Q(className: paletteClass) ?? root;
            host.EnableInClassList(LightClass, !EditorGUIUtility.isProSkin);
        }
    }
}
