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

            if (string.IsNullOrEmpty(address))
                throw new UriFormatException();

            if (!address.StartsWith(RtspPrefix))
                address = RtspPrefix + address;

            if (!Uri.TryCreate(address, UriKind.Absolute, out _))
                return new ValidationResult(false, "Invalid address");

            return new ValidationResult(true, null);
        }
    }
}