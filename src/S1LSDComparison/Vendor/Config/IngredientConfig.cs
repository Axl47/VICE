// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class IngredientConfig
{
	[JsonProperty("itemId")]
	public string ItemId { get; set; } = string.Empty;

	[JsonProperty("quantity")]
	public int Quantity { get; set; } = 1;
}
