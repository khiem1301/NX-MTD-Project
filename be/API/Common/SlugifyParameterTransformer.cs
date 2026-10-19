using System.Text.RegularExpressions;

namespace NX_MTD.API.Common;

public sealed class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value) =>
        value is null ? null : Regex.Replace(value.ToString()!, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
}
