using UnityEngine.SceneManagement;

namespace DesertLantern
{
    internal static class PropertyTravelManager
    {
        private static PropertyId pendingProperty;

        public static void Reset() => pendingProperty = PropertyId.None;

        public static bool Visit(PropertyId property)
        {
            int slot = CountyPortfolio.MapSlot(property);
            if (slot < 0 || CountyPortfolio.Offers[slot].Layout == PropertyLayout.Dashboard)
                return false;
            pendingProperty = property;
            SceneManager.LoadScene("MotelExterior");
            return true;
        }

        public static PropertyId Arrive()
        {
            PropertyId property = pendingProperty == PropertyId.None ? PropertyId.RoadsideMotel : pendingProperty;
            Reset();
            return property;
        }

        public static void ReturnToCounty()
        {
            Reset();
            SceneManager.LoadScene("CountyMap");
        }
    }
}
