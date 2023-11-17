namespace RtspClientSharp.Utils
{
    static class NumberUtils
    {
        public static bool IsNegativeNumber(string input)
        {
            if (int.TryParse(input, out int number))
            {
                return number < 0;
            }
            else
            {
                return false;
            }
        }
    }
}
