using System;
using BakeryShop.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BakeryShop.EditorTools
{
    /// <summary>
    /// content.json의 글자를 ContentJson 객체로 읽는다.
    /// JsonUtility 대신 Newtonsoft를 쓰는 이유: [[x, y], …] 모양의 점 목록을 JsonUtility가 읽지 못한다.
    /// 에디터에서만 쓰므로 빌드에는 JSON 라이브러리가 들어가지 않는다.
    /// </summary>
    public static class ContentJsonReader
    {
        /// <summary>
        /// 읽지 못하면 FormatException을 던진다. 메시지에 줄 번호와 경로가 들어 있다.
        /// 모르는 키도 오류로 본다: "marketPirce" 같은 오타가 조용히 무시되어 값이 0이 되는 일을 막는다.
        /// </summary>
        public static ContentJson Read(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                throw new FormatException("content.json이 비어 있습니다.");
            }

            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
            };
            settings.Converters.Add(new CurvePointConverter());

            ContentJson content;
            try
            {
                content = JsonConvert.DeserializeObject<ContentJson>(jsonText, settings);
            }
            catch (JsonException exception)
            {
                throw new FormatException("content.json을 읽을 수 없습니다: " + exception.Message, exception);
            }

            if (content == null)
            {
                throw new FormatException("content.json의 내용이 없습니다.");
            }

            return content;
        }

        /// <summary>[x, y] 두 숫자의 배열을 CurvePoint로 읽는다.</summary>
        private sealed class CurvePointConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(CurvePoint);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                string path = reader.Path;
                var array = JToken.Load(reader) as JArray;
                if (array == null || array.Count != 2)
                {
                    throw new JsonSerializationException("곡선의 점은 [x, y] 모양이어야 합니다 (" + path + ").");
                }

                return new CurvePoint(array[0].Value<float>(), array[1].Value<float>());
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var point = (CurvePoint)value;
                writer.WriteStartArray();
                writer.WriteValue(point.X);
                writer.WriteValue(point.Y);
                writer.WriteEndArray();
            }
        }
    }
}
