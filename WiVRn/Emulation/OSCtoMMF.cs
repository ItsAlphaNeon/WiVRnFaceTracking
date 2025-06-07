using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using WiVRn.FaceTracking;

namespace WiVRn.Emulation
{
    public class OSCtoMMF
    {
        private Thread? mmfThread;
        private bool running = false;
        private EventWaitHandle? mmfEvent;

        public void Start(OSCReader? oscReader = null)
        {
            if (running) return;
            running = true;
            WiVRnLogger.Log("[OSCtoMMF] Start() called, ensuring event exists and starting MMF thread.");
            // Ensure the event exists before starting the thread
            try
            {
                mmfEvent = EventWaitHandle.OpenExisting("WiVRn.BodyStateEvent");
                WiVRnLogger.Log("[OSCtoMMF] Opened existing EventWaitHandle 'WiVRn.BodyStateEvent'.");
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                mmfEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "WiVRn.BodyStateEvent");
                WiVRnLogger.Log("[OSCtoMMF] Created new EventWaitHandle 'WiVRn.BodyStateEvent'.");
            }
            mmfThread = new Thread(() => RunLoop(oscReader ?? new OSCReader()));
            mmfThread.IsBackground = true;
            mmfThread.Start();
        }

        private unsafe void RunLoop(OSCReader oscReader)
        {
            WiVRnLogger.Log("[OSCtoMMF] RunLoop started. Waiting 1s for OSCReader...");
            Thread.Sleep(1000); // Give OSCReader a moment to start
            int loopCount = 0;
            while (running)
            {
                loopCount++;
                // 2. Prepare FaceState struct
                FaceState state = new FaceState();

                // 3. Map OSC values to FaceState fields
                state.LeftEyeIsValid = true;
                state.RightEyeIsValid = true;
                state.IsEyeFollowingBlendshapesValid = true;
                state.FaceIsValid = true;

                state.LeftEyePose = new Pose
                {
                    Position = new Vector3 { X = 0, Y = 0, Z = 0 },
                    Orientation = new Quaternion { X = 0, Y = 0, Z = 0, W = 1 }
                };
                state.RightEyePose = new Pose
                {
                    Position = new Vector3 { X = 0, Y = 0, Z = 0 },
                    Orientation = new Quaternion { X = 0, Y = 0, Z = 0, W = 1 }
                };

                // 4. Map OSC parameters to ExpressionWeights (unsafe context required)
                unsafe
                {
                    float* weights = state.ExpressionWeights;
                    weights[(int)Expressions.BrowLowererL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/BrowExpressionLeft");
                    weights[(int)Expressions.BrowLowererR] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/BrowExpressionRight");
                    weights[(int)Expressions.CheekPuffL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/CheekPuffLeft");
                    weights[(int)Expressions.CheekPuffR] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/CheekPuffRight");
                    weights[(int)Expressions.EyesClosedL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/EyeLidLeft");
                    weights[(int)Expressions.EyesClosedR] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/EyeLidRight");
                    weights[(int)Expressions.LidTightenerL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/EyeSquintLeft");
                    weights[(int)Expressions.LidTightenerR] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/EyeSquintRight");
                    weights[(int)Expressions.JawDrop] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/JawOpen");
                    weights[(int)Expressions.JawSidewaysLeft] = -GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/JawX");
                    weights[(int)Expressions.JawSidewaysRight] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/JawX");
                    weights[(int)Expressions.LipFunnelerLb] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/LipFunnelLower");
                    weights[(int)Expressions.LipFunnelerLt] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/LipFunnelUpper");
                    weights[(int)Expressions.LipPuckerL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/LipPucker");
                    weights[(int)Expressions.MouthLeft] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/MouthX");
                    weights[(int)Expressions.MouthRight] = -GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/MouthX");
                    weights[(int)Expressions.LipStretcherL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/MouthStretchTightenLeft");
                    weights[(int)Expressions.LipStretcherR] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/MouthStretchTightenRight");
                    weights[(int)Expressions.UpperLipRaiserL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/MouthUpperUp");
                    weights[(int)Expressions.LowerLipDepressorL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/MouthLowerDown");
                    weights[(int)Expressions.LipCornerPullerL] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/SmileSadLeft");
                    weights[(int)Expressions.LipCornerPullerR] = GetOSCParameter(oscReader, "/avatar/parameters/FT/v2/SmileSadRight");
                }

                // 5. Write to MMF
                int size = Marshal.SizeOf<FaceState>();
                using (var mmf = MemoryMappedFile.CreateOrOpen("WiVRn.BodyState", size))
                using (var accessor = mmf.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite))
                {
                    accessor.Write(0, ref state);
                    if (loopCount % 100 == 0) // Log every 2 seconds at 50Hz
                        WiVRnLogger.Log($"[OSCtoMMF] Wrote FaceState to MMF (iteration {loopCount})");
                }

                // 6. Signal the event
                if (mmfEvent != null)
                {
                    mmfEvent.Set();
                    if (loopCount % 100 == 0)
                        WiVRnLogger.Log($"[OSCtoMMF] Signaled BodyStateEvent (iteration {loopCount})");
                }

                if (loopCount % 100 == 0)
                    WiVRnLogger.Log($"[OSCtoMMF] Loop running (iteration {loopCount})");

                Thread.Sleep(20); // 50Hz update
            }
        }

        static float GetOSCParameter(OSCReader oscReader, string name)
        {
            var value = oscReader.GetValue(name);
            if (value == null) return 0.0f;
            if (float.TryParse(value, out var f)) return f;
            // If value is a quoted string, try to parse inside quotes
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                var inner = value.Trim('"');
                if (float.TryParse(inner, out f)) return f;
            }
            return 0.0f;
        }
    }
}
