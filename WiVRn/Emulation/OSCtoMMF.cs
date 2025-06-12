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
        private OSCReader? _oscReader;

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
            _oscReader = oscReader ?? new OSCReader();
            mmfThread = new Thread(RunLoop);
            mmfThread.IsBackground = true;
            mmfThread.Start();
        }

        private static Quaternion QuaternionFromEulerDegrees(float pitch, float yaw, float roll)
        {
            double radPitch = pitch * Math.PI / 180.0;
            double radYaw = yaw * Math.PI / 180.0;
            double radRoll = roll * Math.PI / 180.0;
            double cy = Math.Cos(radYaw * 0.5);
            double sy = Math.Sin(radYaw * 0.5);
            double cp = Math.Cos(radPitch * 0.5);
            double sp = Math.Sin(radPitch * 0.5);
            double cr = Math.Cos(radRoll * 0.5);
            double sr = Math.Sin(radRoll * 0.5);

            return new Quaternion
            {
                W = (float)(cr * cp * cy + sr * sp * sy),
                X = (float)(sr * cp * cy - cr * sp * sy),
                Y = (float)(cr * sp * cy + sr * cp * sy),
                Z = (float)(cr * cp * sy - sr * sp * cy)
            };
        }

        private static void SetDefaultEyePoses(ref FaceState state)
        {
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
        }

        private unsafe void RunLoop()
        {
            var oscReader = _oscReader!;
            WiVRnLogger.Log("[OSCtoMMF] RunLoop started. Waiting 1s for OSCReader...");
            Thread.Sleep(1000); // Give OSCReader a moment to start
            int loopCount = 0;
            while (running)
            {
                loopCount++;
                // 2. Prepare FaceState struct
                FaceState state = new FaceState();

                // 3. Map OSC values to FaceState fields
                {
                    // Use OSC value for eye tracking: "/tracking/eye/LeftRightPitchYaw" expected format:
                    // "leftPitch leftYaw rightPitch rightYaw"
                    var eyeValue = oscReader.GetValue("/tracking/eye/LeftRightPitchYaw");
                    if (!string.IsNullOrEmpty(eyeValue))
                    {
                        var tokens = eyeValue!.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 4 &&
                            float.TryParse(tokens[0], out float leftPitch) &&
                            float.TryParse(tokens[1], out float leftYaw) &&
                            float.TryParse(tokens[2], out float rightPitch) &&
                            float.TryParse(tokens[3], out float rightYaw))
                        {
                            state.LeftEyePose = new Pose
                            {
                                Position = new Vector3 { X = 0, Y = 0, Z = 0 },
                                Orientation = QuaternionFromEulerDegrees(-leftYaw, leftPitch, 0) // Only negate yaw for correct left/right
                            };
                            state.RightEyePose = new Pose
                            {
                                Position = new Vector3 { X = 0, Y = 0, Z = 0 },
                                Orientation = QuaternionFromEulerDegrees(-rightYaw, rightPitch, 0) // Only negate yaw for correct left/right
                            };
                            state.LeftEyeIsValid = true;
                            state.RightEyeIsValid = true;
                            state.FaceIsValid = true;
                            state.IsEyeFollowingBlendshapesValid = true;
                        }
                        else
                        {
                            SetDefaultEyePoses(ref state);
                            state.LeftEyeIsValid = false;
                            state.RightEyeIsValid = false;
                            state.FaceIsValid = false;
                            state.IsEyeFollowingBlendshapesValid = false;
                        }
                    }
                    else
                    {
                        SetDefaultEyePoses(ref state);
                        state.LeftEyeIsValid = false;
                        state.RightEyeIsValid = false;
                        state.FaceIsValid = false;
                        state.IsEyeFollowingBlendshapesValid = false;
                    }
                }

                // 4. Map OSC parameters to ExpressionWeights (unsafe context required)
                unsafe
                {
                    float* weights = state.ExpressionWeights;
                    weights[(int)Expressions.BrowLowererL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/BrowExpressionLeft");
                    weights[(int)Expressions.BrowLowererR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/BrowExpressionRight");
                    weights[(int)Expressions.CheekPuffL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/CheekPuffLeft");
                    weights[(int)Expressions.CheekPuffR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/CheekPuffRight");
                    weights[(int)Expressions.EyesClosedL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/EyeLidLeft");
                    weights[(int)Expressions.EyesClosedR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/EyeLidRight");
                    weights[(int)Expressions.LidTightenerL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/EyeSquintLeft");
                    weights[(int)Expressions.LidTightenerR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/EyeSquintRight");
                    weights[(int)Expressions.JawDrop] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/JawOpen");
                    weights[(int)Expressions.JawSidewaysLeft] = -GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/JawX");
                    weights[(int)Expressions.JawSidewaysRight] = -GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/JawX");
                    weights[(int)Expressions.LipFunnelerLb] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/LipFunnelLower");
                    weights[(int)Expressions.LipFunnelerLt] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/LipFunnelUpper");
                    weights[(int)Expressions.LipFunnelerRb] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/LipFunnelLower");
                    weights[(int)Expressions.LipFunnelerRt] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/LipFunnelUpper");
                    weights[(int)Expressions.LipPuckerL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/LipPucker");
                    weights[(int)Expressions.LipPuckerR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/LipPucker");
                    weights[(int)Expressions.MouthLeft] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/MouthX");
                    weights[(int)Expressions.MouthRight] = -GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/MouthX");
                    weights[(int)Expressions.LipStretcherL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/MouthStretchTightenLeft");
                    weights[(int)Expressions.LipStretcherR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/MouthStretchTightenRight");
                    weights[(int)Expressions.UpperLipRaiserL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/MouthUpperUp");
                    weights[(int)Expressions.UpperLipRaiserR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/MouthUpperUp");
                    weights[(int)Expressions.LowerLipDepressorL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/MouthLowerDown");
                    weights[(int)Expressions.LowerLipDepressorR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/MouthLowerDown");
                    weights[(int)Expressions.LipCornerPullerL] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/SmileSadLeft");
                    weights[(int)Expressions.LipCornerPullerR] = GetOSCParameter(_oscReader!, "/avatar/parameters/FT/v2/SmileSadRight");
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
