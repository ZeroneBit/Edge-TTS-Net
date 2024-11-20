using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace edge_tts_net
{
    internal class DRM
    {
        public static string Generate_Sec_ms_gec()
        {
            var ticks = DRM.GetUnixTimestamp();
            ticks += WIN_EPOCH;
            ticks -= ticks % 300;
            ticks *= S_TO_NS / 100;

            var str = ticks.ToString() + Constants.TRUSTED_CLIENT_TOKEN;

            var sha256 = SHA256.Create();
            var result = BytesToHexString(sha256.ComputeHash(Encoding.ASCII.GetBytes(str)));
            return result;
        }

        private static string BytesToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        public static void HandleClientResponse401Error(string date)
        {
            var serverDate = ParseRfc2616Date(date);
            var clientDate = GetUnixTimestamp();

            DRM.clock_skew_seconds += (serverDate - clientDate);
        }

        public static double ParseRfc2616Date(string date)
        {
            DateTime dateTime = DateTime.ParseExact(date, "ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
        }

        public static double GetUnixTimestamp()
        {
            DateTime utcNow = DateTime.UtcNow;

            double unixTimestamp = new DateTimeOffset(utcNow).ToUnixTimeSeconds();

            return unixTimestamp + DRM.clock_skew_seconds;
        }

        private static double clock_skew_seconds = 0;

        private static double WIN_EPOCH = 11644473600;
        private static double S_TO_NS = 1e9;
    }
}
