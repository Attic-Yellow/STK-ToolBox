using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace STK_ToolBox.Helpers
{
    public static class JsonStorageHelper
    {
        private static string FilePath = Path.Combine("D:\\LBS_DB", "IOMonitoringState.json");

        public static void Save<T>(T data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }

        public static T Load<T>() where T : new()
        {
            if (!File.Exists(FilePath))
                return new T();
            var json = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
