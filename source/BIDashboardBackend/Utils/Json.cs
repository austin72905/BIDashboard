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
            options.Converters.Add(new NullableDateTimeConverterUsingDateTimeParse(dateTimeFormat));

            return JsonSerializer.Serialize(dataObject, options);
        }

        // 將 JSON 字串反序列化為物件
        public static T Deserialize<T>(string jsonString, string dateTimeFormat = "yyyy-MM-dd HH:mm:ss")
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new DateTimeConverterUsingDateTimeParse(dateTimeFormat));
            options.Converters.Add(new NullableDateTimeConverterUsingDateTimeParse(dateTimeFormat));

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

    // 自訂 Nullable<DateTime> 轉換器，使用指定的日期時間格式
    public class NullableDateTimeConverterUsingDateTimeParse : JsonConverter<DateTime?>
    {
        private readonly string _dateTimeFormat;

        public NullableDateTimeConverterUsingDateTimeParse(string dateTimeFormat)
        {
            _dateTimeFormat = dateTimeFormat;
        }

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (string.IsNullOrEmpty(stringValue))
                {
                    return null;
                }
                return DateTime.ParseExact(stringValue, _dateTimeFormat, null);
            }

            throw new JsonException($"無法將 {reader.TokenType} 轉換為 DateTime?");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString(_dateTimeFormat));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
