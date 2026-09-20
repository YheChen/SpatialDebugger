using System;
using UnityEngine;

namespace SpatialDebugger.Vision
{
    /// <summary>Reads Quest camera frames into JPEG bytes for recognition.</summary>
    /// <remarks>
    /// On device <c>PassthroughCameraAccess.GetTexture()</c> returns a
    /// <see cref="RenderTexture"/>, which cannot be read with
    /// <c>GetPixels</c>, so a GPU readback is required. That readback blocks
    /// the main thread, which is why it happens once per pinch and never per
    /// frame.
    /// </remarks>
    public static class CameraCapture
    {
        /// <summary>
        /// Encodes a crop of <paramref name="source"/> as JPEG.
        /// </summary>
        /// <param name="source">The camera frame.</param>
        /// <param name="centre">
        /// Viewport point (0..1) to crop around — the pinched object projected
        /// into the image. Outside 0..1 falls back to the frame centre.
        /// </param>
        /// <param name="cropFraction">Crop size as a fraction of the frame.</param>
        /// <param name="quality">JPEG quality.</param>
        public static byte[] EncodeCrop(Texture source, Vector2 centre, float cropFraction,
            int quality, out int width, out int height, out string error)
        {
            width = height = 0;
            error = null;

            if (source == null)
            {
                error = "no camera texture";
                return null;
            }

            RenderTexture temporary = null;
            var previous = RenderTexture.active;
            Texture2D readable = null;

            try
            {
                cropFraction = Mathf.Clamp(cropFraction, 0.1f, 1f);

                var cropWidth = Mathf.Max(16, Mathf.RoundToInt(source.width * cropFraction));
                var cropHeight = Mathf.Max(16, Mathf.RoundToInt(source.height * cropFraction));

                // A projection outside the frame means the object is not in
                // shot; the centre is the better guess then.
                if (centre.x < 0f || centre.x > 1f || centre.y < 0f || centre.y > 1f)
                {
                    centre = new Vector2(0.5f, 0.5f);
                }

                var x = Mathf.Clamp(Mathf.RoundToInt(centre.x * source.width - cropWidth * 0.5f),
                    0, source.width - cropWidth);
                var y = Mathf.Clamp(Mathf.RoundToInt(centre.y * source.height - cropHeight * 0.5f),
                    0, source.height - cropHeight);

                temporary = RenderTexture.GetTemporary(
                    source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                readable = new Texture2D(cropWidth, cropHeight, TextureFormat.RGB24, false);
                readable.ReadPixels(new Rect(x, y, cropWidth, cropHeight), 0, 0);
                readable.Apply(false);

                var jpg = readable.EncodeToJPG(Mathf.Clamp(quality, 40, 95));
                width = cropWidth;
                height = cropHeight;
                return jpg;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }
    }
}
