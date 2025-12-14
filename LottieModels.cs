using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LottieConverter.Models
{
    public class LottieRoot
    {
        [JsonPropertyName("v")]
        public string Version { get; set; } = "5.7.4";

        [JsonPropertyName("fr")]
        public double FrameRate { get; set; }

        [JsonPropertyName("ip")]
        public double InPoint { get; set; } = 0;

        [JsonPropertyName("op")]
        public double OutPoint { get; set; }

        [JsonPropertyName("w")]
        public int Width { get; set; }

        [JsonPropertyName("h")]
        public int Height { get; set; }

        [JsonPropertyName("nm")]
        public string Name { get; set; }

        [JsonPropertyName("ddd")]
        public int Is3D { get; set; } = 0;

        [JsonPropertyName("assets")]
        public List<LottieAsset> Assets { get; set; } = new();

        [JsonPropertyName("layers")]
        public List<LottieLayer> Layers { get; set; } = new();

        [JsonPropertyName("markers")]
        public List<object> Markers { get; set; } = new();
    }

    public class LottieAsset
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("w")]
        public int Width { get; set; }

        [JsonPropertyName("h")]
        public int Height { get; set; }

        [JsonPropertyName("u")]
        public string Path { get; set; } = ""; // Empty for embedded

        [JsonPropertyName("p")]
        public string ImageData { get; set; } // Base64 for embedded

        [JsonPropertyName("e")]
        public int Embedded { get; set; } = 1;
    }

    public class LottieLayer
    {
        [JsonPropertyName("ddd")]
        public int Is3D { get; set; } = 0;

        [JsonPropertyName("ind")]
        public int Index { get; set; }

        [JsonPropertyName("ty")]
        public int Type { get; set; } = 2; // 2 = Image

        [JsonPropertyName("nm")]
        public string Name { get; set; }

        [JsonPropertyName("refId")]
        public string RefId { get; set; }

        [JsonPropertyName("sr")]
        public double Stretch { get; set; } = 1;

        [JsonPropertyName("ks")]
        public LottieTransform Transform { get; set; }

        [JsonPropertyName("ao")]
        public int AutoOrient { get; set; } = 0;

        [JsonPropertyName("ip")]
        public double InPoint { get; set; }

        [JsonPropertyName("op")]
        public double OutPoint { get; set; }

        [JsonPropertyName("st")]
        public double StartTime { get; set; } = 0;

        [JsonPropertyName("bm")]
        public int BlendMode { get; set; } = 0;
    }

    public class LottieTransform
    {
        [JsonPropertyName("o")]
        public LottieProperty Opacity { get; set; }

        [JsonPropertyName("r")]
        public LottieProperty Rotation { get; set; }

        [JsonPropertyName("p")]
        public LottieMultiProperty Position { get; set; }

        [JsonPropertyName("a")]
        public LottieMultiProperty AnchorPoint { get; set; }

        [JsonPropertyName("s")]
        public LottieMultiProperty Scale { get; set; }
    }

    public class LottieProperty
    {
        [JsonPropertyName("a")]
        public int Animated { get; set; } = 0;

        [JsonPropertyName("k")]
        public double Value { get; set; }
    }
    
    public class LottieMultiProperty
    {
        [JsonPropertyName("a")]
        public int Animated { get; set; } = 0;

        [JsonPropertyName("k")]
        public List<double> Value { get; set; }
    }
}
