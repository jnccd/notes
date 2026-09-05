using Avalonia;
using Avalonia.Media;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotesAvalonia.Configuration;

/// <summary>STJ converter for Avalonia's PixelPoint (stored as { X, Y } in old config files).
/// Avalonia's struct members are read-only for STJ, so round-trip via the constructor.</summary>
public class PixelPointJsonConverter : JsonConverter<PixelPoint?>
{
    public override PixelPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected an object for PixelPoint");

        int x = 0, y = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            string property = reader.GetString()!;
            reader.Read();
            if (property.Equals("X", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.Number)
                x = reader.GetInt32();
            else if (property.Equals("Y", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.Number)
                y = reader.GetInt32();
            else
                reader.Skip();
        }

        return new PixelPoint(x, y);
    }

    public override void Write(Utf8JsonWriter writer, PixelPoint? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();
        writer.WriteNumber("X", value.Value.X);
        writer.WriteNumber("Y", value.Value.Y);
        writer.WriteEndObject();
    }
}

/// <summary>STJ converter for Avalonia's Color (stored as { A, R, G, B } in old config files).</summary>
public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected an object for Color");

        byte a = 0, r = 0, g = 0, b = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            string property = reader.GetString()!;
            reader.Read();
            switch (property.ToUpperInvariant())
            {
                case "A" when reader.TokenType == JsonTokenType.Number: a = reader.GetByte(); break;
                case "R" when reader.TokenType == JsonTokenType.Number: r = reader.GetByte(); break;
                case "G" when reader.TokenType == JsonTokenType.Number: g = reader.GetByte(); break;
                case "B" when reader.TokenType == JsonTokenType.Number: b = reader.GetByte(); break;
                default: reader.Skip(); break;
            }
        }

        return Color.FromArgb(a, r, g, b);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("A", value.A);
        writer.WriteNumber("R", value.R);
        writer.WriteNumber("G", value.G);
        writer.WriteNumber("B", value.B);
        writer.WriteEndObject();
    }
}
