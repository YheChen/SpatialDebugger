using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SpatialDebugger.Core
{
    /// <summary>Every annotation the dispatcher knows how to render.</summary>
    public enum SpatialActionType
    {
        Unknown = 0,
        Label,
        Warning,
        Marker,
        Arrow,
        Highlight,
        Clear
    }

    /// <summary>
    /// Which frame <see cref="SpatialAction.Position"/> and the arrow endpoints
    /// are expressed in.
    /// </summary>
    /// <remarks>
    /// <see cref="Target"/> is the default and the important one: coordinates
    /// are relative to the debugging target the user selected in the headset,
    /// so the backend never needs real Quest world coordinates.
    /// </remarks>
    public enum SpatialSpace
    {
        Target = 0,
        World
    }

    /// <summary>
    /// One renderable annotation, mirroring the backend's wire protocol.
    /// </summary>
    /// <remarks>
    /// Deliberately one flat type rather than a class hierarchy: the payload
    /// is parsed on-device, and a flat shape keeps deserialisation free of
    /// reflection (which IL2CPP managed stripping would otherwise break).
    /// The <c>Has*</c> flags exist because a missing field and a zero vector
    /// mean different things.
    /// </remarks>
    [Serializable]
    public class SpatialAction
    {
        public SpatialActionType Type = SpatialActionType.Unknown;
        public string Id;
        public SpatialSpace Space = SpatialSpace.Target;

        public Vector3 Position;
        public bool HasPosition;

        public Vector3 From;
        public bool HasFrom;

        public Vector3 To;
        public bool HasTo;

        public string Text;

        public Color Color = UnityEngine.Color.white;
        public bool HasColor;

        public float Scale = 1f;

        public float Radius = 0.04f;
        public bool HasRadius;

        /// <summary>Seconds before auto-despawn. 0 means persist until cleared.</summary>
        public float Duration;

        // -- construction helpers ------------------------------------------

        public static SpatialAction Label(Vector3 position, string text) =>
            new SpatialAction { Type = SpatialActionType.Label, Position = position, HasPosition = true, Text = text };

        public static SpatialAction Warning(Vector3 position, string text) =>
            new SpatialAction { Type = SpatialActionType.Warning, Position = position, HasPosition = true, Text = text };

        public static SpatialAction Marker(Vector3 position) =>
            new SpatialAction { Type = SpatialActionType.Marker, Position = position, HasPosition = true };

        public static SpatialAction Arrow(Vector3 from, Vector3 to, string text = null) =>
            new SpatialAction
            {
                Type = SpatialActionType.Arrow,
                From = from, HasFrom = true,
                To = to, HasTo = true,
                Text = text
            };

        public static SpatialAction Highlight(Vector3 position, float radius = 0.04f) =>
            new SpatialAction
            {
                Type = SpatialActionType.Highlight,
                Position = position, HasPosition = true,
                Radius = radius, HasRadius = true
            };

        public static SpatialAction Clear() => new SpatialAction { Type = SpatialActionType.Clear };

        public SpatialAction WithColor(Color color)
        {
            Color = color;
            HasColor = true;
            return this;
        }

        // -- validation ----------------------------------------------------

        /// <summary>
        /// True when this action carries the geometry its type needs. A
        /// malformed action is dropped rather than rendered half-formed.
        /// </summary>
        public bool IsValid(out string error)
        {
            switch (Type)
            {
                case SpatialActionType.Label:
                case SpatialActionType.Warning:
                    if (!HasPosition) { error = Type + " requires 'position'"; return false; }
                    if (string.IsNullOrWhiteSpace(Text)) { error = Type + " requires 'text'"; return false; }
                    break;

                case SpatialActionType.Marker:
                case SpatialActionType.Highlight:
                    if (!HasPosition) { error = Type + " requires 'position'"; return false; }
                    break;

                case SpatialActionType.Arrow:
                    if (!HasFrom || !HasTo) { error = "arrow requires both 'from' and 'to'"; return false; }
                    break;

                case SpatialActionType.Clear:
                    break;

                default:
                    error = "unknown action type";
                    return false;
            }

            if (Scale <= 0f) { error = "scale must be greater than zero"; return false; }
            error = null;
            return true;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}(space={1}, pos={2}, text=\"{3}\")", Type, Space, Position, Text);
        }

        // -- parsing -------------------------------------------------------

        internal static readonly Dictionary<string, SpatialActionType> TypeNames =
            new Dictionary<string, SpatialActionType>(StringComparer.OrdinalIgnoreCase)
            {
                { "label", SpatialActionType.Label },
                { "warning", SpatialActionType.Warning },
                { "marker", SpatialActionType.Marker },
                { "arrow", SpatialActionType.Arrow },
                { "highlight", SpatialActionType.Highlight },
                { "clear", SpatialActionType.Clear },
            };

        public static SpatialActionType ParseType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return SpatialActionType.Unknown;
            return TypeNames.TryGetValue(raw.Trim(), out var type) ? type : SpatialActionType.Unknown;
        }

        public static SpatialSpace ParseSpace(string raw)
        {
            return string.Equals(raw, "world", StringComparison.OrdinalIgnoreCase)
                ? SpatialSpace.World
                : SpatialSpace.Target;
        }

        /// <summary>
        /// Parses <c>#RRGGBB</c> or <c>#RRGGBBAA</c>. Returns false for anything
        /// else so the renderer keeps its per-type default colour.
        /// </summary>
        public static bool TryParseColor(string raw, out Color color)
        {
            color = UnityEngine.Color.white;
            if (string.IsNullOrEmpty(raw)) return false;
            return ColorUtility.TryParseHtmlString(raw.Trim(), out color);
        }
    }
}
