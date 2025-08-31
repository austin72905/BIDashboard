using System.Text.Json;
using System.Text.Json.Serialization;

namespace BIDashboardBackend.Utils
{
    public sealed class Json
    {
        // 將物件序列化為 JSON 格式字串
        public static string Serialize(object dataObject, string dateTimeFormat = "yyyy-MM-dd HH:mm:ss", bool indentation = false)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indentation,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            options.Converters.Add(new DateTimeConverterUsingDateTimeParse(dateTimeFormat));

            return JsonSerializer.Serialize(dataObject, options);
        }

        // 將 JSON 字串反序列化為物件
        public static T Deserialize<T>(string jsonString)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<T>(jsonString, options);
        }
    }

    // 自訂 DateTime 轉換器，使用指定的日期時間格式
    public class DateTimeConverterUsingDateTimeParse : JsonConverter<DateTime>
    {
        private readonly string _dateTimeFormat;

        public DateTimeConverterUsingDateTimeParse(string dateTimeFormat)
        {
            _dateTimeFormat = dateTimeFormat;
        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.ParseExact(reader.GetString(), _dateTimeFormat, null);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(_dateTimeFormat));
        }
    }
}
