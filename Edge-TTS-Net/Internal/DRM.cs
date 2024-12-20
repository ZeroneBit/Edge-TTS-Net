﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace edge_tts_net.Internal
{
    internal class DRM
    {
        public static string Generate_Sec_ms_gec()
        {
            long ticks = DateTime.Now.ToFileTimeUtc();
            ticks -= ticks % 3_000_000_000;

            var str = ticks + Constants.TRUSTED_CLIENT_TOKEN;
            var sha256 = SHA256.Create();
            var result = BytesToHexString(sha256.ComputeHash(Encoding.ASCII.GetBytes(str)));
            return result;
        }

        private static string BytesToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
        }

        public static void HandleClientResponse401Error(string date)
        {
            var serverDate = ParseRfc2616Date(date);
            var clientDate = GetUnixTimestamp();

            clock_skew_seconds += serverDate - clientDate;
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

            return unixTimestamp + clock_skew_seconds;
        }

        private static double clock_skew_seconds = 0;

        private static double WIN_EPOCH = 11644473600;
        private static double S_TO_NS = 1e9;
    }
}
