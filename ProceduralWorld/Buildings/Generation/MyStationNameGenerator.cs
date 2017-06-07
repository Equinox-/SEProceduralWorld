using Equinox.ProceduralWorld.Buildings.Storage;

namespace Equinox.ProceduralWorld.Buildings.Generation
{
    public static class MyStationNameGenerator
    {
        public static string GetName(this MyProceduralRoom module)
        {
            return "M" + module.GetHashCode().ToString("X");
        }
    }
}
