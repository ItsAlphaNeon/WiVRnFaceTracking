using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Threading;
using System.CodeDom;


namespace WVFaceTracking
{
    // Main mod class for WiVRnFaceTracking, inherits from ResoniteMod
    public class WVFaceTracking : ResoniteMod
    {
        // Configuration key for exponent applied to eye openness
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> EyeOpennessExponent =
          new("quest_pro_eye_open_exponent",
            "Exponent to apply to eye openness.  Can be updated at runtime.  Useful for applying different curves for how open your eyes are.",
            () => 1.0f);

        // Configuration key for multiplier applied to eye wideness
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> EyeWideMultiplier =
          new("quest_pro_eye_wide_multiplier",
            "Multiplier to apply to eye wideness.  Can be updated at runtime.  Useful for multiplying the amount your eyes can widen by.",
            () => 1.0f);

        // Configuration key for multiplier applied to eye movement range
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> EyeMovementMultiplier =
          new("quest_pro_eye_movement_multiplier",
            "Multiplier to adjust the movement range of the user's eyes.  Can be updated at runtime.", () => 1.0f);

        // Configuration key for multiplier applied to eye expression range
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> EyeExpressionMultiplier =
          new("quest_pro_eye_expression_multiplier",
            "Multiplier to adjust the range of the user's eye expressions.  Can be updated at runtime.", () => 1.0f);


        // Holds the mod configuration instance
        private static ModConfiguration? _config;

        // Name of the mod (displayed in mod manager)
        public override string Name => "WiVRnFaceTracking";

        // Author of the mod
        public override string Author => "AlphaNeon (Github/ItsAlphaNeon)";

        // Version of the mod
        public override string Version => "1.0.0";

        // Reference to the proxy instance for face tracking
        public static WVProxy? proxy;

        // Runtime values for configuration keys (used by other classes)
        public static float EyeOpenExponent = 1.0f;
        public static float EyeWideMult = 1.0f;
        public static float EyeMoveMult = 1.0f;
        public static float EyeExpressionMult = 1.0f;

        // Static logging method for other classes to use
        public static void Log(string message)
        {
            WVFaceTracking.Msg($"[WiVRnFaceTracking] {message}");
        }

        // Singleton instance for static access to this mod class
        public static WVFaceTracking? Instance { get; private set; }

        // Constructor sets the singleton instance
        public WVFaceTracking()
        {
            Instance = this;
        }

        // Called when the engine initializes the mod
        public override void OnEngineInit()
        {
            // Get the configuration and subscribe to changes
            _config = GetConfiguration();
            if (_config != null)
                _config.OnThisConfigurationChanged += OnConfigurationChanged;

            // Patch all Harmony patches in this assembly
            new Harmony("org.alphaneon.WiVRnFaceTracking").PatchAll();
        }

        // Called when a configuration value changes
        private void OnConfigurationChanged(ConfigurationChangedEvent @event)
        {
            // Update runtime values if the corresponding config key changed
            if (@event.Key == EyeOpennessExponent)
            {
                if (@event.Config.TryGetValue(EyeOpennessExponent, out var openExp))
                {
                    EyeOpenExponent = openExp;
                }
            }

            if (@event.Key == EyeWideMultiplier)
            {
                if (@event.Config.TryGetValue(EyeWideMultiplier, out var wideMulti))
                {
                    EyeWideMult = wideMulti;
                }
            }

            if (@event.Key == EyeMovementMultiplier)
            {
                if (@event.Config.TryGetValue(EyeMovementMultiplier, out var moveMulti))
                {
                    EyeMoveMult = moveMulti;
                }
            }

            if (@event.Key == EyeExpressionMultiplier)
            {
                if (@event.Config.TryGetValue(EyeExpressionMultiplier, out var eyeExpressionMulti))
                {
                    EyeExpressionMult = eyeExpressionMulti;
                }
            }
        }

        // Harmony patch for InputInterface constructor
        [HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Engine) })]
        public class InputInterfaceCtorPatch
        {
            // Called after InputInterface is constructed
            public static void Postfix(InputInterface __instance)
            {
                // Create and initialize the face tracking proxy
                proxy = new WVProxy();

                // If initialization fails, do not register the driver
                if (!proxy.Initialize()) return;

                // Register the proxy as an input driver
                __instance.RegisterInputDriver(proxy);

                // Ensure proxy teardown on engine shutdown
                Engine.Current.OnShutdown += () => proxy.Teardown();
            }
        }
    }
}
