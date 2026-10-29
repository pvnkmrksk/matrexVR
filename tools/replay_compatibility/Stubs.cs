namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration,BeforeSceneLoad }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : System.Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){} }
    public struct Vector3
    {
        public float x,y,z;
        public static Vector3 one=>new Vector3(1,1,1);
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
    }
    public static class Debug { public static void LogError(object value)=>System.Console.Error.WriteLine(value); }
}
