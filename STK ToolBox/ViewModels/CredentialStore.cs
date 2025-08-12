using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace STK_ToolBox.ViewModels
{
    [Serializable]
    public class CredentialEntry
    {
        public string Ip { get; set; }
        public string UserName { get; set; }
        public byte[] ProtectedPassword { get; set; } // DPAPI로 보호된 바이트
        public byte[] Entropy { get; set; } // 추가 엔트로피(옵션)
    }

    [Serializable]
    public class CredentialData
    {
        public List<CredentialEntry> Items { get; set; } = new List<CredentialEntry>();
    }

    public class CredentialStore
    {
        private readonly string _folderPath;
        private readonly string _filePath;
        private CredentialData _data = new CredentialData();

        public CredentialStore()
        {
            _folderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "STK_ToolBox");
            _filePath = Path.Combine(_folderPath, "credentials.xml");
        }

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

        public void Set(string ip, string user, string passwordPlain)
        {
            if (string.IsNullOrWhiteSpace(ip)) return;

            var entry = _data.Items.Find(e => e.Ip == ip);
            if (entry == null)
            {
                entry = new CredentialEntry { Ip = ip };
                _data.Items.Add(entry);
            }

            entry.UserName = user ?? "";
            var entropy = Guid.NewGuid().ToByteArray(); // 간단한 엔트로피
            entry.Entropy = entropy;
            entry.ProtectedPassword = ProtectString(passwordPlain ?? "", entropy);
        }

        public CredentialForUse Get(string ip)
        {
            var e = _data.Items.Find(x => x.Ip == ip);
            if (e == null) return null;

            return new CredentialForUse
            {
                Ip = ip,
                UserName = e.UserName ?? "",
                Password = UnprotectToString(e.ProtectedPassword, e.Entropy) ?? ""
            };
        }

        private static byte[] ProtectString(string plain, byte[] entropy)
        {
            var bytes = Encoding.UTF8.GetBytes(plain ?? "");
            return ProtectedData.Protect(bytes, entropy, DataProtectionScope.CurrentUser);
        }

        private static string UnprotectToString(byte[] protectedBytes, byte[] entropy)
        {
            if (protectedBytes == null) return "";
            var bytes = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public class CredentialForUse
    {
        public string Ip { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string GetPasswordPlain() => Password;
    }
}
