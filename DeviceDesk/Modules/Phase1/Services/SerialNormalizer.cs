namespace DeviceDesk.Modules.Phase1.Services
{
    /// <summary>
    /// Centralized serial normalization to ensure consistent matching across import, scan, and verification
    /// </summary>
    public static class SerialNormalizer
    {
        /// <summary>
        /// Rule: UPPER + trim + remove spaces & hyphens only. Do NOT add/strip anything else.
        /// This ensures RNR-2025-011, RNR 2025 011, and rnr2025011 all normalize to RNR2025011
        /// </summary>
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim().ToUpperInvariant();
            s = s.Replace(" ", "").Replace("-", "");
            return s;
        }
    }
}
