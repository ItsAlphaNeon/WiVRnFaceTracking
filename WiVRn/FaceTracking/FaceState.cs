﻿using System.Runtime.InteropServices;

namespace WiVRn.FaceTracking
{
    public struct FaceState
    {
        #region Constants
        public const int ExpressionCount = 70;
        public const int ConfidenceCount = 2;
        #endregion

        #region Static Fields
        public static readonly FaceState Identity = new FaceState { LeftEyePose = Pose.Identity, RightEyePose = Pose.Identity };
        #endregion

        #region Fields
        [MarshalAs(UnmanagedType.I1)]
        public bool FaceIsValid;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsEyeFollowingBlendshapesValid;

        public unsafe fixed float ExpressionWeights[ExpressionCount];

        public unsafe fixed float ExpressionConfidences[ConfidenceCount];

        [MarshalAs(UnmanagedType.I1)]
        public bool LeftEyeIsValid;

        [MarshalAs(UnmanagedType.I1)]
        public bool RightEyeIsValid;

        public Pose LeftEyePose;
        public Pose RightEyePose;

        public float LeftEyeConfidence;
        public float RightEyeConfidence;
        #endregion
    }
}