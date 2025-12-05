using System;
using System.IO;
using Newtonsoft.Json;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// 간단한 JSON 저장/로드 헬퍼.
    /// 
    /// - 특정 타입(T)의 데이터를 D:\LBS_DB\IOMonitoringState.json 에 저장/복원한다.
    /// - Settings 저장, IOCheck 체크/메모 상태 저장 등 가벼운 구조의 JSON 파일 저장에 적합.
    /// 
    /// 제약:
    /// - 파일 경로는 고정이며 사용자 지정 기능은 제공하지 않음.
    /// - 파일이 없을 경우 Load<T>() 는 new T() 를 반환한다.
    /// </summary>
    public static class JsonStorageHelper
    {
        #region ───────── File Path ─────────

        /// <summary>
        /// JSON 저장 경로.
        /// 필요 시 Path 변경 기능을 추가할 수도 있음.
        /// </summary>
        private static readonly string FilePath =
            Path.Combine(@"D:\LBS_DB", "IOMonitoringState.json");

        #endregion

        #region ───────── Save<T> ─────────

        /// <summary>
        /// 객체를 JSON 문자열로 직렬화하여 파일로 저장한다.
        /// </summary>
        public static void Save<T>(T data)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"JSON 저장 실패: {FilePath}\n{ex.Message}", ex);
            }
        }

        #endregion

        #region ───────── Load<T> ─────────

        /// <summary>
        /// JSON 파일을 읽어 역직렬화 후 T 타입으로 반환한다.
        /// - 파일이 존재하지 않으면 new T() 반환.
        /// - JSON 형식이 깨졌을 경우에도 기본값 new T() 반환.
        /// </summary>
        public static T Load<T>() where T : new()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new T();

                string json = File.ReadAllText(FilePath);
                var data = JsonConvert.DeserializeObject<T>(json);

                return data != null ? data : new T();
            }
            catch
            {
                // JSON 오류 또는 파일 접근 문제 → 기본값 반환
                return new T();
            }
        }

        #endregion
    }
}
