namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
    }
    public static class Debug { public static void LogError(object value)=>System.Console.Error.WriteLine(value); }
}
