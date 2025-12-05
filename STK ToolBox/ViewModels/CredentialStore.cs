using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace STK_ToolBox.ViewModels
{
    #region DTO Classes (Serializable Models)

    [Serializable]
    public class CredentialEntry
    {
        /// <summary>대상 IP</summary>
        public string Ip { get; set; }

        /// <summary>계정 ID</summary>
        public string UserName { get; set; }

        /// <summary>DPAPI로 보호된 비밀번호 바이트</summary>
        public byte[] ProtectedPassword { get; set; }

        /// <summary>추가 엔트로피(보호/복호화용)</summary>
        public byte[] Entropy { get; set; }
    }

    [Serializable]
    public class CredentialData
    {
        /// <summary>IP별 계정 목록</summary>
        public List<CredentialEntry> Items { get; set; } = new List<CredentialEntry>();
    }

    #endregion

    #region CredentialStore (파일 저장/로드 + DPAPI 암복호화)

    /// <summary>
    /// IP별 계정 정보를 로컬(AppData\STK_ToolBox\credentials.xml)에 저장/로드하는 스토어.
    /// 비밀번호는 DPAPI(CurrentUser)로 보호한다.
    /// </summary>
    public class CredentialStore
    {
        #region Fields

        private readonly string _folderPath;
        private readonly string _filePath;
        private CredentialData _data = new CredentialData();

        #endregion

        #region Constructor

        public CredentialStore()
        {
            _folderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "STK_ToolBox");

            _filePath = Path.Combine(_folderPath, "credentials.xml");
        }

        #endregion

        #region Load / Save

        /// <summary>
        /// credentials.xml을 읽어 _data에 로드한다.
        /// 실패 시 빈 CredentialData로 초기화.
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var xs = new XmlSerializer(typeof(CredentialData));
                    using (var fs = File.OpenRead(_filePath))
                    {
                        _data = (CredentialData)xs.Deserialize(fs);
                    }
                }
                else
                {
                    _data = new CredentialData();
                }
            }
            catch
            {
                _data = new CredentialData();
            }
        }

        /// <summary>
        /// _data를 credentials.xml에 저장한다.
        /// 저장 실패 시 조용히 무시(필요시 로깅 추가 가능).
        /// </summary>
        public void Save()
        {
            try
            {
                if (!Directory.Exists(_folderPath))
                    Directory.CreateDirectory(_folderPath);

                var xs = new XmlSerializer(typeof(CredentialData));
                using (var fs = File.Create(_filePath))
                {
                    xs.Serialize(fs, _data);
                }
            }
            catch
            {
                // 저장 실패 무시 (필요시 로깅)
            }
        }

        #endregion

        #region Public API (Set / Get)

        /// <summary>
        /// 해당 IP에 대한 계정을 설정(추가 또는 갱신)하고, 비밀번호를 DPAPI로 보호한다.
        /// </summary>
        public void Set(string ip, string user, string passwordPlain)
        {
            if (string.IsNullOrWhiteSpace(ip)) return;

            var entry = _data.Items.Find(e => e.Ip == ip);
            if (entry == null)
            {
                entry = new CredentialEntry { Ip = ip };
                _data.Items.Add(entry);
            }

            entry.UserName = user ?? string.Empty;

            // 엔트로피를 간단하게 GUID로 생성
            var entropy = Guid.NewGuid().ToByteArray();
            entry.Entropy = entropy;
            entry.ProtectedPassword = ProtectString(passwordPlain ?? string.Empty, entropy);
        }

        /// <summary>
        /// 해당 IP에 대한 복호화된 계정 정보를 반환한다. 없으면 null 반환.
        /// </summary>
        public CredentialForUse Get(string ip)
        {
            var e = _data.Items.Find(x => x.Ip == ip);
            if (e == null) return null;

            return new CredentialForUse
            {
                Ip = ip,
                UserName = e.UserName ?? string.Empty,
                Password = UnprotectToString(e.ProtectedPassword, e.Entropy) ?? string.Empty
            };
        }

        #endregion

        #region DPAPI Helpers

        private static byte[] ProtectString(string plain, byte[] entropy)
        {
            var bytes = Encoding.UTF8.GetBytes(plain ?? string.Empty);
            return ProtectedData.Protect(bytes, entropy, DataProtectionScope.CurrentUser);
        }

        private static string UnprotectToString(byte[] protectedBytes, byte[] entropy)
        {
            if (protectedBytes == null) return string.Empty;

            var bytes = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }

        #endregion
    }

    #endregion

    #region CredentialForUse (사용용 DTO)

    /// <summary>
    /// 실제 사용 시 ViewModel/코드에서 쓰기 편하도록 복호화된 계정 객체.
    /// </summary>
    public class CredentialForUse
    {
        public string Ip { get; set; }
        public string UserName { get; set; }

        /// <summary>복호화된 평문 비밀번호.</summary>
        public string Password { get; set; }

        public string GetPasswordPlain() => Password;
    }

    #endregion
}
