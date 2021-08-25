using System;
using System.Collections.Generic;
using System.Linq;

namespace RtspClientSharp.Rtp
{
    public enum RtpH265NALUType
    {
        #region VCL
        TRAIL_N = 0,
        TRAIL_R = 1,
        TSA_N = 2,
        TSA_R = 3,
        STSA_N = 4,
        STSA_R = 5,
        RADL_N = 6,
        RADL_R = 7,
        RASL_N = 8,
        RASL_R = 9,
        RSV_VCL_N10 = 10,
        RSV_VCL_R11 = 11,
        RSV_VCL_N12 = 12,
        RSV_VCL_R13 = 13,
        RSV_VCL_N14 = 14,
        RSV_VCL_R15 = 15,
        BLA_W_LP = 16,
        BLA_W_RADL = 17,
        BLA_N_LP = 18,
        IDR_W_RADL = 19,
        IDR_N_LP = 20,
        CRA_NUT = 21,
        RSV_IRAP_VCL22 = 22,
        RSV_IRAP_VCL23 = 23,
        RSV_VCL24 = 24,
        RSV_VCL25 = 25,
        RSV_VCL26 = 26,
        RSV_VCL27 = 27,
        RSV_VCL28 = 28,
        RSV_VCL29 = 29,
        RSV_VCL30 = 30,
        RSV_VCL31 = 31,
        #endregion
        #region non-VCL
        VPS_NUT = 32,
        SPS_NUT = 33,
        PPS_NUT = 34,
        AUD_NUT = 35,
        EOS_NUT = 36,
        EOB_NUT = 37,
        FD_NUT = 38,
        PREFIX_SEI_NUT = 39,
        SUFFIX_SEI_NUT = 40,
        RSV_NVCL41 = 41,
        RSV_NVCL42 = 42,
        RSV_NVCL43 = 43,
        RSV_NVCL44 = 44,
        RSV_NVCL45 = 45,
        RSV_NVCL46 = 46,
        RSV_NVCL47 = 47,
        #endregion
        #region - Unspecified by HEVC + RTP HEVC NALU type
        RTPHEVC_AP = 48, // RTP HEVC Aggregation Packet
        RTPHEVC_FP = 49  // RTP HEVC Fragmentation Packet
        #endregion
    }

    public enum SliceType
    {
        Undefined = -1,
        BSlice = 0,
        PSlice = 1,
        ISlice = 2,
    }

    public static class RtpH265TypeUtils
    {
        public const int RtpHevcPayloadHeaderSize = 2;
        public const int RtpHevcFuHeaderSize = 1;
        public const int RtpHevcDonlFieldSize = 2;
        public const int RtpHevcDondFieldSize = 1;
        public const int RtpHevcApNaluLengthFieldSize = 2;
        //7.4.2.1
        public const int MakxSubLayers = 7;
        public const int MaxVpsCount = 16;
        public const int MaxSpsCount = 32;
        public const int MaxPpsCount = 256;
        public const int MaxShortTermRpsCount = 64;
        public const int MaxCuSize = 128;

        public static bool CheckIfIsValid(int nalUType)
        {
            List<RtpH265NALUType> rtpNalUnitTypes = Enum.GetValues(typeof(RtpH265NALUType)).Cast<RtpH265NALUType>().ToList();

            return rtpNalUnitTypes.Where(n => (int)n == nalUType).Count() > 0;
        }
    }
}
