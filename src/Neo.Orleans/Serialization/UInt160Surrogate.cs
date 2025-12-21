using Orleans;

namespace Neo.Orleans.Serialization;

/// <summary>
/// Orleans surrogate for UInt160 serialization.
/// </summary>
[GenerateSerializer]
public struct UInt160Surrogate
{
    [Id(0)] public byte[] Data { get; set; }
}

/// <summary>
/// Converter between UInt160 and its surrogate.
/// </summary>
[RegisterConverter]
public sealed class UInt160SurrogateConverter : IConverter<UInt160, UInt160Surrogate>
{
    public UInt160 ConvertFromSurrogate(in UInt160Surrogate surrogate) =>
        surrogate.Data?.Length == 20 ? new UInt160(surrogate.Data) : UInt160.Zero;

    public UInt160Surrogate ConvertToSurrogate(in UInt160 value) =>
        new() { Data = value.GetSpan().ToArray() };
}
