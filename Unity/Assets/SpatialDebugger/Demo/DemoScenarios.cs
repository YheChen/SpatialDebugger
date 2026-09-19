using System.Collections.Generic;
using SpatialDebugger.AI;
using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Demo
{
    /// <summary>
    /// The on-device copy of the backend's demo scenarios.
    /// </summary>
    /// <remarks>
    /// This is the floor the demo cannot fall through. It depends on nothing:
    /// no network, no backend, no computer vision, no sponsor API, no scene
    /// understanding. If every other system is down, pressing "Demo Analysis"
    /// still produces the full
    /// <c>target -&gt; reasoning -&gt; spatial annotation</c> pipeline.
    /// <para>
    /// Coordinates mirror <c>backend/app/scenarios.py</c> and are metres in
    /// target space, so the target the user picked is the origin.
    /// </para>
    /// </remarks>
    public static class DemoScenarios
    {
        public const string DefaultId = "led_not_working";

        private static readonly Color Red = HexOr("#FF4136", Color.red);
        private static readonly Color Amber = HexOr("#FF9F1C", new Color(1f, 0.62f, 0.11f));
        private static readonly Color Cyan = HexOr("#2EC4F3", Color.cyan);
        private static readonly Color Green = HexOr("#2ECC71", Color.green);

        private static Color HexOr(string hex, Color fallback) =>
            ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : fallback;

        public class Scenario
        {
            public string Id;
            public string Title;
            public string Speech;
            public System.Func<List<SpatialAction>> BuildActions;
        }

        private static readonly List<Scenario> All = new List<Scenario>
        {
            new Scenario
            {
                Id = "led_not_working",
                Title = "LED does not light",
                Speech = "The LED's anode looks like it lands on GPIO 12, but the sketch is " +
                         "driving GPIO 13. Move that jumper one row over and re-flash.",
                BuildActions = () => new List<SpatialAction>
                {
                    SpatialAction.Marker(new Vector3(0f, 0f, 0f)).WithColor(Red),
                    SpatialAction.Label(new Vector3(0f, 0.06f, 0f), "GPIO 12").WithColor(Cyan),
                    SpatialAction.Warning(new Vector3(0f, 0.15f, 0f), "Possible incorrect connection").WithColor(Red),
                    SpatialAction.Arrow(new Vector3(0.10f, 0.25f, 0f), new Vector3(0f, 0.02f, 0f), "Connect here").WithColor(Amber),
                    SpatialAction.Highlight(new Vector3(0f, 0f, 0f), 0.05f).WithColor(Red),
                }
            },
            new Scenario
            {
                Id = "missing_resistor",
                Title = "LED wired without a current-limiting resistor",
                Speech = "That LED goes straight from the GPIO pin to ground with no series " +
                         "resistor. Add about 220 ohms in line before you power it again.",
                BuildActions = () => new List<SpatialAction>
                {
                    SpatialAction.Marker(new Vector3(0f, 0f, 0f)).WithColor(Red),
                    SpatialAction.Warning(new Vector3(0f, 0.16f, 0f), "No current-limiting resistor").WithColor(Red),
                    SpatialAction.Label(new Vector3(0f, 0.07f, 0f), "Add 220 ohm").WithColor(Amber),
                    SpatialAction.Arrow(new Vector3(-0.09f, 0.22f, 0.02f), new Vector3(0f, 0.01f, 0f), "Insert resistor here").WithColor(Amber),
                    SpatialAction.Highlight(new Vector3(0f, 0f, 0f), 0.045f).WithColor(Amber),
                }
            },
            new Scenario
            {
                Id = "gpio_mismatch",
                Title = "Pin in firmware does not match the wiring",
                Speech = "The wire is seated in GPIO 12 but the firmware configures GPIO 4. " +
                         "Either move the wire or change the pin number in your sketch.",
                BuildActions = () => new List<SpatialAction>
                {
                    SpatialAction.Marker(new Vector3(0f, 0f, 0f)).WithColor(Amber),
                    SpatialAction.Label(new Vector3(0f, 0.06f, 0f), "wired: GPIO 12").WithColor(Cyan),
                    SpatialAction.Label(new Vector3(0f, 0.11f, 0f), "code: GPIO 4").WithColor(Amber),
                    SpatialAction.Warning(new Vector3(0f, 0.18f, 0f), "Pin mismatch").WithColor(Red),
                    SpatialAction.Arrow(new Vector3(0f, 0.11f, 0f), new Vector3(0f, 0.02f, 0f)).WithColor(Amber),
                }
            },
            new Scenario
            {
                Id = "power_rail",
                Title = "Breadboard power rail not bridged",
                Speech = "The top and bottom power rails are not bridged, so the right-hand half " +
                         "of the board has no 3V3. Run a jumper across the rails.",
                BuildActions = () => new List<SpatialAction>
                {
                    SpatialAction.Marker(new Vector3(0f, 0f, 0f)).WithColor(Red),
                    SpatialAction.Warning(new Vector3(0f, 0.17f, 0f), "Rail not powered").WithColor(Red),
                    SpatialAction.Label(new Vector3(0.06f, 0.06f, 0f), "3V3 missing").WithColor(Cyan),
                    SpatialAction.Arrow(new Vector3(-0.12f, 0.05f, 0f), new Vector3(0.12f, 0.05f, 0f), "Bridge the rails").WithColor(Green),
                    SpatialAction.Highlight(new Vector3(0f, 0f, 0f), 0.07f).WithColor(Red),
                }
            },
            new Scenario
            {
                Id = "ground_missing",
                Title = "No common ground",
                Speech = "The ESP32's GND is not tied to the breadboard's ground rail, so nothing " +
                         "has a return path. Add a jumper from any GND pin to the blue rail.",
                BuildActions = () => new List<SpatialAction>
                {
                    SpatialAction.Marker(new Vector3(0f, 0f, 0f)).WithColor(Red),
                    SpatialAction.Warning(new Vector3(0f, 0.16f, 0f), "No common ground").WithColor(Red),
                    SpatialAction.Label(new Vector3(0f, 0.07f, 0f), "GND").WithColor(Cyan),
                    SpatialAction.Arrow(new Vector3(0.08f, 0.20f, -0.02f), new Vector3(0f, 0.01f, 0f), "Tie to ground rail").WithColor(Amber),
                }
            },
            new Scenario
            {
                Id = "i2c_pullup",
                Title = "I2C bus missing pull-up resistors",
                Speech = "SDA and SCL are floating -- there are no pull-ups on the bus, so the " +
                         "scan finds nothing. Add 4.7k from each line to 3V3.",
                BuildActions = () => new List<SpatialAction>
                {
                    SpatialAction.Marker(new Vector3(0f, 0f, 0f)).WithColor(Amber),
                    SpatialAction.Label(new Vector3(-0.04f, 0.06f, 0f), "SDA").WithColor(Cyan),
                    SpatialAction.Label(new Vector3(0.04f, 0.06f, 0f), "SCL").WithColor(Cyan),
                    SpatialAction.Warning(new Vector3(0f, 0.17f, 0f), "Missing 4.7k pull-ups").WithColor(Red),
                    SpatialAction.Arrow(new Vector3(-0.04f, 0.20f, 0f), new Vector3(-0.04f, 0.02f, 0f)).WithColor(Amber),
                    SpatialAction.Arrow(new Vector3(0.04f, 0.20f, 0f), new Vector3(0.04f, 0.02f, 0f)).WithColor(Amber),
                }
            },
            new Scenario
            {
                Id = "floating_input",
                Title = "Button input left floating",
                Speech = "That button pin has no pull-up or pull-down, so it reads random noise " +
                         "when the button is open. Use INPUT_PULLUP or add a 10k to 3V3.",
                BuildActions = () => new List<SpatialAction>
                {
                    SpatialAction.Marker(new Vector3(0f, 0f, 0f)).WithColor(Amber),
                    SpatialAction.Warning(new Vector3(0f, 0.15f, 0f), "Floating input").WithColor(Amber),
                    SpatialAction.Label(new Vector3(0f, 0.06f, 0f), "use INPUT_PULLUP").WithColor(Cyan),
                    SpatialAction.Arrow(new Vector3(0.09f, 0.19f, 0f), new Vector3(0f, 0.02f, 0f), "Add 10k here").WithColor(Amber),
                }
            },
            new Scenario
            {
                Id = "reversed_polarity",
                Title = "Component inserted backwards",
                Speech = "The LED is in backwards -- the flat edge and short leg are the cathode " +
                         "and they are on the 3V3 side. Rotate it 180 degrees.",
                BuildActions = () => new List<SpatialAction>
                {
                    SpatialAction.Marker(new Vector3(0f, 0f, 0f)).WithColor(Red),
                    SpatialAction.Warning(new Vector3(0f, 0.15f, 0f), "Reversed polarity").WithColor(Red),
                    SpatialAction.Label(new Vector3(0f, 0.06f, 0f), "cathode (flat edge)").WithColor(Cyan),
                    SpatialAction.Arrow(new Vector3(-0.07f, 0.10f, 0f), new Vector3(0.07f, 0.10f, 0f), "Flip 180 degrees").WithColor(Green),
                    SpatialAction.Highlight(new Vector3(0f, 0f, 0f), 0.035f).WithColor(Red),
                }
            },
        };

        public static IReadOnlyList<Scenario> Catalogue => All;

        public static int Count => All.Count;

        public static Scenario Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var scenario in All)
            {
                if (string.Equals(scenario.Id, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    return scenario;
                }
            }
            return null;
        }

        public static Scenario Default => Get(DefaultId) ?? All[0];

        public static Scenario At(int index)
        {
            if (All.Count == 0) return null;
            return All[((index % All.Count) + All.Count) % All.Count];
        }

        /// <summary>Builds a complete response, indistinguishable from a backend one.</summary>
        public static AIResponse Respond(Scenario scenario, string targetLabel = null)
        {
            scenario = scenario ?? Default;

            var speech = scenario.Speech;
            if (!string.IsNullOrWhiteSpace(targetLabel))
            {
                speech = targetLabel + ": " + speech;
            }

            return new AIResponse
            {
                Speech = speech,
                Actions = scenario.BuildActions(),
                Scenario = scenario.Id,
                Provider = "demo",
                Confidence = 0.82f,
                Success = true,
                IsLocal = true
            };
        }
    }
}
