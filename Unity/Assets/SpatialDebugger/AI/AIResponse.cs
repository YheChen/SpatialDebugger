using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.AI
{
    /// <summary>Why a request did not produce a usable answer.</summary>
    public enum AIFailure
    {
        None = 0,
        NotConfigured,
        ConnectionFailed,
        Timeout,
        HttpError,
        MalformedResponse,
        Cancelled
    }

    /// <summary>
    /// What the dispatcher consumes. Mirrors the backend's response body.
    /// </summary>
    public class AIResponse
    {
        public string Speech = string.Empty;
        public List<SpatialAction> Actions = new List<SpatialAction>();
        public string Scenario;
        public string Provider = "unknown";
        public float Confidence = 1f;

        /// <summary>False when the request failed; <see cref="Failure"/> says why.</summary>
        public bool Success = true;
        public AIFailure Failure = AIFailure.None;
        public string Error;

        /// <summary>True when this came from the on-device demo rather than the backend.</summary>
        public bool IsLocal;

        public static AIResponse Failed(AIFailure failure, string error)
        {
            return new AIResponse
            {
                Success = false,
                Failure = failure,
                Error = error,
                Speech = string.Empty,
                Provider = "none"
            };
        }

        // -- parsing -------------------------------------------------------

        /// <summary>
        /// Parses a backend response body.
        /// </summary>
        /// <remarks>
        /// Walks the JSON by hand rather than reflecting onto types, because
        /// IL2CPP managed stripping on Quest can remove the members a
        /// reflection-based deserialiser needs. Individual malformed actions
        /// are skipped and logged: a partial annotation set still demos, an
        /// exception does not.
        /// </remarks>
        public static bool TryParse(string body, out AIResponse response, out string error)
        {
            response = null;
            error = null;

            if (string.IsNullOrWhiteSpace(body))
            {
                error = "empty response body";
                return false;
            }

            JObject root;
            try
            {
                var token = JToken.Parse(body);
                root = token as JObject;
                if (root == null)
                {
                    error = "response was not a JSON object";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "response was not valid JSON: " + exception.Message;
                return false;
            }

            // The backend also uses {"error": ..., "detail": ...} for failures.
            var errorToken = root["error"];
            if (errorToken != null && errorToken.Type == JTokenType.String)
            {
                error = (string)errorToken + " - " + (string)(root["detail"] ?? "");
                return false;
            }

            var parsed = new AIResponse
            {
                Speech = ReadString(root["speech"]) ?? string.Empty,
                Scenario = ReadString(root["scenario"]),
                Provider = ReadString(root["provider"]) ?? "unknown",
                Confidence = ReadFloat(root["confidence"], 1f)
            };

            if (root["actions"] is JArray actions)
            {
                foreach (var item in actions)
                {
                    if (TryParseAction(item as JObject, out var action, out var actionError))
                    {
                        parsed.Actions.Add(action);
                    }
                    else
                    {
                        Debug.LogWarning("[SpatialDebugger] dropped a malformed action: " + actionError);
                    }
                }
            }

            response = parsed;
            return true;
        }

        internal static bool TryParseAction(JObject json, out SpatialAction action, out string error)
        {
            action = null;
            error = null;

            if (json == null)
            {
                error = "action was not a JSON object";
                return false;
            }

            var type = SpatialAction.ParseType(ReadString(json["type"]));
            if (type == SpatialActionType.Unknown)
            {
                error = "unknown action type '" + ReadString(json["type"]) + "'";
                return false;
            }

            var result = new SpatialAction
            {
                Type = type,
                Id = ReadString(json["id"]),
                Space = SpatialAction.ParseSpace(ReadString(json["space"])),
                Text = ReadString(json["text"]),
                Scale = ReadFloat(json["scale"], 1f),
                Duration = ReadFloat(json["duration"], 0f)
            };

            if (TryReadVector3(json["position"], out var position))
            {
                result.Position = position;
                result.HasPosition = true;
            }

            if (TryReadVector3(json["from"], out var from))
            {
                result.From = from;
                result.HasFrom = true;
            }

            if (TryReadVector3(json["to"], out var to))
            {
                result.To = to;
                result.HasTo = true;
            }

            if (SpatialAction.TryParseColor(ReadString(json["color"]), out var color))
            {
                result.Color = color;
                result.HasColor = true;
            }

            var radius = json["radius"];
            if (radius != null && radius.Type != JTokenType.Null)
            {
                result.Radius = ReadFloat(radius, result.Radius);
                result.HasRadius = true;
            }

            if (!result.IsValid(out error)) return false;

            action = result;
            return true;
        }

        // -- readers -------------------------------------------------------

        private static string ReadString(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            return token.Type == JTokenType.String ? (string)token : token.ToString();
        }

        private static float ReadFloat(JToken token, float fallback)
        {
            if (token == null) return fallback;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                return (float)token;
            }
            return float.TryParse(ReadString(token), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        /// <summary>Reads an <c>[x, y, z]</c> triple.</summary>
        private static bool TryReadVector3(JToken token, out Vector3 vector)
        {
            vector = Vector3.zero;
            if (!(token is JArray array) || array.Count < 3) return false;

            var components = new float[3];
            for (var i = 0; i < 3; i++)
            {
                var component = array[i];
                if (component == null ||
                    (component.Type != JTokenType.Float && component.Type != JTokenType.Integer))
                {
                    return false;
                }

                components[i] = (float)component;
                if (float.IsNaN(components[i]) || float.IsInfinity(components[i])) return false;
            }

            vector = new Vector3(components[0], components[1], components[2]);
            return true;
        }
    }
}
