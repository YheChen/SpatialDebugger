using UnityEngine;

namespace SpatialDebugger.Core
{
    /// <summary>
    /// How an annotation attaches to a real surface, given the depth sensor's
    /// normal at the point that was pinched.
    /// </summary>
    /// <remarks>
    /// Three separate ideas, deliberately kept apart:
    /// <list type="bullet">
    /// <item><b>Anchor position</b> is the depth hit, and is never moved by
    /// anything here beyond a 5 mm standoff along the outward normal.</item>
    /// <item><b>Attachment orientation</b> comes from the normal. It is what
    /// makes a marker stand out of a wall instead of standing up out of an
    /// imaginary floor.</item>
    /// <item><b>Text readability</b> is not this class's business at all. The
    /// caption plate carries a <c>Billboard</c>, which overwrites its world
    /// rotation every LateUpdate, so no rotation applied to an annotation host
    /// can ever turn the text edge-on or backwards.</item>
    /// </list>
    /// Everything is a pure function of its arguments so the rules can be
    /// tested without a headset.
    /// </remarks>
    public static class SurfaceOrientation
    {
        /// <summary>Roughly what kind of surface a normal describes.</summary>
        public enum Kind
        {
            /// <summary>No usable normal: too low-confidence, or none at all.</summary>
            Unknown = 0,

            /// <summary>A desk top, a floor, a ceiling. Within 30 degrees of level.</summary>
            Horizontal,

            /// <summary>A wall, the side of a monitor. Within 30 degrees of upright.</summary>
            Vertical,

            /// <summary>Anything in between.</summary>
            Angled
        }

        /// <summary>
        /// Below this, the normal is ignored and the annotation keeps its
        /// known-good world-up orientation.
        /// </summary>
        /// <remarks>
        /// The sensor reports this essentially as a flag rather than a
        /// gradient -- a verified session returned 1.00 for five hits and 0.00
        /// for a sixth -- so the exact cut point matters far less than having
        /// one. Meta's own <c>SpaceLocator</c> rejects below 0.4; this is a
        /// little stricter because a wrong normal here tips a visible object
        /// rather than just declining to place it.
        /// </remarks>
        public const float MinimumNormalConfidence = 0.5f;

        /// <summary>
        /// Metres to stand an annotation off the surface along its outward
        /// normal, so it does not sit exactly coplanar with the real thing.
        /// </summary>
        public const float SurfaceOffset = 0.005f;

        /// <summary>cos(30 degrees).</summary>
        private const float HorizontalCosine = 0.866f;

        /// <summary>cos(60 degrees).</summary>
        private const float VerticalCosine = 0.5f;

        /// <summary>
        /// Below this, world up lies too close to the normal to give a usable
        /// direction along the surface. sin(~6 degrees), squared.
        /// </summary>
        private const float DegenerateLift = 0.01f;

        /// <summary>True when a normal is worth orienting anything by.</summary>
        public static bool IsUsable(Vector3 normal, float confidence)
        {
            return confidence >= MinimumNormalConfidence && normal.sqrMagnitude > 1e-6f;
        }

