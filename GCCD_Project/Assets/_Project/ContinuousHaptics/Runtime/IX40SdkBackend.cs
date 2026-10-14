namespace GCCD.ContinuousHaptics
{
    public interface IX40DeviceIdentity
    {
        string DeviceKey { get; }
        bool Physical { get; }
    }
    public interface IX40SdkBackend
    {
        bool CheckConnection(bool confirmRenamedX40, out string status);
        int PlayMotors(int[] motors, int durationMillis);
        bool StopRequest(int requestId);
    }
}
