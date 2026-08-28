using System.Security.Cryptography;
using System.Text;

namespace BLL.Helpers
{
    public static class DeviceAuthHelper
    {
        public static bool IsValidDevice(string? suppliedDeviceId, string? suppliedDeviceKey, string configuredDeviceId, string configuredDeviceKey)
        {
            if (string.IsNullOrWhiteSpace(configuredDeviceId) || string.IsNullOrWhiteSpace(configuredDeviceKey))
                return false;

            return FixedTimeEquals(suppliedDeviceId, configuredDeviceId) && FixedTimeEquals(suppliedDeviceKey, configuredDeviceKey);
        }

        private static bool FixedTimeEquals(string? supplied, string expected)
        {
            var suppliedBytes = Encoding.UTF8.GetBytes(supplied ?? string.Empty);
            var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
            return suppliedBytes.Length == expectedBytes.Length
                && CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
        }
    }
}
