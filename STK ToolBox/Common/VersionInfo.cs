using System.Reflection;

namespace STK_ToolBox.Common
{
    /// <summary>
    /// 화면에서 사용할 버전/빌드/워터마크 문자열을 한 곳에서 관리
    /// </summary>
    public static class VersionInfo
    {
        // 팀/제작자 표기(워터마크·)
        public const string Owner = "Lee Dong-hee";

        // 어셈블리 버전(AssemblyInformationalVersion이 있으면 우선, 없으면 AssemblyVersion 사용)
        public static string AppVersion
        {
            get
            {
                var asm = Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(info))
                    return info;

                return asm.GetName().Version.ToString(); // AssemblyVersion("1.1.0.0") 결과
            }
        }

        // 빌드시 생성된 상수(로컬 시각 문자열)
        public static string BuildLocal => BuildInfo.BuildDateLocal;

        // 우상단에 그대로 뿌릴 텍스트
        public static string Display => $"v{AppVersion} • {BuildLocal} • {Owner}";

        // 중앙 워터마크 텍스트
        public static string Watermark => "STK ToolBox — Built by LDH";
    }
}
