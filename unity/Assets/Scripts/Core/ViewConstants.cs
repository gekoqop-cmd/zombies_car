namespace ZombiCar.Core
{
    /// <summary>
    /// Game camera geometry shared by the camera rig, the aspect adapter and the screenshot tool.
    /// </summary>
    public static class ViewConstants
    {
        /// <summary>Camera tilt below the horizon, degrees.</summary>
        public const float c_CameraPitch = 50f;

        /// <summary>Distance from the followed player to the camera, metres.</summary>
        public const float c_CameraDistance = 32f;

        /// <summary>Vertical field of view at 16:9, degrees.</summary>
        public const float c_CameraFov = 30f;

        /// <summary>Upper limit for the vertical FOV on narrow (4:3) screens, degrees.</summary>
        public const float c_MaxCameraFov = 42f;

        public const float c_NearClip = 3f;     // camera is ~32 m away: a far near plane keeps depth precise for decals
        public const float c_FarClip = 140f;
    }
}
