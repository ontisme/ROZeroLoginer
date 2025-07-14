using System;
using System.Globalization;
using System.Windows.Data;
using ROZeroLoginer.Models;

namespace ROZeroLoginer.Utils
{
    public class PrivacyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return values?[0] ?? "";

            var originalValue = values[0]?.ToString() ?? "";
            var settings = values[1] as AppSettings;
            
            // Debug: Always show masked for testing
            System.Diagnostics.Debug.WriteLine($"PrivacyConverter - Value: {originalValue}, Settings: {settings?.GetType().Name}, Parameter: {parameter}");
            
            if (settings == null)
            {
                System.Diagnostics.Debug.WriteLine("Settings is null");
                return originalValue;
            }
            
            if (!settings.PrivacyModeEnabled)
            {
                System.Diagnostics.Debug.WriteLine("Privacy mode disabled");
                return originalValue;
            }

            var fieldType = parameter?.ToString();
            System.Diagnostics.Debug.WriteLine($"Processing field type: {fieldType}");
            
            switch (fieldType)
            {
                case "Name":
                    return settings.HideNames ? MaskString(originalValue) : originalValue;
                case "Username":
                    return settings.HideUsernames ? MaskString(originalValue) : originalValue;
                case "Password":
                    return settings.HidePasswords ? new string('*', Math.Max(8, originalValue.Length)) : originalValue;
                case "SecretKey":
                    return settings.HideSecretKeys ? MaskString(originalValue) : originalValue;
                default:
                    return originalValue;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private string MaskString(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            if (original.Length <= 2)
                return new string('*', original.Length);

            return original[0] + new string('*', Math.Max(1, original.Length - 2)) + original[original.Length - 1];
        }
    }
}