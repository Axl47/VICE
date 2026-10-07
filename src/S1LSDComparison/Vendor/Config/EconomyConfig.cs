// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class EconomyConfig
{
	[JsonProperty("_comment")]
	public string Comment { get; set; } = "Chemistry-station recipe, supplier shop stocking and Product Manager listing.";

	[JsonProperty("recipeEnabled")]
	public bool RecipeEnabled { get; set; } = true;

	[JsonProperty("recipeId")]
	public string RecipeId { get; set; } = "s1lsd:lsd-synthesis";

	[JsonProperty("recipeTitle")]
	public string RecipeTitle { get; set; } = "LSD Crystals";

	[JsonProperty("cookTimeMinutes")]
	public int CookTimeMinutes { get; set; } = 240;

	[JsonProperty("temperatureC")]
	public float TemperatureC { get; set; } = 190f;

	[JsonProperty("temperatureTolerance")]
	public float TemperatureTolerance { get; set; } = 20f;

	[JsonProperty("finalLiquidColor")]
	public string FinalLiquidColor { get; set; } = "#3F5FA8";

	[JsonProperty("ingredients")]
	public List<IngredientConfig> Ingredients { get; set; } = Defaults.Ingredients();

	[JsonProperty("yieldDose")]
	public int YieldDose { get; set; } = 70;

	[JsonProperty("yieldQuantity")]
	public int YieldQuantity { get; set; } = 4;

	[JsonProperty("discoverOnFirstCraft")]
	public bool DiscoverOnFirstCraft { get; set; } = true;

	[JsonProperty("listForSaleOnDiscover")]
	public bool ListForSaleOnDiscover { get; set; }

	[JsonProperty("shopsEnabled")]
	public bool ShopsEnabled { get; set; } = true;

	[JsonProperty("shopNames")]
	public List<string> ShopNames { get; set; } = new List<string>();

	[JsonProperty("shopStock")]
	public Dictionary<string, int> ShopStock { get; set; } = new Dictionary<string, int>();
}
