using System;

namespace STK_ToolBox.Models
{
    /// <summary>
    /// IO Byte 단위 세그먼트 정보 모델.
    /// 
    /// - IOMonitoring 테이블 또는 인버터 설정에서 생성되는 Byte 구간을 표현한다.
    /// - 하나의 세그먼트는 특정 Channel + Device(X/Y) + 시작 주소(HEX) + Byte 크기로 구성된다.
    /// - Source는 어떤 기준(IOMonitoring / Inverter 등)에서 생성되었는지 표시하는 메타 정보.
    /// 
    /// 예)
    ///   Channel = 81
    ///   Device  = "X"
    ///   StartAddressHex = "1A0"
    ///   ByteSize = 2
    ///   Source = "IOMonitoring"
    /// </summary>
    public class IOByteSegment
    {
        #region ───────── Channel / Device ─────────

        /// <summary>
        /// CC-Link 또는 내부 채널 번호.
        /// 예: 81, 60, 1 등.
        /// </summary>
        public int Channel { get; set; }

        /// <summary>
        /// IO Device 구분: "X" 또는 "Y".
        /// </summary>
        public string Device { get; set; }

        #endregion

        #region ───────── Address / Size ─────────

        /// <summary>
        /// 시작 주소(HEX). Device 문자는 제외한 순수 16진 문자열.
        /// 예: "0", "120", "1A0"
        /// </summary>
        public string StartAddressHex { get; set; }

        /// <summary>
        /// 이 세그먼트가 차지하는 Byte 수.
        /// 예: 1, 2, 4, 8 ...
        /// </summary>
        public int ByteSize { get; set; }

        #endregion

        #region ───────── Meta Information ─────────

        /// <summary>
        /// 세그먼트가 생성된 출처.
        /// - "IOMonitoring": DB로부터 생성
        /// - "Inverter": 인버터 설정으로 생성
        /// </summary>
        public string Source { get; set; }

        #endregion

        #region ───────── Debugging ─────────

        public override string ToString()
            => $"CH:{Channel}, Dev:{Device}, Addr:{StartAddressHex}, Bytes:{ByteSize}, Src:{Source}";

        #endregion
    }
}
