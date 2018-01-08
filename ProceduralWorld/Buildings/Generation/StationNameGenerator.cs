using Equinox.ProceduralWorld.Buildings.Storage;

namespace Equinox.ProceduralWorld.Buildings.Generation
{
    public static class StationNameGenerator
    {
        public static string GetName(this ProceduralRoom module)
        {
            return "M" + module.GetHashCode().ToString("X");
        }
    }
}
