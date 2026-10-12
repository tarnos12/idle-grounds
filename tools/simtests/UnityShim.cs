// Lets the Unity-only allocation tests (Category "UnityOnly") COMPILE under plain .NET. They are excluded from the run.
namespace Unity.Profiling
{
    public enum ProfilerCategoryKind { Memory }
    public static class ProfilerCategory { public static readonly ProfilerCategoryKind Memory = ProfilerCategoryKind.Memory; }
    public struct ProfilerRecorder : System.IDisposable
    {
        public static ProfilerRecorder StartNew(ProfilerCategoryKind category, string name) => new ProfilerRecorder();
        public bool Valid => false;
        public long CurrentValue => 0;
        public void Dispose() { }
    }
}
