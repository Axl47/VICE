// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class PatchesConfig
{
	[JsonProperty("stationRecipeGuard")]
	public bool StationRecipeGuard { get; set; } = true;

	[JsonProperty("productManagerGuard")]
	public bool ProductManagerGuard { get; set; } = true;

	[JsonProperty("discoveryHooks")]
	public bool DiscoveryHooks { get; set; } = true;

	[JsonProperty("packagingIcons")]
	public bool PackagingIcons { get; set; } = true;

	[JsonProperty("npcConsume")]
	public bool NpcConsume { get; set; } = true;

	[JsonProperty("customerAffinity")]
	public bool CustomerAffinity { get; set; } = true;

	[JsonProperty("itemModels")]
	public bool ItemModels { get; set; } = true;
}
