using System;
using System.Globalization;
using System.Windows.Controls;

namespace SimpleRtspPlayer.GUI
{
    class AddressValidationRule : ValidationRule
    {
        private const string RtspPrefix = "rtsp://";

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var address = value as string;

            const string invalidAddress = "Invalid address";

            if (string.IsNullOrEmpty(address))
                return new ValidationResult(false, invalidAddress);

            if (!address.StartsWith(RtspPrefix))
                address = RtspPrefix + address;

            if (!Uri.TryCreate(address, UriKind.Absolute, out _))
                return new ValidationResult(false, invalidAddress);

            return new ValidationResult(true, null);
        }
    }
}