        /// <summary>
        /// The surface a normal describes. Symmetric in the normal's sign,
        /// because a desk seen from above and below is still a desk.
        /// </summary>
        public static Kind Classify(Vector3 normal, float confidence)
        {
            if (!IsUsable(normal, confidence)) return Kind.Unknown;

            var level = Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up));
            if (level >= HorizontalCosine) return Kind.Horizontal;
            if (level <= VerticalCosine) return Kind.Vertical;
            return Kind.Angled;
        }

        /// <summary>
        /// The normal, turned to point out of the surface toward the viewer.
        /// </summary>
        /// <remarks>
        /// The depth sensor is free to hand back either face of a surface. Left
        /// alone, an inward normal would stand the marker up <i>inside</i> the
        /// physical object, where the user cannot see it. Flipping against the
        /// direction the viewer is in fixes that without needing to know
        /// anything about the object.
        /// </remarks>
        public static Vector3 OutwardNormal(Vector3 normal, Vector3 towardViewer)
        {
            if (normal.sqrMagnitude < 1e-6f) return Vector3.up;

            var outward = normal.normalized;

            // No opinion about where the viewer is: leave the normal as given.
            if (towardViewer.sqrMagnitude < 1e-6f) return outward;

            return Vector3.Dot(outward, towardViewer) < 0f ? -outward : outward;
        }

        /// <summary>
        /// Which way "up" runs for an annotation on this surface -- the
        /// direction a leader line or a caption should travel.
        /// </summary>
        /// <remarks>
        /// World up, slid onto the surface plane. On a desk that degenerates to
        /// the normal itself, which is exactly the existing behaviour: the
        /// caption floats above the point on a vertical leader. On a wall it
        /// becomes straight up the wall face, so the leader stays visible
        /// instead of pointing end-on at the viewer and vanishing. On a ceiling
        /// it becomes the downward normal, so the caption hangs.
        /// </remarks>
        public static Vector3 LiftAxis(Vector3 outwardNormal)
        {
            if (outwardNormal.sqrMagnitude < 1e-6f) return Vector3.up;

            var normal = outwardNormal.normalized;
            var alongSurface = Vector3.ProjectOnPlane(Vector3.up, normal);

            return alongSurface.sqrMagnitude < DegenerateLift
                ? normal
                : alongSurface.normalized;
        }

        /// <summary>
        /// A rotation whose local +Y is <paramref name="upAxis"/>.
        /// </summary>
        /// <remarks>
        /// Roll about that axis is left unspecified on purpose. Every primitive
        /// an annotation host owns -- the marker's stalk, head and ring, the
        /// label's anchor dot and leader -- is rotationally symmetric about
        /// local +Y, and the caption plate billboards, so roll has nothing to
        /// act on.
        /// </remarks>
        public static Quaternion Rotation(Vector3 upAxis)
        {
            return upAxis.sqrMagnitude < 1e-6f
                ? Quaternion.identity
                : Quaternion.FromToRotation(Vector3.up, upAxis.normalized);
        }

        /// <summary>
        /// Everything one pinch needs to know about attaching to a surface.
        /// </summary>
        public struct Attachment
        {
            /// <summary>False when the normal was not trusted and nothing changed.</summary>
            public bool Oriented;

            public Kind Surface;

            /// <summary>The normal, turned to face the viewer. World up when not oriented.</summary>
            public Vector3 Normal;

            /// <summary>
            /// Where the annotation goes: the depth hit, plus at most
            /// <see cref="SurfaceOffset"/> along <see cref="Normal"/>. The hit
            /// point is never moved for any other reason.
            /// </summary>
            public Vector3 Anchor;

            /// <summary>Local +Y for the marker: out of the surface.</summary>
            public Vector3 MarkerUp;

            /// <summary>Local +Y for the label: along the surface where there is one.</summary>
            public Vector3 LabelUp;
        }

        /// <summary>
        /// Resolves one pinch against the surface it landed on.
        /// </summary>
        /// <remarks>
        /// A normal below <see cref="MinimumNormalConfidence"/> changes nothing:
        /// same anchor, same world-up orientation, same presentation as before
        /// any of this existed. The depth <i>position</i> is still used -- a
        /// poor normal says nothing about the distance, which is measured
        /// separately and was the point of the whole exercise.
        /// </remarks>
        public static Attachment For(Vector3 hitPoint, Vector3 normal, float confidence,
                                     Vector3 viewerPosition)
        {
            var kind = Classify(normal, confidence);

            if (kind == Kind.Unknown)
            {
                return new Attachment
                {
                    Oriented = false,
                    Surface = Kind.Unknown,
                    Normal = Vector3.up,
                    Anchor = hitPoint,
                    MarkerUp = Vector3.up,
                    LabelUp = Vector3.up
                };
            }

            var outward = OutwardNormal(normal, viewerPosition - hitPoint);

            return new Attachment
            {
                Oriented = true,
                Surface = kind,
                Normal = outward,
                Anchor = hitPoint + outward * SurfaceOffset,
                MarkerUp = outward,
                LabelUp = LiftAxis(outward)
            };
        }

        /// <summary>Lowercase name for the per-pinch diagnostic line.</summary>
        public static string Describe(Kind kind)
        {
            switch (kind)
            {
                case Kind.Horizontal: return "horizontal";
                case Kind.Vertical: return "vertical";
                case Kind.Angled: return "angled";
                default: return "unknown";
            }
        }
    }
}
